using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.Services;
using NX2512_HotkeyStudio.UI;

namespace NX2512_HotkeyStudio
{
    public static class Program
    {
        private const string SingleInstanceMutexName = @"Global\NXKeys_HotkeyStudio_SingleInstance";
        private const string ShowUiEventName = @"Global\NXKeys_HotkeyStudio_ShowUI";
        private const string ToggleEventName = @"Global\NXKeys_HotkeyStudio_ToggleEngine";
        private const string StartEventName = @"Global\NXKeys_HotkeyStudio_StartEngine";

        private static Mutex singleMutex;
        private static EventWaitHandle showUiEvent;
        private static EventWaitHandle toggleEvent;
        private static EventWaitHandle startEvent;
        private static LeaderKeyEngine globalEngine;
        private static HotkeyStudioForm mainForm;
        private static NotifyIcon trayIcon;
        private static string activeConfigPath;
        private static Control uiInvoker;

        [STAThread]
        public static void Main(string[] args)
        {
            int cliIndex = FindCliCommandIndex(args);
            if (cliIndex >= 0)
            {
                RunCli(NormalizeCliArgs(args, cliIndex));
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            uiInvoker = new Control();
            uiInvoker.CreateControl();

            bool toggle = HasFlag(args, "--toggle");
            bool background = toggle || HasFlag(args, "--background") || HasFlag(args, "--tray") ||
                              HasFlag(args, "--daemon") || HasFlag(args, "--ensure-background") || HasFlag(args, "--start");
            bool gui = !background || HasFlag(args, "--gui");

            bool createdNew;
            singleMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
            if (!createdNew)
            {
                SignalExisting(gui ? ShowUiEventName : toggle ? ToggleEventName : StartEventName);
                return;
            }

            showUiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowUiEventName);
            toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ToggleEventName);
            startEvent = new EventWaitHandle(false, EventResetMode.AutoReset, StartEventName);

            activeConfigPath = ResolveConfigPath(GetArgValue(args, "--config"));
            Config config = LoadSafeConfig(activeConfigPath);
            globalEngine = new LeaderKeyEngine(config.LeaderKey);
            globalEngine.StatusChanged += AppendRuntimeLog;
            globalEngine.SequenceExecuted += (sequence, item) => AppendRuntimeLog($"Leader queued: {sequence} -> {item?.Command?.ID}");

            if (config.LeaderKey.Enabled)
            {
                try { globalEngine.Start(); }
                catch (Exception ex) { AppendRuntimeLog("Leader start failed: " + ex); }
            }

            SetupTrayIcon();
            StartSignalThread(showUiEvent, OpenGuiForm);
            StartSignalThread(toggleEvent, ToggleLeaderEngine);
            StartSignalThread(startEvent, EnsureLeaderEngineRunning);

            if (background) Application.Run(new TrayApplicationContext());
            else
            {
                mainForm = new HotkeyStudioForm(activeConfigPath, globalEngine);
                Application.Run(mainForm);
            }

            Cleanup();
        }

