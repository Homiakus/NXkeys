using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class DeploymentPlan
    {
        public List<ResolutionResult> Resolutions { get; set; } = new List<ResolutionResult>();
        public Dictionary<string, List<ConflictItem>> Conflicts { get; set; } = new Dictionary<string, List<ConflictItem>>();
        public string OverlayContent { get; set; } = string.Empty;
        public string ResolutionReport { get; set; } = string.Empty;
        public string RadialPlanMarkdown { get; set; } = string.Empty;
        public string RadialPlanJson { get; set; } = string.Empty;
        public List<string> ActionSummary { get; set; } = new List<string>();
        public bool IsValid { get; set; } = true;
    }

    public static class DeploymentEngine
    {
        private const int DefaultMenuScriptVersion = MenuScriptDefaults.Version;
        private const int DefaultToolbarScriptVersion = MenuScriptDefaults.ToolbarVersion;

        public static DeploymentPlan BuildPlan(Config config, CatalogIndex catalog)
        {
            var plan = new DeploymentPlan();
            var resolver = new CommandResolver(catalog);

            plan.Resolutions = resolver.ResolveBindings(config.Keyboard);

            foreach (ResolutionResult res in plan.Resolutions)
            {
                if (res.Status == ResolutionStatus.Resolved && res.Binding != null && res.Binding.Enabled)
                {
                    var conflicts = resolver.FindConflicts(res.Binding.Shortcut, res.CommandID);
                    if (conflicts.Count > 0)
                    {
                        plan.Conflicts[res.Binding.Shortcut] = conflicts;
                    }
                }
            }

            string customStartup = Path.Combine(config.Deployment.ManagedRoot, "custom", "startup");
            string daemonScriptInStartup = Path.Combine(customStartup, "launch-hotkeystudio-daemon.cmd");
            string guiScriptInStartup = Path.Combine(customStartup, "launch-hotkeystudio-gui.cmd");

            plan.OverlayContent = OverlayGenerator.GenerateOverlay(
                config.Deployment.MenuScriptVersion,
                config.Deployment.MainMenubarID,
                plan.Resolutions,
                plan.Conflicts,
                config.Deployment.ClearDetectedConflicts,
                daemonScriptInStartup,
                guiScriptInStartup);

            plan.ResolutionReport = BuildResolutionReport(plan.Resolutions, plan.Conflicts);
            plan.RadialPlanMarkdown = BuildRadialPlanMarkdown(config);
            plan.RadialPlanJson = BuildRadialPlanJson(config);

            plan.ActionSummary.Add($"Mode: {config.Deployment.Mode}");
            plan.ActionSummary.Add($"Managed Root: {config.Deployment.ManagedRoot}");
            plan.ActionSummary.Add($"Resolved Keybindings: {plan.Resolutions.FindAll(x => x.Status == ResolutionStatus.Resolved).Count} / {plan.Resolutions.Count}");
            plan.ActionSummary.Add($"Shortcut Conflicts Detected: {plan.Conflicts.Count}");
            plan.ActionSummary.Add($"Modular Command Sets: {config.Modules?.Count ?? 0}");

            if (config.Role != null && config.Role.Enabled)
            {
                plan.ActionSummary.Add($"Role Deployment: {config.Role.SourceMTX} -> {Path.Combine(config.Role.TargetDirectory, config.Role.TargetName)}");
            }

            return plan;
        }

        public static bool ApplyPlan(Config config, DeploymentPlan plan, out string backupFolder, out string error)
        {
            backupFolder = string.Empty;
            error = string.Empty;
            BackupManifest manifest = null;

            if (config.Deployment.DryRun)
            {
                error = "DryRun is enabled in settings. No changes were written to disk.";
                return true;
            }
            if (config.Deployment.RequireNXStopped && IsNXRunning())
            {
                error = "NX appears to be running. Close Siemens NX before applying this deployment plan.";
                return false;
            }

            try
            {
                string managedRoot = config.Deployment.ManagedRoot;
                string customDirPath = Path.Combine(managedRoot, "custom");
                string customStartup = Path.Combine(managedRoot, "custom", "startup");
                string customApplication = Path.Combine(managedRoot, "custom", "application");
                bool patchExistingCustomDirs = config.Deployment.Mode == "existing-custom-dirs" && config.Deployment.PatchExistingCustomDirs;

                List<string> targetFiles = new List<string>();

                // 1. Overlay file
                string overlayFile = Path.Combine(customStartup, config.Deployment.OverlayFilename);
                AddTarget(targetFiles, overlayFile);

                // 2. Custom dirs file
                string customDirsFile = Path.Combine(managedRoot, "custom_dirs.dat");
                AddTarget(targetFiles, customDirsFile);

                // 3. Launcher script
                string launcherScript = Path.Combine(managedRoot, "launch-nx2512-with-nxkeys.cmd");
                AddTarget(targetFiles, launcherScript);

                // 4. Reports
                string reportFile = Path.Combine(managedRoot, "resolution-report.md");
                string radialMdFile = Path.Combine(managedRoot, "radial-menu-plan.md");
                string radialJsonFile = Path.Combine(managedRoot, "radial-menu-plan.json");
                AddTarget(targetFiles, reportFile);
                AddTarget(targetFiles, radialMdFile);
                AddTarget(targetFiles, radialJsonFile);

                string guiScriptInStartup = Path.Combine(customStartup, "launch-hotkeystudio-gui.cmd");
                string daemonScriptInStartup = Path.Combine(customStartup, "launch-hotkeystudio-daemon.cmd");
                string bridgeMenuPath = Path.Combine(customApplication, "nxkeys_command_bridge.men");
                string rtbPath = Path.Combine(customStartup, "nxkeys_ribbon.rtb");
                string tbrPath = Path.Combine(customStartup, "nxkeys_toolbar.tbr");
                AddTarget(targetFiles, guiScriptInStartup);
                AddTarget(targetFiles, daemonScriptInStartup);
                AddTarget(targetFiles, bridgeMenuPath);
                AddTarget(targetFiles, rtbPath);
                AddTarget(targetFiles, tbrPath);

                // 5. Existing custom dirs file if patched
                if (config.Deployment.Mode == "existing-custom-dirs" &&
                    patchExistingCustomDirs &&
                    !string.IsNullOrWhiteSpace(config.Deployment.ExistingCustomDirsFile))
                {
                    AddTarget(targetFiles, config.Deployment.ExistingCustomDirsFile);
                }

                // 6. Role file deployment if enabled
                if (config.Role != null && config.Role.Enabled && !string.IsNullOrWhiteSpace(config.Role.SourceMTX))
                {
                    string targetRoleFile = Path.Combine(config.Role.TargetDirectory, config.Role.TargetName);
                    AddTarget(targetFiles, targetRoleFile);
                }

                CollectSiemensUserProfileTargets(targetFiles, customDirPath, managedRoot, patchExistingCustomDirs);
                CollectManagedBinaryTargets(targetFiles, managedRoot);

                // Create SHA-256 backup before modifying
                manifest = BackupEngine.CreateBackup(config.Deployment.BackupRoot, config.Profile.Name, targetFiles);
                backupFolder = manifest.BackupDirectory;

                Directory.CreateDirectory(customStartup);
                Directory.CreateDirectory(customApplication);

                // Write generated overlay
                MenuScriptWriter.WriteAllText(overlayFile, plan.OverlayContent);

                // Write custom_dirs.dat
                File.WriteAllText(customDirsFile, customDirPath + Environment.NewLine, Encoding.UTF8);

                // Write launcher CMD
                string launcherCmd = BuildLauncherCmd(config, customDirsFile);
                File.WriteAllText(launcherScript, launcherCmd, Encoding.Default);

                // Write GUI launcher script for Siemens NX menu button
                string guiLauncherCmd = BuildGuiLauncherCmd(managedRoot);
                File.WriteAllText(guiScriptInStartup, guiLauncherCmd, Encoding.Default);

                // Write Daemon launcher script for Siemens NX ribbon button
                string daemonLauncherCmd = BuildDaemonLauncherCmd(managedRoot);
                File.WriteAllText(daemonScriptInStartup, daemonLauncherCmd, Encoding.Default);

                // Write NXOpen command bridge application menu file
                MenuScriptWriter.WriteAllText(bridgeMenuPath, BuildCommandBridgeMenuFile(config.Deployment.MenuScriptVersion));

                // Write Ribbon Tab (.rtb) and Toolbar (.tbr) files
                string rtbContent = BuildRibbonTabFile(managedRoot);
                MenuScriptWriter.WriteAllText(rtbPath, rtbContent);

                string tbrContent = BuildToolbarFile(managedRoot);
                MenuScriptWriter.WriteAllText(tbrPath, tbrContent);

                // Write reports
                File.WriteAllText(reportFile, plan.ResolutionReport, Encoding.UTF8);
                File.WriteAllText(radialMdFile, plan.RadialPlanMarkdown, Encoding.UTF8);
                File.WriteAllText(radialJsonFile, plan.RadialPlanJson, Encoding.UTF8);

                // Patch existing custom_dirs.dat if specified
                if (patchExistingCustomDirs && File.Exists(config.Deployment.ExistingCustomDirsFile))
                {
                    PatchExistingCustomDirs(config.Deployment.ExistingCustomDirsFile, customDirPath);
                }

                // Deploy MTX role if enabled
                if (config.Role != null && config.Role.Enabled && File.Exists(config.Role.SourceMTX))
                {
                    Directory.CreateDirectory(config.Role.TargetDirectory);
                    string targetRolePath = Path.Combine(config.Role.TargetDirectory, config.Role.TargetName);
                    File.Copy(config.Role.SourceMTX, targetRolePath, true);
                }

                // In managed-wrapper mode, generated files stay under managedRoot and are activated by the launcher.
                if (patchExistingCustomDirs)
                {
                    DeployToSiemensUserProfiles(plan.OverlayContent, targetFiles, customDirPath, managedRoot, patchExistingCustomDirs, config.Deployment.MenuScriptVersion);
                }

                // Copy executable binaries and runtime files to managed root
                CopyBinariesToManagedRoot(managedRoot);

                BackupEngine.FinalizeBackupManifest(manifest);
                return true;
            }
            catch (Exception ex)
            {
                if (manifest != null)
                {
                    try { BackupEngine.FinalizeBackupManifest(manifest); } catch { }
                }
                error = ex.ToString();
                return false;
            }
        }

        private static bool IsNXRunning()
        {
            foreach (string processName in new[] { "ugraf", "run_nx", "nx" })
            {
                try
                {
                    foreach (Process proc in Process.GetProcessesByName(processName))
                    {
                        using (proc)
                        {
                            if (IsSiemensNXProcess(proc)) return true;
                        }
                    }
                }
                catch
                {
                    // Treat process enumeration failures as non-fatal; write operations still have file-level checks.
                }
            }
            return false;
        }

        private static bool IsSiemensNXProcess(Process proc)
        {
            string name = (proc.ProcessName ?? string.Empty).ToLowerInvariant();
            if (name == "ugraf" || name == "run_nx") return true;
            if (name != "nx") return false;

            try
            {
                string path = proc.MainModule?.FileName ?? string.Empty;
                if (path.IndexOf(@"\Siemens\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf(@"\DesigncenterNX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf(@"\NXBIN\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            catch { }

            try
            {
                string description = proc.MainModule?.FileVersionInfo?.FileDescription ?? string.Empty;
                return description.IndexOf("Siemens", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       description.IndexOf("Designcenter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       description.IndexOf("NX", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static void AddTarget(List<string> targetFiles, string path)
        {
            if (targetFiles == null || string.IsNullOrWhiteSpace(path)) return;
            string fullPath = Path.GetFullPath(path);
            foreach (string existing in targetFiles)
            {
                if (string.Equals(Path.GetFullPath(existing), fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            targetFiles.Add(fullPath);
        }

        private static void CollectManagedBinaryTargets(List<string> targetFiles, string managedRoot)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!Directory.Exists(baseDir)) return;

            foreach (string file in Directory.GetFiles(baseDir))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".exe" || ext == ".dll" || ext == ".json" || ext == ".cmd" || ext == ".bat")
                {
                    AddTarget(targetFiles, Path.Combine(managedRoot, Path.GetFileName(file)));
                }
            }
        }

        private static void CollectSiemensUserProfileTargets(List<string> targetFiles, string customDirPath, string managedRoot, bool patchCustomDirs)
        {
            if (!patchCustomDirs) return;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            foreach (string bp in new[] { Path.Combine(localAppData, "Siemens"), Path.Combine(appData, "Siemens") })
            {
                if (!Directory.Exists(bp)) continue;

                foreach (string subDir in Directory.GetDirectories(bp))
                {
                    string startupDir = Path.Combine(subDir, "startup");
                    AddTarget(targetFiles, Path.Combine(startupDir, "nxkeys_generated.men"));
                    AddTarget(targetFiles, Path.Combine(startupDir, "launch-hotkeystudio-gui.cmd"));
                    AddTarget(targetFiles, Path.Combine(startupDir, "launch-hotkeystudio-daemon.cmd"));
                    AddTarget(targetFiles, Path.Combine(startupDir, "nxkeys_ribbon.rtb"));
                    AddTarget(targetFiles, Path.Combine(startupDir, "nxkeys_toolbar.tbr"));
                    AddTarget(targetFiles, Path.Combine(subDir, "application", "nxkeys_command_bridge.men"));

                    foreach (string psd in new[] { "All", "UG_APP_MODELING", "UG_APP_GATEWAY", "Reference" })
                    {
                        AddTarget(targetFiles, Path.Combine(subDir, "application", "profiles", psd, "rbn_nxkeys.rtb"));
                    }

                    string customDirsDat = Path.Combine(subDir, "custom_dirs.dat");
                    if (patchCustomDirs && File.Exists(customDirsDat))
                    {
                        AddTarget(targetFiles, customDirsDat);
                    }
                }
            }

            foreach (string envKey in new[] { "UGII_USER_DIR", "UGII_SITE_DIR" })
            {
                string envVal = Environment.GetEnvironmentVariable(envKey);
                if (!string.IsNullOrWhiteSpace(envVal) && Directory.Exists(envVal))
                {
                    AddTarget(targetFiles, Path.Combine(envVal, "startup", "nxkeys_generated.men"));
                }
            }
        }

        private static void CopyBinariesToManagedRoot(string managedRoot)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                Directory.CreateDirectory(managedRoot);

                foreach (string file in Directory.GetFiles(baseDir))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".exe" || ext == ".dll" || ext == ".json" || ext == ".cmd" || ext == ".bat")
                    {
                        string dest = Path.Combine(managedRoot, Path.GetFileName(file));
                        File.Copy(file, dest, true);
                    }
                }
            }
            catch
            {
                // Ignore copy errors if file is currently locked
            }
        }

        private static string BuildLauncherCmd(Config config, string customDirsFile)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("REM Launcher created by NXKeys Studio");
            sb.AppendLine("REM 1. Start HotkeyStudio Leader Key engine in background");
            sb.AppendLine("set \"NXKEYS_EXE=%~dp0NX2512_HotkeyStudio.exe\"");
            sb.AppendLine("if not exist \"%NXKEYS_EXE%\" set \"NXKEYS_EXE=%~dp0dist\\NX2512_HotkeyStudio.exe\"");
            sb.AppendLine("if exist \"%NXKEYS_EXE%\" start \"\" \"%NXKEYS_EXE%\" --ensure-background");
            sb.AppendLine();
            sb.AppendLine("set \"PATH=%~dp0custom\\application;%~dp0custom\\startup;%~dp0;%PATH%\"");
            sb.AppendLine("set \"UGII_USER_DIR=%~dp0custom\"");
            sb.AppendLine($"set \"UGII_CUSTOM_DIRECTORY_FILE={customDirsFile}\"");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(config.Deployment.NXExecutable))
            {
                sb.AppendLine($"start \"\" \"{config.Deployment.NXExecutable}\" %*");
            }
            else
            {
                sb.AppendLine("if defined UGII_ROOT_DIR (");
                sb.AppendLine("    start \"\" \"%UGII_ROOT_DIR%\\ugraf.exe\" %*");
                sb.AppendLine(") else (");
                sb.AppendLine("    start \"\" ugraf.exe %*");
                sb.AppendLine(")");
            }
            return sb.ToString();
        }

        private static string BuildGuiLauncherCmd(string managedRoot = "")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("REM Launcher for opening Hotkey Studio GUI from Siemens NX menu");
            if (!string.IsNullOrWhiteSpace(managedRoot))
            {
                string targetExe = Path.Combine(managedRoot, "NX2512_HotkeyStudio.exe");
                sb.AppendLine($"set \"NXKEYS_EXE={targetExe}\"");
            }
            else
            {
                sb.AppendLine("set \"NXKEYS_EXE=%~dp0NX2512_HotkeyStudio.exe\"");
            }
            sb.AppendLine("if not exist \"%NXKEYS_EXE%\" set \"NXKEYS_EXE=%~dp0NX2512_HotkeyStudio.exe\"");
            sb.AppendLine("if not exist \"%NXKEYS_EXE%\" set \"NXKEYS_EXE=%~dp0..\\NX2512_HotkeyStudio.exe\"");
            sb.AppendLine("if not exist \"%NXKEYS_EXE%\" set \"NXKEYS_EXE=%~dp0..\\..\\NX2512_HotkeyStudio.exe\"");
            sb.AppendLine("if not exist \"%NXKEYS_EXE%\" set \"NXKEYS_EXE=%~dp0..\\..\\dist\\NX2512_HotkeyStudio.exe\"");
            sb.AppendLine("if exist \"%NXKEYS_EXE%\" start \"\" \"%NXKEYS_EXE%\" --gui");
            return sb.ToString();
        }

        private static string BuildDaemonLauncherCmd(string managedRoot = "")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("REM Ensure Hotkey Studio Leader Key Engine is running in background");
            if (!string.IsNullOrWhiteSpace(managedRoot))
            {
                string targetExe = Path.Combine(managedRoot, "NX2512_HotkeyStudio.exe");
                sb.AppendLine($"set \"NXKEYS_EXE={targetExe}\"");
            }
            else
            {
                sb.AppendLine("set \"NXKEYS_EXE=%~dp0NX2512_HotkeyStudio.exe\"");
            }
            sb.AppendLine("if not exist \"%NXKEYS_EXE%\" set \"NXKEYS_EXE=%~dp0NX2512_HotkeyStudio.exe\"");
            sb.AppendLine("if not exist \"%NXKEYS_EXE%\" set \"NXKEYS_EXE=%~dp0..\\NX2512_HotkeyStudio.exe\"");
            sb.AppendLine("if not exist \"%NXKEYS_EXE%\" set \"NXKEYS_EXE=%~dp0..\\..\\NX2512_HotkeyStudio.exe\"");
            sb.AppendLine("if not exist \"%NXKEYS_EXE%\" set \"NXKEYS_EXE=%~dp0..\\..\\dist\\NX2512_HotkeyStudio.exe\"");
            sb.AppendLine("if exist \"%NXKEYS_EXE%\" start \"\" \"%NXKEYS_EXE%\" --ensure-background");
            return sb.ToString();
        }

        private static string BuildRibbonTabFile(string managedRoot = "")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("!");
            sb.AppendLine("! NXKeys Studio ribbon tab file");
            sb.AppendLine("!");
            sb.AppendLine("TITLE NXKeys Studio");
            sb.AppendLine($"VERSION {DefaultToolbarScriptVersion}");
            sb.AppendLine();
            sb.AppendLine("BEGIN_GROUP RBN_NXKEYS_GROUP");
            sb.AppendLine("LABEL NXKeys Studio");
            sb.AppendLine("BITMAP preferences");
            sb.AppendLine();
            sb.AppendLine("    BUTTON UG_NXKEYS_START_DAEMON");
            sb.AppendLine("    RIBBON_STYLE LARGE_IMAGE_AND_TEXT");
            sb.AppendLine();
            sb.AppendLine("    BUTTON UG_NXKEYS_OPEN_STUDIO");
            sb.AppendLine("    RIBBON_STYLE LARGE_IMAGE_AND_TEXT");
            sb.AppendLine();
            sb.AppendLine("END_GROUP");
            return sb.ToString();
        }

        private static string BuildCommandBridgeMenuFile(int version)
        {
            int menVersion = MenuScriptDefaults.NormalizeVersion(version);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("! NXKeys Direct Command Bridge application menu");
            sb.AppendLine("! This file must be loaded from custom\\application.");
            sb.AppendLine($"VERSION {menVersion}");
            sb.AppendLine();
            sb.AppendLine("EDIT UG_GATEWAY_MAIN_MENUBAR");
            sb.AppendLine();
            sb.AppendLine("MENU UG_NXKEYS_CASCADE");
            sb.AppendLine("    SEPARATOR");
            sb.AppendLine("    BUTTON UG_NXKEYS_BRIDGE_STATUS");
            sb.AppendLine("    LABEL NXKeys Bridge Status");
            sb.AppendLine("    ACTIONS NXKEYS_COMMAND_BRIDGE_STATUS");
            sb.AppendLine("END_OF_MENU");
            return sb.ToString();
        }

        private static string BuildToolbarFile(string managedRoot = "")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("!");
            sb.AppendLine("! NXKeys Studio toolbar file");
            sb.AppendLine("!");
            sb.AppendLine("TITLE NXKeys Studio");
            sb.AppendLine($"VERSION {DefaultToolbarScriptVersion}");
            sb.AppendLine("DOCK TOP");
            sb.AppendLine();
            sb.AppendLine("BUTTON UG_NXKEYS_START_DAEMON");
            sb.AppendLine("STYLE IMAGE_AND_TEXT");
            sb.AppendLine();
            sb.AppendLine("BUTTON UG_NXKEYS_OPEN_STUDIO");
            sb.AppendLine("STYLE IMAGE_AND_TEXT");
            return sb.ToString();
        }

        private static void PatchExistingCustomDirs(string customDirsFilePath, string customDirPath)
        {
            string[] lines = File.ReadAllLines(customDirsFilePath);
            bool alreadyExists = false;

            foreach (string line in lines)
            {
                if (string.Equals(line.Trim(), customDirPath.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                List<string> newLines = new List<string>(lines);
                newLines.Add(customDirPath);
                File.WriteAllLines(customDirsFilePath, newLines, Encoding.UTF8);
            }
        }

        private static void DeployToSiemensUserProfiles(string overlayContent, List<string> targetFiles, string customDirPath, string managedRoot = "", bool patchCustomDirs = false, int menuScriptVersion = DefaultMenuScriptVersion)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            string[] basePaths = {
                Path.Combine(localAppData, "Siemens"),
                Path.Combine(appData, "Siemens")
            };

            foreach (string bp in basePaths)
            {
                if (!Directory.Exists(bp)) continue;

                foreach (string subDir in Directory.GetDirectories(bp))
                {
                    try
                    {
                        // 1. Direct startup folder overlay & ribbon tab deployment
                        string startupDir = Path.Combine(subDir, "startup");
                        Directory.CreateDirectory(startupDir);
                        string applicationDir = Path.Combine(subDir, "application");
                        Directory.CreateDirectory(applicationDir);

                        string targetOverlay = Path.Combine(startupDir, "nxkeys_generated.men");
                        MenuScriptWriter.WriteAllText(targetOverlay, overlayContent);

                        File.WriteAllText(Path.Combine(startupDir, "launch-hotkeystudio-gui.cmd"), BuildGuiLauncherCmd(managedRoot), Encoding.Default);
                        File.WriteAllText(Path.Combine(startupDir, "launch-hotkeystudio-daemon.cmd"), BuildDaemonLauncherCmd(managedRoot), Encoding.Default);
                        MenuScriptWriter.WriteAllText(Path.Combine(startupDir, "nxkeys_ribbon.rtb"), BuildRibbonTabFile(managedRoot));
                        MenuScriptWriter.WriteAllText(Path.Combine(startupDir, "nxkeys_toolbar.tbr"), BuildToolbarFile(managedRoot));
                        MenuScriptWriter.WriteAllText(Path.Combine(applicationDir, "nxkeys_command_bridge.men"), BuildCommandBridgeMenuFile(menuScriptVersion));

                        AddTarget(targetFiles, targetOverlay);

                        // 2. Deploy .rtb files to application/profiles/All, UG_APP_MODELING, UG_APP_GATEWAY, Reference for NX 2512 Ribbon Bar
                        string[] profileSubDirs = { "All", "UG_APP_MODELING", "UG_APP_GATEWAY", "Reference" };
                        foreach (string psd in profileSubDirs)
                        {
                            string profDir = Path.Combine(subDir, "application", "profiles", psd);
                            Directory.CreateDirectory(profDir);
                            MenuScriptWriter.WriteAllText(Path.Combine(profDir, "rbn_nxkeys.rtb"), BuildRibbonTabFile(managedRoot));
                        }

                        // 3. Direct custom_dirs.dat patch
                        string customDirsDat = Path.Combine(subDir, "custom_dirs.dat");
                        if (patchCustomDirs && File.Exists(customDirsDat))
                        {
                            PatchExistingCustomDirs(customDirsDat, customDirPath);
                            AddTarget(targetFiles, customDirsDat);
                        }
                    }
                    catch
                    {
                        // Ignore permission or locked directory issues
                    }
                }
            }

            // Check UGII_USER_DIR and UGII_SITE_DIR environment variables
            foreach (string envKey in new[] { "UGII_USER_DIR", "UGII_SITE_DIR" })
            {
                string envVal = Environment.GetEnvironmentVariable(envKey);
                if (!string.IsNullOrWhiteSpace(envVal) && Directory.Exists(envVal))
                {
                    try
                    {
                        string startupDir = Path.Combine(envVal, "startup");
                        Directory.CreateDirectory(startupDir);
                        string targetOverlay = Path.Combine(startupDir, "nxkeys_generated.men");
                        MenuScriptWriter.WriteAllText(targetOverlay, overlayContent);
                        AddTarget(targetFiles, targetOverlay);
                    }
                    catch { }
                }
            }
        }

        private static string BuildResolutionReport(List<ResolutionResult> resolutions, Dictionary<string, List<ConflictItem>> conflicts)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# NXKeys Resolution Report");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("## Keybinding Resolutions");
            sb.AppendLine();
            sb.AppendLine("| Shortcut | Name | Scope | Command ID | Status | Reason |");
            sb.AppendLine("|---|---|---|---|---|---|");

            foreach (ResolutionResult res in resolutions)
            {
                string statusIcon = res.Status == ResolutionStatus.Resolved ? "OK" : res.Status.ToString();
                sb.AppendLine($"| `{res.Binding.Shortcut}` | {res.Binding.Command?.Name} | {res.Binding.Scope} | `{res.CommandID}` | {statusIcon} | {res.Reason} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Accelerator Conflicts");
            sb.AppendLine();

            if (conflicts == null || conflicts.Count == 0)
            {
                sb.AppendLine("No shortcut conflicts detected.");
            }
            else
            {
                foreach (var kvp in conflicts)
                {
                    sb.AppendLine($"### Shortcut: `{kvp.Key}`");
                    foreach (ConflictItem c in kvp.Value)
                    {
                        sb.AppendLine($"- `{c.Command.ID}` ({c.Command.DisplayLabel})");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string BuildRadialPlanMarkdown(Config config)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Modular Radial Menu Configuration Plan");
            sb.AppendLine();
            sb.AppendLine("Active NX module selects the visible command set. Direction semantics, selection filters and confirmation rules remain consistent across modules.");
            sb.AppendLine();
            sb.AppendLine("## Common slot semantics");
            sb.AppendLine();
            sb.AppendLine("| Slot | Meaning |");
            sb.AppendLine("|---|---|");
            foreach (var kvp in ModuleDefaults.SlotSemantics)
            {
                sb.AppendLine($"| `{kvp.Key}` | {kvp.Value} |");
            }
            sb.AppendLine();

            if (config?.Modules != null && config.Modules.Count > 0)
            {
                foreach (ModuleConfig module in config.Modules)
                {
                    if (module == null || !module.Enabled) continue;
                    sb.AppendLine($"## {module.Label} (`{module.ID}`)");
                    sb.AppendLine();
                    sb.AppendLine($"Leader prefix: `{module.LeaderPrefix}`");
                    if (module.NXApplicationIDs != null && module.NXApplicationIDs.Count > 0)
                    {
                        sb.AppendLine($"NX applications: `{string.Join("`, `", module.NXApplicationIDs)}`");
                    }
                    sb.AppendLine();

                    if (module.CommandSets != null)
                    {
                        foreach (ModuleCommandSet set in module.CommandSets)
                        {
                            if (set?.Commands == null || set.Commands.Count == 0) continue;
                            sb.AppendLine($"### {set.Label}");
                            sb.AppendLine();
                            sb.AppendLine("| Slot | Command Name | Command ID | Selection | Confirmation |");
                            sb.AppendLine("|---|---|---|---|---|");
                            foreach (ModuleCommand command in set.Commands.OrderBy(c => DirectionOrder(c.Slot)))
                            {
                                string selection = command.RequiresSelection ? "required" : "";
                                string confirmation = command.ConfirmBeforeExecute || command.Destructive ? "confirm" : "";
                                sb.AppendLine($"| `{command.Slot}` | {command.Command?.Name} | `{command.Command?.ID}` | {selection} | {confirmation} |");
                            }
                            sb.AppendLine();
                        }
                    }
                }
            }

            if (config?.Radials != null && config.Radials.Count > 0)
            {
                sb.AppendLine("## Legacy radials");
                sb.AppendLine();
                foreach (RadialMenu rm in config.Radials)
                {
                    if (!rm.Enabled) continue;
                    sb.AppendLine($"### {rm.Name} (Trigger: `{rm.Trigger}`)");
                    sb.AppendLine();
                    sb.AppendLine("| Direction | Command Name | Command ID |");
                    sb.AppendLine("|---|---|---|");

                    if (rm.Items != null)
                    {
                        foreach (RadialItem item in rm.Items.OrderBy(i => DirectionOrder(i.Direction)))
                        {
                            sb.AppendLine($"| `{item.Direction}` | {item.Command?.Name} | `{item.Command?.ID}` |");
                        }
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static int DirectionOrder(string direction)
        {
            switch ((direction ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "N": return 0;
                case "NE": return 1;
                case "E": return 2;
                case "SE": return 3;
                case "S": return 4;
                case "SW": return 5;
                case "W": return 6;
                case "NW": return 7;
                default: return 99;
            }
        }

        private static string BuildRadialPlanJson(Config config)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(new
            {
                modules = config?.Modules ?? new List<ModuleConfig>(),
                legacy_radials = config?.Radials ?? new List<RadialMenu>()
            }, options);
        }
    }
}
