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

        public static int NormalizeVersion(int version)
        {
            return version > 0 && version <= MaxVersion ? version : Version;
        }

        public static int ExpectedVersionForPath(string path)
        {
            string ext = Path.GetExtension(path ?? string.Empty);
            return string.Equals(ext, ".tbr", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".rtb", StringComparison.OrdinalIgnoreCase)
                ? ToolbarVersion
                : Version;
        }
    }

    public sealed class Config
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("profile")]
        public ProfileConfig Profile { get; set; } = new ProfileConfig();

        [JsonPropertyName("scan")]
        public ScanConfig Scan { get; set; } = new ScanConfig();

        [JsonPropertyName("deployment")]
        public DeploymentConfig Deployment { get; set; } = new DeploymentConfig();

        [JsonPropertyName("keyboard")]
        public List<Binding> Keyboard { get; set; } = new List<Binding>();

        [JsonPropertyName("radials")]
        public List<RadialMenu> Radials { get; set; } = new List<RadialMenu>();

        [JsonPropertyName("modules")]
        public List<ModuleConfig> Modules { get; set; } = new List<ModuleConfig>();

        [JsonPropertyName("workflow_controls")]
        public WorkflowControls WorkflowControls { get; set; } = new WorkflowControls();

        [JsonPropertyName("performance")]
        public PerformanceConfig Performance { get; set; } = new PerformanceConfig();

        [JsonPropertyName("role_deployment")]
        public RoleDeployment Role { get; set; } = new RoleDeployment();

        [JsonPropertyName("leader_key")]
        public LeaderKeyConfig LeaderKey { get; set; } = new LeaderKeyConfig();

        public static Config Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("Config file not found", path);
            }

            string json;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                json = sr.ReadToEnd();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            Config config = JsonSerializer.Deserialize<Config>(json, options) ?? new Config();
            config.ExpandEnvironment();
            config.ApplyDefaults();
            config.Validate();

            return config;
        }

        public void Save(string path)
        {
            Validate();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(path, json + Environment.NewLine);
        }

        public void ApplyDefaults()
        {
            if (SchemaVersion <= 0) SchemaVersion = 2;
            if (Profile == null) Profile = new ProfileConfig();
            if (string.IsNullOrWhiteSpace(Profile.NXVersion)) Profile.NXVersion = "2512";

            if (Scan == null) Scan = new ScanConfig();
            if (Scan.MaxDepth <= 0) Scan.MaxDepth = 8;
            if (Scan.MaxFiles <= 0) Scan.MaxFiles = 25000;
            if (Scan.MenuExtensions == null || Scan.MenuExtensions.Count == 0)
            {
                Scan.MenuExtensions = new List<string> { ".men", ".tbr", ".rtb", ".gly", ".abr" };
            }
            if (Scan.RoleExtensions == null || Scan.RoleExtensions.Count == 0)
            {
                Scan.RoleExtensions = new List<string> { ".mtx" };
            }
            if (Scan.LauncherExtensions == null || Scan.LauncherExtensions.Count == 0)
            {
                Scan.LauncherExtensions = new List<string> { ".bat", ".cmd", ".ps1" };
            }

            if (Deployment == null) Deployment = new DeploymentConfig();
            if (string.IsNullOrWhiteSpace(Deployment.Mode)) Deployment.Mode = "managed-wrapper";
            if (string.IsNullOrWhiteSpace(Deployment.ManagedRoot))
            {
                Deployment.ManagedRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NXKeys", "managed", "NX" + Profile.NXVersion);
            }
            if (string.IsNullOrWhiteSpace(Deployment.BackupRoot))
            {
                Deployment.BackupRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NXKeys", "backups");
            }
            if (string.IsNullOrWhiteSpace(Deployment.OverlayFilename)) Deployment.OverlayFilename = "nxkeys_generated.men";
            Deployment.MenuScriptVersion = MenuScriptDefaults.NormalizeVersion(Deployment.MenuScriptVersion);
            if (string.IsNullOrWhiteSpace(Deployment.MainMenubarID)) Deployment.MainMenubarID = "UG_GATEWAY_MAIN_MENUBAR";

            if (Role == null) Role = new RoleDeployment();
            if (string.IsNullOrWhiteSpace(Role.TargetName)) Role.TargetName = "NX_Pro_Hybrid_" + Profile.NXVersion + ".mtx";
            if (string.IsNullOrWhiteSpace(Role.DefaultRoleEnv)) Role.DefaultRoleEnv = "UGII_DEFAULT_ROLE";

            if (Keyboard == null) Keyboard = new List<Binding>();
            if (Radials == null) Radials = new List<RadialMenu>();
            if (Modules == null) Modules = new List<ModuleConfig>();
            if (Modules.Count == 0) Modules = ModuleDefaults.BuildDefaultModules();
            if (WorkflowControls == null) WorkflowControls = new WorkflowControls();
            WorkflowControls.ApplyDefaults();
            if (Performance == null) Performance = new PerformanceConfig();
            Performance.ApplyDefaults();
            if (LeaderKey == null) LeaderKey = new LeaderKeyConfig();
            LeaderKey.ApplyDefaults();
            LeaderKey.RuntimeModules = Modules;
            LeaderKey.MergeModules(Modules);
            ClearLeaderShortcutFallbacks();

            SchemaVersion = 2;
        }

        private void ClearLeaderShortcutFallbacks()
        {
            if (LeaderKey?.Sequences == null) return;
            foreach (LeaderSequenceItem sequence in LeaderKey.Sequences)
            {
                if (sequence != null) sequence.ShortcutToExecute = string.Empty;
            }
        }

        public void ExpandEnvironment()
        {
            if (Scan?.Roots != null)
            {
                for (int i = 0; i < Scan.Roots.Count; i++) Scan.Roots[i] = ExpandPath(Scan.Roots[i]);
            }
            if (Scan?.InstallHints != null)
            {
                for (int i = 0; i < Scan.InstallHints.Count; i++) Scan.InstallHints[i] = ExpandPath(Scan.InstallHints[i]);
            }
            if (Scan?.ProfileHints != null)
            {
                for (int i = 0; i < Scan.ProfileHints.Count; i++) Scan.ProfileHints[i] = ExpandPath(Scan.ProfileHints[i]);
            }

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

        private static readonly Regex PercentEnvRegex = new Regex(@"%([A-Za-z_][A-Za-z0-9_]*)%", RegexOptions.Compiled);

        public static string ExpandPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            string expanded = PercentEnvRegex.Replace(path, match =>
            {
                string name = match.Groups[1].Value;
                string value = Environment.GetEnvironmentVariable(name);
                return value ?? match.Value;
            });

            expanded = Environment.ExpandEnvironmentVariables(expanded);

            if (expanded.StartsWith("~"))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                expanded = Path.Combine(home, expanded.Substring(1).TrimStart('\\', '/'));
            }

            try
            {
                return Path.GetFullPath(expanded);
            }
            catch
            {
                return expanded;
            }
        }

        public void Validate()
        {
            List<string> problems = new List<string>();

            if (SchemaVersion != 1 && SchemaVersion != 2)
            {
                problems.Add($"unsupported schema_version {SchemaVersion}");
            }
            if (Profile == null || string.IsNullOrWhiteSpace(Profile.Name))
            {
                problems.Add("profile.name is required");
            }
            if (Deployment == null || string.IsNullOrWhiteSpace(Deployment.ManagedRoot))
            {
                problems.Add("deployment.managed_root is required");
            }
            if (Deployment == null || string.IsNullOrWhiteSpace(Deployment.BackupRoot))
            {
                problems.Add("deployment.backup_root is required");
            }

            if (Deployment != null && Deployment.Mode != "managed-wrapper" && Deployment.Mode != "existing-custom-dirs")
            {
                problems.Add("deployment.mode must be managed-wrapper or existing-custom-dirs");
            }

            var seenShortcut = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var seenCommand = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (Keyboard != null)
            {
                for (int i = 0; i < Keyboard.Count; i++)
                {
                    Binding binding = Keyboard[i];
                    if (!binding.Enabled) continue;

                    if (string.IsNullOrWhiteSpace(binding.Shortcut))
                    {
                        problems.Add($"keyboard[{i}].shortcut is required");
                    }
                    if (binding.Command == null || (string.IsNullOrWhiteSpace(binding.Command.ID) && string.IsNullOrWhiteSpace(binding.Command.Name)))
                    {
                        problems.Add($"keyboard[{i}].command needs id or name");
                    }

                    string shortcutKey = NormalizeKey(binding.Shortcut);
                    if (seenShortcut.TryGetValue(shortcutKey, out string prevCmd))
                    {
                        problems.Add($"duplicate shortcut \"{binding.Shortcut}\" for {prevCmd} and {binding.Command?.Name}");
                    }
                    else
                    {
                        seenShortcut[shortcutKey] = binding.Command?.Name ?? binding.Shortcut;
                    }

                    string commandKey = NormalizeKey(binding.Command?.ID);
                    if (string.IsNullOrEmpty(commandKey)) commandKey = NormalizeKey(binding.Command?.Name);

                    if (!string.IsNullOrEmpty(commandKey))
                    {
                        if (seenCommand.TryGetValue(commandKey, out string prevShortcut) && prevShortcut != binding.Shortcut)
                        {
                            problems.Add($"command \"{binding.Command?.Name}\" has multiple shortcuts: {prevShortcut} and {binding.Shortcut}");
                        }
                        else
                        {
                            seenCommand[commandKey] = binding.Shortcut;
                        }
                    }
                }
            }

            if (Role != null && Role.Enabled && string.IsNullOrWhiteSpace(Role.SourceMTX))
            {
                problems.Add("role_deployment.source_mtx is required when role deployment is enabled");
            }

            if (Radials != null)
            {
                for (int i = 0; i < Radials.Count; i++)
                {
                    RadialMenu radial = Radials[i];
                    if (!radial.Enabled) continue;

                    if (string.IsNullOrWhiteSpace(radial.Name) || string.IsNullOrWhiteSpace(radial.Trigger))
                    {
                        problems.Add($"radials[{i}] needs name and trigger");
                    }

                    var seenDir = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (radial.Items != null)
                    {
                        foreach (RadialItem item in radial.Items)
                        {
                            string dir = (item.Direction ?? string.Empty).Trim().ToUpperInvariant();
                            if (!IsValidDirection(dir))
                            {
                                problems.Add($"radial \"{radial.Name}\" has invalid direction \"{item.Direction}\"");
                            }
                            if (seenDir.Contains(dir))
                            {
                                problems.Add($"radial \"{radial.Name}\" repeats direction \"{dir}\"");
                            }
                            seenDir.Add(dir);
                        }
                    }
                }
            }

            ValidateModules(problems);

            if (problems.Count > 0)
            {
                problems.Sort();
                throw new InvalidOperationException("Configuration is invalid:\n- " + string.Join("\n- ", problems));
            }
        }

        private void ValidateModules(List<string> problems)
        {
            if (Modules == null) return;
            var seenModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Modules.Count; i++)
            {
                ModuleConfig module = Modules[i];
                if (module == null || !module.Enabled) continue;
                if (string.IsNullOrWhiteSpace(module.ID))
                {
                    problems.Add($"modules[{i}].id is required");
                    continue;
                }
                if (!seenModules.Add(module.ID.Trim()))
                {
                    problems.Add($"duplicate module id \"{module.ID}\"");
                }
                if (module.CommandSets == null) continue;
                foreach (ModuleCommandSet set in module.CommandSets)
                {
                    if (set == null) continue;
                    var seenSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (set.Commands == null) continue;
                    foreach (ModuleCommand item in set.Commands)
                    {
                        if (item == null) continue;
                        string slot = (item.Slot ?? string.Empty).Trim().ToUpperInvariant();
                        if (string.IsNullOrWhiteSpace(slot))
                        {
                            problems.Add($"module \"{module.ID}\" command set \"{set.ID}\" has command without slot");
                        }
                        if (!seenSlots.Add(slot))
                        {
                            problems.Add($"module \"{module.ID}\" command set \"{set.ID}\" repeats slot \"{slot}\"");
                        }
                        if (item.Command == null || (string.IsNullOrWhiteSpace(item.Command.ID) && string.IsNullOrWhiteSpace(item.Command.Name)))
                        {
                            problems.Add($"module \"{module.ID}\" command set \"{set.ID}\" slot \"{slot}\" needs command id or name");
                        }
                    }
                }
            }
        }

        private static bool IsValidDirection(string dir)
        {
            switch (dir)
            {
                case "N": case "NE": case "E": case "SE":
                case "S": case "SW": case "W": case "NW":
                    return true;
                default:
                    return false;
            }
        }

        private static string NormalizeKey(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            return s.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("+", "");
        }
    }

    public sealed class ProfileConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "NX Pro Hybrid";

        [JsonPropertyName("nx_version")]
        public string NXVersion { get; set; } = "2512";

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public sealed class ScanConfig
    {
        [JsonPropertyName("roots")]
        public List<string> Roots { get; set; } = new List<string>();

        [JsonPropertyName("install_hints")]
        public List<string> InstallHints { get; set; } = new List<string>();

        [JsonPropertyName("profile_hints")]
        public List<string> ProfileHints { get; set; } = new List<string>();

        [JsonPropertyName("menu_extensions")]
        public List<string> MenuExtensions { get; set; } = new List<string> { ".men", ".tbr", ".rtb", ".gly", ".abr" };

        [JsonPropertyName("role_extensions")]
        public List<string> RoleExtensions { get; set; } = new List<string> { ".mtx" };

        [JsonPropertyName("launcher_extensions")]
        public List<string> LauncherExtensions { get; set; } = new List<string> { ".bat", ".cmd", ".ps1" };

        [JsonPropertyName("max_depth")]
        public int MaxDepth { get; set; } = 8;

        [JsonPropertyName("max_files")]
        public int MaxFiles { get; set; } = 25000;

        [JsonPropertyName("follow_symlinks")]
        public bool FollowSymlinks { get; set; } = false;
    }

    public sealed class DeploymentConfig
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "managed-wrapper";

        [JsonPropertyName("managed_root")]
        public string ManagedRoot { get; set; } = string.Empty;

        [JsonPropertyName("backup_root")]
        public string BackupRoot { get; set; } = string.Empty;

        [JsonPropertyName("overlay_filename")]
        public string OverlayFilename { get; set; } = "nxkeys_generated.men";

        [JsonPropertyName("menuscript_version")]
        public int MenuScriptVersion { get; set; } = MenuScriptDefaults.Version;

        [JsonPropertyName("main_menubar_id")]
        public string MainMenubarID { get; set; } = "UG_GATEWAY_MAIN_MENUBAR";

        [JsonPropertyName("nx_executable")]
        public string NXExecutable { get; set; } = string.Empty;

        [JsonPropertyName("existing_custom_dirs_file")]
        public string ExistingCustomDirsFile { get; set; } = string.Empty;

        [JsonPropertyName("patch_existing_custom_dirs")]
        public bool PatchExistingCustomDirs { get; set; } = true;

        [JsonPropertyName("require_nx_stopped")]
        public bool RequireNXStopped { get; set; } = true;

        [JsonPropertyName("clear_detected_conflicts")]
        public bool ClearDetectedConflicts { get; set; } = false;

        [JsonPropertyName("atomic_writes")]
        public bool AtomicWrites { get; set; } = true;

        [JsonPropertyName("dry_run")]
        public bool DryRun { get; set; } = true;
    }

    public sealed class Binding
    {
        [JsonPropertyName("shortcut")]
        public string Shortcut { get; set; } = string.Empty;

        [JsonPropertyName("command")]
        public CommandRef Command { get; set; } = new CommandRef();

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = "Global";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class CommandRef
    {
        [JsonPropertyName("id")]
        public string ID { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("aliases")]
        public List<string> Aliases { get; set; } = new List<string>();
    }

    public sealed class RadialMenu
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("trigger")]
        public string Trigger { get; set; } = string.Empty;

        [JsonPropertyName("module")]
        public string Module { get; set; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("items")]
        public List<RadialItem> Items { get; set; } = new List<RadialItem>();
    }

    public sealed class RadialItem
    {
        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("command")]
        public CommandRef Command { get; set; } = new CommandRef();

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class ModuleConfig
    {
        [JsonPropertyName("id")]
        public string ID { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("nx_application_ids")]
        public List<string> NXApplicationIDs { get; set; } = new List<string>();

        [JsonPropertyName("switch_command")]
        public CommandRef SwitchCommand { get; set; } = new CommandRef();

        [JsonPropertyName("leader_prefix")]
        public string LeaderPrefix { get; set; } = string.Empty;

        [JsonPropertyName("selection_priorities")]
        public List<ModuleCommand> SelectionPriorities { get; set; } = new List<ModuleCommand>();

        [JsonPropertyName("command_sets")]
        public List<ModuleCommandSet> CommandSets { get; set; } = new List<ModuleCommandSet>();

        [JsonPropertyName("radials")]
        public List<RadialMenu> Radials { get; set; } = new List<RadialMenu>();
    }

    public sealed class ModuleCommandSet
    {
        [JsonPropertyName("id")]
        public string ID { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("slot_semantics")]
        public Dictionary<string, string> SlotSemantics { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("commands")]
        public List<ModuleCommand> Commands { get; set; } = new List<ModuleCommand>();
    }

    public sealed class ModuleCommand
    {
        [JsonPropertyName("slot")]
        public string Slot { get; set; } = string.Empty;

        [JsonPropertyName("command")]
        public CommandRef Command { get; set; } = new CommandRef();

        [JsonPropertyName("requires_selection")]
        public bool RequiresSelection { get; set; }

        [JsonPropertyName("destructive")]
        public bool Destructive { get; set; }

        [JsonPropertyName("confirm_before_execute")]
        public bool ConfirmBeforeExecute { get; set; }

        [JsonPropertyName("fallback")]
        public string Fallback { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class WorkflowControls
    {
        [JsonPropertyName("accept_ok")]
        public CommandRef AcceptOK { get; set; } = new CommandRef();

        [JsonPropertyName("apply")]
        public CommandRef Apply { get; set; } = new CommandRef();

        [JsonPropertyName("cancel")]
        public CommandRef Cancel { get; set; } = new CommandRef();

        [JsonPropertyName("back_previous_step")]
        public CommandRef BackPreviousStep { get; set; } = new CommandRef();

        [JsonPropertyName("confirm_dangerous")]
        public bool ConfirmDangerous { get; set; } = true;

        public void ApplyDefaults()
        {
            if (AcceptOK == null) AcceptOK = new CommandRef();
            if (Apply == null) Apply = new CommandRef();
            if (Cancel == null) Cancel = new CommandRef();
            if (BackPreviousStep == null) BackPreviousStep = new CommandRef();
            if (string.IsNullOrWhiteSpace(AcceptOK.Name) && string.IsNullOrWhiteSpace(AcceptOK.ID)) AcceptOK.Name = "OK";
            if (string.IsNullOrWhiteSpace(Apply.Name) && string.IsNullOrWhiteSpace(Apply.ID)) Apply.Name = "Apply";
            if (string.IsNullOrWhiteSpace(Cancel.Name) && string.IsNullOrWhiteSpace(Cancel.ID)) Cancel.Name = "Cancel";
            if (string.IsNullOrWhiteSpace(BackPreviousStep.Name) && string.IsNullOrWhiteSpace(BackPreviousStep.ID)) BackPreviousStep.Name = "Back";
            ConfirmDangerous = true;
        }
    }

    public sealed class PerformanceConfig
    {
        [JsonPropertyName("catalog_cache_enabled")]
        public bool CatalogCacheEnabled { get; set; } = true;

        [JsonPropertyName("lazy_studio_scan")]
        public bool LazyStudioScan { get; set; } = true;

        [JsonPropertyName("bridge_watcher")]
        public bool BridgeWatcher { get; set; } = true;

        public void ApplyDefaults()
        {
            CatalogCacheEnabled = true;
            LazyStudioScan = true;
            BridgeWatcher = true;
        }
    }

    public sealed class RoleDeployment
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("source_mtx")]
        public string SourceMTX { get; set; } = string.Empty;

        [JsonPropertyName("target_directory")]
        public string TargetDirectory { get; set; } = string.Empty;

        [JsonPropertyName("target_name")]
        public string TargetName { get; set; } = string.Empty;

        [JsonPropertyName("set_as_default")]
        public bool SetAsDefault { get; set; } = false;

        [JsonPropertyName("default_role_env")]
        public string DefaultRoleEnv { get; set; } = "UGII_DEFAULT_ROLE";
    }

    public sealed class LeaderKeyConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("trigger_key")]
        public string TriggerKey { get; set; } = "CapsLock";

        [JsonPropertyName("hud_delay_ms")]
        public int HudDelayMs { get; set; } = 150;

        [JsonPropertyName("first_key_timeout_ms")]
        public int FirstKeyTimeoutMs { get; set; } = 20000;

        [JsonPropertyName("next_key_timeout_ms")]
        public int NextKeyTimeoutMs { get; set; } = 20000;

        [JsonPropertyName("sticky_mode_on_double_tap")]
        public bool StickyModeOnDoubleTap { get; set; } = true;

        [JsonPropertyName("hud_opacity")]
        public double HudOpacity { get; set; } = 0.95;

        [JsonPropertyName("hook_only_when_nx_active")]
        public bool HookOnlyWhenNXActive { get; set; } = true;

        [JsonPropertyName("sequences")]
        public List<LeaderSequenceItem> Sequences { get; set; } = new List<LeaderSequenceItem>();

        [JsonIgnore]
        public List<ModuleConfig> RuntimeModules { get; set; } = new List<ModuleConfig>();

        public void ApplyDefaults()
        {
            if (string.IsNullOrWhiteSpace(TriggerKey)) TriggerKey = "CapsLock";
            if (HudDelayMs <= 0) HudDelayMs = 150;
            if (FirstKeyTimeoutMs <= 0) FirstKeyTimeoutMs = 20000;
            if (NextKeyTimeoutMs <= 0) NextKeyTimeoutMs = 20000;
            if (HudOpacity <= 0 || HudOpacity > 1.0) HudOpacity = 0.95;

            if (Sequences == null || Sequences.Count == 0)
            {
                Sequences = GetDefaultSequences();
            }
        }

        public void MergeModules(List<ModuleConfig> modules)
        {
            if (modules == null || modules.Count == 0) return;
            if (Sequences == null) Sequences = new List<LeaderSequenceItem>();

            var seen = new HashSet<string>(Sequences.Select(s => (s.Sequence ?? string.Empty).Replace(" ", "").ToUpperInvariant()));
            foreach (ModuleConfig module in modules)
            {
                if (module == null || !module.Enabled || string.IsNullOrWhiteSpace(module.LeaderPrefix)) continue;
                if (module.CommandSets == null) continue;
                foreach (ModuleCommandSet set in module.CommandSets)
                {
                    if (set?.Commands == null) continue;
                    foreach (ModuleCommand command in set.Commands)
                    {
                        if (command?.Command == null || string.IsNullOrWhiteSpace(command.Slot)) continue;
                        string sequence = module.LeaderPrefix.Trim().ToUpperInvariant() + " " + command.Slot.Trim().ToUpperInvariant();
                        string key = sequence.Replace(" ", "").ToUpperInvariant();
                        if (seen.Contains(key)) continue;
                        LeaderSequenceItem item = new LeaderSequenceItem(sequence, module.Label, command.Command.ID, command.Command.Name, string.Empty)
                        {
                            ModuleID = module.ID,
                            RequiresSelection = command.RequiresSelection,
                            Destructive = command.Destructive,
                            ConfirmBeforeExecute = command.ConfirmBeforeExecute || command.Destructive,
                            Fallback = command.Fallback,
                            Notes = string.IsNullOrWhiteSpace(command.Notes) ? command.Command.Name : command.Notes
                        };
                        Sequences.Add(item);
                        seen.Add(key);
                    }
                }
            }
        }

        public static List<LeaderSequenceItem> GetDefaultSequences()
        {
            return new List<LeaderSequenceItem>
            {
                // M — Modeling
                new LeaderSequenceItem("M S", "Modeling", "UG_CREATE_SKETCH", "Sketch", "Alt+1"),
                new LeaderSequenceItem("M E", "Modeling", "UG_MODELING_EXTRUDED_FEATURE", "Extrude", "Alt+2"),
                new LeaderSequenceItem("M H", "Modeling", "UG_MODELING_HOLE_FEATURE", "Hole", "Alt+3"),
                new LeaderSequenceItem("M R", "Modeling", "UG_MODELING_REVOLVED_FEATURE", "Revolve", "Ctrl+3"),
                new LeaderSequenceItem("M B", "Modeling", "UG_MODELING_BLEND_FEATURE", "Edge Blend", "Alt+4"),
                new LeaderSequenceItem("M C", "Modeling", "UG_MODELING_CHAMFER_FEATURE", "Chamfer", "Alt+5"),
                new LeaderSequenceItem("M P", "Modeling", "UG_MODELING_PATTERNFEATURE_FEATURE", "Pattern Feature", "Ctrl+4"),
                new LeaderSequenceItem("M M", "Modeling", "UG_MODELING_MIRRORFEATURE_FEATURE", "Mirror Feature", "Ctrl+5"),
                new LeaderSequenceItem("M D", "Modeling", "UG_MODELING_DATUMPLANE_FEATURE", "Datum Plane", "Ctrl+6"),
                new LeaderSequenceItem("M U", "Modeling", "UG_MODELING_UNITE_FEATURE", "Unite", "Ctrl+7"),
                new LeaderSequenceItem("M T", "Modeling", "UG_MODELING_SUBTRACT_FEATURE", "Subtract", ""),
                new LeaderSequenceItem("M I", "Modeling", "UG_MODELING_INTERSECT_FEATURE", "Intersect", ""),

                // S — Sketch
                new LeaderSequenceItem("S L", "Sketch", "UG_SKETCH_LINE", "Line", "L"),
                new LeaderSequenceItem("S R", "Sketch", "UG_SKETCH_RECTANGLE", "Rectangle", "R"),
                new LeaderSequenceItem("S C", "Sketch", "UG_SKETCH_CIRCLE", "Circle", "O"),
                new LeaderSequenceItem("S A", "Sketch", "UG_SKETCH_ARC", "Arc", "A"),
                new LeaderSequenceItem("S T", "Sketch", "UG_SKETCH_TRIM", "Trim", "T"),
                new LeaderSequenceItem("S E", "Sketch", "UG_SKETCH_EXTEND", "Extend", ""),
                new LeaderSequenceItem("S O", "Sketch", "UG_SKETCH_OFFSET_CURVE", "Offset Curve", ""),
                new LeaderSequenceItem("S D", "Sketch", "UG_SKETCH_RAPID_DIMENSION", "Rapid Dimension", "D"),
                new LeaderSequenceItem("S G", "Sketch", "UG_SKETCH_GEOMETRIC_CONSTRAINT", "Geometric Constraints", "C"),
                new LeaderSequenceItem("S M", "Sketch", "UG_SKETCH_MIRROR_CURVE", "Mirror Curve", "M"),
                new LeaderSequenceItem("S P", "Sketch", "UG_SKETCH_PATTERN_CURVE", "Pattern Curve", "P"),
                new LeaderSequenceItem("S K", "Sketch", "UG_SKETCH_CHECKER", "Sketch Checker", ""),
                new LeaderSequenceItem("S F", "Sketch", "UG_SKETCH_FINISH", "Finish Sketch", "Ctrl+Q"),

                // E — Edit/Synchronous
                new LeaderSequenceItem("E P", "Edit", "UG_EDIT_PARAMETERS", "Edit Parameters", ""),
                new LeaderSequenceItem("E S", "Edit", "UG_EDIT_SKETCH", "Edit Sketch", ""),
                new LeaderSequenceItem("E M", "Edit", "UG_MODELING_MOVE_FACE", "Move Face", ""),
                new LeaderSequenceItem("E R", "Edit", "UG_MODELING_REPLACE_FACE", "Replace Face", ""),
                new LeaderSequenceItem("E D", "Edit", "UG_MODELING_DELETE_FACE", "Delete Face", ""),
                new LeaderSequenceItem("E H", "Edit", "UG_MODELING_RESIZE_HOLE", "Resize Hole", ""),
                new LeaderSequenceItem("E T", "Edit", "UG_MODELING_RESIZE_PATTERN", "Resize Pattern", ""),
                new LeaderSequenceItem("E O", "Edit", "UG_MODELING_OFFSET_REGION", "Offset Region", ""),
                new LeaderSequenceItem("E B", "Edit", "UG_EDIT_ROLLBACK", "Edit with Rollback", ""),
                new LeaderSequenceItem("E F", "Edit", "UG_FEATURE_PLAYBACK", "Feature Playback", ""),

                // A — Assembly
                new LeaderSequenceItem("A A", "Assembly", "UG_ASSEMBLIES_ADD_COMPONENT", "Add Component", ""),
                new LeaderSequenceItem("A N", "Assembly", "UG_ASSEMBLIES_NEW_COMPONENT", "Create New Component", ""),
                new LeaderSequenceItem("A M", "Assembly", "UG_ASSEMBLIES_MOVE_COMPONENT", "Move Component", ""),
                new LeaderSequenceItem("A C", "Assembly", "UG_ASSEMBLIES_CONSTRAINTS", "Assembly Constraints", ""),
                new LeaderSequenceItem("A R", "Assembly", "UG_ASSEMBLIES_REPLACE_COMPONENT", "Replace Component", ""),
                new LeaderSequenceItem("A X", "Assembly", "UG_ASSEMBLIES_REMOVE_COMPONENT", "Remove Component", ""),
                new LeaderSequenceItem("A W", "Assembly", "UG_ASSEMBLIES_MAKE_WORK_PART", "Make Work Part", ""),
                new LeaderSequenceItem("A D", "Assembly", "UG_ASSEMBLIES_MAKE_DISPLAYED_PART", "Make Displayed Part", ""),
                new LeaderSequenceItem("A P", "Assembly", "UG_ASSEMBLIES_PATTERN_COMPONENT", "Pattern Component", ""),
                new LeaderSequenceItem("A V", "Assembly", "UG_ASSEMBLIES_NAVIGATOR", "Assembly Navigator", ""),
                new LeaderSequenceItem("A S", "Assembly", "UG_ASSEMBLIES_SHOW_ONLY", "Show Only", ""),
                new LeaderSequenceItem("A L", "Assembly", "UG_ASSEMBLIES_LOAD_OPTIONS", "Load Options", ""),

                // D — Drafting/PMI
                new LeaderSequenceItem("D B", "Drafting", "UG_DRAFTING_BASE_VIEW", "Base View", ""),
                new LeaderSequenceItem("D P", "Drafting", "UG_DRAFTING_PROJECTED_VIEW", "Projected View", ""),
                new LeaderSequenceItem("D S", "Drafting", "UG_DRAFTING_SECTION_VIEW", "Section View", ""),
                new LeaderSequenceItem("D T", "Drafting", "UG_DRAFTING_DETAIL_VIEW", "Detail View", ""),
                new LeaderSequenceItem("D M", "Drafting", "UG_DRAFTING_RAPID_DIMENSION", "Rapid Dimension", ""),
                new LeaderSequenceItem("D N", "Drafting", "UG_DRAFTING_NOTE", "Note", ""),
                new LeaderSequenceItem("D F", "Drafting", "UG_DRAFTING_FEATURE_CONTROL_FRAME", "Feature Control Frame", ""),
                new LeaderSequenceItem("D R", "Drafting", "UG_DRAFTING_SURFACE_FINISH", "Surface Finish Symbol", ""),
                new LeaderSequenceItem("D C", "Drafting", "UG_DRAFTING_CENTERLINE", "Centerline", ""),
                new LeaderSequenceItem("D L", "Drafting", "UG_DRAFTING_PARTS_LIST", "Parts List", ""),
                new LeaderSequenceItem("D A", "Drafting", "UG_DRAFTING_BALLOON", "Balloon", ""),
                new LeaderSequenceItem("D U", "Drafting", "UG_DRAFTING_UPDATE_VIEWS", "Update Views", ""),

                // V — View
                new LeaderSequenceItem("V F", "View", "UG_VIEW_FIT", "Fit", "Ctrl+F"),
                new LeaderSequenceItem("V T", "View", "UG_VIEW_POPUP_ORIENT_TFRTRI", "Trimetric", "Home"),
                new LeaderSequenceItem("V I", "View", "UG_VIEW_POPUP_ORIENT_TFRISO", "Isometric", "End"),
                new LeaderSequenceItem("V O", "View", "UG_VIEW_POPUP_SNAP_VIEW", "Closest Orthographic", "F8"),
                new LeaderSequenceItem("V N", "View", "UG_SKETCH_ORIENT_VIEW_TO_SKETCH", "Normal to Sketch", "Shift+F8"),
                new LeaderSequenceItem("V W", "View", "UG_WCS_DISPLAY", "WCS Display", "W"),
                new LeaderSequenceItem("V H", "View", "UG_VIEW_HIDE", "Hide", ""),
                new LeaderSequenceItem("V S", "View", "UG_VIEW_SHOW_ONLY", "Show Only", ""),
                new LeaderSequenceItem("V R", "View", "UG_VIEW_RESET_ORIENT", "Reset Orientation", ""),
                new LeaderSequenceItem("V L", "View", "UG_LAYER_SETTINGS", "Layer Settings", "Ctrl+L"),

                // I — Inspect
                new LeaderSequenceItem("I M", "Inspect", "UG_HELP_MEASURE", "Measure", "F4"),
                new LeaderSequenceItem("I I", "Inspect", "UG_INFO_OBJECT", "Object Information", "Ctrl+I"),
                new LeaderSequenceItem("I D", "Inspect", "UG_EDIT_OBJECT_DISPLAY", "Object Display", "Ctrl+J"),
                new LeaderSequenceItem("I L", "Inspect", "UG_LAYER_SETTINGS", "Layer Settings", "Ctrl+L"),
                new LeaderSequenceItem("I F", "Inspect", "UG_SEL_SIMILAR_FACES", "Select Similar Faces", ""),
                new LeaderSequenceItem("I E", "Inspect", "UG_SEL_SIMILAR_EDGES", "Select Similar Edges", ""),
                new LeaderSequenceItem("I C", "Inspect", "UG_SEL_SIMILAR_COMPONENTS", "Select Similar Components", ""),
                new LeaderSequenceItem("I P", "Inspect", "UG_INFO_SHOW_PARENTS", "Show Parents", ""),
                new LeaderSequenceItem("I H", "Inspect", "UG_INFO_SHOW_CHILDREN", "Show Children", ""),

                // R — Reuse
                new LeaderSequenceItem("R E", "Reuse", "UG_EXPRESSIONS", "Expressions", "Ctrl+E"),
                new LeaderSequenceItem("R W", "Reuse", "UG_MODELING_WAVE_LINKER", "WAVE Geometry Linker", ""),
                new LeaderSequenceItem("R L", "Reuse", "UG_NAVIGATOR_REUSE_LIBRARY", "Reuse Library", ""),
                new LeaderSequenceItem("R C", "Reuse", "UG_CREATE_FEATURE_TEMPLATE", "Create Feature Template", ""),
                new LeaderSequenceItem("R T", "Reuse", "UG_EDIT_FEATURE_TEMPLATE", "Edit Feature Template", ""),
                new LeaderSequenceItem("R P", "Reuse", "UG_REPLACE_FEATURE_TEMPLATE", "Replace Feature Template", ""),
                new LeaderSequenceItem("R B", "Reuse", "UG_PARAMETER_TABLE", "Parameter Table", ""),
                new LeaderSequenceItem("R N", "Reuse", "UG_NAVIGATOR_PART", "Part Navigator", "")
            };
        }
    }

    public sealed class LeaderSequenceItem
    {
        [JsonPropertyName("sequence")]
        public string Sequence { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("module_id")]
        public string ModuleID { get; set; } = string.Empty;

        [JsonPropertyName("command")]
        public CommandRef Command { get; set; } = new CommandRef();

        [JsonPropertyName("shortcut_to_execute")]
        public string ShortcutToExecute { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("requires_selection")]
        public bool RequiresSelection { get; set; }

        [JsonPropertyName("destructive")]
        public bool Destructive { get; set; }

        [JsonPropertyName("confirm_before_execute")]
        public bool ConfirmBeforeExecute { get; set; }

        [JsonPropertyName("fallback")]
        public string Fallback { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;

        public LeaderSequenceItem() { }

        public LeaderSequenceItem(string sequence, string category, string commandId, string commandName, string shortcutToExecute)
        {
            Sequence = sequence;
            Category = category;
            ModuleID = ModuleDefaults.ModuleIdForCategory(category);
            Command = new CommandRef { ID = commandId, Name = commandName };
            ShortcutToExecute = shortcutToExecute;
            Notes = commandName;
            Enabled = true;
        }
    }

    public static class ModuleDefaults
    {
        public static readonly Dictionary<string, string> SlotSemantics =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["N"] = "start/create/open primary object",
                ["NE"] = "next main process step",
                ["E"] = "add object/material/dependency",
                ["SE"] = "transform or replace",
                ["S"] = "finish/delete/secondary processing",
                ["SW"] = "remove/reduce/relax",
                ["W"] = "structure/link/pattern",
                ["NW"] = "inspect/measure/service command"
            };

        public static List<ModuleConfig> BuildDefaultModules()
        {
            return new List<ModuleConfig>
            {
                Module("modeling", "Modeling", "M", new[] { "UG_APP_MODELING" }, "UG_APP_MODELING", "Modeling", new[]
                {
                    Cmd("N", "UG_CREATE_SKETCH", "Sketch", false, false),
                    Cmd("NE", "UG_MODELING_EXTRUDED_FEATURE", "Extrude", false, false),
                    Cmd("E", "UG_MODELING_HOLE_FEATURE", "Hole", false, false),
                    Cmd("SE", "UG_MODELING_REVOLVED_FEATURE", "Revolve", false, false),
                    Cmd("S", "UG_MODELING_BLEND_FEATURE", "Edge Blend", true, false),
                    Cmd("SW", "UG_MODELING_CHAMFER_FEATURE", "Chamfer", true, false),
                    Cmd("W", "UG_MODELING_PATTERNFEATURE_FEATURE", "Pattern Feature", true, false),
                    Cmd("NW", "UG_MODELING_MIRRORFEATURE_FEATURE", "Mirror Feature", true, false)
                }),
                Module("sketch", "Sketch", "S", new[] { "UG_APP_SKETCH", "UG_APP_MODELING" }, "UG_CREATE_SKETCH", "Sketch", new[]
                {
                    Cmd("N", "UG_SKETCH_LINE", "Line", false, false),
                    Cmd("NE", "UG_SKETCH_RECTANGLE", "Rectangle", false, false),
                    Cmd("E", "UG_SKETCH_CIRCLE", "Circle", false, false),
                    Cmd("SE", "UG_SKETCH_ARC", "Arc", false, false),
                    Cmd("S", "UG_SKETCH_TRIM", "Trim", true, false),
                    Cmd("SW", "UG_SKETCH_EXTEND", "Extend", true, false),
                    Cmd("W", "UG_SKETCH_OFFSET_CURVE", "Offset Curve", true, false),
                    Cmd("NW", "UG_SKETCH_CHECKER", "Sketch Checker", false, false)
                }),
                Module("assembly", "Assembly", "A", new[] { "UG_APP_ASSEMBLIES" }, "UG_APP_ASSEMBLIES", "Assemblies", new[]
                {
                    Cmd("N", "UG_ASSEMBLIES_ADD_COMPONENT", "Add Component", false, false),
                    Cmd("NE", "UG_ASSEMBLIES_NEW_COMPONENT", "Create New Component", false, false),
                    Cmd("E", "UG_ASSEMBLIES_MOVE_COMPONENT", "Move Component", true, false),
                    Cmd("SE", "UG_ASSEMBLIES_CONSTRAINTS", "Assembly Constraints", true, false),
                    Cmd("S", "UG_ASSEMBLIES_REPLACE_COMPONENT", "Replace Component", true, true),
                    Cmd("SW", "UG_ASSEMBLIES_REMOVE_COMPONENT", "Remove Component", true, true),
                    Cmd("W", "UG_ASSEMBLIES_PATTERN_COMPONENT", "Pattern Component", true, false),
                    Cmd("NW", "UG_ASSEMBLIES_NAVIGATOR", "Assembly Navigator", false, false)
                }),
                Module("drafting", "Drafting", "D", new[] { "UG_APP_DRAFTING" }, "UG_APP_DRAFTING", "Drafting", new[]
                {
                    Cmd("N", "UG_DRAFTING_BASE_VIEW", "Base View", false, false),
                    Cmd("NE", "UG_DRAFTING_PROJECTED_VIEW", "Projected View", false, false),
                    Cmd("E", "UG_DRAFTING_SECTION_VIEW", "Section View", false, false),
                    Cmd("SE", "UG_DRAFTING_DETAIL_VIEW", "Detail View", false, false),
                    Cmd("S", "UG_DRAFTING_UPDATE_VIEWS", "Update Views", false, false),
                    Cmd("SW", "UG_DRAFTING_VIEW_STYLE", "View Style", true, false),
                    Cmd("W", "UG_DRAFTING_PARTS_LIST", "Parts List", false, false),
                    Cmd("NW", "UG_DRAFTING_RAPID_DIMENSION", "Rapid Dimension", false, false)
                }),
                Module("pmi", "PMI", "P", new[] { "UG_APP_PMI" }, "UG_APP_PMI", "PMI", new[]
                {
                    Cmd("N", "UG_PMI_RAPID_DIMENSION", "Rapid Dimension", false, false),
                    Cmd("NE", "UG_PMI_DATUM_FEATURE_SYMBOL", "Datum Feature Symbol", false, false),
                    Cmd("E", "UG_PMI_FEATURE_CONTROL_FRAME", "Feature Control Frame", false, false),
                    Cmd("SE", "UG_PMI_SURFACE_FINISH", "Surface Finish Symbol", false, false),
                    Cmd("S", "UG_PMI_NOTE", "PMI Note", false, false),
                    Cmd("SW", "UG_PMI_EDIT", "Edit PMI", true, false),
                    Cmd("W", "UG_PMI_MODEL_VIEW", "Model View", false, false),
                    Cmd("NW", "UG_PMI_VALIDATE", "Validate PMI", false, false)
                }),
                Module("surface", "Surface", "U", new[] { "UG_APP_MODELING", "UG_APP_STUDIO" }, "UG_APP_MODELING", "Surface Modeling", new[]
                {
                    Cmd("N", "UG_MODELING_THROUGH_CURVES_FEATURE", "Through Curves", false, false),
                    Cmd("NE", "UG_MODELING_SWEPT_FEATURE", "Swept", false, false),
                    Cmd("E", "UG_MODELING_STUDIO_SURFACE_FEATURE", "Studio Surface", false, false),
                    Cmd("SE", "UG_MODELING_TRIM_SHEET_FEATURE", "Trim Sheet", true, false),
                    Cmd("S", "UG_MODELING_SEW_FEATURE", "Sew", true, false),
                    Cmd("SW", "UG_MODELING_UNTRIM_FEATURE", "Untrim", true, false),
                    Cmd("W", "UG_MODELING_EXTRACT_GEOMETRY", "Extract Geometry", true, false),
                    Cmd("NW", "UG_ANALYSIS_FACE_CURVATURE", "Face Curvature", true, false)
                }),
                Module("sheet_metal", "Sheet Metal", "H", new[] { "UG_APP_SHEETMETAL" }, "UG_APP_SHEETMETAL", "Sheet Metal", new[]
                {
                    Cmd("N", "UG_SHEET_METAL_BASE_TAB", "Base Tab", false, false),
                    Cmd("NE", "UG_SHEET_METAL_FLANGE", "Flange", true, false),
                    Cmd("E", "UG_SHEET_METAL_CONTOUR_FLANGE", "Contour Flange", false, false),
                    Cmd("SE", "UG_SHEET_METAL_BEND", "Bend", true, false),
                    Cmd("S", "UG_SHEET_METAL_UNBEND", "Unbend", true, false),
                    Cmd("SW", "UG_SHEET_METAL_REBEND", "Rebend", true, false),
                    Cmd("W", "UG_SHEET_METAL_FLAT_PATTERN", "Flat Pattern", false, false),
                    Cmd("NW", "UG_SHEET_METAL_VALIDATE", "Sheet Metal Preferences", false, false)
                }),
                Module("manufacturing", "CAM / Manufacturing", "C", new[] { "UG_APP_MANUFACTURING" }, "UG_APP_MANUFACTURING", "Manufacturing", new[]
                {
                    Cmd("N", "UG_CAM_CREATE_OPERATION", "Create Operation", false, false),
                    Cmd("NE", "UG_CAM_CREATE_TOOL", "Create Tool", false, false),
                    Cmd("E", "UG_CAM_GENERATE_TOOL_PATH", "Generate Tool Path", true, false),
                    Cmd("SE", "UG_CAM_VERIFY_TOOL_PATH", "Verify Tool Path", true, false),
                    Cmd("S", "UG_CAM_POSTPROCESS", "Postprocess", true, true),
                    Cmd("SW", "UG_CAM_DELETE_OPERATION", "Delete Operation", true, true),
                    Cmd("W", "UG_CAM_OPERATION_NAVIGATOR", "Operation Navigator", false, false),
                    Cmd("NW", "UG_CAM_INFORMATION", "Tool Path Information", true, false)
                }),
                Module("simulation", "CAE / Simulation", "X", new[] { "UG_APP_SFEM", "UG_APP_DESFEM" }, "UG_APP_SFEM", "Simulation", new[]
                {
                    Cmd("N", "UG_SIM_CREATE_SOLUTION", "Create Solution", false, false),
                    Cmd("NE", "UG_SIM_CREATE_LOAD", "Create Load", false, false),
                    Cmd("E", "UG_SIM_CREATE_CONSTRAINT", "Create Constraint", false, false),
                    Cmd("SE", "UG_SIM_MESH", "Mesh", true, false),
                    Cmd("S", "UG_SIM_SOLVE", "Solve", true, true),
                    Cmd("SW", "UG_SIM_DELETE", "Delete Simulation Object", true, true),
                    Cmd("W", "UG_SIM_NAVIGATOR", "Simulation Navigator", false, false),
                    Cmd("NW", "UG_SIM_RESULTS", "Results", false, false)
                }),
                Module("routing", "Routing", "G", new[] { "UG_APP_ROUTING" }, "UG_APP_ROUTING", "Routing", new[]
                {
                    Cmd("N", "UG_ROUTE_CREATE_ROUTE", "Create Route", false, false),
                    Cmd("NE", "UG_ROUTE_PLACE_PART", "Place Part", false, false),
                    Cmd("E", "UG_ROUTE_ADD_STOCK", "Add Stock", true, false),
                    Cmd("SE", "UG_ROUTE_EDIT_ROUTE", "Edit Route", true, false),
                    Cmd("S", "UG_ROUTE_DELETE", "Delete Route Object", true, true),
                    Cmd("SW", "UG_ROUTE_REMOVE_PART", "Remove Part", true, true),
                    Cmd("W", "UG_ROUTE_NAVIGATOR", "Routing Navigator", false, false),
                    Cmd("NW", "UG_ROUTE_VALIDATE", "Validate Route", false, false)
                }),
                Module("mold", "Mold / Tooling", "O", new[] { "UG_APP_MOLDWIZARD" }, "UG_APP_MOLDWIZARD", "Mold Wizard", new[]
                {
                    Cmd("N", "UG_MOLD_INITIALIZE_PROJECT", "Initialize Project", false, false),
                    Cmd("NE", "UG_MOLD_PARTING", "Parting", true, false),
                    Cmd("E", "UG_MOLD_MOLD_BASE", "Mold Base", false, false),
                    Cmd("SE", "UG_MOLD_GATE", "Gate", true, false),
                    Cmd("S", "UG_MOLD_COOLING", "Cooling", false, false),
                    Cmd("SW", "UG_MOLD_EJECTOR", "Ejector", false, false),
                    Cmd("W", "UG_MOLD_LIBRARY", "Mold Library", false, false),
                    Cmd("NW", "UG_MOLD_VALIDATE", "Validate Mold Design", false, false)
                }),
                Module("reuse", "Reuse / Templates", "R", new[] { "UG_APP_MODELING" }, "UG_NAVIGATOR_REUSE_LIBRARY", "Reuse Library", new[]
                {
                    Cmd("N", "UG_EXPRESSIONS", "Expressions", false, false),
                    Cmd("NE", "UG_MODELING_WAVE_LINKER", "WAVE Geometry Linker", false, false),
                    Cmd("E", "UG_NAVIGATOR_REUSE_LIBRARY", "Reuse Library", false, false),
                    Cmd("SE", "UG_CREATE_FEATURE_TEMPLATE", "Create Feature Template", true, false),
                    Cmd("S", "UG_REPLACE_FEATURE_TEMPLATE", "Replace Feature Template", true, true),
                    Cmd("SW", "UG_NAVIGATOR_PART", "Part Navigator", false, false),
                    Cmd("W", "UG_PARAMETER_TABLE", "Parameter Table", false, false),
                    Cmd("NW", "UG_COMMAND_FINDER", "Command Finder", false, false)
                }),
                Module("inspect_view", "Inspect / View", "V", new[] { "UG_APP_GATEWAY", "UG_APP_MODELING" }, "UG_APP_GATEWAY", "Inspect and View", new[]
                {
                    Cmd("N", "UG_VIEW_FIT", "Fit", false, false),
                    Cmd("NE", "UG_VIEW_POPUP_ORIENT_TFRTRI", "Trimetric", false, false),
                    Cmd("E", "UG_HELP_MEASURE", "Measure", true, false),
                    Cmd("SE", "UG_INFO_OBJECT", "Object Information", true, false),
                    Cmd("S", "UG_VIEW_HIDE", "Hide", true, false),
                    Cmd("SW", "UG_VIEW_SHOW_ONLY", "Show Only", true, false),
                    Cmd("W", "UG_LAYER_SETTINGS", "Layer Settings", false, false),
                    Cmd("NW", "UG_COMMAND_FINDER", "Command Finder", false, false)
                }),
                SelectionModule()
            };
        }

        public static string ModuleIdForCategory(string category)
        {
            string key = (category ?? string.Empty).Trim().ToLowerInvariant();
            switch (key)
            {
                case "modeling": return "modeling";
                case "sketch": return "sketch";
                case "assembly": return "assembly";
                case "drafting": return "drafting";
                case "view": return "inspect_view";
                case "inspect": return "inspect_view";
                case "reuse": return "reuse";
                case "selection filters": return "selection_object";
                default: return key.Replace(" ", "_");
            }
        }

        private static ModuleConfig SelectionModule()
        {
            ModuleConfig module = Module("selection_object", "Selection / Object", "F", new[] { "UG_APP_GATEWAY", "UG_APP_MODELING" }, "UG_APP_GATEWAY", "Selection", new[]
            {
                Cmd("N", "UG_SEL_BODY_PRIORITY", "Body Selection Priority", false, false),
                Cmd("NE", "UG_SEL_FACE_PRIORITY", "Face Selection Priority", false, false),
                Cmd("E", "UG_SEL_EDGE_PRIORITY", "Edge Selection Priority", false, false),
                Cmd("SE", "UG_SEL_FEATURE_PRIORITY", "Feature Selection Priority", false, false),
                Cmd("S", "UG_SEL_COMPONENT_PRIORITY", "Component Selection Priority", false, false),
                Cmd("SW", "UG_SEL_CURVE_PRIORITY", "Curve Selection Priority", false, false),
                Cmd("W", "UG_SEL_DATUM_PRIORITY", "Datum Selection Priority", false, false),
                Cmd("NW", "UG_SEL_TYPE_RESET", "Reset Selection Filter", false, false)
            });
            module.SelectionPriorities = module.CommandSets[0].Commands;
            module.Radials[0].Kind = "object";
            return module;
        }

        private static ModuleConfig Module(string id, string label, string prefix, IEnumerable<string> applications, string switchId, string switchName, IEnumerable<ModuleCommand> commands)
        {
            var commandList = commands.ToList();
            return new ModuleConfig
            {
                ID = id,
                Label = label,
                Enabled = true,
                LeaderPrefix = prefix,
                NXApplicationIDs = applications.ToList(),
                SwitchCommand = new CommandRef { ID = switchId, Name = switchName },
                CommandSets = new List<ModuleCommandSet>
                {
                    new ModuleCommandSet
                    {
                        ID = "primary",
                        Label = "Primary",
                        SlotSemantics = new Dictionary<string, string>(SlotSemantics, StringComparer.OrdinalIgnoreCase),
                        Commands = commandList
                    }
                },
                Radials = new List<RadialMenu>
                {
                    new RadialMenu
                    {
                        Name = label + " Radial 1 - Primary",
                        Module = id,
                        Kind = "application",
                        Trigger = "Ctrl+Shift+MB1",
                        Enabled = true,
                        Items = commandList.Select(c => new RadialItem { Direction = c.Slot, Command = c.Command, Notes = c.Notes }).ToList()
                    }
                }
            };
        }

        private static ModuleCommand Cmd(string slot, string id, string name, bool requiresSelection, bool destructive)
        {
            return new ModuleCommand
            {
                Slot = slot,
                Command = new CommandRef { ID = id, Name = name },
                RequiresSelection = requiresSelection,
                Destructive = destructive,
                ConfirmBeforeExecute = destructive
            };
        }
    }
}
