using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NXKeys.StateMachines
{
    public sealed class LeaderTimeoutPolicy
    {
        [JsonPropertyName("root_ms")]
        public int RootMs { get; set; } = 20000;

        [JsonPropertyName("prefix_ms")]
        public int PrefixMs { get; set; } = 20000;

        [JsonPropertyName("search_ms")]
        public int SearchMs { get; set; } = 20000;

        [JsonPropertyName("confirmation_ms")]
        public int ConfirmationMs { get; set; } = 20000;

        [JsonPropertyName("result_ms")]
        public int ResultMs { get; set; } = 20000;

        [JsonPropertyName("module_switch_ms")]
        public int ModuleSwitchMs { get; set; } = 20000;

        public void Normalize()
        {
            RootMs = Clamp(RootMs, 500, 60000, 20000);
            PrefixMs = Clamp(PrefixMs, 500, 60000, 20000);
            SearchMs = Clamp(SearchMs, 500, 120000, 20000);
            ConfirmationMs = Clamp(ConfirmationMs, 1000, 120000, 20000);
            ResultMs = Clamp(ResultMs, 1000, 120000, 20000);
            ModuleSwitchMs = Clamp(ModuleSwitchMs, 1000, 60000, 20000);
        }

        private static int Clamp(int value, int minimum, int maximum, int fallback)
        {
            if (value <= 0) value = fallback;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    public sealed class SelectionGuardPolicy
    {
        [JsonPropertyName("minimum")]
        public int Minimum { get; set; }

        [JsonPropertyName("maximum")]
        public int Maximum { get; set; } = -1;

        [JsonPropertyName("types_any")]
        public List<string> TypesAny { get; set; } = new List<string>();

        [JsonPropertyName("types_all")]
        public List<string> TypesAll { get; set; } = new List<string>();
    }

    public sealed class ContextGuardPolicy
    {
        [JsonPropertyName("modules")]
        public List<string> Modules { get; set; } = new List<string>();

        [JsonPropertyName("bridge_statuses")]
        public List<string> BridgeStatuses { get; set; } = new List<string>();

        [JsonPropertyName("interaction_states")]
        public List<string> InteractionStates { get; set; } = new List<string>();

        [JsonPropertyName("require_work_part")]
        public bool? RequireWorkPart { get; set; }

        [JsonPropertyName("require_display_part")]
        public bool? RequireDisplayPart { get; set; }

        [JsonPropertyName("minimum_context_confidence")]
        public int MinimumContextConfidence { get; set; }

        [JsonPropertyName("selection")]
        public SelectionGuardPolicy Selection { get; set; } = new SelectionGuardPolicy();

        public ContextGuardPolicy Clone()
        {
            return new ContextGuardPolicy
            {
                Modules = new List<string>(Modules ?? new List<string>()),
                BridgeStatuses = new List<string>(BridgeStatuses ?? new List<string>()),
                InteractionStates = new List<string>(InteractionStates ?? new List<string>()),
                RequireWorkPart = RequireWorkPart,
                RequireDisplayPart = RequireDisplayPart,
                MinimumContextConfidence = MinimumContextConfidence,
                Selection = new SelectionGuardPolicy
                {
                    Minimum = Selection?.Minimum ?? 0,
                    Maximum = Selection?.Maximum ?? -1,
                    TypesAny = new List<string>(Selection?.TypesAny ?? new List<string>()),
                    TypesAll = new List<string>(Selection?.TypesAll ?? new List<string>())
                }
            };
        }

        public void Overlay(ContextGuardPolicy value)
        {
            if (value == null) return;
            if (value.Modules != null && value.Modules.Count > 0) Modules = new List<string>(value.Modules);
            if (value.BridgeStatuses != null && value.BridgeStatuses.Count > 0) BridgeStatuses = new List<string>(value.BridgeStatuses);
            if (value.InteractionStates != null && value.InteractionStates.Count > 0) InteractionStates = new List<string>(value.InteractionStates);
            if (value.RequireWorkPart.HasValue) RequireWorkPart = value.RequireWorkPart;
            if (value.RequireDisplayPart.HasValue) RequireDisplayPart = value.RequireDisplayPart;
            if (value.MinimumContextConfidence > 0) MinimumContextConfidence = value.MinimumContextConfidence;
            if (value.Selection != null)
            {
                if (value.Selection.Minimum > 0) Selection.Minimum = value.Selection.Minimum;
                if (value.Selection.Maximum >= 0) Selection.Maximum = value.Selection.Maximum;
                if (value.Selection.TypesAny != null && value.Selection.TypesAny.Count > 0)
                    Selection.TypesAny = new List<string>(value.Selection.TypesAny);
                if (value.Selection.TypesAll != null && value.Selection.TypesAll.Count > 0)
                    Selection.TypesAll = new List<string>(value.Selection.TypesAll);
            }
        }
    }

    public sealed class FallbackPolicy
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "show_reason";

        [JsonPropertyName("target_module")]
        public string TargetModule { get; set; } = string.Empty;

        [JsonPropertyName("retry_once")]
        public bool RetryOnce { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public sealed class LeaderBehaviorDefaults
    {
        [JsonPropertyName("minimum_context_confidence")]
        public int MinimumContextConfidence { get; set; } = 60;

        [JsonPropertyName("bridge_statuses")]
        public List<string> BridgeStatuses { get; set; } = new List<string> { "running" };

        [JsonPropertyName("interaction_states")]
        public List<string> InteractionStates { get; set; } = new List<string> { "idle" };

        [JsonPropertyName("on_unavailable")]
        public FallbackPolicy OnUnavailable { get; set; } = new FallbackPolicy();

        public ContextGuardPolicy ToGuards()
        {
            return new ContextGuardPolicy
            {
                MinimumContextConfidence = MinimumContextConfidence,
                BridgeStatuses = new List<string>(BridgeStatuses ?? new List<string>()),
                InteractionStates = new List<string>(InteractionStates ?? new List<string>())
            };
        }
    }

    public sealed class CommandBehaviorPolicy
    {
        [JsonPropertyName("guards")]
        public ContextGuardPolicy Guards { get; set; } = new ContextGuardPolicy();

        [JsonPropertyName("confirmation")]
        public string Confirmation { get; set; } = string.Empty;

        [JsonPropertyName("on_unavailable")]
        public FallbackPolicy OnUnavailable { get; set; } = new FallbackPolicy();
    }

    public sealed class ResolvedCommandBehavior
    {
        public ContextGuardPolicy Guards { get; set; } = new ContextGuardPolicy();
        public FallbackPolicy OnUnavailable { get; set; } = new FallbackPolicy();
        public bool ConfirmationRequired { get; set; }
    }

    public sealed class LeaderBehaviorProfile
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("timeouts")]
        public LeaderTimeoutPolicy Timeouts { get; set; } = new LeaderTimeoutPolicy();

        [JsonPropertyName("defaults")]
        public LeaderBehaviorDefaults Defaults { get; set; } = new LeaderBehaviorDefaults();

        [JsonPropertyName("commands")]
        public Dictionary<string, CommandBehaviorPolicy> Commands { get; set; } =
            new Dictionary<string, CommandBehaviorPolicy>(StringComparer.OrdinalIgnoreCase);

        [JsonIgnore]
        public string SourcePath { get; private set; } = string.Empty;

        public static LeaderBehaviorProfile LoadDefault()
        {
            foreach (string candidate in CandidatePaths())
            {
                if (File.Exists(candidate)) return Load(candidate);
            }
            var fallback = new LeaderBehaviorProfile();
            fallback.Normalize();
            return fallback;
        }

        public static LeaderBehaviorProfile Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Путь профиля автоматов не задан.", nameof(path));
            string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                LeaderBehaviorProfile profile = JsonSerializer.Deserialize<LeaderBehaviorProfile>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = false,
                    ReadCommentHandling = JsonCommentHandling.Disallow
                });
                if (profile == null) throw new InvalidOperationException("Профиль автоматов пуст: " + fullPath);
                profile.SourcePath = fullPath;
                profile.Normalize();
                return profile;
            }
        }

        public ResolvedCommandBehavior Resolve(SequenceDefinition command)
        {
            ContextGuardPolicy guards = (Defaults ?? new LeaderBehaviorDefaults()).ToGuards();
            var fallback = CloneFallback(Defaults?.OnUnavailable);
            bool confirmationRequired = command?.Destructive == true || command?.ConfirmBeforeExecute == true;

            if (command != null)
            {
                guards.RequireWorkPart = command.NeedsWorkPart;
                if (command.RequiresSelection) guards.Selection.Minimum = Math.Max(1, command.MinimumSelectionCount);
                if (!string.IsNullOrWhiteSpace(command.ModuleId)) guards.Modules.Add(command.ModuleId);

                string key = NormalizeSequence(command.Id);
                if (Commands != null && Commands.TryGetValue(key, out CommandBehaviorPolicy policy) && policy != null)
                {
                    guards.Overlay(policy.Guards);
                    if (policy.OnUnavailable != null &&
                        (!string.IsNullOrWhiteSpace(policy.OnUnavailable.Action) ||
                         !string.IsNullOrWhiteSpace(policy.OnUnavailable.Message) ||
                         !string.IsNullOrWhiteSpace(policy.OnUnavailable.TargetModule)))
                    {
                        fallback = CloneFallback(policy.OnUnavailable);
                    }
                    if (string.Equals(policy.Confirmation, "required", StringComparison.OrdinalIgnoreCase)) confirmationRequired = true;
                    if (string.Equals(policy.Confirmation, "none", StringComparison.OrdinalIgnoreCase) && command.Destructive != true)
                        confirmationRequired = false;
                }
            }

            guards.Modules = guards.Modules
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(ContextGuardEvaluator.NormalizeModule)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            guards.BridgeStatuses = (guards.BridgeStatuses ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            guards.InteractionStates = (guards.InteractionStates ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ResolvedCommandBehavior
            {
                Guards = guards,
                OnUnavailable = fallback,
                ConfirmationRequired = confirmationRequired
            };
        }

        private void Normalize()
        {
            if (SchemaVersion != 1) throw new InvalidOperationException("Неподдерживаемая версия профиля автоматов: " + SchemaVersion);
            Timeouts ??= new LeaderTimeoutPolicy();
            Timeouts.Normalize();
            Defaults ??= new LeaderBehaviorDefaults();
            Defaults.OnUnavailable ??= new FallbackPolicy();
            Commands ??= new Dictionary<string, CommandBehaviorPolicy>(StringComparer.OrdinalIgnoreCase);
            Commands = Commands.ToDictionary(
                pair => NormalizeSequence(pair.Key),
                pair => pair.Value ?? new CommandBehaviorPolicy(),
                StringComparer.OrdinalIgnoreCase);
        }

        private static FallbackPolicy CloneFallback(FallbackPolicy source)
        {
            source ??= new FallbackPolicy();
            return new FallbackPolicy
            {
                Action = string.IsNullOrWhiteSpace(source.Action) ? "show_reason" : source.Action.Trim().ToLowerInvariant(),
                TargetModule = ContextGuardEvaluator.NormalizeModule(source.TargetModule),
                RetryOnce = source.RetryOnce,
                Message = source.Message ?? string.Empty
            };
        }

        private static string NormalizeSequence(string sequence)
        {
            return new string((sequence ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        private static IEnumerable<string> CandidatePaths()
        {
            string environment = Environment.GetEnvironmentVariable("NXKEYS_STATE_MACHINE_CONFIG");
            if (!string.IsNullOrWhiteSpace(environment)) yield return Path.GetFullPath(Environment.ExpandEnvironmentVariables(environment));

            string baseDirectory = AppContext.BaseDirectory;
            yield return Path.Combine(baseDirectory, "nx2512-state-machines.json");
            yield return Path.GetFullPath(Path.Combine(baseDirectory, "config", "nx2512-state-machines.json"));
            yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "config", "nx2512-state-machines.json"));
            yield return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "config", "nx2512-state-machines.json"));
        }
    }
}
