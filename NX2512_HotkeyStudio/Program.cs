using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        private static SynchronizationContext mainSyncContext;

        [STAThread]
        public static void Main(string[] args)
        {
            int cliCommandIndex = FindCliCommandIndex(args);
            if (cliCommandIndex >= 0)
            {
                RunCli(NormalizeCliArgs(args, cliCommandIndex));
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            uiInvoker = new Control();
            uiInvoker.CreateControl();
            mainSyncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            bool isToggleRequest = HasFlag(args, "--toggle");
            bool isStartRequest = HasFlag(args, "--background") || HasFlag(args, "--tray") || HasFlag(args, "--daemon") || HasFlag(args, "--ensure-background") || HasFlag(args, "--start");
            bool isBackground = isStartRequest || isToggleRequest;
            bool isGui = args == null || args.Length == 0 || HasFlag(args, "--gui") || !isBackground;

            bool createdNew;
            singleMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance of HotkeyStudio is already running in background!
                if (isGui)
                {
                    try
                    {
                        using (var ev = EventWaitHandle.OpenExisting(ShowUiEventName))
                        {
                            ev.Set(); // Signal running background instance to show GUI
                        }
                    }
                    catch { }
                }
                else if (isToggleRequest)
                {
                    try
                    {
                        using (var ev = EventWaitHandle.OpenExisting(ToggleEventName))
                        {
                            ev.Set(); // Signal running background instance to toggle Leader Key
                        }
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        using (var ev = EventWaitHandle.OpenExisting(StartEventName))
                        {
                            ev.Set(); // Signal running background instance to ensure Leader Key is enabled
                        }
                    }
                    catch { }
                }
                return; // Exit secondary process quietly
            }

            // Primary instance setup
            showUiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowUiEventName);
            toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ToggleEventName);
            startEvent = new EventWaitHandle(false, EventResetMode.AutoReset, StartEventName);

            activeConfigPath = GetArgValue(args, "--config");
            if (string.IsNullOrWhiteSpace(activeConfigPath) || !File.Exists(activeConfigPath))
            {
                activeConfigPath = FindDefaultConfig();
            }

            Config config;
            try { config = Config.Load(activeConfigPath); }
            catch { config = new Config(); config.ApplyDefaults(); }

            // Initialize background Leader Key engine
            if (config.LeaderKey == null) config.LeaderKey = new LeaderKeyConfig();
            config.LeaderKey.ApplyDefaults();

            globalEngine = new LeaderKeyEngine(config.LeaderKey);
            globalEngine.StatusChanged += status => AppendRuntimeLog(status);
            globalEngine.SequenceExecuted += (seq, item) => AppendRuntimeLog($"Queued direct Leader command -> {seq} | {item?.Command?.ID} | {item?.Command?.Name}");
            if (config.LeaderKey.Enabled)
            {
                try
                {
                    globalEngine.Start();
                    AppendRuntimeLog($"Leader engine started. Config={activeConfigPath}; Trigger={config.LeaderKey.TriggerKey}; Sequences={config.LeaderKey.Sequences.Count}");
                }
                catch (Exception ex)
                {
                    AppendRuntimeLog("Leader engine failed to start: " + ex);
                    ShowNotification("Leader Key", "Не удалось запустить keyboard hook. См. logs\\leader-key.log");
                }
            }

            SetupTrayIcon();

            // Background listener thread for ShowUI signals from NX Menu button or CLI
            Thread listenerThread = new Thread(() =>
            {
                while (true)
                {
                    if (showUiEvent.WaitOne())
                    {
                        try
                        {
                            if (uiInvoker != null && uiInvoker.IsHandleCreated)
                            {
                                uiInvoker.Invoke(new Action(() => OpenGuiForm()));
                            }
                            else if (mainSyncContext != null)
                            {
                                mainSyncContext.Post(_ => OpenGuiForm(), null);
                            }
                            else
                            {
                                OpenGuiForm();
                            }
                        }
                        catch { }
                    }
                }
            })
            { IsBackground = true };
            listenerThread.Start();

            // Background listener thread for Toggle signals from NX Ribbon button
            Thread toggleThread = new Thread(() =>
            {
                while (true)
                {
                    if (toggleEvent.WaitOne())
                    {
                        try
                        {
                            if (uiInvoker != null && uiInvoker.IsHandleCreated)
                            {
                                uiInvoker.Invoke(new Action(() => ToggleLeaderEngine()));
                            }
                            else if (mainSyncContext != null)
                            {
                                mainSyncContext.Post(_ => ToggleLeaderEngine(), null);
                            }
                            else
                            {
                                ToggleLeaderEngine();
                            }
                        }
                        catch { }
                    }
                }
            })
            { IsBackground = true };
            toggleThread.Start();

            // Background listener thread for idempotent Start/Ensure-on signals from NX Ribbon button
            Thread startThread = new Thread(() =>
            {
                while (true)
                {
                    if (startEvent.WaitOne())
                    {
                        try
                        {
                            if (uiInvoker != null && uiInvoker.IsHandleCreated)
                            {
                                uiInvoker.Invoke(new Action(() => EnsureLeaderEngineRunning()));
                            }
                            else if (mainSyncContext != null)
                            {
                                mainSyncContext.Post(_ => EnsureLeaderEngineRunning(), null);
                            }
                            else
                            {
                                EnsureLeaderEngineRunning();
                            }
                        }
                        catch { }
                    }
                }
            })
            { IsBackground = true };
            startThread.Start();

            if (isBackground)
            {
                // Run quietly in system tray
                Application.Run(new TrayApplicationContext());
            }
            else
            {
                // Run with GUI open
                mainForm = new HotkeyStudioForm(activeConfigPath, globalEngine);
                Application.Run(mainForm);
            }

            // Clean up resources on exit
            if (globalEngine != null) { globalEngine.Stop(); globalEngine.Dispose(); }
            if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
            if (uiInvoker != null && !uiInvoker.IsDisposed) { uiInvoker.Dispose(); }
            if (singleMutex != null) { singleMutex.ReleaseMutex(); singleMutex.Dispose(); }
            if (startEvent != null) { startEvent.Dispose(); }
        }

        private static bool IsPureCliCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return false;
            string c = cmd.Trim().ToLowerInvariant();
            return c == "validate" || c == "scan" || c == "catalog" || c == "plan" || c == "apply" || c == "backups" || c == "bridge-status" || c == "health" || c == "restore" || c == "leader";
        }

        private static int FindCliCommandIndex(string[] args)
        {
            if (args == null) return -1;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? string.Empty;
                if (string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    continue;
                }
                if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (IsPureCliCommand(arg)) return i;
            }
            return -1;
        }

        private static string[] NormalizeCliArgs(string[] args, int commandIndex)
        {
            if (args == null || commandIndex < 0 || commandIndex >= args.Length) return args ?? Array.Empty<string>();
            List<string> normalized = new List<string> { args[commandIndex] };
            for (int i = 0; i < args.Length; i++)
            {
                if (i == commandIndex) continue;
                normalized.Add(args[i]);
            }
            return normalized.ToArray();
        }

        private static bool HasFlag(string[] args, string flag)
        {
            return args != null && args.Any(x => string.Equals(x, flag, StringComparison.OrdinalIgnoreCase));
        }

        private static string FindDefaultConfig()
        {
            string candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nx2512-pro-hybrid.json");
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "nx2512-pro-hybrid.json");
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);

            return "nx2512-pro-hybrid.json";
        }

        private static void ShowNotification(string title, string message)
        {
            if (trayIcon != null)
            {
                trayIcon.ShowBalloonTip(2500, title, message, ToolTipIcon.Info);
            }
        }

        private static void AppendRuntimeLog(string message)
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NXKeys",
                    "logs");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "leader-key.log");
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Process.GetCurrentProcess().Id}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        private static void SetupTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "NXKeys Hotkey Studio (Leader Key Active)",
                Visible = true
            };

            ContextMenuStrip ctx = new ContextMenuStrip();
            ToolStripMenuItem openItem = new ToolStripMenuItem("Открыть NXKeys Studio UI", null, (s, e) => OpenGuiForm());
            ToolStripMenuItem toggleItem = new ToolStripMenuItem("Переключить Leader Key (CapsLock)", null, (s, e) =>
            {
                if (globalEngine != null)
                {
                    if (globalEngine.IsRunning) globalEngine.Stop();
                    else globalEngine.Start();
                }
            });
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Выход", null, (s, e) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            });

            ctx.Items.Add(openItem);
            ctx.Items.Add(toggleItem);
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = ctx;
            trayIcon.DoubleClick += (s, e) => OpenGuiForm();
        }

        public static void OpenGuiForm()
        {
            try
            {
                if (mainForm != null && !mainForm.IsDisposed)
                {
                    mainForm.Show();
                    mainForm.WindowState = FormWindowState.Normal;
                    mainForm.BringToFront();
                    mainForm.Activate();
                }
                else
                {
                    mainForm = new HotkeyStudioForm(activeConfigPath, globalEngine);
                    mainForm.Show();
                    mainForm.BringToFront();
                    mainForm.Activate();
                }
            }
            catch { }
        }

        private static void ToggleLeaderEngine()
        {
            if (globalEngine != null)
            {
                if (globalEngine.IsRunning)
                {
                    globalEngine.Stop();
                    ShowNotification("Leader Key (CapsLock)", "Перехват клавиш временно ПРИОСТАНОВЛЕН");
                }
                else
                {
                    globalEngine.Start();
                    ShowNotification("Leader Key (CapsLock)", "Перехват клавиш АКТИВИРОВАН");
                }
            }
        }

        private static void EnsureLeaderEngineRunning()
        {
            if (globalEngine == null) return;
            if (!globalEngine.IsRunning)
            {
                globalEngine.Start();
                ShowNotification("Leader Key (CapsLock)", "Перехват клавиш АКТИВИРОВАН");
            }
            else
            {
                ShowNotification("Leader Key (CapsLock)", "Перехват клавиш уже активен");
            }
        }

        private class TrayApplicationContext : ApplicationContext { }

        public static int MainUnloaded(string arg)
        {
            Main(new[] { "--gui" });
            return 0;
        }

        private static void RunCli(string[] args)
        {
            string command = args[0].ToLowerInvariant();
            string configPath = GetArgValue(args, "--config") ?? "nx2512-pro-hybrid.json";
            Environment.ExitCode = 0;

            try
            {
                switch (command)
                {
                    case "validate":
                        Config cfgVal = Config.Load(configPath);
                        Console.WriteLine($"[OK] Configuration '{cfgVal.Profile.Name}' is valid. Leader sequences: {cfgVal.LeaderKey?.Sequences?.Count ?? 0}");
                        break;

                    case "leader":
                        Config cfgL = Config.Load(configPath);
                        if (cfgL.LeaderKey == null) cfgL.LeaderKey = new LeaderKeyConfig();
                        cfgL.LeaderKey.ApplyDefaults();

                        using (var engine = new LeaderKeyEngine(cfgL.LeaderKey))
                        {
                            engine.StatusChanged += status => Console.WriteLine($"[LEADER ENGINE] {status}");
                            engine.SequenceExecuted += (seq, item) => Console.WriteLine($"[EXECUTED] Leader -> {seq} ({item.Command?.Name})");
                            engine.Start();

                            Console.WriteLine($"NX Leader Key Engine started. Active Trigger Key: {cfgL.LeaderKey.TriggerKey}");
                            Console.WriteLine($"Sequences registered: {cfgL.LeaderKey.Sequences.Count}");
                            Console.WriteLine("Press Ctrl+C to terminate background hook service.");

                            var resetEvent = new ManualResetEvent(false);
                            Console.CancelKeyPress += (s, e) => { e.Cancel = true; resetEvent.Set(); };
                            resetEvent.WaitOne();

                            engine.Stop();
                            Console.WriteLine("NX Leader Key Engine stopped.");
                        }
                        break;

                    case "scan":
                        Config cfgScan = Config.Load(configPath);
                        ScanResult scanRes = NxScanner.Scan(cfgScan);
                        bool jsonOut = HasFlag(args, "--json");

                        if (jsonOut)
                        {
                            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                            {
                                roots = scanRes.DiscoveredRoots,
                                menu_files_count = scanRes.MenuFiles.Count,
                                catalog_commands_count = scanRes.Catalog.Commands.Count
                            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        }
                        else
                        {
                            Console.WriteLine($"Roots discovered: {scanRes.DiscoveredRoots.Count}");
                            Console.WriteLine($"Menu files found: {scanRes.MenuFiles.Count}");
                            Console.WriteLine($"Catalog total commands: {scanRes.Catalog.Commands.Count}");
                        }
                        break;

                    case "catalog":
                        string query = GetArgValue(args, "--query");
                        if (string.IsNullOrWhiteSpace(query))
                        {
                            Console.WriteLine("Error: --query parameter is required for catalog search.");
                            Environment.ExitCode = 1;
                            return;
                        }
                        Config cfgCat = Config.Load(configPath);
                        ScanResult scanCat = NxScanner.Scan(cfgCat);
                        var resolver = new CommandResolver(scanCat.Catalog);

                        List<CandidateMatch> matches = new List<CandidateMatch>();
                        List<string> qList = new List<string> { query };

                        foreach (var kvp in scanCat.Catalog.Commands)
                        {
                            CommandItem item = kvp.Value;
                            double score = CommandResolver.ScoreCommand(qList, item);
                            if (score > 0.4)
                            {
                                matches.Add(new CandidateMatch
                                {
                                    ID = item.ID,
                                    Label = item.DisplayLabel,
                                    Score = score,
                                    ApiMatch = item.ApiCandidates.Count > 0 ? item.ApiCandidates[0].ApiTarget : string.Empty
                                });
                            }
                        }

                        matches.Sort((a, b) => b.Score.CompareTo(a.Score));
                        Console.WriteLine($"Search results for '{query}' ({matches.Count} matches):");
                        foreach (var m in matches.Take(15))
                        {
                            Console.WriteLine($" - {m.ID} | Label: '{m.Label}' | Score: {m.Score:F2} | API: {m.ApiMatch}");
                        }
                        break;

                    case "plan":
                        Config cfgPlan = Config.Load(configPath);
                        ScanResult scanPlan = NxScanner.Scan(cfgPlan);
                        DeploymentPlan plan = DeploymentEngine.BuildPlan(cfgPlan, scanPlan.Catalog);

                        Console.WriteLine("=== DEPLOYMENT PLAN ===");
                        foreach (string line in plan.ActionSummary) Console.WriteLine(line);
                        Console.WriteLine();
                        Console.WriteLine(plan.ResolutionReport);
                        break;

                    case "apply":
                        Config cfgApply = Config.Load(configPath);
                        if (HasFlag(args, "--yes") || HasFlag(args, "-y"))
                        {
                            cfgApply.Deployment.DryRun = false;
                        }
                        if (HasFlag(args, "--dry-run"))
                        {
                            cfgApply.Deployment.DryRun = true;
                        }
                        if (HasFlag(args, "--allow-running-nx"))
                        {
                            cfgApply.Deployment.RequireNXStopped = false;
                        }

                        ScanResult scanApp = NxScanner.Scan(cfgApply);
                        DeploymentPlan planApp = DeploymentEngine.BuildPlan(cfgApply, scanApp.Catalog);

                        bool ok = DeploymentEngine.ApplyPlan(cfgApply, planApp, out string backupDir, out string err);
                        if (ok)
                        {
                            if (cfgApply.Deployment.DryRun)
                            {
                                Console.WriteLine("[DRY-RUN] Plan built successfully. No files were written.");
                            }
                            else
                            {
                                Console.WriteLine($"[SUCCESS] Plan applied. Backup folder: {backupDir}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[ERROR] Failed to apply plan: {err}");
                            Environment.ExitCode = 1;
                        }
                        break;

                    case "backups":
                        Config cfgB = Config.Load(configPath);
                        var backups = BackupEngine.ListBackups(cfgB.Deployment.BackupRoot);
                        Console.WriteLine($"Discovered {backups.Count} backups in {cfgB.Deployment.BackupRoot}:");
                        foreach (var b in backups)
                        {
                            Console.WriteLine($" - {b.Timestamp} | Profile: {b.ProfileName} | Files: {b.Entries.Count}");
                        }
                        break;

                    case "bridge-status":
                        PrintBridgeStatus();
                        break;

                    case "health":
                        Config cfgHealth = Config.Load(configPath);
                        PrintHealth(NxKeysHealthService.Check(cfgHealth));
                        break;

                    case "restore":
                        Config cfgR = Config.Load(configPath);
                        bool force = HasFlag(args, "--force");
                        string manifest = GetArgValue(args, "--manifest");
                        var rest = string.IsNullOrWhiteSpace(manifest)
                            ? BackupEngine.RestoreLatest(cfgR.Deployment.BackupRoot, force)
                            : BackupEngine.RestoreFromManifest(manifest, force);

                        if (rest.Success)
                        {
                            Console.WriteLine($"[SUCCESS] Restored from {rest.ManifestPath}");
                            foreach (string rf in rest.RestoredFiles) Console.WriteLine($"  - {rf}");
                        }
                        else
                        {
                            Console.WriteLine($"[ERROR] Restore failed: {rest.ErrorMessage}");
                            Environment.ExitCode = 1;
                        }
                        break;

                    default:
                        Console.WriteLine($"Unknown command '{command}'. Available: validate, scan, catalog, plan, apply, backups, bridge-status, health, restore, leader");
                        Environment.ExitCode = 1;
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Execution error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }

        private static void PrintBridgeStatus()
        {
            string root = NxCommandBridgeClient.BridgeRoot;
            string pending = Path.Combine(root, "pending");
            string completed = Path.Combine(root, "completed");
            string failed = Path.Combine(root, "failed");
            string statusPath = Path.Combine(root, "status.json");
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NXKeys",
                "logs",
                "nx-command-bridge.log");

            Console.WriteLine("NXKeys direct command bridge");
            Console.WriteLine($"Root: {root}");
            Console.WriteLine($"Loaded in NX: {(File.Exists(statusPath) ? "yes" : "no")}");

            if (File.Exists(statusPath))
            {
                Console.WriteLine("Status:");
                Console.WriteLine(File.ReadAllText(statusPath).Trim());
            }

            Console.WriteLine($"Pending: {CountJsonFiles(pending)}");
            Console.WriteLine($"Completed: {CountJsonFiles(completed)}");
            Console.WriteLine($"Failed: {CountJsonFiles(failed)}");
            PrintRecentFiles("Recent failed", failed, "*.result.txt", 5);
            PrintRecentFiles("Recent completed", completed, "*.result.txt", 5);
            Console.WriteLine($"Log: {logPath}");
        }

        private static void PrintHealth(NxKeysHealthReport report)
        {
            Console.WriteLine("NXKeys health");
            Console.WriteLine($"Managed root: {report.ManagedRoot}");
            Console.WriteLine($"NX running: {(report.NxRunning ? "yes" : "no")}");
            foreach (string proc in report.NxProcesses) Console.WriteLine($" - {proc}");
            Console.WriteLine($"MenuScript versions OK: {(report.MenuScriptVersionOk ? "yes" : "no")}");
            Console.WriteLine($"Invalid NXKeys VERSION files: {report.StaleFiles.Count}");
            foreach (NxKeysMenuFileStatus stale in report.StaleFiles.Take(20))
            {
                Console.WriteLine($" - VERSION {stale.Version}: {stale.Path}");
            }
            Console.WriteLine("Generated NXKeys menu file versions:");
            foreach (NxKeysMenuFileStatus file in report.MenuFiles.Take(30))
            {
                Console.WriteLine($" - VERSION {file.Version}: {file.Path}");
            }
            Console.WriteLine($"Bridge loaded: {(report.BridgeLoaded ? "yes" : "no")}");
            Console.WriteLine($"Pending: {report.PendingCount}");
            Console.WriteLine($"Completed: {report.CompletedCount}");
            Console.WriteLine($"Failed: {report.FailedCount}");
            if (report.LastFailures.Count > 0)
            {
                Console.WriteLine("Recent failures:");
                foreach (string failure in report.LastFailures) Console.WriteLine($" - {failure}");
            }
            Console.WriteLine($"Managed package OK: {(report.ManagedPackageOk ? "yes" : "no")}");
            Console.WriteLine($"Bridge DLL locked: {(report.BridgeDllLocked ? "yes" : "no")}");
            foreach (string missing in report.MissingManagedFiles) Console.WriteLine($"Missing: {missing}");
            foreach (string mismatch in report.HashMismatches) Console.WriteLine($"Hash mismatch: {mismatch}");
        }

        private static int CountJsonFiles(string directory)
        {
            return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json").Length : 0;
        }

        private static void PrintRecentFiles(string title, string directory, string pattern, int count)
        {
            if (!Directory.Exists(directory)) return;
            FileInfo[] files = new DirectoryInfo(directory)
                .GetFiles(pattern)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(count)
                .ToArray();
            if (files.Length == 0) return;

            Console.WriteLine(title + ":");
            foreach (FileInfo file in files)
            {
                string firstLine = File.ReadLines(file.FullName).FirstOrDefault() ?? string.Empty;
                Console.WriteLine($" - {file.Name}: {firstLine}");
            }
        }

        private static string GetArgValue(string[] args, string flag)
        {
            if (args == null) return null;
            string prefix = flag + "=";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != null && args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i].Substring(prefix.Length).Trim('"');
                }
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length) return args[i + 1];
                    return null;
                }
            }
            return null;
        }
    }
}
