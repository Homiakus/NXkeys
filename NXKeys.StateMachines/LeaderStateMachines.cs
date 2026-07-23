using System;
using System.Collections.Generic;
using System.Linq;
using NXKeys.Protocol;

namespace NXKeys.StateMachines
{
    public enum LeaderState
    {
        Idle,
        Root,
        Prefix,
        Search,
        AwaitingConfirmation,
        Dispatching,
        AwaitingResult,
        SwitchingModule,
        Failed
    }

    public enum LeaderActionKind
    {
        None,
        Activated,
        Updated,
        Beep,
        Cancelled,
        TimedOut,
        Rejected,
        RequireConfirmation,
        ExecuteCommand,
        SwitchModule,
        RequestQueued,
        RequestCompleted,
        RequestFailed
    }

    public sealed class SequenceDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Sequence { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string CommandId { get; set; } = string.Empty;
        public string CommandName { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public bool RequiresSelection { get; set; }
        public int MinimumSelectionCount { get; set; } = 1;
        public bool NeedsWorkPart { get; set; } = true;
        public bool Destructive { get; set; }
        public bool ConfirmBeforeExecute { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public sealed class GuardResult
    {
        public bool Allowed { get; set; }
        public bool RequiresModuleSwitch { get; set; }
        public string TargetModuleId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class LeaderTransition
    {
        public LeaderActionKind Action { get; set; }
        public LeaderState State { get; set; }
        public string Prefix { get; set; } = string.Empty;
        public string SearchQuery { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string TargetModuleId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public bool ConfirmationAccepted { get; set; }
        public SequenceDefinition Command { get; set; }
        public IReadOnlyList<SequenceDefinition> Candidates { get; set; } = Array.Empty<SequenceDefinition>();
    }

    public sealed class SequenceAutomaton
    {
        private sealed class Node
        {
            public int Id { get; set; }
            public int ParentId { get; set; } = -1;
            public string Token { get; set; } = string.Empty;
            public Dictionary<string, int> Children { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public SequenceDefinition Terminal { get; set; }
        }

        private readonly Dictionary<int, Node> nodes = new Dictionary<int, Node>();
        private readonly List<SequenceDefinition> definitions;

        public int RootNodeId => 0;
        public IReadOnlyList<SequenceDefinition> Definitions => definitions;

        public SequenceAutomaton(IEnumerable<SequenceDefinition> source)
        {
            definitions = (source ?? Enumerable.Empty<SequenceDefinition>())
                .Where(item => item != null && item.Enabled)
                .ToList();
            nodes[0] = new Node { Id = 0 };
            Compile();
        }

        public static IReadOnlyList<string> TokenizeSequence(string sequence)
        {
            if (string.IsNullOrWhiteSpace(sequence)) return Array.Empty<string>();
            return sequence
                .Where(char.IsLetterOrDigit)
                .Select(character => char.ToUpperInvariant(character).ToString())
                .ToArray();
        }

        public bool TryAdvance(int nodeId, string token, out int nextNodeId)
        {
            nextNodeId = nodeId;
            if (!nodes.TryGetValue(nodeId, out Node node)) return false;
            string normalized = NormalizeToken(token);
            if (string.IsNullOrEmpty(normalized)) return false;
            return node.Children.TryGetValue(normalized, out nextNodeId);
        }

        public int ParentOf(int nodeId)
        {
            return nodes.TryGetValue(nodeId, out Node node) && node.ParentId >= 0 ? node.ParentId : RootNodeId;
        }

        public SequenceDefinition TerminalAt(int nodeId)
        {
            return nodes.TryGetValue(nodeId, out Node node) ? node.Terminal : null;
        }

        public string PrefixAt(int nodeId)
        {
            if (nodeId == RootNodeId) return string.Empty;
            var tokens = new Stack<string>();
            int cursor = nodeId;
            while (cursor != RootNodeId && nodes.TryGetValue(cursor, out Node node))
            {
                tokens.Push(node.Token);
                cursor = node.ParentId;
            }
            return string.Join(" ", tokens);
        }

        public IReadOnlyList<SequenceDefinition> CandidatesAt(int nodeId)
        {
            if (!nodes.ContainsKey(nodeId)) return Array.Empty<SequenceDefinition>();
            var result = new List<SequenceDefinition>();
            var queue = new Queue<int>();
            queue.Enqueue(nodeId);
            while (queue.Count > 0)
            {
                Node node = nodes[queue.Dequeue()];
                if (node.Terminal != null) result.Add(node.Terminal);
                foreach (int child in node.Children.Values) queue.Enqueue(child);
            }
            return result.OrderBy(item => item.Sequence, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public IReadOnlyList<SequenceDefinition> Search(string query)
        {
            string normalized = NormalizeSearch(query);
            if (string.IsNullOrWhiteSpace(normalized)) return definitions;
            string[] tokens = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return definitions
                .Select(item => new
                {
                    Item = item,
                    Score = tokens.Count(token => NormalizeSearch(BuildSearchText(item)).Contains(token, StringComparison.OrdinalIgnoreCase))
                })
                .Where(value => value.Score > 0)
                .OrderByDescending(value => value.Score)
                .ThenBy(value => value.Item.Sequence, StringComparer.OrdinalIgnoreCase)
                .Select(value => value.Item)
                .ToList();
        }

        private void Compile()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SequenceDefinition definition in definitions)
            {
                IReadOnlyList<string> tokens = TokenizeSequence(definition.Sequence);
                if (tokens.Count == 0)
                    throw new InvalidOperationException("Leader sequence is empty: " + definition.Id);
                string canonical = string.Join(string.Empty, tokens);
                if (!seen.Add(canonical))
                    throw new InvalidOperationException("Duplicate Leader sequence: " + definition.Sequence);

                int nodeId = RootNodeId;
                foreach (string token in tokens)
                {
                    Node node = nodes[nodeId];
                    if (!node.Children.TryGetValue(token, out int childId))
                    {
                        childId = nodes.Count;
                        nodes[childId] = new Node { Id = childId, ParentId = nodeId, Token = token };
                        node.Children[token] = childId;
                    }
                    nodeId = childId;
                }

                Node terminalNode = nodes[nodeId];
                if (terminalNode.Terminal != null)
                    throw new InvalidOperationException("Duplicate terminal Leader sequence: " + definition.Sequence);
                terminalNode.Terminal = definition;
            }

            foreach (Node node in nodes.Values)
            {
                if (node.Terminal != null && node.Children.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Leader sequence is both a command and a prefix; the longer branch would be unreachable: " + node.Terminal.Sequence);
                }
            }
        }

        private static string BuildSearchText(SequenceDefinition item)
        {
            return string.Join(" ", new[]
            {
                item.Sequence,
                item.ModuleId,
                item.CommandId,
                item.CommandName,
                item.SearchText
            });
        }

        private static string NormalizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return string.Empty;
            char value = token.FirstOrDefault(char.IsLetterOrDigit);
            return value == default ? string.Empty : char.ToUpperInvariant(value).ToString();
        }

        private static string NormalizeSearch(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return new string(value.ToUpperInvariant().Select(character => char.IsLetterOrDigit(character) ? character : ' ').ToArray())
                .Replace("  ", " ")
                .Trim();
        }
    }

    public sealed class ContextGuardEvaluator
    {
        private static readonly HashSet<string> SharedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "selection_object", "inspect_view", "reuse"
        };

        public GuardResult Evaluate(SequenceDefinition command, NxContextSnapshot context, bool allowModuleSwitch)
        {
            if (command == null || !command.Enabled)
                return Deny("Команда отключена.");
            if (string.IsNullOrWhiteSpace(command.CommandId))
                return Deny("У команды отсутствует точный NX BUTTON ID.");
            if (context == null || !context.IsFresh)
                return Deny("Контекст NX отсутствует или устарел.");
            if (!string.Equals(context.Status, "running", StringComparison.OrdinalIgnoreCase))
                return Deny("NX Command Bridge не готов.");
            if (context.ModalDialogActive)
                return Deny("Закройте активный модальный диалог NX.");

            string commandModule = NormalizeModule(command.ModuleId);
            string activeModule = NormalizeModule(context.ModuleId);
            bool moduleMatches = string.IsNullOrWhiteSpace(commandModule) ||
                                 string.IsNullOrWhiteSpace(activeModule) ||
                                 string.Equals(commandModule, activeModule, StringComparison.OrdinalIgnoreCase) ||
                                 SharedModules.Contains(commandModule);
            if (!moduleMatches)
            {
                if (allowModuleSwitch)
                {
                    return new GuardResult
                    {
                        Allowed = false,
                        RequiresModuleSwitch = true,
                        TargetModuleId = commandModule,
                        Reason = "Требуется переключение модуля NX."
                    };
                }
                return Deny("NX не подтвердил переключение в модуль " + commandModule + ".");
            }

            if (command.RequiresSelection)
            {
                if (context.SelectionCount < 0 || string.Equals(context.SelectionState, "unknown", StringComparison.OrdinalIgnoreCase))
                    return Deny("NX не предоставил надёжный контекст выбора.");
                if (context.SelectionCount < Math.Max(1, command.MinimumSelectionCount))
                    return Deny("Сначала выберите подходящий объект.");
            }

            if (command.NeedsWorkPart && !context.WorkPartAvailable)
                return Deny("Нет активной рабочей детали.");

            return new GuardResult { Allowed = true };
        }

        public static bool CommandNeedsWorkPart(string commandId)
        {
            string id = commandId ?? string.Empty;
            if (id.StartsWith("UG_FILE_", StringComparison.OrdinalIgnoreCase)) return false;
            if (id.StartsWith("UG_APP_", StringComparison.OrdinalIgnoreCase)) return false;
            if (id.StartsWith("UG_VIEW_", StringComparison.OrdinalIgnoreCase)) return false;
            if (id.StartsWith("UG_HELP_", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public static string NormalizeModule(string moduleId)
        {
            string value = (moduleId ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
            switch (value)
            {
                case "view":
                case "inspect":
                case "inspect_/_view": return "inspect_view";
                case "selection_filters":
                case "selection": return "selection_object";
                case "cam_/_manufacturing":
                case "cam": return "manufacturing";
                case "cae_/_simulation":
                case "cae": return "simulation";
                case "mold_/_tooling": return "mold";
                case "reuse_/_templates": return "reuse";
                default: return value;
            }
        }

        private static GuardResult Deny(string reason)
        {
            return new GuardResult { Allowed = false, Reason = reason ?? string.Empty };
        }
    }

    public sealed class LeaderStateMachine
    {
        private readonly SequenceAutomaton automaton;
        private readonly ContextGuardEvaluator guards;
        private int nodeId;
        private string targetModuleId = string.Empty;
        private bool manualModuleSwitch;

        public LeaderState State { get; private set; } = LeaderState.Idle;
        public bool Sticky { get; private set; }
        public string Prefix => automaton.PrefixAt(nodeId);
        public string SearchQuery { get; private set; } = string.Empty;
        public SequenceDefinition PendingCommand { get; private set; }
        public string PendingRequestId { get; private set; } = string.Empty;
        public NxContextSnapshot Context { get; private set; }
        public bool IsCapturing => State != LeaderState.Idle;

        public LeaderStateMachine(SequenceAutomaton automaton, ContextGuardEvaluator guards = null)
        {
            this.automaton = automaton ?? throw new ArgumentNullException(nameof(automaton));
            this.guards = guards ?? new ContextGuardEvaluator();
            nodeId = automaton.RootNodeId;
        }

        public LeaderTransition Activate(bool sticky, NxContextSnapshot context)
        {
            Context = context;
            Sticky = sticky;
            nodeId = automaton.RootNodeId;
            SearchQuery = string.Empty;
            PendingCommand = null;
            PendingRequestId = string.Empty;
            targetModuleId = string.Empty;
            manualModuleSwitch = false;
            State = LeaderState.Root;
            return Snapshot(LeaderActionKind.Activated);
        }

        public LeaderTransition InputToken(string token)
        {
            if (State == LeaderState.Search) return AppendSearch(token);
            if (State != LeaderState.Root && State != LeaderState.Prefix)
                return Snapshot(LeaderActionKind.Beep, "Ввод недоступен в текущем состоянии.");

            if (!automaton.TryAdvance(nodeId, token, out int next))
                return Snapshot(LeaderActionKind.Beep, "Нет такой ветви сочетания.");

            nodeId = next;
            SequenceDefinition terminal = automaton.TerminalAt(nodeId);
            if (terminal != null) return PrepareCommand(terminal, true);

            State = LeaderState.Prefix;
            return Snapshot(LeaderActionKind.Updated);
        }

        public LeaderTransition EnterSearch()
        {
            if (State != LeaderState.Root && State != LeaderState.Prefix)
                return Snapshot(LeaderActionKind.Beep, "Поиск недоступен в текущем состоянии.");
            State = LeaderState.Search;
            SearchQuery = string.Empty;
            return Snapshot(LeaderActionKind.Updated);
        }

        public LeaderTransition AppendSearch(string token)
        {
            if (State != LeaderState.Search) return Snapshot(LeaderActionKind.Beep);
            if (!string.IsNullOrWhiteSpace(token)) SearchQuery += token.ToUpperInvariant();
            return Snapshot(LeaderActionKind.Updated);
        }

        public LeaderTransition Backspace()
        {
            if (State == LeaderState.Search)
            {
                if (SearchQuery.Length > 0) SearchQuery = SearchQuery.Substring(0, SearchQuery.Length - 1);
                else State = LeaderState.Root;
                return Snapshot(LeaderActionKind.Updated);
            }
            if (State == LeaderState.Prefix)
            {
                nodeId = automaton.ParentOf(nodeId);
                State = nodeId == automaton.RootNodeId ? LeaderState.Root : LeaderState.Prefix;
                return Snapshot(LeaderActionKind.Updated);
            }
            if (State == LeaderState.Root) return Cancel("Leader закрыт.");
            return Snapshot(LeaderActionKind.Beep);
        }

        public LeaderTransition Confirm()
        {
            if (State != LeaderState.AwaitingConfirmation || PendingCommand == null)
                return Snapshot(LeaderActionKind.Beep, "Нет команды, ожидающей подтверждения.");
            State = LeaderState.Dispatching;
            return Snapshot(LeaderActionKind.ExecuteCommand, confirmationAccepted: true);
        }

        public LeaderTransition BeginManualModuleSwitch(string moduleId)
        {
            if (!IsCapturing) return Snapshot(LeaderActionKind.Beep);
            targetModuleId = ContextGuardEvaluator.NormalizeModule(moduleId);
            if (string.IsNullOrWhiteSpace(targetModuleId)) return Snapshot(LeaderActionKind.Beep);
            manualModuleSwitch = true;
            PendingCommand = null;
            State = LeaderState.SwitchingModule;
            return Snapshot(LeaderActionKind.SwitchModule, targetModuleId: targetModuleId);
        }

        public LeaderTransition MarkRequestQueued(string requestId)
        {
            if (State != LeaderState.Dispatching)
                return Snapshot(LeaderActionKind.RequestFailed, "Команда поставлена в очередь из недопустимого состояния.");
            PendingRequestId = requestId ?? string.Empty;
            State = LeaderState.AwaitingResult;
            return Snapshot(LeaderActionKind.RequestQueued, requestId: PendingRequestId);
        }

        public LeaderTransition CompleteRequest(bool success, string message)
        {
            if (State != LeaderState.AwaitingResult && State != LeaderState.Dispatching)
                return Snapshot(LeaderActionKind.None);
            LeaderActionKind action = success ? LeaderActionKind.RequestCompleted : LeaderActionKind.RequestFailed;
            SequenceDefinition completed = PendingCommand;
            ResetAfterCommand();
            LeaderTransition transition = Snapshot(action, message);
            transition.Command = completed;
            return transition;
        }

        public LeaderTransition UpdateContext(NxContextSnapshot context)
        {
            Context = context;
            if (State == LeaderState.SwitchingModule &&
                context != null && context.IsFresh &&
                string.Equals(ContextGuardEvaluator.NormalizeModule(context.ModuleId), targetModuleId, StringComparison.OrdinalIgnoreCase))
            {
                targetModuleId = string.Empty;
                if (manualModuleSwitch)
                {
                    manualModuleSwitch = false;
                    State = LeaderState.Root;
                    nodeId = automaton.RootNodeId;
                    return Snapshot(LeaderActionKind.Updated, "Модуль NX переключён.");
                }
                SequenceDefinition command = PendingCommand;
                return PrepareCommand(command, false);
            }

            if (State == LeaderState.AwaitingConfirmation && PendingCommand != null)
            {
                GuardResult guard = guards.Evaluate(PendingCommand, Context, false);
                if (!guard.Allowed) return Reject(guard.Reason);
            }
            return Snapshot(LeaderActionKind.None);
        }

        public LeaderTransition Cancel(string reason = "Отменено пользователем.")
        {
            Sticky = false;
            nodeId = automaton.RootNodeId;
            SearchQuery = string.Empty;
            PendingCommand = null;
            PendingRequestId = string.Empty;
            targetModuleId = string.Empty;
            manualModuleSwitch = false;
            State = LeaderState.Idle;
            return Snapshot(LeaderActionKind.Cancelled, reason);
        }

        public LeaderTransition Timeout()
        {
            if (Sticky && (State == LeaderState.Root || State == LeaderState.Prefix || State == LeaderState.Search))
                return Snapshot(LeaderActionKind.None);
            LeaderTransition transition = Cancel("Тайм-аут ожидания.");
            transition.Action = LeaderActionKind.TimedOut;
            return transition;
        }

        public IReadOnlyList<SequenceDefinition> CurrentCandidates()
        {
            if (State == LeaderState.Search) return automaton.Search(SearchQuery);
            return automaton.CandidatesAt(nodeId);
        }

        private LeaderTransition PrepareCommand(SequenceDefinition command, bool allowModuleSwitch)
        {
            if (command == null) return Reject("Команда не определена.");
            PendingCommand = command;
            GuardResult guard = guards.Evaluate(command, Context, allowModuleSwitch);
            if (guard.RequiresModuleSwitch)
            {
                targetModuleId = guard.TargetModuleId;
                manualModuleSwitch = false;
                State = LeaderState.SwitchingModule;
                return Snapshot(LeaderActionKind.SwitchModule, guard.Reason, targetModuleId: targetModuleId);
            }
            if (!guard.Allowed) return Reject(guard.Reason);
            if (command.Destructive || command.ConfirmBeforeExecute)
            {
                State = LeaderState.AwaitingConfirmation;
                return Snapshot(LeaderActionKind.RequireConfirmation, "Для выполнения нажмите Enter.");
            }
            State = LeaderState.Dispatching;
            return Snapshot(LeaderActionKind.ExecuteCommand);
        }

        private LeaderTransition Reject(string reason)
        {
            SequenceDefinition rejected = PendingCommand;
            ResetAfterCommand();
            LeaderTransition transition = Snapshot(LeaderActionKind.Rejected, reason);
            transition.Command = rejected;
            return transition;
        }

        private void ResetAfterCommand()
        {
            nodeId = automaton.RootNodeId;
            SearchQuery = string.Empty;
            PendingCommand = null;
            PendingRequestId = string.Empty;
            targetModuleId = string.Empty;
            manualModuleSwitch = false;
            State = Sticky ? LeaderState.Root : LeaderState.Idle;
        }

        private LeaderTransition Snapshot(
            LeaderActionKind action,
            string reason = null,
            string targetModuleId = null,
            string requestId = null,
            bool confirmationAccepted = false)
        {
            return new LeaderTransition
            {
                Action = action,
                State = State,
                Prefix = Prefix,
                SearchQuery = SearchQuery,
                Reason = reason ?? string.Empty,
                TargetModuleId = targetModuleId ?? string.Empty,
                RequestId = requestId ?? PendingRequestId,
                ConfirmationAccepted = confirmationAccepted,
                Command = PendingCommand,
                Candidates = CurrentCandidates()
            };
        }
    }
}
