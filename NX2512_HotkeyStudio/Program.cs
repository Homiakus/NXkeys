using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.Services;
using NX2512_HotkeyStudio.UI;

namespace NX2512_HotkeyStudio
{
    public static class Program
    {
        private static string singleInstanceMutexName = string.Empty;
        private static string showUiEventName = string.Empty;
        private static string toggleEventName = string.Empty;
        private static string startEventName = string.Empty;

        private static Mutex singleMutex;
        private static EventWaitHandle showUiEvent;
        private static EventWaitHandle toggleEvent;
        private static EventWaitHandle startEvent;
        private static LeaderKeyEngine globalEngine;
        private static HotkeyStudioForm mainForm;
        private static NotifyIcon trayIcon;
        private static Control uiInvoker;
        private static string activeConfigPath = string.Empty;
        private static readonly CancellationTokenSource signalCancellation = new CancellationTokenSource();
        private static readonly List<Thread> signalThreads = new List<Thread>();

        [STAThread]
        public static void Main(string[] args)
        {
            int commandIndex = FindCliCommandIndex(args);
            if (commandIndex >= 0)
            {
                RunCli(NormalizeCliArgs(args, commandIndex));
                return;
            }

            try
            {
                RunDesktop(args ?? Array.Empty<string>());
            }
            catch (Exception exception)
            {
                AppendRuntimeLog("Fatal startup error: " + exception);
                MessageBox.Show(exception.Message, "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
            finally
            {
                Cleanup();
            }
        }

        private static void RunDesktop(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool toggle = HasFlag(args, "--toggle");
            bool background = toggle || HasFlag(args, "--background") || HasFlag(args, "--tray") ||
                              HasFlag(args, "--daemon") || HasFlag(args, "--ensure-background") || HasFlag(args, "--start");
            bool openGui = !background || HasFlag(args, "--gui");

            activeConfigPath = ResolveConfigPath(GetArgValue(args, "--config"));
            ConfigureInstanceScope(activeConfigPath);
            singleMutex = new Mutex(true, singleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                SignalExisting(openGui ? showUiEventName : toggle ? toggleEventName : startEventName);
                return;
            }

            Config config = Config.Load(activeConfigPath);
            if (!string.IsNullOrWhiteSpace(activeConfigPath))
                NxCommandBridgeClient.ConfigureSecurity(activeConfigPath);
            if (config.SchemaVersion != Config.CurrentSchemaVersion || !config.LeaderKey.AdaptiveModuleMode)
                throw new InvalidOperationException(
                    $"NXKeys требует канонический адаптивный профиль schema v{Config.CurrentSchemaVersion}.");

            uiInvoker = new Control();
            uiInvoker.CreateControl();
            showUiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, showUiEventName);
            toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, toggleEventName);
            startEvent = new EventWaitHandle(false, EventResetMode.AutoReset, startEventName);

            globalEngine = new LeaderKeyEngine(config.LeaderKey);
            globalEngine.StatusChanged += AppendRuntimeLog;
            globalEngine.SequenceExecuted += (sequence, item) =>
                AppendRuntimeLog($"Adaptive command queued: {sequence} -> {item?.Command?.ID}");

            if (config.LeaderKey.Enabled)
            {
                try { globalEngine.Start(); }
                catch (Exception exception) { AppendRuntimeLog("Leader start failed: " + exception); }
            }

            SetupTrayIcon();
            StartSignalThread(showUiEvent, OpenGuiForm);
            StartSignalThread(toggleEvent, ToggleLeaderEngine);
            StartSignalThread(startEvent, EnsureLeaderEngineRunning);

            if (background)
            {
                if (openGui) OpenGuiForm();
                Application.Run(new TrayApplicationContext());
            }
            else
            {
                mainForm = new HotkeyStudioForm(activeConfigPath, globalEngine);
                Application.Run(mainForm);
            }
        }

