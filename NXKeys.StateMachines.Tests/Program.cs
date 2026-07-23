using System;
using System.Collections.Generic;
using NXKeys.Protocol;
using NXKeys.StateMachines;

namespace NXKeys.StateMachines.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("DFA распознаёт точную последовательность", ExactSequenceExecutes);
            Run("DFA отклоняет неизвестную ветвь", InvalidBranchBeeps);
            Run("DFA запрещает дубликаты", DuplicateSequenceFailsCompilation);
            Run("DFA запрещает терминал-префикс", TerminalPrefixFailsCompilation);
            Run("Esc всегда возвращает Idle", CancelAlwaysReturnsIdle);
            Run("Тайм-аут возвращает Idle", TimeoutReturnsIdle);
            Run("Разрушительная команда требует Enter", DestructiveCommandRequiresConfirmation);
            Run("Команда с выбором блокируется без выбора", SelectionGuardBlocks);
            Run("Устаревший контекст блокирует выполнение", StaleContextBlocks);
            Run("Смена модуля подтверждается новым контекстом", ModuleSwitchWaitsForContext);
            Run("Результат завершает AwaitingResult", RequestResultCompletes);

            Console.WriteLine(failures == 0
                ? "[OK] Все инварианты автоматов выполнены."
                : "[FAIL] Нарушено инвариантов: " + failures);
            return failures == 0 ? 0 : 1;
        }

        private static void ExactSequenceExecutes()
        {
            LeaderStateMachine machine = CreateMachine(Command("ME", "modeling"));
            machine.Activate(false, Context("modeling"));
            Assert(machine.InputToken("M").State == LeaderState.Prefix, "После M ожидается Prefix.");
            LeaderTransition transition = machine.InputToken("E");
            Assert(transition.Action == LeaderActionKind.ExecuteCommand, "ME должна дать ExecuteCommand.");
            Assert(machine.State == LeaderState.Dispatching, "Состояние должно быть Dispatching.");
        }

        private static void InvalidBranchBeeps()
        {
            LeaderStateMachine machine = CreateMachine(Command("ME", "modeling"));
            machine.Activate(false, Context("modeling"));
            LeaderTransition transition = machine.InputToken("X");
            Assert(transition.Action == LeaderActionKind.Beep, "Неизвестная ветвь должна дать Beep.");
            Assert(machine.State == LeaderState.Root, "Состояние должно остаться Root.");
        }

        private static void DuplicateSequenceFailsCompilation()
        {
            AssertThrows(() => new SequenceAutomaton(new[]
            {
                Command("ME", "modeling"),
                Command("M E", "modeling")
            }));
        }

        private static void TerminalPrefixFailsCompilation()
        {
            AssertThrows(() => new SequenceAutomaton(new[]
            {
                Command("M", "modeling"),
                Command("ME", "modeling")
            }));
        }

        private static void CancelAlwaysReturnsIdle()
        {
            LeaderStateMachine machine = CreateMachine(Command("ME", "modeling"));
            machine.Activate(true, Context("modeling"));
            machine.InputToken("M");
            LeaderTransition transition = machine.Cancel();
            Assert(transition.Action == LeaderActionKind.Cancelled, "Ожидается Cancelled.");
            Assert(machine.State == LeaderState.Idle, "После cancel должен быть Idle.");
            Assert(!machine.Sticky, "Cancel обязан снять Sticky.");
        }

        private static void TimeoutReturnsIdle()
        {
            LeaderStateMachine machine = CreateMachine(Command("ME", "modeling"));
            machine.Activate(false, Context("modeling"));
            machine.InputToken("M");
            LeaderTransition transition = machine.Timeout();
            Assert(transition.Action == LeaderActionKind.TimedOut, "Ожидается TimedOut.");
            Assert(machine.State == LeaderState.Idle, "Тайм-аут должен вернуть Idle.");
        }

        private static void DestructiveCommandRequiresConfirmation()
        {
            SequenceDefinition command = Command("DX", "modeling");
            command.Destructive = true;
            command.ConfirmBeforeExecute = true;
            LeaderStateMachine machine = CreateMachine(command);
            machine.Activate(false, Context("modeling"));
            machine.InputToken("D");
            LeaderTransition terminal = machine.InputToken("X");
            Assert(terminal.Action == LeaderActionKind.RequireConfirmation, "Опасная команда обязана ждать подтверждение.");
            Assert(machine.State == LeaderState.AwaitingConfirmation, "Ожидается AwaitingConfirmation.");
            LeaderTransition confirmed = machine.Confirm();
            Assert(confirmed.Action == LeaderActionKind.ExecuteCommand, "Enter должен разрешить dispatch.");
            Assert(confirmed.ConfirmationAccepted, "Подтверждение должно попасть в переход.");
        }

        private static void SelectionGuardBlocks()
        {
            SequenceDefinition command = Command("SB", "modeling");
            command.RequiresSelection = true;
            LeaderStateMachine machine = CreateMachine(command);
            machine.Activate(false, Context("modeling", 0));
            machine.InputToken("S");
            LeaderTransition transition = machine.InputToken("B");
            Assert(transition.Action == LeaderActionKind.Rejected, "Команда должна быть отклонена без выбора.");
            Assert(machine.State == LeaderState.Idle, "После отказа non-sticky должен вернуться Idle.");
        }

        private static void StaleContextBlocks()
        {
            NxContextSnapshot stale = Context("modeling");
            stale.UpdatedUtc = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
            LeaderStateMachine machine = CreateMachine(Command("ME", "modeling"));
            machine.Activate(false, stale);
            machine.InputToken("M");
            LeaderTransition transition = machine.InputToken("E");
            Assert(transition.Action == LeaderActionKind.Rejected, "Устаревший контекст должен блокировать execution.");
        }

        private static void ModuleSwitchWaitsForContext()
        {
            LeaderStateMachine machine = CreateMachine(Command("DB", "drafting"));
            machine.Activate(false, Context("modeling"));
            machine.InputToken("D");
            LeaderTransition switchTransition = machine.InputToken("B");
            Assert(switchTransition.Action == LeaderActionKind.SwitchModule, "Ожидается SwitchModule.");
            Assert(machine.State == LeaderState.SwitchingModule, "Автомат должен ждать контекст нового модуля.");
            LeaderTransition contextTransition = machine.UpdateContext(Context("drafting"));
            Assert(contextTransition.Action == LeaderActionKind.ExecuteCommand, "После подтверждения drafting команда должна dispatch-иться.");
            Assert(machine.State == LeaderState.Dispatching, "Ожидается Dispatching.");
        }

        private static void RequestResultCompletes()
        {
            LeaderStateMachine machine = CreateMachine(Command("ME", "modeling"));
            machine.Activate(false, Context("modeling"));
            machine.InputToken("M");
            machine.InputToken("E");
            LeaderTransition queued = machine.MarkRequestQueued("request-1");
            Assert(queued.Action == LeaderActionKind.RequestQueued, "Ожидается RequestQueued.");
            Assert(machine.State == LeaderState.AwaitingResult, "Ожидается AwaitingResult.");
            LeaderTransition completed = machine.CompleteRequest(true, "OK");
            Assert(completed.Action == LeaderActionKind.RequestCompleted, "Ожидается RequestCompleted.");
            Assert(machine.State == LeaderState.Idle, "После результата non-sticky должен быть Idle.");
        }

        private static LeaderStateMachine CreateMachine(params SequenceDefinition[] commands)
        {
            return new LeaderStateMachine(new SequenceAutomaton(commands));
        }

        private static SequenceDefinition Command(string sequence, string module)
        {
            return new SequenceDefinition
            {
                Id = sequence.Replace(" ", string.Empty).ToUpperInvariant(),
                Sequence = sequence,
                ModuleId = module,
                CommandId = "UG_TEST_" + sequence.Replace(" ", string.Empty).ToUpperInvariant(),
                CommandName = sequence,
                NeedsWorkPart = true,
                Enabled = true
            };
        }

        private static NxContextSnapshot Context(string module, int selectionCount = 1)
        {
            return new NxContextSnapshot
            {
                SchemaVersion = NxProtocolConstants.SchemaVersion,
                Revision = 10,
                Status = "running",
                ApplicationId = "UG_APP_" + module.ToUpperInvariant(),
                ModuleId = module,
                ModuleLabel = module,
                SelectionCount = selectionCount,
                SelectionState = selectionCount < 0 ? "unknown" : selectionCount == 0 ? "none" : selectionCount == 1 ? "single" : "multiple",
                WorkPartAvailable = true,
                DisplayPartAvailable = true,
                ModalDialogActive = false,
                ContextConfidence = 100,
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("[OK] " + name);
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine("[FAIL] " + name + ": " + ex.Message);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertThrows(Action action)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException("Ожидалось InvalidOperationException.");
        }
    }
}
