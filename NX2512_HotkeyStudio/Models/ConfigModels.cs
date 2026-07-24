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
        public const int CurrentSchemaVersion = 4;
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
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        public void ApplyDefaults()
        {
            if (SchemaVersion < MinimumSupportedSchemaVersion || SchemaVersion > CurrentSchemaVersion)
                SchemaVersion = CurrentSchemaVersion;
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
                        if (string.IsNullOrWhiteSpace(command.InputKey))
                            command.InputKey = LeaderKey.ResolveInputKey(command.Slot, order);
                        command.InputKey = LeaderKeyConfig.NormalizeInputKey(command.InputKey);
                        if (command.DisplayOrder <= 0) command.DisplayOrder = order;
                        order++;
                    }
                }
            }
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
                problems.Add("schema_version must be 3 or 4");
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
                var levelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var fullSequences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var rootBranches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (ModuleCommand command in commands)
                {
                    string slot = ModuleDefaults.NormalizeSlot(command.Slot);
                    if (SchemaVersion <= 3 && !ModuleDefaults.Slots.Contains(slot, StringComparer.OrdinalIgnoreCase))
                        problems.Add($"module {module.ID} has invalid slot {slot}");
                    string submenuKey = LeaderKeyConfig.NormalizeInputKey(command.SubmenuKey);
                    string inputKey = LeaderKeyConfig.NormalizeInputKey(command.InputKey);
                    if (string.IsNullOrWhiteSpace(inputKey)) problems.Add($"module {module.ID} has empty input_key");
                    string rootKey = string.IsNullOrWhiteSpace(submenuKey) ? inputKey : submenuKey;
                    string branchKind = string.IsNullOrWhiteSpace(submenuKey) ? "command" : "submenu";
                    if (!string.IsNullOrWhiteSpace(rootKey) && rootBranches.TryGetValue(rootKey, out string existingKind) &&
                        !string.Equals(existingKind, branchKind, StringComparison.OrdinalIgnoreCase))
                        problems.Add($"module {module.ID} root key {rootKey} is both command and submenu");
                    else if (!string.IsNullOrWhiteSpace(rootKey)) rootBranches[rootKey] = branchKind;
                    string level = string.IsNullOrWhiteSpace(submenuKey) ? "$root" : submenuKey;
                    if (!string.IsNullOrWhiteSpace(inputKey) && !levelKeys.Add(level + "|" + inputKey))
                        problems.Add($"module {module.ID} repeats input_key {inputKey} in level {level}");
                    string sequence = prefix + submenuKey + inputKey;
                    if (!string.IsNullOrWhiteSpace(inputKey) && !fullSequences.Add(sequence))
                        problems.Add($"module {module.ID} repeats sequence {sequence}");
                    if (command.DisplayOrder <= 0) problems.Add($"module {module.ID} has invalid display_order {command.DisplayOrder}");
                    if (command.Command == null || string.IsNullOrWhiteSpace(command.Command.ID))
                        problems.Add($"module {module.ID} key {inputKey} requires exact command.id");
                    if (command.Command == null || string.IsNullOrWhiteSpace(command.Command.Name))
                        problems.Add($"module {module.ID} key {inputKey} requires command.name");
                }
            }
            if (ids.Count == 0) problems.Add("at least one enabled module is required");
        }
    }

    public sealed class ProfileConfig
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "NX Adaptive Modules";
        [JsonPropertyName("nx_version")] public string NXVersion { get; set; } = "2512";
        [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    }

    public sealed class ScanConfig
    {
        [JsonPropertyName("roots")] public List<string> Roots { get; set; } = new List<string>();
        [JsonPropertyName("install_hints")] public List<string> InstallHints { get; set; } = new List<string>();
        [JsonPropertyName("profile_hints")] public List<string> ProfileHints { get; set; } = new List<string>();
        [JsonPropertyName("menu_extensions")] public List<string> MenuExtensions { get; set; } = new List<string>();
        [JsonPropertyName("role_extensions")] public List<string> RoleExtensions { get; set; } = new List<string>();
        [JsonPropertyName("launcher_extensions")] public List<string> LauncherExtensions { get; set; } = new List<string>();
        [JsonPropertyName("max_depth")] public int MaxDepth { get; set; } = 8;
        [JsonPropertyName("max_files")] public int MaxFiles { get; set; } = 25000;
        [JsonPropertyName("follow_symlinks")] public bool FollowSymlinks { get; set; }
        public void ApplyDefaults()
        {
            if (MenuExtensions == null || MenuExtensions.Count == 0) MenuExtensions = new List<string> { ".men", ".tbr", ".rtb", ".gly", ".abr" };
            if (RoleExtensions == null || RoleExtensions.Count == 0) RoleExtensions = new List<string> { ".mtx" };
            if (LauncherExtensions == null || LauncherExtensions.Count == 0) LauncherExtensions = new List<string> { ".bat", ".cmd", ".ps1" };
            if (MaxDepth <= 0) MaxDepth = 8;
            if (MaxFiles <= 0) MaxFiles = 25000;
        }
    }

    public sealed class DeploymentConfig
    {
        [JsonPropertyName("mode")] public string Mode { get; set; } = "managed-wrapper";
        [JsonPropertyName("managed_root")] public string ManagedRoot { get; set; } = string.Empty;
        [JsonPropertyName("backup_root")] public string BackupRoot { get; set; } = string.Empty;
        [JsonPropertyName("overlay_filename")] public string OverlayFilename { get; set; } = "nxkeys_generated.men";
        [JsonPropertyName("menuscript_version")] public int MenuScriptVersion { get; set; } = MenuScriptDefaults.Version;
        [JsonPropertyName("main_menubar_id")] public string MainMenubarID { get; set; } = "UG_GATEWAY_MAIN_MENUBAR";
        [JsonPropertyName("nx_executable")] public string NXExecutable { get; set; } = string.Empty;
        [JsonPropertyName("existing_custom_dirs_file")] public string ExistingCustomDirsFile { get; set; } = string.Empty;
        [JsonPropertyName("patch_existing_custom_dirs")] public bool PatchExistingCustomDirs { get; set; }
        [JsonPropertyName("require_nx_stopped")] public bool RequireNXStopped { get; set; } = true;
        [JsonPropertyName("clear_detected_conflicts")] public bool ClearDetectedConflicts { get; set; }
        [JsonPropertyName("atomic_writes")] public bool AtomicWrites { get; set; } = true;
        [JsonPropertyName("dry_run")] public bool DryRun { get; set; } = true;
        public void ApplyDefaults(string nxVersion)
        {
            if (string.IsNullOrWhiteSpace(Mode)) Mode = "managed-wrapper";
            if (string.IsNullOrWhiteSpace(ManagedRoot)) ManagedRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "managed", "NX" + nxVersion);
            if (string.IsNullOrWhiteSpace(BackupRoot)) BackupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "backups");
            if (string.IsNullOrWhiteSpace(OverlayFilename)) OverlayFilename = "nxkeys_generated.men";
            if (string.IsNullOrWhiteSpace(MainMenubarID)) MainMenubarID = "UG_GATEWAY_MAIN_MENUBAR";
            MenuScriptVersion = MenuScriptDefaults.NormalizeVersion(MenuScriptVersion);
        }
    }

    public sealed class Binding
    {
        [JsonPropertyName("shortcut")] public string Shortcut { get; set; } = string.Empty;
        [JsonPropertyName("command")] public CommandRef Command { get; set; } = new CommandRef();
        [JsonPropertyName("scope")] public string Scope { get; set; } = "Global";
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("notes")] public string Notes { get; set; } = string.Empty;
    }

    public sealed class CommandRef
    {
        [JsonPropertyName("id")] public string ID { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("aliases")] public List<string> Aliases { get; set; } = new List<string>();
    }

    public sealed class ModuleConfig
    {
        [JsonPropertyName("id")] public string ID { get; set; } = string.Empty;
        [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("nx_application_ids")] public List<string> NXApplicationIDs { get; set; } = new List<string>();
        [JsonPropertyName("switch_command")] public CommandRef SwitchCommand { get; set; } = new CommandRef();
        [JsonPropertyName("leader_prefix")] public string LeaderPrefix { get; set; } = string.Empty;
        [JsonPropertyName("selection_priorities")] public List<ModuleCommand> SelectionPriorities { get; set; } = new List<ModuleCommand>();
        [JsonPropertyName("command_sets")] public List<ModuleCommandSet> CommandSets { get; set; } = new List<ModuleCommandSet>();
    }

    public sealed class ModuleCommandSet
    {
        [JsonPropertyName("id")] public string ID { get; set; } = string.Empty;
        [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
        [JsonPropertyName("slot_semantics")] public Dictionary<string, string> SlotSemantics { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        [JsonPropertyName("commands")] public List<ModuleCommand> Commands { get; set; } = new List<ModuleCommand>();
    }

    public sealed class ModuleCommand
    {
        [JsonPropertyName("slot")] public string Slot { get; set; } = string.Empty;
        [JsonPropertyName("submenu_key")] public string SubmenuKey { get; set; } = string.Empty;
        [JsonPropertyName("submenu_label")] public string SubmenuLabel { get; set; } = string.Empty;
        [JsonPropertyName("input_key")] public string InputKey { get; set; } = string.Empty;
        [JsonPropertyName("icon_hint")] public string IconHint { get; set; } = string.Empty;
        [JsonPropertyName("display_order")] public int DisplayOrder { get; set; }
        [JsonPropertyName("command")] public CommandRef Command { get; set; } = new CommandRef();
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("requires_selection")] public bool RequiresSelection { get; set; }
        [JsonPropertyName("destructive")] public bool Destructive { get; set; }
        [JsonPropertyName("confirm_before_execute")] public bool ConfirmBeforeExecute { get; set; }
        [JsonPropertyName("fallback")] public string Fallback { get; set; } = string.Empty;
        [JsonPropertyName("notes")] public string Notes { get; set; } = string.Empty;
    }

    public sealed class WorkflowControls
    {
        [JsonPropertyName("accept_ok")] public CommandRef AcceptOK { get; set; } = new CommandRef();
        [JsonPropertyName("apply")] public CommandRef Apply { get; set; } = new CommandRef();
        [JsonPropertyName("cancel")] public CommandRef Cancel { get; set; } = new CommandRef();
        [JsonPropertyName("back_previous_step")] public CommandRef BackPreviousStep { get; set; } = new CommandRef();
        [JsonPropertyName("confirm_dangerous")] public bool ConfirmDangerous { get; set; } = true;
        public void ApplyDefaults()
        {
            AcceptOK ??= new CommandRef(); Apply ??= new CommandRef(); Cancel ??= new CommandRef(); BackPreviousStep ??= new CommandRef();
            if (string.IsNullOrWhiteSpace(AcceptOK.Name) && string.IsNullOrWhiteSpace(AcceptOK.ID)) AcceptOK.Name = "OK";
            if (string.IsNullOrWhiteSpace(Apply.Name) && string.IsNullOrWhiteSpace(Apply.ID)) Apply.Name = "Apply";
            if (string.IsNullOrWhiteSpace(Cancel.Name) && string.IsNullOrWhiteSpace(Cancel.ID)) Cancel.Name = "Cancel";
            if (string.IsNullOrWhiteSpace(BackPreviousStep.Name) && string.IsNullOrWhiteSpace(BackPreviousStep.ID)) BackPreviousStep.Name = "Back";
            ConfirmDangerous = true;
        }
    }



    public sealed class PerformanceConfig
    {
        [JsonPropertyName("catalog_cache_enabled")] public bool CatalogCacheEnabled { get; set; } = true;
        [JsonPropertyName("lazy_studio_scan")] public bool LazyStudioScan { get; set; } = true;
        [JsonPropertyName("bridge_watcher")] public bool BridgeWatcher { get; set; } = true;
        public void ApplyDefaults() { CatalogCacheEnabled = true; LazyStudioScan = true; BridgeWatcher = true; }
    }

    public sealed class RoleDeployment
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; }
        [JsonPropertyName("source_mtx")] public string SourceMTX { get; set; } = string.Empty;
        [JsonPropertyName("target_directory")] public string TargetDirectory { get; set; } = string.Empty;
        [JsonPropertyName("target_name")] public string TargetName { get; set; } = string.Empty;
        [JsonPropertyName("set_as_default")] public bool SetAsDefault { get; set; }
        [JsonPropertyName("default_role_env")] public string DefaultRoleEnv { get; set; } = "UGII_DEFAULT_ROLE";
        public void ApplyDefaults(string nxVersion)
        {
            if (string.IsNullOrWhiteSpace(TargetName)) TargetName = "NX_Adaptive_Modules_" + nxVersion + ".mtx";
            if (string.IsNullOrWhiteSpace(DefaultRoleEnv)) DefaultRoleEnv = "UGII_DEFAULT_ROLE";
        }
    }

    public sealed class LeaderKeyConfig
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("trigger_key")] public string TriggerKey { get; set; } = "CapsLock";
        [JsonPropertyName("adaptive_module_mode")] public bool AdaptiveModuleMode { get; set; } = true;
        [JsonPropertyName("hud_delay_ms")] public int HudDelayMs { get; set; } = 120;
        [JsonPropertyName("first_key_timeout_ms")] public int FirstKeyTimeoutMs { get; set; } = 20000;
        [JsonPropertyName("next_key_timeout_ms")] public int NextKeyTimeoutMs { get; set; } = 20000;
        [JsonPropertyName("sticky_mode_on_double_tap")] public bool StickyModeOnDoubleTap { get; set; } = true;
        [JsonPropertyName("hud_opacity")] public double HudOpacity { get; set; } = 0.95;
        [JsonPropertyName("hook_only_when_nx_active")] public bool HookOnlyWhenNXActive { get; set; } = true;
        [JsonPropertyName("slot_key_map")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string> SlotKeyMap { get; set; }
        [JsonIgnore] public List<LeaderSequenceItem> Sequences { get; set; } = new List<LeaderSequenceItem>();
        [JsonIgnore] public List<ModuleConfig> RuntimeModules { get; set; } = new List<ModuleConfig>();

        public void ApplyDefaults()
        {
            if (string.IsNullOrWhiteSpace(TriggerKey)) TriggerKey = "CapsLock";
            AdaptiveModuleMode = true;
            if (HudDelayMs <= 0) HudDelayMs = 120;
            if (FirstKeyTimeoutMs <= 0) FirstKeyTimeoutMs = 20000;
            if (NextKeyTimeoutMs <= 0) NextKeyTimeoutMs = 20000;
            if (HudOpacity <= 0 || HudOpacity > 1.0) HudOpacity = 0.95;
            if (SlotKeyMap != null)
                SlotKeyMap = new Dictionary<string, string>(SlotKeyMap, StringComparer.OrdinalIgnoreCase);
            Sequences ??= new List<LeaderSequenceItem>();
            RuntimeModules ??= new List<ModuleConfig>();
        }

        public void MergeModules(List<ModuleConfig> modules) => RebuildFromModules(modules);

        public void RebuildFromModules(IEnumerable<ModuleConfig> modules)
        {
            ApplyDefaults();
            RuntimeModules = (modules ?? Enumerable.Empty<ModuleConfig>()).Where(value => value != null).ToList();
            var result = new List<LeaderSequenceItem>();
            foreach (ModuleConfig module in RuntimeModules.Where(value => value.Enabled))
            {
                string prefix = NormalizeInputKey(module.LeaderPrefix);
                IEnumerable<ModuleCommand> commands = module.CommandSets?
                    .Where(set => set?.Commands != null).SelectMany(set => set.Commands).Where(value => value != null)
                    ?? Enumerable.Empty<ModuleCommand>();
                int fallbackOrder = 1;
                foreach (ModuleCommand moduleCommand in commands.OrderBy(command => command.DisplayOrder <= 0 ? int.MaxValue : command.DisplayOrder))
                {
                    if (!moduleCommand.Enabled) continue;
                    string slot = ModuleDefaults.NormalizeSlot(moduleCommand.Slot);
                    string submenuKey = NormalizeInputKey(moduleCommand.SubmenuKey);
                    string inputKey = NormalizeInputKey(moduleCommand.InputKey);
                    if (string.IsNullOrWhiteSpace(inputKey)) inputKey = ResolveInputKey(slot, fallbackOrder);
                    if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(inputKey) || moduleCommand.Command == null) continue;
                    string sequence = string.IsNullOrWhiteSpace(submenuKey)
                        ? prefix + " " + inputKey
                        : prefix + " " + submenuKey + " " + inputKey;
                    result.Add(new LeaderSequenceItem
                    {
                        Sequence = sequence,
                        Category = module.Label,
                        ModuleID = module.ID,
                        Slot = slot,
                        SubmenuKey = submenuKey,
                        SubmenuLabel = moduleCommand.SubmenuLabel ?? string.Empty,
                        InputKey = inputKey,
                        IconHint = string.IsNullOrWhiteSpace(moduleCommand.IconHint)
                            ? CommandIconHints.FromCommand(moduleCommand.Command?.ID, moduleCommand.Command?.Name, submenuKey, moduleCommand.SubmenuLabel)
                            : moduleCommand.IconHint.Trim(),
                        DisplayOrder = moduleCommand.DisplayOrder <= 0 ? fallbackOrder : moduleCommand.DisplayOrder,
                        Command = moduleCommand.Command,
                        RequiresSelection = moduleCommand.RequiresSelection,
                        Destructive = moduleCommand.Destructive,
                        ConfirmBeforeExecute = moduleCommand.ConfirmBeforeExecute || moduleCommand.Destructive,
                        Fallback = moduleCommand.Fallback,
                        Notes = string.IsNullOrWhiteSpace(moduleCommand.Notes)
                            ? ModuleDefaults.SemanticForSlot(slot, moduleCommand.Command.Name)
                            : moduleCommand.Notes,
                        Enabled = true
                    });
                    fallbackOrder++;
                }
            }
            Sequences = result;
        }

        public string ResolveInputKey(string slot) => ResolveInputKey(slot, 0);

        public string ResolveInputKey(string slot, int fallbackOrder)
        {
            string normalizedSlot = ModuleDefaults.NormalizeSlot(slot);
            if (SlotKeyMap != null && SlotKeyMap.TryGetValue(normalizedSlot, out string value))
            {
                string mapped = NormalizeInputKey(value);
                if (!string.IsNullOrWhiteSpace(mapped)) return mapped;
            }
            if (ModuleDefaults.DefaultSlotKeyMap.TryGetValue(normalizedSlot, out string defaultValue))
                return NormalizeInputKey(defaultValue);
            if (fallbackOrder > 0 && fallbackOrder <= ModuleDefaults.DefaultInputKeys.Count)
                return ModuleDefaults.DefaultInputKeys[fallbackOrder - 1];
            return string.Empty;
        }

        internal void Validate(List<string> problems)
        {
            if (!AdaptiveModuleMode) problems.Add("leader_key.adaptive_module_mode must be true");
            int expected = RuntimeModules.Where(module => module.Enabled)
                .Sum(module => module.CommandSets?.Where(set => set?.Commands != null)
                    .Sum(set => set.Commands.Count(command => command != null && command.Enabled)) ?? 0);
            if (Sequences.Count != expected) problems.Add($"derived adaptive sequence count mismatch: {Sequences.Count} != {expected}");
        }

        public static string NormalizeInputKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            char character = value.Trim().FirstOrDefault(char.IsLetterOrDigit);
            return character == default ? string.Empty : char.ToUpperInvariant(character).ToString();
        }
    }

    public sealed class LeaderSequenceItem
    {
        [JsonPropertyName("sequence")] public string Sequence { get; set; } = string.Empty;
        [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
        [JsonPropertyName("module_id")] public string ModuleID { get; set; } = string.Empty;
        [JsonPropertyName("slot")] public string Slot { get; set; } = string.Empty;
        [JsonPropertyName("submenu_key")] public string SubmenuKey { get; set; } = string.Empty;
        [JsonPropertyName("submenu_label")] public string SubmenuLabel { get; set; } = string.Empty;
        [JsonPropertyName("input_key")] public string InputKey { get; set; } = string.Empty;
        [JsonPropertyName("icon_hint")] public string IconHint { get; set; } = string.Empty;
        [JsonPropertyName("display_order")] public int DisplayOrder { get; set; }
        [JsonPropertyName("command")] public CommandRef Command { get; set; } = new CommandRef();
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("requires_selection")] public bool RequiresSelection { get; set; }
        [JsonPropertyName("destructive")] public bool Destructive { get; set; }
        [JsonPropertyName("confirm_before_execute")] public bool ConfirmBeforeExecute { get; set; }
        [JsonPropertyName("fallback")] public string Fallback { get; set; } = string.Empty;
        [JsonPropertyName("notes")] public string Notes { get; set; } = string.Empty;
        public string DisplayPath(string triggerKey)
        {
            string path = string.IsNullOrWhiteSpace(SubmenuKey) ? InputKey : SubmenuKey + " → " + InputKey;
            return $"{triggerKey} → {path}";
        }
    }

    public static class CommandIconHints
    {
        public static string FromCommand(string commandId, string commandName = "", string submenuKey = "", string submenuLabel = "")
        {
            string value = string.Join(" ", commandId ?? string.Empty, commandName ?? string.Empty, submenuLabel ?? string.Empty).ToUpperInvariant();
            if (value.Contains("WAVE")) return "wave";
            if (value.Contains("LAYER")) return "layer";
            if (value.Contains("MATERIAL")) return "material";
            if (value.Contains("SHEET") || value.Contains("FLANGE") || value.Contains("BEND") || value.Contains("SBSM")) return "sheet_metal";
            if (value.Contains("ASSEMB") || value.Contains("COMPONENT") || value.Contains("CONSTRAINTS")) return "assembly";
            if (value.Contains("SKETCH") || value.Contains("RECTANGLE") || value.Contains("CIRCLE") || value.Contains("ARC") || value.Contains("LINE")) return "sketch";
            if (value.Contains("SEL_") || value.Contains("SELECT") || value.Contains("DESELECT")) return "selection";
            if (value.Contains("VIEW") || value.Contains("DISPLAY") || value.Contains("SHOW") || value.Contains("HIDE")) return "view";
            if (value.Contains("MEASURE") || value.Contains("INFO") || value.Contains("ANALYSIS")) return "inspect";
            if (value.Contains("MIRROR") || value.Contains("PATTERN")) return "pattern";
            if (value.Contains("EXTRUDE") || value.Contains("REVOLVE") || value.Contains("HOLE") || value.Contains("BLEND") || value.Contains("CHAMFER")) return "feature";
            if (!string.IsNullOrWhiteSpace(submenuKey)) return "menu";
            return "command";
        }

        public static string Glyph(string hint, string commandId = "")
        {
            string id = (commandId ?? string.Empty).ToUpperInvariant();
            if (id.Contains("EXTRUDE")) return "⬡";
            if (id.Contains("REVOLVE")) return "↺";
            if (id.Contains("HOLE")) return "◎";
            if (id.Contains("BLEND") || id.Contains("FILLET")) return "⌒";
            if (id.Contains("CHAMFER")) return "⟁";
            if (id.Contains("RECTANGLE")) return "▭";
            if (id.Contains("CIRCLE")) return "○";
            if (id.Contains("ARC")) return "⌒";
            if (id.Contains("LINE")) return "╱";
            if (id.Contains("CONSTRAINT")) return "⧉";
            if (id.Contains("PATTERN")) return "❖";
            if (id.Contains("MIRROR")) return "⧖";
            if (id.Contains("WAVE")) return "〰";
            if (id.Contains("LAYER")) return "≡";
            if (id.Contains("MATERIAL")) return "◈";
            if (id.Contains("BODY_PRIORITY")) return "⬛";
            if (id.Contains("FACE_PRIORITY")) return "▨";
            if (id.Contains("EDGE_PRIORITY")) return "━";
            if (id.Contains("DESELECT")) return "✕";

            string value = (string.IsNullOrWhiteSpace(hint) ? FromCommand(commandId) : hint).Trim().ToLowerInvariant();
            switch (value)
            {
                case "wave": return "〰";
                case "layer": return "≡";
                case "material": return "◈";
                case "sheet_metal": return "📜";
                case "assembly": return "🧩";
                case "sketch": return "📐";
                case "selection": return "🎯";
                case "view": return "👁";
                case "inspect": return "📏";
                case "pattern": return "❖";
                case "feature": return "⚡";
                case "menu": return "📁";
                default: return "NX";
            }
        }
    }

    public static class ModuleDefaults
    {
        public static readonly IReadOnlyList<string> Slots = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        public static readonly IReadOnlyDictionary<string, string> DefaultSlotKeyMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["N"] = "W", ["NE"] = "E", ["E"] = "D", ["SE"] = "C",
                ["S"] = "X", ["SW"] = "Z", ["W"] = "A", ["NW"] = "Q"
            };
        public static readonly IReadOnlyList<string> DefaultInputKeys = new[] { "W", "E", "D", "C", "X", "Z", "A", "Q" };
        public static readonly IReadOnlyDictionary<string, string> SlotSemantics =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["N"] = "запуск, создание или открытие основного объекта",
                ["NE"] = "следующий основной шаг процесса",
                ["E"] = "добавление объекта, материала или зависимости",
                ["SE"] = "преобразование или замена",
                ["S"] = "завершение, удаление или вторичная обработка",
                ["SW"] = "удаление, уменьшение или ослабление",
                ["W"] = "структура, связь или паттерн",
                ["NW"] = "инспекция, измерение или сервисная команда"
            };
        public static string NormalizeSlot(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
        public static string SemanticForSlot(string slot, string fallback) =>
            SlotSemantics.TryGetValue(NormalizeSlot(slot), out string value) ? value : fallback ?? string.Empty;
        public static string ModuleIdForCategory(string category)
        {
            string value = (category ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Contains("model")) return "modeling";
            if (value.Contains("sketch")) return "sketch";
            if (value.Contains("assembl")) return "assembly";
            if (value.Contains("draft")) return "drafting";
            if (value.Contains("sheet")) return "sheet_metal";
            if (value.Contains("manufact") || value.Contains("cam")) return "manufacturing";
            if (value.Contains("simulat") || value.Contains("cae")) return "simulation";
            if (value.Contains("select")) return "selection_object";
            if (value.Contains("inspect") || value.Contains("view")) return "inspect_view";
            return value.Replace(' ', '_');
        }
    }
}