        private static void RunCli(string[] args)
        {
            Environment.ExitCode = 0;
            try
            {
                string command = args[0].ToLowerInvariant();
                string configPath = ResolveConfigPath(GetArgValue(args, "--config"));
                Config config = Config.Load(configPath);
                if (!string.IsNullOrWhiteSpace(configPath))
                    NxCommandBridgeClient.ConfigureSecurity(configPath);

                switch (command)
                {
                    case "validate":
                    case "verify":
                    case "--verify":
                        Console.WriteLine($"[OK] '{config.Profile.Name}': {config.Keyboard.Count(x => x.Enabled)} базовых, " +
                                          $"{config.Modules.Count(x => x.Enabled)} модулей, {config.LeaderKey.Sequences.Count} команд.");
                        break;
                    case "scan":
                        PrintScan(NxScanner.Scan(config, GetArgValue(args, "--catalog")), HasFlag(args, "--json"));
                        break;
                    case "catalog":
                        SearchCatalog(config, GetArgValue(args, "--query"), GetArgValue(args, "--catalog"));
                        break;
                    case "plan":
                        PrintPlan(DeploymentEngine.BuildPlan(config,
                            NxScanner.Scan(config, GetArgValue(args, "--catalog")).Catalog));
                        break;
                    case "apply":
                    case "--apply":
                        Apply(config, args);
                        break;
                    case "repair":
                    case "--repair":
                        {
                            Config restored = Config.LoadEmbedded();
                            if (string.IsNullOrWhiteSpace(configPath))
                                configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "nx2512-v8-profile.json");
                            restored.Save(configPath);
                            ScanResult repairScan = NxScanner.Scan(restored, GetArgValue(args, "--catalog"));
                            DeploymentPlan repairPlan = DeploymentEngine.BuildPlan(restored, repairScan.Catalog);
                            if (!DeploymentEngine.ApplyPlan(restored, repairPlan, out string repBackup, out string repError))
                                throw new InvalidOperationException(repError);
                            Console.WriteLine("[OK] Канонический профиль восстановлен и развернут в NX. Резервная копия: " + repBackup);
                        }
                        break;
                    case "launch":
                        LaunchNx(config, configPath, args);
                        break;
                    case "leader":
                        RunLeader(config);
                        break;
                    case "backups":
                        foreach (BackupManifest item in BackupEngine.ListBackups(config.Deployment.BackupRoot))
                            Console.WriteLine($"{item.Timestamp} | {item.ProfileName} | {item.Entries.Count} файлов");
                        break;
                    case "restore":
                        Restore(config, args);
                        break;
                    case "bridge-status":
                        PrintBridgeStatus();
                        break;
                    case "health":
                        PrintHealth(NxKeysHealthService.Check(config));
                        break;
                    case "export-icons":
                    case "icons":
                        int iconCount = OperationThumbnailRenderer.ExportAllIcons(config, 128);
                        Console.WriteLine($"[OK] Сгенерировано {iconCount} миниатюр операций и manifest.json.");
                        break;
                    case "doc-map":
                    case "generate-docs":
                        {
                            string outPath = GetArgValue(args, "--out");
                            if (string.IsNullOrWhiteSpace(outPath))
                            {
                                string candidate = AppDomain.CurrentDomain.BaseDirectory;
                                while (!string.IsNullOrWhiteSpace(candidate))
                                {
                                    if (Directory.Exists(Path.Combine(candidate, ".git")) ||
                                        File.Exists(Path.Combine(candidate, "config", "nx2512-v8-profile.json")))
                                    {
                                        outPath = Path.Combine(candidate, "FULL_COMMAND_MAP.md");
                                        break;
                                    }
                                    DirectoryInfo parent = Directory.GetParent(candidate);
                                    if (parent == null || parent.FullName == candidate) break;
                                    candidate = parent.FullName;
                                }
                                if (string.IsNullOrWhiteSpace(outPath))
                                    outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FULL_COMMAND_MAP.md");
                            }
                            outPath = Path.GetFullPath(outPath);
                            var generator = new DocumentationGenerator(config);
                            generator.GenerateMarkdownMap(outPath);
                            Console.WriteLine($"[OK] Runtime-driven documentation map written to: {outPath}");
                        }
                        break;
                    case "help":
                    case "--help":
                    case "-h":
                        PrintHelp();
                        break;
                    default:
                        throw new ArgumentException("Неизвестная команда: " + command);
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("[ERROR] " + exception.Message);
                Environment.ExitCode = 1;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine(@"NXKeys — Адаптивные модульные команды для Siemens NX 2512
Использование:
  NX2512_HotkeyStudio.exe [команда] [опции]

Команды:
  (без аргументов)          Запустить Control Center (GUI)
  --daemon / --minimized    Запустить в фоновом режиме в системном трее
  --apply / apply           Применить профиль и развернуть ribbon/overlay в NX
  --verify / validate       Проверить валидность профиля и инвариантов
  --repair / repair         Восстановить канонический профиль v8 и развернуть в NX
  health / bridge-status    Проверить состояние среды NX, Bridge и очереди
  plan                      Сформировать план развертывания без применения
  doc-map                   Сгенерировать карту команд FULL_COMMAND_MAP.md
  --help / help             Показать эту справку

Опции:
  --config <path>           Путь к файлу профиля (по умолчанию nx2512-v8-profile.json)
  --dry-run                 Тестовый запуск без изменения файлов на диске
  --yes / -y                Автоматическое подтверждение применения");
        }

        private static void Apply(Config config, string[] args)
        {
            if (HasFlag(args, "--yes") || HasFlag(args, "-y")) config.Deployment.DryRun = false;
            if (HasFlag(args, "--dry-run")) config.Deployment.DryRun = true;
            if (HasFlag(args, "--allow-running-nx")) config.Deployment.RequireNXStopped = false;
            ScanResult scan = NxScanner.Scan(config, GetArgValue(args, "--catalog"));
            DeploymentPlan plan = DeploymentEngine.BuildPlan(config, scan.Catalog);
            if (!DeploymentEngine.ApplyPlan(config, plan, out string backup, out string error))
                throw new InvalidOperationException(error);
            Console.WriteLine(config.Deployment.DryRun
                ? "[DRY-RUN] План проверен, файлы не изменялись."
                : "[OK] Пакет установлен. Резервная копия: " + backup);
        }

        private static void LaunchNx(Config config, string configPath, string[] args)
        {
            int processId = NxRuntimeService.Launch(config, configPath, ArgumentsAfterSeparator(args), out string error);
            if (processId < 0) throw new InvalidOperationException(error);
            Console.WriteLine("Siemens NX запущен. PID=" + processId);
        }

        private static void Restore(Config config, string[] args)
        {
            bool force = HasFlag(args, "--force");
            string manifest = GetArgValue(args, "--manifest");
            RestoreResult result = string.IsNullOrWhiteSpace(manifest)
                ? BackupEngine.RestoreLatest(config.Deployment.BackupRoot, force)
                : BackupEngine.RestoreFromManifest(manifest, force);
            if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
            Console.WriteLine("[OK] Восстановление завершено: " + result.ManifestPath);
        }

        private static void PrintScan(ScanResult result, bool json)
        {
            if (json)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    roots = result.DiscoveredRoots,
                    menu_files = result.MenuFiles.Count,
                    role_files = result.RoleFiles.Count,
                    launcher_files = result.LauncherFiles.Count,
                    commands = result.Catalog.Commands.Count,
                    api_catalog = result.DocumentationCatalogDirectory,
                    warnings = result.Warnings
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                return;
            }
            Console.WriteLine("Корней: " + result.DiscoveredRoots.Count);
            Console.WriteLine("MenuScript-файлов: " + result.MenuFiles.Count);
            Console.WriteLine("Команд: " + result.Catalog.Commands.Count);
            Console.WriteLine("API-каталог: " +
                              (string.IsNullOrWhiteSpace(result.DocumentationCatalogDirectory)
                                  ? "не найден"
                                  : result.DocumentationCatalogDirectory));
            foreach (string warning in result.Warnings) Console.WriteLine("Предупреждение: " + warning);
        }

        private static void SearchCatalog(Config config, string query, string catalogDirectory)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Для catalog требуется --query.");
            ScanResult scan = NxScanner.Scan(config, catalogDirectory);
            var matches = scan.Catalog.Commands.Values
                .Select(item => new { Item = item, Score = CommandResolver.ScoreCommand(new List<string> { query }, item) })
                .Where(value => value.Score > 0.35)
                .OrderByDescending(value => value.Score)
                .Take(30);
            foreach (var match in matches)
                Console.WriteLine($"{match.Score:F2} | {match.Item.ID} | {match.Item.DisplayLabel} | " +
                                  match.Item.ApiCandidates.FirstOrDefault()?.ApiTarget);
        }

