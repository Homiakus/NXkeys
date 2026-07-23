using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        public List<string> ActionSummary { get; set; } = new List<string>();
        public bool IsValid { get; set; } = true;
    }

    public static class DeploymentEngine
    {
        private const int MenuVersion = MenuScriptDefaults.Version;
        private const int ToolbarVersion = MenuScriptDefaults.ToolbarVersion;
        private const string PackageVersion = "0.3.0";

        private sealed class DeploymentFile
        {
            public string Destination { get; set; } = string.Empty;
            public byte[] Content { get; set; } = Array.Empty<byte>();
            public bool Required { get; set; } = true;
        }

        public static DeploymentPlan BuildPlan(Config config, CatalogIndex catalog)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.ApplyDefaults();
            config.Validate();
            catalog ??= new CatalogIndex();
            var resolver = new CommandResolver(catalog);
            var plan = new DeploymentPlan { Resolutions = resolver.ResolveBindings(config.Keyboard) };
            foreach (ResolutionResult resolution in plan.Resolutions)
            {
                if (resolution.Status != ResolutionStatus.Resolved || resolution.Binding == null || !resolution.Binding.Enabled) continue;
                List<ConflictItem> conflicts = resolver.FindConflicts(resolution.Binding.Shortcut, resolution.CommandID);
                if (conflicts.Count > 0) plan.Conflicts[resolution.Binding.Shortcut] = conflicts;
            }

            string startup = Path.Combine(config.Deployment.ManagedRoot, "custom", "startup");
            plan.OverlayContent = OverlayGenerator.GenerateOverlay(
                config.Deployment.MenuScriptVersion,
                config.Deployment.MainMenubarID,
                plan.Resolutions,
                plan.Conflicts,
                config.Deployment.ClearDetectedConflicts,
                Path.Combine(startup, "launch-hotkeystudio-daemon.cmd"),
                Path.Combine(startup, "launch-hotkeystudio-gui.cmd"));
            plan.ResolutionReport = BuildResolutionReport(config, plan.Resolutions, plan.Conflicts);

            int enabled = config.Keyboard.Count(binding => binding != null && binding.Enabled);
            int resolved = plan.Resolutions.Count(item => item.Status == ResolutionStatus.Resolved && item.Binding?.Enabled == true);
            int moduleCommands = config.Modules.Where(module => module != null && module.Enabled)
                .Sum(module => module.CommandSets?.Where(set => set?.Commands != null).Sum(set => set.Commands.Count) ?? 0);
            plan.IsValid = enabled == BasicShortcutPolicy.Required.Count && resolved == enabled && moduleCommands > 0;
            plan.ActionSummary.Add("Движок: C# transactional deployment");
            plan.ActionSummary.Add("Режим: " + config.Deployment.Mode);
            plan.ActionSummary.Add("Managed root: " + config.Deployment.ManagedRoot);
            plan.ActionSummary.Add($"Базовые сочетания: {resolved} / {enabled}");
            plan.ActionSummary.Add("Адаптивные модули: " + config.Modules.Count(module => module != null && module.Enabled));
            plan.ActionSummary.Add("Производные модульные команды: " + moduleCommands);
            plan.ActionSummary.Add("Конфликты базовых сочетаний: " + plan.Conflicts.Count);
            return plan;
        }

        public static bool ApplyPlan(Config config, DeploymentPlan plan, out string backupFolder, out string error)
        {
            backupFolder = string.Empty;
            error = string.Empty;
            BackupManifest backup = null;
            string stagingRoot = string.Empty;
            if (config == null || plan == null) { error = "Профиль или план развёртывания не задан."; return false; }
            if (!plan.IsValid) { error = "План не прошёл проверку базовых сочетаний и модульной карты."; return false; }
            if (config.Deployment.DryRun) { error = "Dry-run включён: файлы не изменялись."; return true; }
            if (config.Deployment.RequireNXStopped && NxRuntimeService.IsRunning())
            { error = "Siemens NX запущен. Закройте NX перед обновлением managed-пакета."; return false; }

            try
            {
                config.ApplyDefaults();
                config.Validate();
                string managedRoot = Path.GetFullPath(config.Deployment.ManagedRoot);
                string customRoot = Path.Combine(managedRoot, "custom");
                string startup = Path.Combine(customRoot, "startup");
                string application = Path.Combine(customRoot, "application");
                string manifestPath = Path.Combine(managedRoot, "package-manifest.json");
                Directory.CreateDirectory(managedRoot);

                List<DeploymentFile> files = BuildDeploymentFiles(config, plan, managedRoot, startup, application);
                ValidateRequiredPackage(files, managedRoot, application);
                PackageManifest previous = LoadPackageManifest(manifestPath);
                var nextPaths = new HashSet<string>(files.Select(file => Path.GetFullPath(file.Destination)), StringComparer.OrdinalIgnoreCase);
                List<string> staleFiles = FindStaleManagedFiles(previous, managedRoot, nextPaths);

                if (string.Equals(config.Deployment.Mode, "existing-custom-dirs", StringComparison.OrdinalIgnoreCase) &&
                    config.Deployment.PatchExistingCustomDirs)
                {
                    if (string.IsNullOrWhiteSpace(config.Deployment.ExistingCustomDirsFile))
                        throw new InvalidOperationException("Для existing-custom-dirs необходимо явно задать deployment.existing_custom_dirs_file.");
                    string customDirs = Path.GetFullPath(config.Deployment.ExistingCustomDirsFile);
                    AddOrReplace(files, customDirs, TextFileCodec.AppendUniquePath(customDirs, customRoot), true);
                    nextPaths.Add(customDirs);
                }

                if (config.Role != null && config.Role.Enabled)
                {
                    if (string.IsNullOrWhiteSpace(config.Role.SourceMTX) || !File.Exists(config.Role.SourceMTX))
                        throw new FileNotFoundException("Экспортированная роль .mtx не найдена.", config.Role.SourceMTX);
                    if (string.IsNullOrWhiteSpace(config.Role.TargetDirectory))
                        throw new InvalidOperationException("Для роли необходимо явно задать role_deployment.target_directory.");
                    string roleTarget = Path.Combine(config.Role.TargetDirectory, config.Role.TargetName);
                    AddOrReplace(files, roleTarget, File.ReadAllBytes(config.Role.SourceMTX), true);
                    nextPaths.Add(Path.GetFullPath(roleTarget));
                }

                List<string> backupTargets = files.Select(file => file.Destination)
                    .Concat(staleFiles).Concat(new[] { manifestPath }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                backup = BackupEngine.CreateBackup(config.Deployment.BackupRoot, config.Profile.Name, backupTargets);
                backupFolder = backup.BackupDirectory;

                stagingRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NXKeys", "staging", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stagingRoot);
                var staged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (DeploymentFile file in files)
                {
                    string stagePath = Path.Combine(stagingRoot, HashBytes(Encoding.UTF8.GetBytes(file.Destination)) + ".bin");
                    File.WriteAllBytes(stagePath, file.Content);
                    if (!string.Equals(HashFile(stagePath), HashBytes(file.Content), StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Проверка staging-файла не пройдена: " + file.Destination);
                    staged[file.Destination] = stagePath;
                }

                foreach (DeploymentFile file in files.OrderBy(file => CommitOrder(file.Destination)))
                {
                    string destination = Path.GetFullPath(file.Destination);
                    if (File.Exists(destination) && string.Equals(HashFile(destination), HashBytes(file.Content), StringComparison.OrdinalIgnoreCase)) continue;
                    AtomicFileWriter.WriteAllBytes(destination, File.ReadAllBytes(staged[file.Destination]), config.Deployment.AtomicWrites);
                }
                foreach (string stale in staleFiles) if (File.Exists(stale)) AtomicFileWriter.DeleteWithRetry(stale);

                PackageManifest current = BuildPackageManifest(config, managedRoot,
                    files.Where(file => IsUnderRoot(file.Destination, managedRoot)));
                AtomicFileWriter.WriteAllText(manifestPath,
                    JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
                    config.Deployment.AtomicWrites);
                VerifyInstalledPackage(current);
                BackupEngine.FinalizeBackupManifest(backup);
                TryDeleteDirectory(stagingRoot);
                return true;
            }
            catch (Exception exception)
            {
                string rollback = string.Empty;
                if (backup != null)
                {
                    try
                    {
                        BackupEngine.FinalizeBackupManifest(backup);
                        RestoreResult result = BackupEngine.RestoreFromManifest(Path.Combine(backup.BackupDirectory, "manifest.json"), true);
                        rollback = result.Success ? " Автоматический откат выполнен." : " Откат завершился ошибкой: " + result.ErrorMessage;
                    }
                    catch (Exception rollbackError) { rollback = " Откат завершился исключением: " + rollbackError.Message; }
                }
                TryDeleteDirectory(stagingRoot);
                error = exception + rollback;
                return false;
            }
        }

        private static List<DeploymentFile> BuildDeploymentFiles(Config config, DeploymentPlan plan,
            string managedRoot, string startup, string application)
        {
            var files = new List<DeploymentFile>();
            string customRoot = Path.Combine(managedRoot, "custom");
            AddOrReplace(files, Path.Combine(startup, config.Deployment.OverlayFilename),
                Encoding.UTF8.GetBytes(MenuScriptWriter.Normalize(plan.OverlayContent, MenuVersion)), true);
            AddOrReplace(files, Path.Combine(managedRoot, "custom_dirs.dat"), new UTF8Encoding(false).GetBytes(customRoot + "\r\n"), true);
            AddOrReplace(files, Path.Combine(managedRoot, "launch-nx2512-with-nxkeys.cmd"), Encoding.Default.GetBytes(BuildLauncherCmd()), true);
            AddOrReplace(files, Path.Combine(startup, "launch-hotkeystudio-gui.cmd"), Encoding.Default.GetBytes(BuildGuiLauncherCmd(managedRoot)), true);
            AddOrReplace(files, Path.Combine(startup, "launch-hotkeystudio-daemon.cmd"), Encoding.Default.GetBytes(BuildDaemonLauncherCmd(managedRoot)), true);
            AddOrReplace(files, Path.Combine(application, "nxkeys_command_bridge.men"),
                Encoding.UTF8.GetBytes(MenuScriptWriter.Normalize(BuildCommandBridgeMenuFile(config.Deployment.MenuScriptVersion), MenuVersion)), true);
            AddOrReplace(files, Path.Combine(startup, "nxkeys_ribbon.rtb"),
                Encoding.UTF8.GetBytes(MenuScriptWriter.Normalize(BuildRibbonTabFile(), ToolbarVersion)), true);
            AddOrReplace(files, Path.Combine(startup, "nxkeys_toolbar.tbr"),
                Encoding.UTF8.GetBytes(MenuScriptWriter.Normalize(BuildToolbarFile(), ToolbarVersion)), true);
            AddOrReplace(files, Path.Combine(managedRoot, "resolution-report.md"), Encoding.UTF8.GetBytes(plan.ResolutionReport), false);

            string profileJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            AddOrReplace(files, Path.Combine(managedRoot, "nx2512-pro-hybrid.json"), Encoding.UTF8.GetBytes(profileJson + Environment.NewLine), true);
            string behaviorSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nx2512-state-machines.json");
            if (File.Exists(behaviorSource))
                AddOrReplace(files, Path.Combine(managedRoot, "nx2512-state-machines.json"), File.ReadAllBytes(behaviorSource), true);

            CollectArtifacts(files, AppDomain.CurrentDomain.BaseDirectory, managedRoot, false);
            string bridgeSource = Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bridge"))
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bridge")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom", "application");
            CollectArtifacts(files, bridgeSource, application, true);
            CollectArtifacts(files, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "control-center"), Path.Combine(managedRoot, "control-center"), false);
            CollectArtifacts(files, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "catalog-studio"), Path.Combine(managedRoot, "catalog-studio"), false);
            return files;
        }

        private static void CollectArtifacts(List<DeploymentFile> files, string sourceDirectory,
            string destinationDirectory, bool bridgeOnly)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory)) return;
            foreach (string source in Directory.GetFiles(Path.GetFullPath(sourceDirectory), "*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(source);
                if (!IsRuntimeArtifact(name) || string.Equals(name, "package-manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
                bool isBridge = name.StartsWith("NX2512_CommandBridge", StringComparison.OrdinalIgnoreCase);
                if (bridgeOnly != isBridge) continue;
                AddOrReplace(files, Path.Combine(destinationDirectory, name), File.ReadAllBytes(source),
                    name.Equals("NX2512_HotkeyStudio.exe", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("NX2512_CommandBridge.dll", StringComparison.OrdinalIgnoreCase));
            }
        }

        private static bool IsRuntimeArtifact(string name)
        {
            string extension = Path.GetExtension(name).ToLowerInvariant();
            return extension == ".exe" || extension == ".dll" || extension == ".json" ||
                   extension == ".config" || name.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateRequiredPackage(List<DeploymentFile> files, string managedRoot, string application)
        {
            string studio = Path.GetFullPath(Path.Combine(managedRoot, "NX2512_HotkeyStudio.exe"));
            string bridge = Path.GetFullPath(Path.Combine(application, "NX2512_CommandBridge.dll"));
            if (!files.Any(file => Path.GetFullPath(file.Destination).Equals(studio, StringComparison.OrdinalIgnoreCase)))
                throw new FileNotFoundException("Staging не содержит NX2512_HotkeyStudio.exe.");
            if (!files.Any(file => Path.GetFullPath(file.Destination).Equals(bridge, StringComparison.OrdinalIgnoreCase)))
                throw new FileNotFoundException("Staging не содержит NX2512_CommandBridge.dll.");
            if (files.Any(file => file.Destination.IndexOf(Path.Combine("custom", "startup", "NX2512_CommandBridge.dll"), StringComparison.OrdinalIgnoreCase) >= 0))
                throw new InvalidOperationException("CommandBridge нельзя устанавливать в custom/startup.");
        }

        private static PackageManifest LoadPackageManifest(string path)
        {
            try { return File.Exists(path) ? JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(path)) : null; }
            catch { return null; }
        }

        private static List<string> FindStaleManagedFiles(PackageManifest previous, string managedRoot, HashSet<string> nextPaths)
        {
            if (previous?.Files == null) return new List<string>();
            var result = new List<string>();
            foreach (PackageFileEntry entry in previous.Files)
            {
                string path = Path.GetFullPath(Path.Combine(managedRoot, entry.RelativePath ?? string.Empty));
                if (!IsUnderRoot(path, managedRoot) || nextPaths.Contains(path)) continue;
                result.Add(path);
            }
            return result;
        }

        private static PackageManifest BuildPackageManifest(Config config, string managedRoot, IEnumerable<DeploymentFile> files)
        {
            var manifest = new PackageManifest
            {
                SchemaVersion = 1,
                PackageVersion = PackageVersion,
                TargetNX = config.Profile.NXVersion,
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                ManagedRoot = managedRoot
            };
            foreach (DeploymentFile file in files.OrderBy(file => file.Destination, StringComparer.OrdinalIgnoreCase))
            {
                string full = Path.GetFullPath(file.Destination);
                manifest.Files.Add(new PackageFileEntry
                {
                    RelativePath = Path.GetRelativePath(managedRoot, full),
                    Sha256 = HashBytes(file.Content),
                    Size = file.Content.LongLength,
                    Required = file.Required
                });
            }
            return manifest;
        }

        private static void VerifyInstalledPackage(PackageManifest manifest)
        {
            foreach (PackageFileEntry entry in manifest.Files)
            {
                string path = Path.Combine(manifest.ManagedRoot, entry.RelativePath);
                if (!File.Exists(path))
                {
                    if (entry.Required) throw new FileNotFoundException("После установки отсутствует обязательный файл.", path);
                    continue;
                }
                if (!string.Equals(HashFile(path), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("SHA-256 установленного файла не совпадает: " + path);
            }
        }

        private static string BuildResolutionReport(Config config, IEnumerable<ResolutionResult> resolutions,
            Dictionary<string, List<ConflictItem>> conflicts)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# NXKeys resolution report").AppendLine();
            builder.AppendLine("- Profile: " + config.Profile.Name);
            builder.AppendLine("- Basic shortcuts: " + config.Keyboard.Count(binding => binding.Enabled));
            builder.AppendLine("- Adaptive modules: " + config.Modules.Count(module => module.Enabled)).AppendLine();
            builder.AppendLine("| Shortcut | Command | BUTTON ID | Status | Conflicts |");
            builder.AppendLine("|---|---|---|---|---|");
            foreach (ResolutionResult result in resolutions)
            {
                int count = result.Binding != null && conflicts.TryGetValue(result.Binding.Shortcut, out List<ConflictItem> list) ? list.Count : 0;
                builder.AppendLine($"| {result.Binding?.Shortcut} | {result.Binding?.Command?.Name} | `{result.CommandID}` | {result.Status} | {count} |");
            }
            return builder.ToString();
        }

        private static string BuildLauncherCmd() =>
            "@echo off\r\nsetlocal\r\n\"%~dp0NX2512_HotkeyStudio.exe\" launch --config \"%~dp0nx2512-pro-hybrid.json\" -- %*\r\nexit /b %ERRORLEVEL%\r\n";
        private static string BuildGuiLauncherCmd(string root) =>
            "@echo off\r\nstart \"\" \"" + Path.Combine(root, "NX2512_HotkeyStudio.exe") + "\" --gui --config \"" + Path.Combine(root, "nx2512-pro-hybrid.json") + "\"\r\n";
        private static string BuildDaemonLauncherCmd(string root) =>
            "@echo off\r\nstart \"\" \"" + Path.Combine(root, "NX2512_HotkeyStudio.exe") + "\" --ensure-background --config \"" + Path.Combine(root, "nx2512-pro-hybrid.json") + "\"\r\n";
        private static string BuildCommandBridgeMenuFile(int version) =>
            "! NXKeys Command Bridge\r\nVERSION " + MenuScriptDefaults.NormalizeVersion(version) + "\r\nAPPLICATION_BUTTON NXKEYS_COMMAND_BRIDGE\r\nLABEL NXKeys Command Bridge\r\nLIBRARIES NX2512_CommandBridge\r\n";
        private static string BuildRibbonTabFile() =>
            "! NXKeys launch ribbon\r\nVERSION " + ToolbarVersion + "\r\nRIBBON_STYLE\r\nRIBBON_TAB NXKEYS_TAB\r\nLABEL NXKeys\r\nBUTTON UG_NXKEYS_START_DAEMON\r\nBUTTON UG_NXKEYS_OPEN_STUDIO\r\nEND_OF_RIBBON_TAB\r\n";
        private static string BuildToolbarFile() =>
            "! NXKeys launch toolbar\r\nVERSION " + ToolbarVersion + "\r\nTOOLBAR NXKEYS_TOOLBAR\r\nLABEL NXKeys\r\nBUTTON UG_NXKEYS_START_DAEMON\r\nBUTTON UG_NXKEYS_OPEN_STUDIO\r\nEND_OF_TOOLBAR\r\n";

        private static void AddOrReplace(List<DeploymentFile> files, string destination, byte[] content, bool required)
        {
            string full = Path.GetFullPath(destination);
            files.RemoveAll(file => Path.GetFullPath(file.Destination).Equals(full, StringComparison.OrdinalIgnoreCase));
            files.Add(new DeploymentFile { Destination = full, Content = content ?? Array.Empty<byte>(), Required = required });
        }

        private static int CommitOrder(string path)
        {
            string name = Path.GetFileName(path);
            if (name.Equals("package-manifest.json", StringComparison.OrdinalIgnoreCase)) return 100;
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return 10;
            return 20;
        }

        private static bool IsUnderRoot(string path, string root)
        {
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string HashFile(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        private static string HashBytes(byte[] content)
        {
            using (SHA256 sha = SHA256.Create())
                return Convert.ToHexString(sha.ComputeHash(content ?? Array.Empty<byte>())).ToLowerInvariant();
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) Directory.Delete(path, true); }
            catch { }
        }
    }
}
