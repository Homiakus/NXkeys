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
        public string RadialPlanMarkdown { get; set; } = string.Empty;
        public string RadialPlanJson { get; set; } = string.Empty;
        public List<string> ActionSummary { get; set; } = new List<string>();
        public bool IsValid { get; set; } = true;
    }

    public static class DeploymentEngine
    {
        private const int MenuVersion = MenuScriptDefaults.Version;
        private const int ToolbarVersion = MenuScriptDefaults.ToolbarVersion;
        private const string PackageVersion = "0.2.0";

        private sealed class DeploymentFile
        {
            public string Destination { get; set; } = string.Empty;
            public byte[] Content { get; set; } = Array.Empty<byte>();
            public bool Required { get; set; } = true;
        }

        public static DeploymentPlan BuildPlan(Config config, CatalogIndex catalog)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (catalog == null) catalog = new CatalogIndex();

            var plan = new DeploymentPlan();
            var resolver = new CommandResolver(catalog);
            plan.Resolutions = resolver.ResolveBindings(config.Keyboard);

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

            plan.ResolutionReport = BuildResolutionReport(plan.Resolutions, plan.Conflicts);
            plan.RadialPlanMarkdown = BuildRadialPlanMarkdown(config);
            plan.RadialPlanJson = JsonSerializer.Serialize(new
            {
                modules = config.Modules ?? new List<ModuleConfig>(),
                legacy_radials = config.Radials ?? new List<RadialMenu>()
            }, new JsonSerializerOptions { WriteIndented = true });

            int resolved = plan.Resolutions.Count(x => x.Status == ResolutionStatus.Resolved);
            int unresolved = plan.Resolutions.Count(x => x.Status != ResolutionStatus.Resolved && x.Binding != null && x.Binding.Enabled);
            plan.IsValid = resolved > 0;
            plan.ActionSummary.Add("Движок: C# transactional deployment");
            plan.ActionSummary.Add("Режим: " + config.Deployment.Mode);
            plan.ActionSummary.Add("Managed root: " + config.Deployment.ManagedRoot);
            plan.ActionSummary.Add($"Разрешено привязок: {resolved} / {plan.Resolutions.Count}");
            plan.ActionSummary.Add("Неразрешённых или неоднозначных: " + unresolved);
            plan.ActionSummary.Add("Обнаружено конфликтов: " + plan.Conflicts.Count);
            plan.ActionSummary.Add("Модулей: " + (config.Modules?.Count ?? 0));
            return plan;
        }

        public static bool ApplyPlan(Config config, DeploymentPlan plan, out string backupFolder, out string error)
        {
            backupFolder = string.Empty;
            error = string.Empty;
            BackupManifest backup = null;
            string stagingRoot = string.Empty;

            if (config == null || plan == null)
            {
                error = "Профиль или план развёртывания не задан.";
                return false;
            }
            if (!plan.IsValid)
            {
                error = "План не содержит ни одной подтверждённой команды.";
                return false;
            }
            if (config.Deployment.DryRun)
            {
                error = "Dry-run включён: файлы не изменялись.";
                return true;
            }
            if (config.Deployment.RequireNXStopped && NxRuntimeService.IsRunning())
            {
                error = "Siemens NX запущен. Закройте NX перед обновлением managed-пакета.";
                return false;
            }

            try
            {
                string managedRoot = Path.GetFullPath(config.Deployment.ManagedRoot);
                string customRoot = Path.Combine(managedRoot, "custom");
                string startup = Path.Combine(customRoot, "startup");
                string application = Path.Combine(customRoot, "application");
                string manifestPath = Path.Combine(managedRoot, "package-manifest.json");
                Directory.CreateDirectory(managedRoot);

                List<DeploymentFile> files = BuildDeploymentFiles(config, plan, managedRoot, startup, application);
                ValidateRequiredPackage(files, managedRoot, application);

                PackageManifest previous = LoadPackageManifest(manifestPath);
                HashSet<string> nextPaths = new HashSet<string>(files.Select(x => Path.GetFullPath(x.Destination)), StringComparer.OrdinalIgnoreCase);
                List<string> staleFiles = FindStaleManagedFiles(previous, managedRoot, nextPaths);

                if (string.Equals(config.Deployment.Mode, "existing-custom-dirs", StringComparison.OrdinalIgnoreCase) &&
                    config.Deployment.PatchExistingCustomDirs)
                {
                    if (string.IsNullOrWhiteSpace(config.Deployment.ExistingCustomDirsFile))
                        throw new InvalidOperationException("Для existing-custom-dirs необходимо явно задать deployment.existing_custom_dirs_file.");
                    string customDirsPath = Path.GetFullPath(config.Deployment.ExistingCustomDirsFile);
                    byte[] patched = TextFileCodec.AppendUniquePath(customDirsPath, customRoot);
                    AddOrReplace(files, customDirsPath, patched, true);
                    nextPaths.Add(customDirsPath);
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

                List<string> backupTargets = files.Select(x => x.Destination)
                    .Concat(staleFiles)
                    .Concat(new[] { manifestPath })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                backup = BackupEngine.CreateBackup(config.Deployment.BackupRoot, config.Profile.Name, backupTargets);
                backupFolder = backup.BackupDirectory;

                stagingRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NXKeys", "staging", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stagingRoot);

                foreach (DeploymentFile file in files)
                {
                    string stagePath = Path.Combine(stagingRoot, HashBytes(Encoding.UTF8.GetBytes(file.Destination)) + ".bin");
                    File.WriteAllBytes(stagePath, file.Content);
                    if (!string.Equals(HashBytes(File.ReadAllBytes(stagePath)), HashBytes(file.Content), StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Проверка staging-файла не пройдена: " + file.Destination);
                }

                foreach (DeploymentFile file in files.OrderBy(x => CommitOrder(x.Destination)))
                {
                    string destination = Path.GetFullPath(file.Destination);
                    if (File.Exists(destination) && string.Equals(HashFile(destination), HashBytes(file.Content), StringComparison.OrdinalIgnoreCase)) continue;
                    AtomicFileWriter.WriteAllBytes(destination, file.Content, config.Deployment.AtomicWrites);
                }

                foreach (string stale in staleFiles)
                {
                    if (File.Exists(stale)) AtomicFileWriter.DeleteWithRetry(stale);
                }

                PackageManifest current = BuildPackageManifest(config, managedRoot, files.Where(x => IsUnderRoot(x.Destination, managedRoot)));
                AtomicFileWriter.WriteAllText(manifestPath,
                    JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
                    config.Deployment.AtomicWrites);

                VerifyInstalledPackage(current);
                BackupEngine.FinalizeBackupManifest(backup);
                TryDeleteDirectory(stagingRoot);
                return true;
            }
            catch (Exception ex)
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
                    catch (Exception rollbackError)
                    {
                        rollback = " Откат завершился исключением: " + rollbackError.Message;
                    }
                }
                TryDeleteDirectory(stagingRoot);
                error = ex + rollback;
                return false;
            }
        }

        private static List<DeploymentFile> BuildDeploymentFiles(Config config, DeploymentPlan plan, string managedRoot, string startup, string application)
        {
            var files = new List<DeploymentFile>();
            string customRoot = Path.Combine(managedRoot, "custom");
            string customDirsPath = Path.Combine(managedRoot, "custom_dirs.dat");

            AddOrReplace(files, Path.Combine(startup, config.Deployment.OverlayFilename),
                Encoding.UTF8.GetBytes(MenuScriptWriter.Normalize(plan.OverlayContent, MenuVersion)), true);
            AddOrReplace(files, customDirsPath, new UTF8Encoding(false).GetBytes(customRoot + "\r\n"), true);
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
            AddOrReplace(files, Path.Combine(managedRoot, "radial-menu-plan.md"), Encoding.UTF8.GetBytes(plan.RadialPlanMarkdown), false);
            AddOrReplace(files, Path.Combine(managedRoot, "radial-menu-plan.json"), Encoding.UTF8.GetBytes(plan.RadialPlanJson), false);

            string profileJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            AddOrReplace(files, Path.Combine(managedRoot, "nx2512-pro-hybrid.json"), Encoding.UTF8.GetBytes(profileJson + Environment.NewLine), true);

            CollectArtifacts(files, AppDomain.CurrentDomain.BaseDirectory, managedRoot, false);

            string bridgeSource = Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bridge"))
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bridge")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom", "application");
            CollectArtifacts(files, bridgeSource, application, true);

            CollectArtifacts(files, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "control-center"), Path.Combine(managedRoot, "control-center"), false);
            CollectArtifacts(files, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "catalog-studio"), Path.Combine(managedRoot, "catalog-studio"), false);
            return files;
        }

        private static void CollectArtifacts(List<DeploymentFile> files, string sourceDirectory, string destinationDirectory, bool bridgeOnly)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory)) return;
            string sourceFull = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar);
            string destinationFull = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar);

            foreach (string source in Directory.GetFiles(sourceFull, "*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(source);
                if (!IsRuntimeArtifact(name)) continue;
                if (!bridgeOnly && name.StartsWith("NX2512_CommandBridge", StringComparison.OrdinalIgnoreCase)) continue;
                if (bridgeOnly && !name.StartsWith("NX2512_CommandBridge", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, "package-manifest.json", StringComparison.OrdinalIgnoreCase)) continue;

                string destination = Path.Combine(destinationFull, name);
                AddOrReplace(files, destination, File.ReadAllBytes(source),
                    name.Equals("NX2512_HotkeyStudio.exe", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("NX2512_CommandBridge.dll", StringComparison.OrdinalIgnoreCase));
            }
        }

        private static bool IsRuntimeArtifact(string name)
        {
            string extension = Path.GetExtension(name).ToLowerInvariant();
            return extension == ".exe" || extension == ".dll" || extension == ".json" ||
                   extension == ".config" || extension == ".deps" || extension == ".runtimeconfig" ||
                   name.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateRequiredPackage(List<DeploymentFile> files, string managedRoot, string application)
        {
            string studio = Path.GetFullPath(Path.Combine(managedRoot, "NX2512_HotkeyStudio.exe"));
            string bridge = Path.GetFullPath(Path.Combine(application, "NX2512_CommandBridge.dll"));
            if (!files.Any(x => string.Equals(Path.GetFullPath(x.Destination), studio, StringComparison.OrdinalIgnoreCase)) && !File.Exists(studio))
                throw new FileNotFoundException("В staging-наборе отсутствует NX2512_HotkeyStudio.exe.", studio);
            if (!files.Any(x => string.Equals(Path.GetFullPath(x.Destination), bridge, StringComparison.OrdinalIgnoreCase)) && !File.Exists(bridge))
                throw new FileNotFoundException("В staging-наборе отсутствует NX2512_CommandBridge.dll.", bridge);
        }

        private static void AddOrReplace(List<DeploymentFile> files, string destination, byte[] content, bool required)
        {
            string full = Path.GetFullPath(destination);
            DeploymentFile existing = files.FirstOrDefault(x => string.Equals(Path.GetFullPath(x.Destination), full, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Content = content ?? Array.Empty<byte>();
                existing.Required = existing.Required || required;
                return;
            }
            files.Add(new DeploymentFile { Destination = full, Content = content ?? Array.Empty<byte>(), Required = required });
        }

        private static PackageManifest LoadPackageManifest(string path)
        {
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(path)) : null;
            }
            catch { return null; }
        }

        private static List<string> FindStaleManagedFiles(PackageManifest previous, string managedRoot, HashSet<string> nextPaths)
        {
            var result = new List<string>();
            if (previous?.Files == null) return result;
            foreach (PackageFileEntry entry in previous.Files)
            {
                string path = Path.GetFullPath(Path.Combine(managedRoot, entry.RelativePath ?? string.Empty));
                if (!IsUnderRoot(path, managedRoot) || nextPaths.Contains(path)) continue;
                result.Add(path);
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static PackageManifest BuildPackageManifest(Config config, string managedRoot, IEnumerable<DeploymentFile> files)
        {
            var manifest = new PackageManifest
            {
                PackageVersion = PackageVersion,
                TargetNX = config.Profile.NXVersion,
                ManagedRoot = managedRoot,
                CreatedUtc = DateTime.UtcNow.ToString("O")
            };
            foreach (DeploymentFile file in files.OrderBy(x => x.Destination, StringComparer.OrdinalIgnoreCase))
            {
                string destination = Path.GetFullPath(file.Destination);
                manifest.Files.Add(new PackageFileEntry
                {
                    RelativePath = Path.GetRelativePath(managedRoot, destination),
                    Sha256 = HashFile(destination),
                    Size = new FileInfo(destination).Length,
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
                if (!File.Exists(path)) throw new FileNotFoundException("После установки отсутствует файл пакета.", path);
                string actual = HashFile(path);
                if (!string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("SHA-256 установленного файла не совпадает с манифестом: " + path);
            }
        }

        private static bool IsUnderRoot(string path, string root)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(path);
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static int CommitOrder(string path)
        {
            string name = Path.GetFileName(path);
            if (name.Equals("NX2512_HotkeyStudio.exe", StringComparison.OrdinalIgnoreCase)) return 10;
            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return 20;
            if (name.EndsWith(".men", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".rtb", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".tbr", StringComparison.OrdinalIgnoreCase)) return 50;
            if (name.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)) return 60;
            return 40;
        }

        private static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path)) return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        private static string HashBytes(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return Convert.ToHexString(sha.ComputeHash(bytes ?? Array.Empty<byte>())).ToLowerInvariant();
        }

        private static string BuildLauncherCmd()
        {
            return "@echo off\r\n" +
                   "setlocal\r\n" +
                   "set \"NXKEYS_EXE=%~dp0NX2512_HotkeyStudio.exe\"\r\n" +
                   "if not exist \"%NXKEYS_EXE%\" ( echo NX2512_HotkeyStudio.exe not found & exit /b 2 )\r\n" +
                   "\"%NXKEYS_EXE%\" launch --config \"%~dp0nx2512-pro-hybrid.json\" -- %*\r\n" +
                   "exit /b %errorlevel%\r\n";
        }

        private static string BuildGuiLauncherCmd(string managedRoot)
        {
            string exe = Path.Combine(managedRoot, "NX2512_HotkeyStudio.exe");
            return "@echo off\r\nsetlocal\r\n" +
                   "if not exist \"" + exe + "\" exit /b 2\r\n" +
                   "start \"\" \"" + exe + "\" --gui --config \"" + Path.Combine(managedRoot, "nx2512-pro-hybrid.json") + "\"\r\n";
        }

        private static string BuildDaemonLauncherCmd(string managedRoot)
        {
            string exe = Path.Combine(managedRoot, "NX2512_HotkeyStudio.exe");
            return "@echo off\r\nsetlocal\r\n" +
                   "if not exist \"" + exe + "\" exit /b 2\r\n" +
                   "start \"\" \"" + exe + "\" --ensure-background --config \"" + Path.Combine(managedRoot, "nx2512-pro-hybrid.json") + "\"\r\n";
        }

        private static string BuildCommandBridgeMenuFile(int version)
        {
            return "! NXKeys Command Bridge\n" +
                   "VERSION " + MenuScriptDefaults.NormalizeVersion(version) + "\n\n" +
                   "EDIT UG_GATEWAY_MAIN_MENUBAR\n\n" +
                   "MENU UG_NXKEYS_CASCADE\n" +
                   "    SEPARATOR\n" +
                   "    BUTTON UG_NXKEYS_BRIDGE_STATUS\n" +
                   "    LABEL Состояние NXKeys Bridge\n" +
                   "    ACTIONS NXKEYS_COMMAND_BRIDGE_STATUS\n" +
                   "END_OF_MENU\n";
        }

        private static string BuildRibbonTabFile()
        {
            return "! NXKeys ribbon\nTITLE NXKeys Studio\nVERSION " + ToolbarVersion + "\n\n" +
                   "BEGIN_GROUP RBN_NXKEYS_GROUP\nLABEL NXKeys Studio\nBITMAP preferences\n" +
                   "    BUTTON UG_NXKEYS_START_DAEMON\n    RIBBON_STYLE LARGE_IMAGE_AND_TEXT\n" +
                   "    BUTTON UG_NXKEYS_OPEN_STUDIO\n    RIBBON_STYLE LARGE_IMAGE_AND_TEXT\nEND_GROUP\n";
        }

        private static string BuildToolbarFile()
        {
            return "! NXKeys toolbar\nTITLE NXKeys Studio\nVERSION " + ToolbarVersion + "\nDOCK TOP\n\n" +
                   "BUTTON UG_NXKEYS_START_DAEMON\nSTYLE IMAGE_AND_TEXT\n\n" +
                   "BUTTON UG_NXKEYS_OPEN_STUDIO\nSTYLE IMAGE_AND_TEXT\n";
        }

        private static string BuildResolutionReport(List<ResolutionResult> resolutions, Dictionary<string, List<ConflictItem>> conflicts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Отчёт разрешения команд NXKeys");
            sb.AppendLine();
            sb.AppendLine("Сформирован: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("| Сочетание | Команда | Область | BUTTON ID | Статус | Причина |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (ResolutionResult result in resolutions ?? new List<ResolutionResult>())
            {
                sb.AppendLine($"| `{result.Binding?.Shortcut}` | {result.Binding?.Command?.Name} | {result.Binding?.Scope} | `{result.CommandID}` | {result.Status} | {result.Reason} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Конфликты");
            if (conflicts == null || conflicts.Count == 0) sb.AppendLine("Конфликты не обнаружены.");
            else
            {
                foreach (var pair in conflicts)
                {
                    sb.AppendLine("### `" + pair.Key + "`");
                    foreach (ConflictItem item in pair.Value) sb.AppendLine("- `" + item.Command?.ID + "` — " + item.Command?.DisplayLabel);
                }
            }
            return sb.ToString();
        }

        private static string BuildRadialPlanMarkdown(Config config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# План модульных радиальных меню NXKeys");
            sb.AppendLine();
            sb.AppendLine("Радиальные меню применяются автоматически только через заранее экспортированную и проверенную роль `.mtx`.");
            sb.AppendLine();
            foreach (ModuleConfig module in config.Modules ?? new List<ModuleConfig>())
            {
                if (module == null || !module.Enabled) continue;
                sb.AppendLine("## " + module.Label + " (`" + module.ID + "`)");
                sb.AppendLine();
                sb.AppendLine("Leader-префикс: `" + module.LeaderPrefix + "`");
                foreach (ModuleCommandSet set in module.CommandSets ?? new List<ModuleCommandSet>())
                {
                    if (set?.Commands == null || set.Commands.Count == 0) continue;
                    sb.AppendLine();
                    sb.AppendLine("### " + set.Label);
                    sb.AppendLine("| Слот | Команда | BUTTON ID | Требуется выбор | Подтверждение |");
                    sb.AppendLine("|---|---|---|---|---|");
                    foreach (ModuleCommand command in set.Commands)
                    {
                        sb.AppendLine($"| `{command.Slot}` | {command.Command?.Name} | `{command.Command?.ID}` | {(command.RequiresSelection ? "да" : "нет")} | {(command.Destructive || command.ConfirmBeforeExecute ? "да" : "нет")} |");
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) Directory.Delete(path, true); }
            catch { }
        }
    }
}