        private static void PrintPlan(DeploymentPlan plan)
        {
            foreach (string line in plan.ActionSummary) Console.WriteLine(line);
            Console.WriteLine();
            Console.WriteLine(plan.ResolutionReport);
        }

        private static void RunLeader(Config config)
        {
            using (var engine = new LeaderKeyEngine(config.LeaderKey))
            {
                engine.StatusChanged += value => Console.WriteLine("[LEADER] " + value);
                engine.Start();
                Console.WriteLine("Адаптивный Leader запущен. Для выхода нажмите Ctrl+C.");
                var stop = new ManualResetEvent(false);
                Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; stop.Set(); };
                stop.WaitOne();
                engine.Stop();
            }
        }

        private static void PrintBridgeStatus()
        {
            string root = NxCommandBridgeClient.BridgeRoot;
            Console.WriteLine("Bridge root: " + root);
            Console.WriteLine("Context: " +
                              (File.Exists(NxCommandBridgeClient.ContextPath)
                                  ? NxCommandBridgeClient.ContextPath
                                  : "нет"));
            NxBridgeContext context = NxCommandBridgeClient.ReadContext();
            if (context != null)
            {
                string age = DateTimeOffset.TryParse(context.UpdatedUtc, out DateTimeOffset updated)
                    ? Math.Max(0, (DateTimeOffset.UtcNow - updated.ToUniversalTime()).TotalSeconds).ToString("0.0") + "s"
                    : "unknown";
                Console.WriteLine("Context age: " + age);
                Console.WriteLine("Context module: " + context.ModuleId + " / " + context.ModuleLabel);
                Console.WriteLine("Context status: " + context.Status);
            }
            foreach (string directory in new[] { "pending", "processing", "completed", "failed" })
            {
                string path = Path.Combine(root, directory);
                Console.WriteLine(directory + ": " +
                                  (Directory.Exists(path) ? Directory.GetFiles(path, "*.json").Length : 0));
            }
        }