        private static void RunCli(string[] args)
        {
            Environment.ExitCode = 0;
            try
            {
                string command = args[0].ToLowerInvariant();
                string configPath = ResolveConfigPath(GetArgValue(args, "--config"));
                Config config = Config.Load(configPath);

                switch (command)
                {
                    case "validate":
                        Console.WriteLine($"[OK] Профиль '{config.Profile.Name}' корректен. Leader-команд: {config.LeaderKey?.Sequences?.Count ?? 0}");
                        break;

                    case "scan":
                        PrintScan(NxScanner.Scan(config, GetArgValue(args, "--catalog")), HasFlag(args, "--json"));
                        break;

                    case "catalog":
                        SearchCatalog(config, GetArgValue(args, "--query"), GetArgValue(args, "--catalog"));
                        break;

                    case "plan":
                        PrintPlan(DeploymentEngine.BuildPlan(config, NxScanner.Scan(config, GetArgValue(args, "--catalog")).Catalog));
                        break;

                    case "apply":
                        if (HasFlag(args, "--yes") || HasFlag(args, "-y")) config.Deployment.DryRun = false;
                        if (HasFlag(args, "--dry-run")) config.Deployment.DryRun = true;
                        if (HasFlag(args, "--allow-running-nx")) config.Deployment.RequireNXStopped = false;
                        ScanResult scan = NxScanner.Scan(config, GetArgValue(args, "--catalog"));
                        DeploymentPlan plan = DeploymentEngine.BuildPlan(config, scan.Catalog);
                        if (!DeploymentEngine.ApplyPlan(config, plan, out string backup, out string applyError))
                            throw new InvalidOperationException(applyError);
                        Console.WriteLine(config.Deployment.DryRun
                            ? "[DRY-RUN] План проверен, файлы не изменялись."
                            : "[OK] Пакет установлен. Резервная копия: " + backup);
                        break;

                    case "launch":
                        string[] nxArgs = ArgumentsAfterSeparator(args);
                        int processId = NxRuntimeService.Launch(config, configPath, nxArgs, out string launchError);
                        if (processId < 0) throw new InvalidOperationException(launchError);
                        Console.WriteLine("Siemens NX запущен. PID=" + processId);
                        break;

                    case "leader":
                        RunLeader(config);
                        break;

                    case "backups":
                        foreach (BackupManifest item in BackupEngine.ListBackups(config.Deployment.BackupRoot))
                            Console.WriteLine($"{item.Timestamp} | {item.ProfileName} | {item.Entries.Count} файлов");
                        break;

                    case "restore":
                        bool force = HasFlag(args, "--force");
                        string manifest = GetArgValue(args, "--manifest");
                        RestoreResult restore = string.IsNullOrWhiteSpace(manifest)
                            ? BackupEngine.RestoreLatest(config.Deployment.BackupRoot, force)
                            : BackupEngine.RestoreFromManifest(manifest, force);
                        if (!restore.Success) throw new InvalidOperationException(restore.ErrorMessage);
                        Console.WriteLine("[OK] Восстановление завершено: " + restore.ManifestPath);
                        break;

                    case "bridge-status":
                        PrintBridgeStatus();
                        break;

                    case "health":
                        PrintHealth(NxKeysHealthService.Check(config));
                        break;

                    default:
                        throw new ArgumentException("Неизвестная команда: " + command);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ERROR] " + ex.Message);
                Environment.ExitCode = 1;
            }
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
            Console.WriteLine("API-каталог: " + (string.IsNullOrWhiteSpace(result.DocumentationCatalogDirectory) ? "не найден" : result.DocumentationCatalogDirectory));
            foreach (string warning in result.Warnings) Console.WriteLine("Предупреждение: " + warning);
        }

