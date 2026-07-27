using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace NX2512_HotkeyStudio.Models
{
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

}