        private static void PrintHealth(NxKeysHealthReport report)
        {
            Console.WriteLine("Managed root: " + report.ManagedRoot);
            Console.WriteLine("Expected custom dirs: " + report.ExpectedCustomDirsFile);
            foreach (var pair in report.EnvironmentCustomDirsFiles)
                Console.WriteLine("UGII_CUSTOM_DIRECTORY_FILE[" + pair.Key + "]: " + pair.Value);
            foreach (string warning in report.EnvironmentWarnings)
                Console.WriteLine("Environment warning: " + warning);
            Console.WriteLine("NX запущен: " + (report.NxRunning ? "да" : "нет"));
            foreach (string process in report.NxProcesses) Console.WriteLine("  " + process);
            Console.WriteLine("MenuScript versions: " + (report.MenuScriptVersionOk ? "OK" : "ERROR"));
            Console.WriteLine("Bridge loaded: " + (report.BridgeLoaded ? "да" : "нет"));
            Console.WriteLine("Bridge status: " + (File.Exists(report.BridgeStatusPath) ? report.BridgeStatusPath : "нет"));
            Console.WriteLine("Bridge context: " + (File.Exists(report.BridgeContextPath) ? report.BridgeContextPath : "нет"));
            Console.WriteLine("Context age: " + (report.BridgeContextAgeSeconds >= 0 ? report.BridgeContextAgeSeconds.ToString("0.0") + "s" : "нет"));
            Console.WriteLine("Bridge log: " + (File.Exists(report.BridgeLogPath) ? report.BridgeLogPath : "нет"));
            foreach (string line in report.LastBridgeLogLines) Console.WriteLine("  log: " + line);
            Console.WriteLine("Bridge queue: pending=" + report.PendingCount + ", completed=" + report.CompletedCount + ", failed=" + report.FailedCount);
            foreach (string failure in report.LastFailures) Console.WriteLine("  failed: " + failure);
            Console.WriteLine("Managed package: " + (report.ManagedPackageOk ? "OK" : "ERROR"));
            foreach (string missing in report.MissingManagedFiles) Console.WriteLine("Отсутствует: " + missing);
            foreach (string mismatch in report.HashMismatches) Console.WriteLine("SHA mismatch: " + mismatch);
        }