        private static void SearchCatalog(Config config, string query, string catalogDirectory)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Для catalog требуется --query.");
            ScanResult scan = NxScanner.Scan(config, catalogDirectory);
            var matches = scan.Catalog.Commands.Values
                .Select(item => new { Item = item, Score = CommandResolver.ScoreCommand(new List<string> { query }, item) })
                .Where(x => x.Score > 0.35)
                .OrderByDescending(x => x.Score)
                .Take(30);
            foreach (var match in matches)
                Console.WriteLine($"{match.Score:F2} | {match.Item.ID} | {match.Item.DisplayLabel} | {match.Item.ApiCandidates.FirstOrDefault()?.ApiTarget}");
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
                Console.WriteLine("Leader запущен. Для выхода нажмите Ctrl+C.");
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
            Console.WriteLine("Context: " + (File.Exists(NxCommandBridgeClient.ContextPath) ? NxCommandBridgeClient.ContextPath : "нет"));
            foreach (string directory in new[] { "pending", "completed", "failed" })
            {
                string path = Path.Combine(root, directory);
                Console.WriteLine(directory + ": " + (Directory.Exists(path) ? Directory.GetFiles(path, "*.json").Length : 0));
            }
        }

        private static void PrintHealth(NxKeysHealthReport report)
        {
            Console.WriteLine("Managed root: " + report.ManagedRoot);
            Console.WriteLine("NX запущен: " + (report.NxRunning ? "да" : "нет"));
            foreach (string process in report.NxProcesses) Console.WriteLine("  " + process);
            Console.WriteLine("MenuScript versions: " + (report.MenuScriptVersionOk ? "OK" : "ERROR"));
            Console.WriteLine("Bridge loaded: " + (report.BridgeLoaded ? "да" : "нет"));
            Console.WriteLine("Managed package: " + (report.ManagedPackageOk ? "OK" : "ERROR"));
            foreach (string missing in report.MissingManagedFiles) Console.WriteLine("Отсутствует: " + missing);
            foreach (string mismatch in report.HashMismatches) Console.WriteLine("SHA mismatch: " + mismatch);
        }

        private static Config LoadSafeConfig(string path)
        {
            try { return Config.Load(path); }
            catch
            {
                var config = new Config();
                config.ApplyDefaults();
                return config;
            }
        }

        private static string ResolveConfigPath(string requested)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(requested)) candidates.Add(requested);
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nx2512-pro-hybrid.json"));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "nx2512-pro-hybrid.json"));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "nx2512-ergo-80.json"));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "config", "nx2512-pro-hybrid.json"));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "internal", "defaults", "nx2512-pro-hybrid.json"));
            foreach (string candidate in candidates)
            {
                string expanded = Config.ExpandPath(candidate);
                if (File.Exists(expanded)) return Path.GetFullPath(expanded);
            }
            throw new FileNotFoundException("Профиль NXKeys не найден. Передайте --config с существующим JSON-файлом.");
        }

        private static void SetupTrayIcon()
        {
            trayIcon = new NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "NXKeys Leader", Visible = true };
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
                while (true)
                {
                    handle.WaitOne();
                    try
                    {
                        if (uiInvoker != null && uiInvoker.IsHandleCreated) uiInvoker.BeginInvoke(action);
                    }
                    catch { }
                }
            }) { IsBackground = true };
            thread.Start();
        }

        private static void SignalExisting(string eventName)
        {
            try { using (var handle = EventWaitHandle.OpenExisting(eventName)) handle.Set(); }
            catch { }
        }

        private static void OpenGuiForm()
        {
            if (mainForm == null || mainForm.IsDisposed) mainForm = new HotkeyStudioForm(activeConfigPath, globalEngine);
            mainForm.Show();
            mainForm.WindowState = FormWindowState.Normal;
            mainForm.BringToFront();
            mainForm.Activate();
        }

        private static void ToggleLeaderEngine()
        {
            if (globalEngine == null) return;
            if (globalEngine.IsRunning) globalEngine.Stop(); else globalEngine.Start();
        }

        private static void EnsureLeaderEngineRunning()
        {
            if (globalEngine != null && !globalEngine.IsRunning) globalEngine.Start();
        }

        private static void Cleanup()
        {
            try { globalEngine?.Stop(); globalEngine?.Dispose(); } catch { }
            try { if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); } } catch { }
            try { uiInvoker?.Dispose(); } catch { }
            try { singleMutex?.ReleaseMutex(); singleMutex?.Dispose(); } catch { }
            try { showUiEvent?.Dispose(); toggleEvent?.Dispose(); startEvent?.Dispose(); } catch { }
        }

        private static bool IsCliCommand(string value)
        {
            string command = (value ?? string.Empty).Trim().ToLowerInvariant();
            return new[] { "validate", "scan", "catalog", "plan", "apply", "launch", "backups", "restore", "bridge-status", "health", "leader" }.Contains(command);
        }

        private static int FindCliCommandIndex(string[] args)
        {
            if (args == null) return -1;
            for (int index = 0; index < args.Length; index++)
            {
                if (args[index] == "--config" || args[index] == "--catalog") { index++; continue; }
                if (IsCliCommand(args[index])) return index;
            }
            return -1;
        }

        private static string[] NormalizeCliArgs(string[] args, int commandIndex)
        {
            var result = new List<string> { args[commandIndex] };
            for (int index = 0; index < args.Length; index++) if (index != commandIndex) result.Add(args[index]);
            return result.ToArray();
        }

        private static string GetArgValue(string[] args, string name)
        {
            if (args == null) return null;
            for (int index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length) return args[index + 1];
                if (args[index].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) return args[index].Substring(name.Length + 1);
            }
            return null;
        }

        private static bool HasFlag(string[] args, string flag) => args != null && args.Any(x => string.Equals(x, flag, StringComparison.OrdinalIgnoreCase));

        private static string[] ArgumentsAfterSeparator(string[] args)
        {
            int separator = Array.IndexOf(args, "--");
            return separator < 0 ? Array.Empty<string>() : args.Skip(separator + 1).ToArray();
        }

        private static void AppendRuntimeLog(string message)
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "logs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "leader-key.log"), $"{DateTime.Now:O} [{Process.GetCurrentProcess().Id}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        private sealed class TrayApplicationContext : ApplicationContext { }
    }
}
