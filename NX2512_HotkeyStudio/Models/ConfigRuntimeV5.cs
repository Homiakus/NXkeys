using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NX2512_HotkeyStudio.Models
{
    public static class MenuScriptDefaults
    {
        public const int Version = 139;
        public const int MaxVersion = 139;
        public const int ToolbarVersion = 170;
        public static int NormalizeVersion(int value) => value > 0 && value <= MaxVersion ? value : Version;
        public static int ExpectedVersionForPath(string path)
        {
            string extension = Path.GetExtension(path ?? string.Empty);
            return extension.Equals(".tbr", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".rtb", StringComparison.OrdinalIgnoreCase) ? ToolbarVersion : Version;
        }
    }

    public static class BasicShortcutPolicy
    {
        public static readonly IReadOnlyDictionary<string, string> Required =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ctrl+N"] = "UG_FILE_NEW",
                ["Ctrl+O"] = "UG_FILE_OPEN",
                ["Ctrl+S"] = "UG_FILE_SAVE_PART",
                ["Ctrl+Shift+S"] = "UG_FILE_SAVE_AS",
                ["Ctrl+Z"] = "UG_EDIT_UNDO",
                ["Ctrl+Y"] = "UG_EDIT_REDO",
                ["Ctrl+X"] = "UG_EDIT_CUT",
                ["Ctrl+C"] = "UG_EDIT_COPY",
                ["Ctrl+V"] = "UG_EDIT_PASTE",
                ["Delete"] = "UG_EDIT_DELETE",
                ["Ctrl+F"] = "UG_VIEW_FIT",
                ["F5"] = "UG_VIEW_REFRESH"
            };

        public static string NormalizeShortcut(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();

        public static bool IsAllowed(string value)
        {
            string normalized = NormalizeShortcut(value);
            return Required.Keys.Any(key => NormalizeShortcut(key) == normalized);
        }
    }

    public sealed class Config
    {
        public const int CurrentSchemaVersion = 6;
        private const int MinimumSupportedSchemaVersion = 3;

        [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [JsonPropertyName("profile")] public ProfileConfig Profile { get; set; } = new ProfileConfig();
        [JsonPropertyName("scan")] public ScanConfig Scan { get; set; } = new ScanConfig();
        [JsonPropertyName("deployment")] public DeploymentConfig Deployment { get; set; } = new DeploymentConfig();
        [JsonPropertyName("keyboard")] public List<Binding> Keyboard { get; set; } = new List<Binding>();
        [JsonPropertyName("modules")] public List<ModuleConfig> Modules { get; set; } = new List<ModuleConfig>();
        [JsonPropertyName("workflow_controls")] public WorkflowControls WorkflowControls { get; set; } = new WorkflowControls();
        [JsonPropertyName("performance")] public PerformanceConfig Performance { get; set; } = new PerformanceConfig();
        [JsonPropertyName("role_deployment")] public RoleDeployment Role { get; set; } = new RoleDeployment();
        [JsonPropertyName("leader_key")] public LeaderKeyConfig LeaderKey { get; set; } = new LeaderKeyConfig();

        public static Config Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Config file not found", path);
            string json;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8)) json = reader.ReadToEnd();
            ValidateSourceSchemaVersion(json);
            Config config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new Config();
            config.ExpandEnvironment();
            config.ApplyDefaults();
            config.Validate();
            return config;
        }

        private static void ValidateSourceSchemaVersion(string json)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                }))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object ||
                        !document.RootElement.TryGetProperty("schema_version", out JsonElement schemaElement) ||
                        !schemaElement.TryGetInt32(out int sourceVersion))
                        throw new InvalidOperationException("Configuration schema_version is required and must be an integer.");
                    if (sourceVersion < MinimumSupportedSchemaVersion || sourceVersion > CurrentSchemaVersion)
                        throw new InvalidOperationException(
                            $"Unsupported configuration schema_version {sourceVersion}. Supported range is " +
                            $"{MinimumSupportedSchemaVersion}..{CurrentSchemaVersion}.");
                }
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Configuration JSON is invalid: " + exception.Message, exception);
            }
        }

        public void Save(string path)
        {
            ApplyDefaults();
            Validate();
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }) + Environment.NewLine;
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            NX2512_HotkeyStudio.Services.AtomicFileWriter.WriteAllText(
                path, json, true, new UTF8Encoding(false));
        }

        public void ApplyDefaults()
        {
            if (SchemaVersion < MinimumSupportedSchemaVersion || SchemaVersion > CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Unsupported configuration schema_version {SchemaVersion}. Supported range is " +
                    $"{MinimumSupportedSchemaVersion}..{CurrentSchemaVersion}.");
            Profile ??= new ProfileConfig();
            if (string.IsNullOrWhiteSpace(Profile.NXVersion)) Profile.NXVersion = "2512";
            Scan ??= new ScanConfig();
            Scan.ApplyDefaults();
            Deployment ??= new DeploymentConfig();
            Deployment.ApplyDefaults(Profile.NXVersion);
            Keyboard ??= new List<Binding>();
            Modules ??= new List<ModuleConfig>();
            WorkflowControls ??= new WorkflowControls();
            WorkflowControls.ApplyDefaults();
            Performance ??= new PerformanceConfig();
            Performance.ApplyDefaults();
            Role ??= new RoleDeployment();
            Role.ApplyDefaults(Profile.NXVersion);
            LeaderKey ??= new LeaderKeyConfig();
            LeaderKey.ApplyDefaults();
            MigrateModuleCommands();
            LeaderKey.SlotKeyMap = null;
            SchemaVersion = CurrentSchemaVersion;
            LeaderKey.RebuildFromModules(Modules);
        }

        private void MigrateModuleCommands()
        {
            foreach (ModuleConfig module in Modules ?? Enumerable.Empty<ModuleConfig>())
            {
                if (module?.CommandSets == null) continue;
                foreach (ModuleCommandSet set in module.CommandSets.Where(set => set?.Commands != null))
                {
                    int order = 1;
                    foreach (ModuleCommand command in set.Commands.Where(command => command != null))
                    {
                        command.Command ??= new CommandRef();
                        command.Path ??= new List<string>();
                        command.PathLabels ??= new List<string>();
                        command.Aliases ??= new List<List<string>>();
                        command.SearchAliases ??= new List<string>();
                        if (string.IsNullOrWhiteSpace(command.InputKey))
                            command.InputKey = LeaderKey.ResolveInputKey(command.Slot, order);
                        command.InputKey = LeaderKeyConfig.NormalizeInputKey(command.InputKey);
                        if (command.DisplayOrder <= 0) command.DisplayOrder = order;
                        if (string.Equals(set.ID, "primary", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(command.InputKey))
                            AddAlias(command, command.InputKey);
                        if (!string.IsNullOrWhiteSpace(command.SubmenuKey) &&
                            !string.IsNullOrWhiteSpace(command.InputKey))
                            AddAlias(command, command.SubmenuKey, command.InputKey);
                        if (string.IsNullOrWhiteSpace(command.Action))
                            command.Action = SelectionIntent.ActionFor(command);
                        if (string.IsNullOrWhiteSpace(command.SelectionType))
                            command.SelectionType = SelectionIntent.SelectionTypeFor(command);
                        order++;
                    }
                }
            }
            MnemonicPathGenerator.Apply(Modules);
        }

        private static void AddAlias(ModuleCommand command, params string[] path)
        {
            List<string> alias = MnemonicPathGenerator.NormalizePath(path).ToList();
            if (alias.Count == 0) return;
            command.Aliases ??= new List<List<string>>();
            if (!command.Aliases.Any(existing => MnemonicPathGenerator.NormalizePath(existing)
                    .SequenceEqual(alias, StringComparer.OrdinalIgnoreCase)))
                command.Aliases.Add(alias);
        }

        public void ExpandEnvironment()
        {
            ExpandList(Scan?.Roots);
            ExpandList(Scan?.InstallHints);
            ExpandList(Scan?.ProfileHints);
            if (Deployment != null)
            {
                Deployment.ManagedRoot = ExpandPath(Deployment.ManagedRoot);
                Deployment.BackupRoot = ExpandPath(Deployment.BackupRoot);
                Deployment.NXExecutable = ExpandPath(Deployment.NXExecutable);
                Deployment.ExistingCustomDirsFile = ExpandPath(Deployment.ExistingCustomDirsFile);
            }
            if (Role != null)
            {
                Role.SourceMTX = ExpandPath(Role.SourceMTX);
                Role.TargetDirectory = ExpandPath(Role.TargetDirectory);
            }
        }

        private static void ExpandList(List<string> values)
        {
            if (values == null) return;
            for (int index = 0; index < values.Count; index++) values[index] = ExpandPath(values[index]);
        }

        private static readonly Regex PercentEnvironment =
            new Regex(@"%([A-Za-z_][A-Za-z0-9_]*)%", RegexOptions.Compiled);

        public static string ExpandPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string expanded = PercentEnvironment.Replace(path, match =>
                Environment.GetEnvironmentVariable(match.Groups[1].Value) ?? match.Value);
            expanded = Environment.ExpandEnvironmentVariables(expanded);
            if (expanded.StartsWith("~", StringComparison.Ordinal))
                expanded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), expanded.Substring(1).TrimStart('\\', '/'));
            try { return Path.GetFullPath(expanded); }
            catch { return expanded; }
        }

        public void Validate()
        {
            var problems = new List<string>();
            if (SchemaVersion < MinimumSupportedSchemaVersion || SchemaVersion > CurrentSchemaVersion)
                problems.Add("schema_version must be between 3 and 6");
            if (Profile == null || string.IsNullOrWhiteSpace(Profile.Name)) problems.Add("profile.name is required");
            if (Deployment == null || string.IsNullOrWhiteSpace(Deployment.ManagedRoot)) problems.Add("deployment.managed_root is required");
            if (Deployment == null || string.IsNullOrWhiteSpace(Deployment.BackupRoot)) problems.Add("deployment.backup_root is required");
            if (Deployment != null && Deployment.Mode != "managed-wrapper" && Deployment.Mode != "existing-custom-dirs")
                problems.Add("deployment.mode must be managed-wrapper or existing-custom-dirs");
            ValidateBasicShortcuts(problems);
            ValidateModules(problems);
            LeaderKey?.Validate(problems);
            if (Role != null && Role.Enabled && string.IsNullOrWhiteSpace(Role.SourceMTX))
                problems.Add("role_deployment.source_mtx is required when role deployment is enabled");
            if (problems.Count == 0) return;
            problems.Sort(StringComparer.OrdinalIgnoreCase);
            throw new InvalidOperationException("Configuration is invalid:\n- " + string.Join("\n- ", problems));
        }

        private void ValidateBasicShortcuts(List<string> problems)
        {
            List<Binding> enabled = (Keyboard ?? new List<Binding>()).Where(value => value != null && value.Enabled).ToList();
            var byShortcut = new Dictionary<string, Binding>(StringComparer.OrdinalIgnoreCase);
            foreach (Binding binding in enabled)
            {
                string normalized = BasicShortcutPolicy.NormalizeShortcut(binding.Shortcut);
                if (!BasicShortcutPolicy.IsAllowed(binding.Shortcut))
                    problems.Add("non-basic shortcut is forbidden: " + binding.Shortcut);
                if (!byShortcut.TryAdd(normalized, binding)) problems.Add("duplicate shortcut: " + binding.Shortcut);
                if (binding.Command == null || string.IsNullOrWhiteSpace(binding.Command.ID))
                    problems.Add("basic shortcut requires exact command.id: " + binding.Shortcut);
            }
            foreach (KeyValuePair<string, string> required in BasicShortcutPolicy.Required)
            {
                string key = BasicShortcutPolicy.NormalizeShortcut(required.Key);
                if (!byShortcut.TryGetValue(key, out Binding binding))
                    problems.Add("missing required basic shortcut: " + required.Key);
                else if (!string.Equals(binding.Command?.ID, required.Value, StringComparison.OrdinalIgnoreCase))
                    problems.Add(required.Key + " must target " + required.Value);
            }
            if (enabled.Count != BasicShortcutPolicy.Required.Count)
                problems.Add($"exactly {BasicShortcutPolicy.Required.Count} enabled basic shortcuts are required");
        }

        private void ValidateModules(List<string> problems)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModuleConfig module in (Modules ?? new List<ModuleConfig>()).Where(value => value != null && value.Enabled))
            {
                if (string.IsNullOrWhiteSpace(module.ID) || !ids.Add(module.ID)) problems.Add("module id is missing or repeated: " + module.ID);
                string prefix = LeaderKeyConfig.NormalizeInputKey(module.LeaderPrefix);
                if (string.IsNullOrWhiteSpace(prefix) || !prefixes.Add(prefix)) problems.Add("module leader_prefix is missing or repeated: " + module.ID);
                List<ModuleCommand> commands = module.CommandSets?
                    .Where(set => set?.Commands != null).SelectMany(set => set.Commands).Where(value => value != null).ToList()
                    ?? new List<ModuleCommand>();
                if (commands.Count == 0) problems.Add($"module {module.ID} must contain commands");

                var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (ModuleCommand command in commands.Where(command => command.Enabled))
                {
                    IReadOnlyList<string> canonical = MnemonicPathGenerator.NormalizePath(command.Path);
                    if (canonical.Count == 0)
                    {
                        string submenu = LeaderKeyConfig.NormalizeInputKey(command.SubmenuKey);
                        string input = LeaderKeyConfig.NormalizeInputKey(command.InputKey);
                        canonical = string.IsNullOrWhiteSpace(submenu) ? new[] { input } : new[] { submenu, input };
                    }
                    ValidatePath(module, command, canonical, false, paths, problems);
                    foreach (List<string> alias in command.Aliases ?? new List<List<string>>())
                        ValidatePath(module, command, MnemonicPathGenerator.NormalizePath(alias), true, paths, problems);

                    if (command.DisplayOrder <= 0) problems.Add($"module {module.ID} has invalid display_order {command.DisplayOrder}");
                    if (command.Command == null || string.IsNullOrWhiteSpace(command.Command.ID))
                        problems.Add($"module {module.ID} requires exact command.id");
                    if (string.Equals(command.Action, "switch_module", StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrWhiteSpace(command.TargetModuleID))
                        problems.Add($"module {module.ID} switch command {command.Command?.ID} requires target_module_id");
                    if (command.Command == null || string.IsNullOrWhiteSpace(command.Command.Name))
                        problems.Add($"module {module.ID} requires command.name");
                }

                string[] ordered = paths.Keys.OrderBy(value => value.Length).ThenBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
                for (int left = 0; left < ordered.Length; left++)
                {
                    for (int right = left + 1; right < ordered.Length; right++)
                    {
                        if (ordered[right].StartsWith(ordered[left], StringComparison.OrdinalIgnoreCase))
                            problems.Add($"module {module.ID} path {ordered[left]} is both a command and a prefix of {ordered[right]}");
                    }
                }
            }
            if (ids.Count == 0) problems.Add("at least one enabled module is required");
        }

        private static void ValidatePath(
            ModuleConfig module,
            ModuleCommand command,
            IReadOnlyList<string> path,
            bool alias,
            Dictionary<string, string> paths,
            List<string> problems)
        {
            if (path == null || path.Count == 0)
            {
                problems.Add($"module {module.ID} command {command.Command?.ID} has empty {(alias ? "alias" : "path")}");
                return;
            }
            if (path.Count > 5) problems.Add($"module {module.ID} command {command.Command?.ID} path is longer than 5 tokens");
            if (path.Any(token => string.IsNullOrWhiteSpace(LeaderKeyConfig.NormalizeInputKey(token))))
                problems.Add($"module {module.ID} command {command.Command?.ID} contains invalid path token");
            string key = string.Concat(path);
            string owner = command.Command?.ID ?? command.Command?.Name ?? string.Empty;
            if (paths.TryGetValue(key, out string existing) && !string.Equals(existing, owner, StringComparison.OrdinalIgnoreCase))
                problems.Add($"module {module.ID} repeats mnemonic path {key}: {existing} and {owner}");
            else
                paths[key] = owner;
        }
    }

    public static class SelectionIntent
    {
        public const string ExecuteCommandAction = "execute_command";
        public const string SetSelectionFilterAction = "set_selection_filter";

        public static string ActionFor(ModuleCommand command)
        {
            string explicitAction = command?.Action?.Trim();
            if (!string.IsNullOrWhiteSpace(explicitAction)) return explicitAction;
            return IsSelectionFilterCommand(command?.Command?.ID)
                ? SetSelectionFilterAction
                : ExecuteCommandAction;
        }

        public static string SelectionTypeFor(ModuleCommand command)
        {
            string explicitType = command?.SelectionType?.Trim();
            if (!string.IsNullOrWhiteSpace(explicitType)) return NormalizeSelectionType(explicitType);
            string inferred = InferSelectionType(command?.Command?.ID, command?.Command?.Name, command?.Notes);
            return string.IsNullOrWhiteSpace(inferred) && command?.RequiresSelection == true ? "all" : inferred;
        }

        public static string InferSelectionType(string commandId, string commandName = "", string notes = "")
        {
            string text = string.Join(" ", commandId ?? string.Empty, commandName ?? string.Empty, notes ?? string.Empty).ToUpperInvariant();
            if (text.Contains("DESELECT")) return "none";
            if (text.Contains("SELECT_ALL")) return "all";
            if (text.Contains("RESET")) return "reset";
            if (text.Contains("EDGE")) return "edge";
            if (text.Contains("FACE") || text.Contains("SURFACE") || text.Contains("SHEET_BOUNDARY")) return "face";
            if (text.Contains("BODY") || text.Contains("SOLID") || text.Contains("SHEET_METAL")) return "body";
            if (text.Contains("COMPONENT") || text.Contains("ASSEMBL")) return "component";
            if (text.Contains("CURVE") || text.Contains("LINE") || text.Contains("ARC") || text.Contains("CIRCLE")) return "curve";
            if (text.Contains("DATUM") || text.Contains("COORDINATE_SYSTEM")) return "datum";
            if (text.Contains("FEATURE") || text.Contains("TEMPLATE")) return "feature";
            if (text.Contains("OPERATION") || text.Contains("TOOL_PATH") || text.Contains("CAM_")) return "operation";
            return string.Empty;
        }

        public static bool IsSelectionFilterCommand(string commandId)
        {
            string id = commandId ?? string.Empty;
            return id.StartsWith("UG_SEL_", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSelectionType(string value)
        {
            string normalized = new string((value ?? string.Empty).Trim().ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
            while (normalized.Contains("__", StringComparison.Ordinal)) normalized = normalized.Replace("__", "_");
            return normalized.Trim('_');
        }
    }

}