        private static string ResolveConfigPath(string requested)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(requested)) candidates.Add(requested);
            foreach (string name in new[] { "nx2512-v8-profile.json", "nx2512-pro-hybrid.json" })
            {
                candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name));
                candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", name));
                candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "config", name));
            }

            foreach (string candidate in candidates)
            {
                string expanded = Config.ExpandPath(candidate);
                if (File.Exists(expanded)) return Path.GetFullPath(expanded);
            }
            // No profile file found — Config.Load will build the hardcoded v8 profile.
            return string.Empty;
        }

        private static void ConfigureInstanceScope(string configPath)
        {
            string normalized = string.IsNullOrWhiteSpace(configPath)
                ? "HARDCODED_V8_PROFILE"
                : Path.GetFullPath(configPath).ToUpperInvariant();
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
                .Substring(0, 16);
            string scope = "Local\\NXKeys_" + digest;
            singleInstanceMutexName = scope + "_HotkeyStudio";
            showUiEventName = scope + "_ShowUI";
            toggleEventName = scope + "_ToggleEngine";
            startEventName = scope + "_StartEngine";
        }

        private static void SetupTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "NXKeys Adaptive Leader",
                Visible = true
            };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Открыть NXKeys Studio", null, (_, _) => OpenGuiForm());
            menu.Items.Add("Переключить Leader", null, (_, _) => ToggleLeaderEngine());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Выход", null, (_, _) => Application.Exit());
            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += (_, _) => OpenGuiForm();
        }

        private static void StartSignalThread(EventWaitHandle handle, Action action)
        {
            var thread = new Thread(() =>
            {
                WaitHandle[] handles = { handle, signalCancellation.Token.WaitHandle };
                while (!signalCancellation.IsCancellationRequested)
                {
                    int signaled = WaitHandle.WaitAny(handles);
                    if (signaled == 1 || signalCancellation.IsCancellationRequested) return;
                    try
                    {
                        if (uiInvoker != null && uiInvoker.IsHandleCreated) uiInvoker.BeginInvoke(action);
                    }
                    catch (ObjectDisposedException) { return; }
                    catch (InvalidOperationException) { return; }
                }
            }) { IsBackground = true, Name = "NXKeys instance signal" };
            lock (signalThreads) signalThreads.Add(thread);
            thread.Start();
        }

        private static void SignalExisting(string eventName)
        {
            try { using (var handle = EventWaitHandle.OpenExisting(eventName)) handle.Set(); }
            catch { }
        }

        private static void OpenGuiForm()
        {
            if (mainForm == null || mainForm.IsDisposed)
                mainForm = new HotkeyStudioForm(activeConfigPath, globalEngine);
            mainForm.Show();
            mainForm.WindowState = FormWindowState.Normal;
            mainForm.BringToFront();
            mainForm.Activate();
        }

        private static void ToggleLeaderEngine()
        {
            if (globalEngine == null) return;
            if (globalEngine.IsRunning) globalEngine.Stop();
            else globalEngine.Start();
        }

        private static void EnsureLeaderEngineRunning()
        {
            if (globalEngine != null && !globalEngine.IsRunning) globalEngine.Start();
        }

        private static void Cleanup()
        {
            try { signalCancellation.Cancel(); } catch { }
            lock (signalThreads)
            {
                foreach (Thread thread in signalThreads)
                    try { if (thread.IsAlive) thread.Join(500); } catch { }
                signalThreads.Clear();
            }
            try { globalEngine?.Stop(); globalEngine?.Dispose(); } catch { }
            try { if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); } } catch { }
            try { uiInvoker?.Dispose(); } catch { }
            try { singleMutex?.ReleaseMutex(); singleMutex?.Dispose(); } catch { }
            try { showUiEvent?.Dispose(); toggleEvent?.Dispose(); startEvent?.Dispose(); } catch { }
        }

        private static bool IsCliCommand(string value)
        {
            string command = (value ?? string.Empty).Trim().ToLowerInvariant();
            return new[]
            {
                "validate", "verify", "--verify", "scan", "catalog", "plan", "apply", "--apply",
                "repair", "--repair", "launch", "leader", "backups", "restore", "bridge-status",
                "health", "export-icons", "icons", "doc-map", "generate-docs", "help", "--help", "-h"
            }.Contains(command);
        }

        private static int FindCliCommandIndex(string[] args)
        {
            if (args == null) return -1;
            for (int index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "--config", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[index], "--catalog", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[index], "--manifest", StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    continue;
                }
                if (IsCliCommand(args[index])) return index;
            }
            return -1;
        }

        private static string[] NormalizeCliArgs(string[] args, int commandIndex)
        {
            var result = new List<string> { args[commandIndex] };
            for (int index = 0; index < args.Length; index++)
                if (index != commandIndex) result.Add(args[index]);
            return result.ToArray();
        }

        private static string GetArgValue(string[] args, string name)
        {
            if (args == null) return null;
            for (int index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                    return args[index + 1];
                if (args[index].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return args[index].Substring(name.Length + 1);
            }
            return null;
        }

        private static bool HasFlag(string[] args, string flag) =>
            args != null && args.Any(value => string.Equals(value, flag, StringComparison.OrdinalIgnoreCase));

        private static string[] ArgumentsAfterSeparator(string[] args)
        {
            int separator = Array.IndexOf(args, "--");
            return separator < 0 ? Array.Empty<string>() : args.Skip(separator + 1).ToArray();
        }

        private static void AppendRuntimeLog(string message)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "logs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "leader-key.log"),
                    $"{DateTime.Now:O} [{Process.GetCurrentProcess().Id}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        private sealed class TrayApplicationContext : ApplicationContext { }
    }
}
