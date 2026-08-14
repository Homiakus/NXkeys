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

    public sealed partial class Config
    {
        public const int CurrentSchemaVersion = 8;
        private const int MinimumSupportedSchemaVersion = 3;

        [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [JsonPropertyName("operations")] public List<OperationContract> Operations { get; set; } = new List<OperationContract>();
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
            {
                return LoadEmbedded();
            }
            string json;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8)) json = reader.ReadToEnd();
            ValidateSourceSchemaVersion(json);
            Config deserialized = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new Config();
            deserialized.ExpandEnvironment();
            deserialized.ApplyDefaults();
            deserialized.Validate();
            return deserialized;
        }

        public static Config LoadEmbedded()
        {
            var assembly = typeof(Config).Assembly;
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("nx2512-v8-profile.json", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(resourceName))
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string json = reader.ReadToEnd();
                    Config deserialized = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    }) ?? new Config();
                    deserialized.ExpandEnvironment();
                    deserialized.ApplyDefaults();
                    deserialized.Validate();
                    return deserialized;
                }
            }
            Config config = new Config();
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

            List<ModuleConfig> preservedModules = Modules;
            List<Binding> preservedKeyboard = Keyboard;
            bool isV8Profile = SchemaVersion == 8 && Operations != null && Operations.Count > 0;
            if (isV8Profile)
            {
                Modules = null;
                Keyboard = null;
            }

            try
            {
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
            finally
            {
                if (isV8Profile)
                {
                    Modules = preservedModules;
                    Keyboard = preservedKeyboard;
                }
            }
        }

        public void ApplyDefaults()
        {
            if (SchemaVersion < MinimumSupportedSchemaVersion || SchemaVersion > CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Unsupported configuration schema_version {SchemaVersion}. Supported range is " +
                    $"{MinimumSupportedSchemaVersion}..{CurrentSchemaVersion}.");

            if (SchemaVersion == 8 && Operations != null && Operations.Count > 0)
            {
                TranslateV8OperationsToLegacy();
            }
            else if ((Modules == null || Modules.Count == 0) &&
                     (Operations == null || Operations.Count == 0))
            {
                TranslateV8OperationsToLegacy();
            }

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
                        // v8-translated commands already have locked paths — skip
                        // auto-generated slot aliases that would mask the real path.
                        bool isV8Translated = string.Equals(command.PathSource, "v8", StringComparison.OrdinalIgnoreCase);
                        if (!isV8Translated &&
                            string.Equals(set.ID, "primary", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(command.InputKey))
                            AddAlias(command, command.InputKey);
                        if (!isV8Translated &&!string.IsNullOrWhiteSpace(command.SubmenuKey) &&
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
                problems.Add($"schema_version must be between {MinimumSupportedSchemaVersion} and {CurrentSchemaVersion}");
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
            // Only infer selection type for selection-filter commands (UG_SEL_*).
            // For execute_command operations, an empty selection filter is the correct
            // default — inferring "feature" from "UG_MODELING_EXTRUDED_FEATURE" would
            // cause allowlist mismatches.
            if (!IsSelectionFilterCommand(command?.Command?.ID))
                return string.Empty;
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

    public sealed partial class Config
    {
        /// <summary>
        /// Builds a hardcoded curated set of v8 modules.  No JSON profile needed.
        /// Paths are hand-curated and locked so MnemonicPathGenerator won't rewrite them.
        /// </summary>
        private void BuildHardcodedModules()
        {
            Modules = new List<ModuleConfig>();
            Keyboard = new List<Binding>();
            foreach (var kvp in BasicShortcutPolicy.Required)
                Keyboard.Add(new Binding { Shortcut = kvp.Key, Enabled = true, Command = new CommandRef { ID = kvp.Value, Name = kvp.Value } });

            // Helper to register a command in a module
            void Add(string modulePrefix, string nxAppId, string path, string cmdId, string cmdName, string notes = "")
            {
                string moduleId = "v8_" + modulePrefix.ToLowerInvariant();
                ModuleConfig mod = Modules.FirstOrDefault(m => string.Equals(m.ID, moduleId, StringComparison.OrdinalIgnoreCase));
                if (mod == null)
                {
                    mod = new ModuleConfig
                    {
                        ID = moduleId, Enabled = true, Label = modulePrefix, LeaderPrefix = modulePrefix,
                        NXApplicationIDs = new List<string> { nxAppId },
                        SwitchCommand = new CommandRef { ID = nxAppId, Name = "Switch to " + modulePrefix },
                        CommandSets = new List<ModuleCommandSet> { new ModuleCommandSet { ID = "primary", Commands = new List<ModuleCommand>() } }
                    };
                    Modules.Add(mod);
                }
                var tokens = path.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToUpperInvariant()).Where(t => t.Length > 0).ToList();
                if (tokens.Count == 0) return;
                mod.CommandSets[0].Commands.Add(new ModuleCommand
                {
                    Enabled = true,
                    Command = new CommandRef { ID = cmdId, Name = cmdName },
                    Action = SelectionIntent.ExecuteCommandAction,
                    SelectionType = string.Empty,
                    Path = tokens,
                    PathLabels = new List<string> { cmdName },
                    Aliases = new List<List<string>>(),
                    SearchAliases = new List<string> { cmdName, cmdId },
                    DisplayOrder = mod.CommandSets[0].Commands.Count + 1,
                    PathLocked = true,
                    PathSource = "hardcoded",
                    Notes = notes
                });
            }

            // ═══════════════════════════════════════════════════════════════
            // M — MODELING (all create/edit/transform/delete/file commands)
            // ═══════════════════════════════════════════════════════════════
            Add("M", "UG_APP_MODELING", "C S", "UG_CREATE_SKETCH", "Create Sketch");
            Add("M", "UG_APP_MODELING", "C E", "UG_MODELING_EXTRUDED_FEATURE", "Extrude");
            Add("M", "UG_APP_MODELING", "C R", "UG_MODELING_REVOLVED_FEATURE", "Revolve");
            Add("M", "UG_APP_MODELING", "C H", "UG_MODELING_HOLE_FEATURE", "Hole");
            Add("M", "UG_APP_MODELING", "C B", "UG_MODELING_BLEND_FEATURE", "Edge Blend");
            Add("M", "UG_APP_MODELING", "C C", "UG_MODELING_CHAMFER_FEATURE", "Chamfer");
            Add("M", "UG_APP_MODELING", "C U", "UG_MODELING_FF_SWEPT", "Swept");
            Add("M", "UG_APP_MODELING", "C L", "UG_MODELING_FF_THROUGH_CURVES", "Through Curves");
            Add("M", "UG_APP_MODELING", "C D", "UG_MODELING_FF_FIT_SURFACE", "Studio Surface");
            Add("M", "UG_APP_MODELING", "C G", "UG_MODELING_EXTRACT_GEOMETRY", "Extract Geometry");
            Add("M", "UG_APP_MODELING", "C F", "UG_MODELING_SHEET_FEATURE", "Sheet Body");
            Add("M", "UG_APP_MODELING", "C X", "UG_EXPRESSIONS", "Expressions");
            // Edit
            Add("M", "UG_APP_MODELING", "E B", "UG_MODELING_BLEND_FEATURE", "Edge Blend");
            Add("M", "UG_APP_MODELING", "E C", "UG_MODELING_CHAMFER_FEATURE", "Chamfer");
            Add("M", "UG_APP_MODELING", "E S", "UG_MODELING_SEW_FEATURE", "Sew");
            Add("M", "UG_APP_MODELING", "E T", "UG_MODELING_TRIM_SHEET_FEATURE", "Trim Sheet");
            Add("M", "UG_APP_MODELING", "E X", "UG_MODELING_FF_EXTEND_SHEET", "Extend Sheet");
            Add("M", "UG_APP_MODELING", "E U", "UG_MODELING_UNTRIM_FEATURE", "Untrim");
            Add("M", "UG_APP_MODELING", "E R", "UG_SMART_REPLACE_COMPONENT", "Replace Component");
            // Transform
            Add("M", "UG_APP_MODELING", "T C", "UG_EDIT_COPY", "Copy");
            Add("M", "UG_APP_MODELING", "T M", "UG_ASSY_MOVE_COMPONENT", "Move Component");
            Add("M", "UG_APP_MODELING", "T P", "UG_MODELING_PATTERNFEATURE_FEATURE", "Pattern Feature");
            Add("M", "UG_APP_MODELING", "T F", "UG_MODELING_MIRRORFEATURE_FEATURE", "Mirror Feature");
            // Delete
            Add("M", "UG_APP_MODELING", "D C", "UG_ASSEMBLIES_REMOVE_COMPONENT", "Remove Component");
            Add("M", "UG_APP_MODELING", "D O", "UG_CAM_DELETE_OPERATION", "Delete Operation");
            // Manage
            Add("M", "UG_APP_MODELING", "M W", "UG_ASSY_WAVE_LINKER", "WAVE Geometry Linker");
            Add("M", "UG_APP_MODELING", "M L", "UG_LAYER_SETTINGS", "Layer Settings");
            Add("M", "UG_APP_MODELING", "M V", "UG_LAYER_MOVE", "Move to Layer");
            Add("M", "UG_APP_MODELING", "M M", "UG_MATERIAL_LIBRARY_MANAGER", "Material Library");
            Add("M", "UG_APP_MODELING", "M N", "UG_NAVIGATOR_PART", "Part Navigator");
            // File
            Add("M", "UG_APP_MODELING", "F S", "UG_FILE_SAVE_PART", "Save");
            Add("M", "UG_APP_MODELING", "F A", "UG_FILE_SAVE_AS", "Save As");
            Add("M", "UG_APP_MODELING", "F O", "UG_FILE_OPEN", "Open");
            Add("M", "UG_APP_MODELING", "F N", "UG_FILE_NEW", "New");
            // Direct single-key shortcuts
            Add("M", "UG_APP_MODELING", "S", "UG_CREATE_SKETCH", "Create Sketch (direct)");
            Add("M", "UG_APP_MODELING", "X", "UG_MODELING_EXTRUDED_FEATURE", "Extrude (direct)");
            Add("M", "UG_APP_MODELING", "R", "UG_MODELING_REVOLVED_FEATURE", "Revolve (direct)");
            Add("M", "UG_APP_MODELING", "H", "UG_MODELING_HOLE_FEATURE", "Hole (direct)");
            Add("M", "UG_APP_MODELING", "B", "UG_MODELING_BLEND_FEATURE", "Edge Blend (direct)");

            // ═══════════════════════════════════════════════════════════════
            // S — SKETCH
            // ═══════════════════════════════════════════════════════════════
            Add("S", "UG_APP_SKETCH", "L", "UG_SKETCH_LINE", "Line");
            Add("S", "UG_APP_SKETCH", "R", "UG_SKETCH_RECTANGLE", "Rectangle");
            Add("S", "UG_APP_SKETCH", "C", "UG_SKETCH_CIRCLE", "Circle");
            Add("S", "UG_APP_SKETCH", "A", "UG_SKETCH_ARC", "Arc");
            Add("S", "UG_APP_SKETCH", "T", "UG_SKETCH_QUICK_TRIM", "Quick Trim");
            Add("S", "UG_APP_SKETCH", "E", "UG_SKETCH_QUICK_EXTEND", "Quick Extend");
            Add("S", "UG_APP_SKETCH", "O", "UG_SKETCH_OFFSET_CURVES", "Offset Curve");
            Add("S", "UG_APP_SKETCH", "D R", "UG_SKETCH_RAPID_DIMENSION", "Rapid Dimension");
            Add("S", "UG_APP_SKETCH", "D L", "UG_SKETCH_LINEAR_DIMENSION", "Linear Dimension");
            Add("S", "UG_APP_SKETCH", "F", "UG_SKETCH_FINISH", "Finish Sketch");
            Add("S", "UG_APP_SKETCH", "Z", "UG_SKETCH_CHECKER", "Sketch Checker");

            // ═══════════════════════════════════════════════════════════════
            // K — CONSTRAINTS (sketch constraints)
            // ═══════════════════════════════════════════════════════════════
            Add("K", "UG_APP_SKETCH", "C", "UG_SKETCH_COINCIDENT_CONSTRAINT", "Coincident");
            Add("K", "UG_APP_SKETCH", "H", "UG_SKETCH_HORIZONTAL_CONSTRAINT", "Horizontal");
            Add("K", "UG_APP_SKETCH", "V", "UG_SKETCH_VERTICAL_CONSTRAINT", "Vertical");
            Add("K", "UG_APP_SKETCH", "T", "UG_SKETCH_TANGENT_CONSTRAINT", "Tangent");
            Add("K", "UG_APP_SKETCH", "P", "UG_SKETCH_PARALLEL_CONSTRAINT", "Parallel");
            Add("K", "UG_APP_SKETCH", "N", "UG_SKETCH_PERPENDICULAR_CONSTRAINT", "Perpendicular");

            // ═══════════════════════════════════════════════════════════════
            // A — ASSEMBLY
            // ═══════════════════════════════════════════════════════════════
            Add("A", "UG_APP_ASSEMBLIES", "C A", "UG_ASSY_INSERT_COMPONENT", "Add Component");
            Add("A", "UG_APP_ASSEMBLIES", "C N", "UG_ASSY_INSERT_NEW_COMPONENT", "New Component");
            Add("A", "UG_APP_ASSEMBLIES", "T M", "UG_ASSY_MOVE_COMPONENT", "Move Component");
            Add("A", "UG_APP_ASSEMBLIES", "C K", "UG_ASSY_MATE_COMPONENT", "Assembly Constraints");
            Add("A", "UG_APP_ASSEMBLIES", "E R", "UG_SMART_REPLACE_COMPONENT", "Replace Component");
            Add("A", "UG_APP_ASSEMBLIES", "X C", "UG_ASSEMBLIES_REMOVE_COMPONENT", "Remove Component");
            Add("A", "UG_APP_ASSEMBLIES", "T P", "UG_ASSEMBLIES_PATTERN_COMPONENT", "Pattern Component");

            // ═══════════════════════════════════════════════════════════════
            // D — DRAFTING
            // ═══════════════════════════════════════════════════════════════
            Add("D", "UG_APP_DRAFTING", "C B", "UG_DRAFT_SMASH_VIEW", "Base View");
            Add("D", "UG_APP_DRAFTING", "C P", "UG_DRAFT_DRW_PROJECT_VIEW", "Projected View");
            Add("D", "UG_APP_DRAFTING", "C S", "UG_DRAFT_DRW_SECTION_VIEW", "Section View");
            Add("D", "UG_APP_DRAFTING", "C D", "UG_DRAFT_DRW_DETAIL_VIEW", "Detail View");
            Add("D", "UG_APP_DRAFTING", "P U", "UG_DRAFT_DRW_UPDATE_VIEW", "Update Views");
            Add("D", "UG_APP_DRAFTING", "E V", "UG_DRAFT_EDIT_VIEW_STYLE", "View Style");
            Add("D", "UG_APP_DRAFTING", "A R", "UG_DRAFT_DIMENSION_LINEAR", "Rapid Dimension");

            // ═══════════════════════════════════════════════════════════════
            // V — VIEW / DISPLAY
            // ═══════════════════════════════════════════════════════════════
            Add("V", "UG_APP_DRAFTING", "F", "UG_VIEW_FIT", "Fit View");
            Add("V", "UG_APP_DRAFTING", "T", "UG_VIEW_POPUP_ORIENT_TFRTRI", "Orient Trimetric");
            Add("V", "UG_APP_DRAFTING", "H", "UG_EDIT_BLANK_SELECTED", "Hide Selected");
            Add("V", "UG_APP_DRAFTING", "S", "UG_EDIT_MD_SHOWHIDE_ALL", "Show All");

            // ═══════════════════════════════════════════════════════════════
            // I — INSPECT / MEASURE
            // ═══════════════════════════════════════════════════════════════
            Add("I", "UG_APP_SFEM", "M", "UG_INFO_GEOMETRIC_MEASUREMENT", "Measure");
            Add("I", "UG_APP_SFEM", "O", "UG_INFO_OBJECT", "Object Info");
            Add("I", "UG_APP_SFEM", "F", "UG_ANALYSIS_FACE_CURVATURE", "Face Curvature");

            // ═══════════════════════════════════════════════════════════════
            // H — SHEET METAL
            // ═══════════════════════════════════════════════════════════════
            Add("H", "UG_APP_SHEETMETAL", "C B", "UG_SHEET_METAL_BASE_TAB", "Base Tab");
            Add("H", "UG_APP_SHEETMETAL", "C F", "UG_SHEET_METAL_FLANGE", "Flange");
            Add("H", "UG_APP_SHEETMETAL", "C C", "UG_SHEET_METAL_CONTOUR_FLANGE", "Contour Flange");
            Add("H", "UG_APP_SHEETMETAL", "E B", "UG_SHEET_METAL_BEND", "Bend");
            Add("H", "UG_APP_SHEETMETAL", "T U", "UG_SHEET_METAL_UNBEND", "Unbend");
            Add("H", "UG_APP_SHEETMETAL", "T R", "UG_SHEET_METAL_REBEND", "Rebend");
            Add("H", "UG_APP_SHEETMETAL", "P F", "UG_SHEET_METAL_FLAT_PATTERN", "Flat Pattern");
            Add("H", "UG_APP_SHEETMETAL", "C S", "UG_SBSM_SHEETMETAL_FROM_SOLID_FEATURE", "Convert to Sheet Metal");

            // ═══════════════════════════════════════════════════════════════
            // N — MANUFACTURING / CAM
            // ═══════════════════════════════════════════════════════════════
            Add("N", "UG_APP_MANUFACTURING", "C O", "UG_CAM_CREATE_OPERATION", "Create Operation");
            Add("N", "UG_APP_MANUFACTURING", "C T", "UG_CAM_CREATE_TOOL", "Create Tool");
            Add("N", "UG_APP_MANUFACTURING", "P G", "UG_CAM_GENERATE_TOOL_PATH", "Generate Toolpath");
            Add("N", "UG_APP_MANUFACTURING", "P P", "UG_CAM_POSTPROCESS", "Postprocess");
            Add("N", "UG_APP_MANUFACTURING", "X O", "UG_CAM_DELETE_OPERATION", "Delete Operation");

            // ═══════════════════════════════════════════════════════════════
            // R — ROUTING
            // ═══════════════════════════════════════════════════════════════
            Add("R", "UG_APP_ROUTING", "C R", "UG_ROUTE_CREATE_ROUTE", "Create Route");
            Add("R", "UG_APP_ROUTING", "C P", "UG_ROUTE_PLACE_PART", "Place Part");
            Add("R", "UG_APP_ROUTING", "C S", "UG_ROUTE_ADD_STOCK", "Add Stock");
            Add("R", "UG_APP_ROUTING", "E R", "UG_ROUTE_EDIT_ROUTE", "Edit Route");

            // ═══════════════════════════════════════════════════════════════
            // G — GATEWAY (application switching)
            // ═══════════════════════════════════════════════════════════════
            Add("G", "UG_APP_GATEWAY", "M", "UG_APP_MODELING", "Switch to Modeling");
            Add("G", "UG_APP_GATEWAY", "S", "UG_APP_SKETCH", "Switch to Sketch");
            Add("G", "UG_APP_GATEWAY", "A", "UG_APP_ASSEMBLIES", "Switch to Assemblies");
            Add("G", "UG_APP_GATEWAY", "D", "UG_APP_DRAFTING", "Switch to Drafting");
            Add("G", "UG_APP_GATEWAY", "H", "UG_APP_SHEETMETAL", "Switch to Sheet Metal");
            Add("G", "UG_APP_GATEWAY", "C", "UG_APP_MANUFACTURING", "Switch to Manufacturing");
            Add("G", "UG_APP_GATEWAY", "N", "UG_APP_SFEM", "Switch to Simulation");
            Add("G", "UG_APP_GATEWAY", "P", "UG_APP_PMI", "Switch to PMI");
            Add("G", "UG_APP_GATEWAY", "R", "UG_APP_ROUTING", "Switch to Routing");
            Add("G", "UG_APP_GATEWAY", "O", "UG_APP_MOLDWIZARD", "Switch to Mold Wizard");
            Add("G", "UG_APP_GATEWAY", "L", "UG_NAVIGATOR_REUSE_LIBRARY", "Reuse Library");
            Add("G", "UG_APP_GATEWAY", "V", "UG_APP_GATEWAY", "Switch to Gateway");

            // ═══════════════════════════════════════════════════════════════
            // U — SURFACE
            // ═══════════════════════════════════════════════════════════════
            Add("U", "UG_APP_STUDIO", "C T", "UG_MODELING_THROUGH_CURVES_FEATURE", "Through Curves");
            Add("U", "UG_APP_STUDIO", "C S", "UG_MODELING_SWEPT_FEATURE", "Swept");
            Add("U", "UG_APP_STUDIO", "C D", "UG_MODELING_STUDIO_SURFACE_FEATURE", "Studio Surface");
            Add("U", "UG_APP_STUDIO", "E T", "UG_MODELING_TRIM_SHEET_FEATURE", "Trim Sheet");
            Add("U", "UG_APP_STUDIO", "E S", "UG_MODELING_SEW_FEATURE", "Sew");
            Add("U", "UG_APP_STUDIO", "I C", "UG_ANALYSIS_FACE_CURVATURE", "Face Curvature");

        }

        // Maps NX application IDs to v8 module leader-key prefixes.
        // Used to assign direct / workspace_key operations to the correct context module.
        private static readonly Dictionary<string, string> NxAppIdToModulePrefix =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UG_APP_MODELING"] = "M",
                ["UG_APP_SKETCH"] = "S",
                ["UG_APP_ASSEMBLIES"] = "A",
                ["UG_APP_DRAFTING"] = "D",
                ["UG_APP_PMI"] = "P",
                ["UG_APP_STUDIO"] = "U",
                ["UG_APP_SHEETMETAL"] = "H",
                ["UG_APP_MANUFACTURING"] = "N",
                ["UG_APP_SFEM"] = "I",
                ["UG_APP_DESFEM"] = "I",
                ["UG_APP_ROUTING"] = "R",
                ["UG_APP_MOLDWIZARD"] = "L",
                ["UG_APP_GATEWAY"] = "G",

                // V8 profile availability.applications uses short app names
                // (e.g. "modeling"), not UG_APP_* IDs.  Alias them to the same
                // prefixes so ResolveModulePrefix can honor availability for
                // application-specific operations (modeling.revolve → "M").
                ["modeling"] = "M",
                ["sketch"] = "S",
                ["assemblies"] = "A",
                ["drafting"] = "D",
                ["pmi"] = "P",
                ["surface"] = "U",
                ["sheetmetal"] = "H",
                ["manufacturing"] = "N",
                ["simulation"] = "I",
                ["routing"] = "R",
            };

        // Reverse map: v8 module leader-key prefix → NX application ID,
        // used to populate NXApplicationIDs and SwitchCommand on translated modules.
        private static readonly Dictionary<string, string> ModulePrefixToNxAppId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["M"] = "UG_APP_MODELING",
                ["S"] = "UG_APP_SKETCH",
                ["A"] = "UG_APP_ASSEMBLIES",
                ["D"] = "UG_APP_DRAFTING",
                ["P"] = "UG_APP_PMI",
                ["U"] = "UG_APP_STUDIO",
                ["H"] = "UG_APP_SHEETMETAL",
                ["N"] = "UG_APP_MANUFACTURING",
                ["I"] = "UG_APP_SFEM",
                ["R"] = "UG_APP_ROUTING",
                ["L"] = "UG_APP_MOLDWIZARD",
                ["G"] = "UG_APP_GATEWAY",
                ["Q"] = "UG_APP_MODELING",
            };

        private static bool IsTbdAdapter(OperationContract op)
        {
            string kind = op?.Adapter?.Kind ?? string.Empty;
            // button_id and capability adapters are always ready to execute regardless of status
            // ("tbd_adapter" in status means the mapping needs human review, not
            // that the button ID is missing).
            if (string.Equals(kind, "button_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "capability", StringComparison.OrdinalIgnoreCase))
                return false;
            // internal adapters need explicit "mapped" status to be executable.
            string status = op?.Adapter?.Status ?? string.Empty;
            return status.Contains("tbd", StringComparison.OrdinalIgnoreCase) ||
                   status.Contains("unmapped", StringComparison.OrdinalIgnoreCase) ||
                   !string.Equals(status, "mapped", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines the target v8 module prefix for an operation.
        /// Priority: availability.applications[0] (when it maps to a known NX app) →
        /// first leader token (for global/context-free operations) →
        /// fallback "M" (modeling).
        /// </summary>
        private static string ResolveModulePrefix(OperationContract op)
        {
            string appId = op.Availability?.Applications?.FirstOrDefault() ?? string.Empty;

            // 1. Specific application → mapped module prefix.
            //    Operations like modeling.revolve have availability ["modeling"]
            //    which maps to module prefix "M".
            if (!string.IsNullOrWhiteSpace(appId) &&
                !string.Equals(appId, "global", StringComparison.OrdinalIgnoreCase) &&
                NxAppIdToModulePrefix.TryGetValue(appId, out string appPrefix))
            {
                return appPrefix;
            }

            // 2. Global operations starting with "S": selection filters like global.faces
            //    belong to Modeling ("M") where S is the Selection submenu prefix (M -> S -> F).
            //    They must not be routed into v8_s (Sketch), where 1-token keys (L, F, E, C, T, M)
            //    are reserved for Sketch geometry.
            if (op.Paths?.Leader != null && op.Paths.Leader.Count >= 1)
            {
                string p0 = op.Paths.Leader[0].ToUpperInvariant();
                if (string.Equals(p0, "S", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(appId) || string.Equals(appId, "global", StringComparison.OrdinalIgnoreCase)))
                {
                    return "M";
                }
                return p0;
            }

            // 3. Direct / workspace_key with global availability → try mapped prefix.
            if (!string.IsNullOrWhiteSpace(appId) &&
                NxAppIdToModulePrefix.TryGetValue(appId, out string fallbackPrefix))
                return fallbackPrefix;

            return "M"; // fallback: modeling
        }

        /// <summary>
        /// Returns true when the module prefix was resolved from a specific
        /// application (not from the first leader token).  When true, the
        /// full leader path should be used as the inner navigation path.
        /// </summary>
        private static bool ModulePrefixFromApplication(OperationContract op)
        {
            string appId = op.Availability?.Applications?.FirstOrDefault() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(appId) &&
                   !string.Equals(appId, "global", StringComparison.OrdinalIgnoreCase) &&
                   NxAppIdToModulePrefix.ContainsKey(appId);
        }

        private void TranslateV8OperationsToLegacy()
        {
            Modules ??= new List<ModuleConfig>();
            Modules.Clear();
            Keyboard ??= new List<Binding>();
            if (Keyboard.Count == 0)
            {
                foreach (var kvp in BasicShortcutPolicy.Required)
                {
                    Keyboard.Add(new Binding
                    {
                        Shortcut = kvp.Key,
                        Enabled = true,
                        Command = new CommandRef { ID = kvp.Value, Name = kvp.Value }
                    });
                }
            }

            // ── Pass 1: group operations into modules ──────────────────────────
            var modulesDict = new Dictionary<string, ModuleConfig>(StringComparer.OrdinalIgnoreCase);
            var moduleOps = new Dictionary<string, List<OperationContract>>(StringComparer.OrdinalIgnoreCase);
            var duplicateLeaderWarnings = new List<string>();
            var skippedInvalidKeyWarnings = new List<string>();
            var seenLeaderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int totalSkipped = 0;

            foreach (var op in Operations)
            {
                if (op.Paths == null) { totalSkipped++; continue; }
                if (op.OperationID != null && (
                    op.OperationID.StartsWith("global.sketch_direct_", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(op.OperationID, "sketch.finish_sketch", StringComparison.OrdinalIgnoreCase)))
                {
                    totalSkipped++;
                    continue;
                }

                var leaderPath = op.Paths.Leader;
                string directPath = op.Paths.Direct;
                string workspaceKey = op.Paths.WorkspaceKey;
                bool hasLeader = leaderPath != null && leaderPath.Count >= 1;
                bool hasDirect = !string.IsNullOrWhiteSpace(directPath);
                bool hasWorkspace = !string.IsNullOrWhiteSpace(workspaceKey);

                if (!hasLeader && !hasDirect && !hasWorkspace)
                { totalSkipped++; continue; }

                // Validate direct / workspace keys: only A-Z and 0-9 pass through
                // MapKey in LeaderKeyEngine (line 738-742).  Keys outside this set
                // (Space, Esc, F1, Cyrillic, etc.) are unreachable and are skipped.
                string singleKey = hasDirect ? directPath : hasWorkspace ? workspaceKey : null;
                if (!hasLeader && singleKey != null)
                {
                    string normalized = singleKey.Trim().ToUpperInvariant();
                    if (normalized.Length != 1 ||
                        !((normalized[0] >= 'A' && normalized[0] <= 'Z') ||
                          (normalized[0] >= '0' && normalized[0] <= '9')))
                    {
                        skippedInvalidKeyWarnings.Add(
                            $"Операция {op.OperationID} использует недопустимую " +
                            $"direct/workspace клавишу '{singleKey}' — пропущена.");
                        totalSkipped++;
                        continue;
                    }
                }

                string modulePrefix = ResolveModulePrefix(op);
                string moduleId = "v8_" + modulePrefix.ToLowerInvariant();

                if (!modulesDict.ContainsKey(moduleId))
                {
                    modulesDict[moduleId] = null; // placeholder
                    moduleOps[moduleId] = new List<OperationContract>();
                }
                moduleOps[moduleId].Add(op);
            }

            // ── Pass 2: build modules and resolve path conflicts ───────────────
            foreach (var kvp in moduleOps)
            {
                string moduleId = kvp.Key;
                string modulePrefix = moduleId.Substring(3).ToUpperInvariant(); // "v8_m" → "M"
                string nxAppId = ModulePrefixToNxAppId.TryGetValue(modulePrefix, out string appId)
                    ? appId : "UG_APP_GATEWAY";

                var module = new ModuleConfig
                {
                    ID = moduleId,
                    Enabled = true,
                    Label = modulePrefix,
                    LeaderPrefix = modulePrefix,
                    NXApplicationIDs = new List<string> { nxAppId },
                    SwitchCommand = new CommandRef { ID = nxAppId, Name = "Switch to " + modulePrefix },
                    CommandSets = new List<ModuleCommandSet>
                    {
                        new ModuleCommandSet { ID = "primary", Commands = new List<ModuleCommand>() }
                    }
                };

                // First pass: collect paths and detect duplicates.
                var pathFirstSeen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var pathIsDuplicate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pendingOps = new List<(OperationContract op, List<string> innerPath, int order)>();
                int order = 0;

                foreach (var op in kvp.Value)
                {
                    // Skip global direct-key wrappers that duplicate dedicated domain operations.
                    if (op.OperationID != null && (
                        op.OperationID.StartsWith("global.sketch_direct_", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(op.OperationID, "sketch.finish_sketch", StringComparison.OrdinalIgnoreCase)))
                    {
                        totalSkipped++;
                        continue;
                    }

                    var leaderPath = op.Paths.Leader;
                    string directPath = op.Paths.Direct;
                    string workspaceKey = op.Paths.WorkspaceKey;
                    bool hasLeader = leaderPath != null && leaderPath.Count >= 1;

                    List<string> rawPath;
                    if (hasLeader)
                    {
                        // When the module prefix came from availability.applications
                        // (e.g. modeling.revolve → module M), the FULL leader path is
                        // the navigation within that module (e.g. ["C","R"]).
                        // When the module prefix came from global selection filters in Modeling
                        // (e.g. global.faces -> ["S","F"]), use the full leader path ["S","F"].
                        // For other global ops (like ["M","L"]), skip the first token.
                        bool prefixFromApp = ModulePrefixFromApplication(op);
                        bool isGlobalSelectionInModeling = string.Equals(modulePrefix, "M", StringComparison.OrdinalIgnoreCase) &&
                            op.Paths.Leader.Count >= 2 &&
                            string.Equals(op.Paths.Leader[0], "S", StringComparison.OrdinalIgnoreCase);

                        rawPath = (prefixFromApp || isGlobalSelectionInModeling
                            ? leaderPath
                            : leaderPath.Skip(1))
                            .Select(t => t.ToUpperInvariant())
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToList();
                    }
                    else if (!string.IsNullOrWhiteSpace(directPath))
                        rawPath = new List<string> { directPath.Trim().ToUpperInvariant() };
                    else
                        rawPath = new List<string> { workspaceKey.Trim().ToUpperInvariant() };

                    if (rawPath.Count == 0) { totalSkipped++; continue; }

                    string rawKey = string.Concat(rawPath);
                    if (pathFirstSeen.ContainsKey(rawKey))
                        pathIsDuplicate.Add(rawKey);
                    else
                        pathFirstSeen[rawKey] = op.OperationID;

                    order++;
                    pendingOps.Add((op, rawPath, order));
                }

                // Second pass: build commands.  Leave duplicates unlocked so
                // MnemonicPathGenerator.ReserveUnique can resolve them without
                // creating prefix conflicts (which locked paths forbid).
                var usedPaths = new Dictionary<string, ModuleCommand>(StringComparer.OrdinalIgnoreCase);

                foreach (var (op, innerPath, displayOrder) in pendingOps)
                {
                    string commandId = op.Adapter?.Value ?? op.OperationID ?? string.Empty;
                    string commandName = op.CommandName ?? string.Empty;
                    string adapterKind = op.Adapter?.Kind ?? string.Empty;
                    bool isButtonId = string.Equals(adapterKind, "button_id", StringComparison.OrdinalIgnoreCase);
                    bool isCapability = string.Equals(adapterKind, "capability", StringComparison.OrdinalIgnoreCase);
                    bool isTbd = IsTbdAdapter(op);
                    string pathKey = string.Concat(innerPath);
                    bool duplicate = pathIsDuplicate.Contains(pathKey);

                    if (duplicate)
                    {
                        duplicateLeaderWarnings.Add(
                            $"Дублирующийся путь в модуле {moduleId}: [{string.Join(" ", innerPath)}] " +
                            $"для {op.OperationID} — будет разрешён генератором мнемоник.");
                    }

                    string displayId = (isButtonId || isCapability) ? commandId : op.OperationID ?? string.Empty;
                    string effectiveAction = !string.IsNullOrWhiteSpace(op.Action)
                        ? op.Action
                        : (isCapability ? "run_capability" : (isButtonId ? SelectionIntent.ExecuteCommandAction : "local_behavior"));
                    string effectiveSelectionType = !string.IsNullOrWhiteSpace(op.SelectionFilter)
                        ? op.SelectionFilter
                        : (string.Equals(effectiveAction, SelectionIntent.SetSelectionFilterAction, StringComparison.OrdinalIgnoreCase) ? op.Adapter?.Value ?? string.Empty : string.Empty);
                    bool destructive = string.Equals(op.Risk, "destructive", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(op.OperationID, "assemblies.remove_component", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(op.OperationID, "manufacturing.delete_operation", StringComparison.OrdinalIgnoreCase);
                    bool confirmRequired = op.ConfirmationRequired || destructive;

                    var mc = new ModuleCommand
                    {
                        Enabled = !isTbd,
                        Command = new CommandRef { ID = displayId, Name = commandName },
                        Action = effectiveAction,
                        SelectionType = effectiveSelectionType,
                        Destructive = destructive,
                        ConfirmBeforeExecute = confirmRequired,
                        RequiresSelection = op.RequiresSelection,
                        TargetModuleID = op.TargetApplicationId,
                        Path = innerPath,
                        PathLabels = new List<string> { commandName },
                        Aliases = new List<List<string>>(),
                        SearchAliases = new List<string>(),
                        DisplayOrder = displayOrder,
                        // Lock only unique paths.  Duplicates are left unlocked
                        // so MnemonicPathGenerator can safely reassign one of them.
                        PathLocked = !duplicate,
                        PathSource = duplicate ? string.Empty : "v8",
                        Notes = isTbd
                            ? "tbd_adapter — команда не готова к выполнению"
                            : (op.Adapter?.Status ?? string.Empty)
                    };

                    if (!duplicate) usedPaths[pathKey] = mc;
                    module.CommandSets[0].Commands.Add(mc);
                }

                modulesDict[moduleId] = module;
                Modules.Add(module);
            }

            // ── Diagnostics ────────────────────────────────────────────────────
            if (duplicateLeaderWarnings.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NXKeys] v8 translation: {duplicateLeaderWarnings.Count} duplicate path(s):");
                foreach (string w in duplicateLeaderWarnings)
                    System.Diagnostics.Debug.WriteLine($"  - {w}");
            }
            if (skippedInvalidKeyWarnings.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NXKeys] v8 translation: {skippedInvalidKeyWarnings.Count} invalid direct/workspace key(s):");
                foreach (string w in skippedInvalidKeyWarnings)
                    System.Diagnostics.Debug.WriteLine($"  - {w}");
            }
            if (totalSkipped > 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NXKeys] v8 translation: {totalSkipped} operation(s) skipped (no usable path).");
            }
        }
    }
}
