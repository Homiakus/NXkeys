using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using NXOpen;
using NXOpen.MenuBar;
using NXOpen.UF;

namespace NX2512_CommandBridge
{
    public static class Program
    {
        private const string ApplicationName = "NXKEYS_COMMAND_BRIDGE";
        private const string StatusActionName = "NXKEYS_COMMAND_BRIDGE_STATUS";

        private static Session theSession;
        private static UFSession theUfSession;
        private static UI theUI;
        private static ListingWindow listingWindow;
        private static Timer pollTimer;
        private static FileSystemWatcher pendingWatcher;
        private static bool isInitialized;
        private static bool isProcessing;
        private static bool pendingWake = true;
        private static DateTime lastFullPollUtc = DateTime.MinValue;
        private static DateTime lastContextWriteUtc = DateTime.MinValue;

        public static int Main(string[] args)
        {
            return Startup();
        }

        public static int Startup()
        {
            if (isInitialized)
            {
                WriteLog("NXKeys Command Bridge already initialized.");
                return 0;
            }

            try
            {
                theSession = Session.GetSession();
                theUfSession = UFSession.GetUFSession();
                theUI = UI.GetUI();
                listingWindow = theSession.ListingWindow;

                try
                {
                    theUI.MenuBarManager.RegisterApplication(
                        ApplicationName,
                        new MenuBarManager.InitializeMenuApplication(ApplicationInit),
                        new MenuBarManager.EnterMenuApplication(ApplicationEnter),
                        new MenuBarManager.ExitMenuApplication(ApplicationExit),
                        true,
                        true,
                        true);
                }
                catch (NXException ex)
                {
                    WriteLog("RegisterApplication warning: " + ex.Message);
                }

                try
                {
                    theUI.MenuBarManager.AddMenuAction(
                        StatusActionName,
                        new MenuBarManager.ActionCallback(StatusCallback));
                }
                catch (NXException ex)
                {
                    WriteLog("AddMenuAction warning: " + ex.Message);
                }

                EnsureDirectories();
                pollTimer = new Timer();
                pollTimer.Interval = 150;
                pollTimer.Tick += PollTimerTick;
                pollTimer.Start();

                pendingWatcher = new FileSystemWatcher(PendingDirectory, "*.json");
                pendingWatcher.Created += PendingWatcherChanged;
                pendingWatcher.Renamed += PendingWatcherChanged;
                pendingWatcher.Changed += PendingWatcherChanged;
                pendingWatcher.EnableRaisingEvents = true;

                isInitialized = true;
                WriteStatus("running");
                WriteContext("running", null, "initialized", "NXKeys Command Bridge initialized.");
                WriteLog("NXKeys Command Bridge initialized. Pending=" + PendingDirectory);
            }
            catch (Exception ex)
            {
                WriteLog("NXKeys Command Bridge failed to initialize: " + ex);
                throw;
            }

            return 0;
        }

        public static int GetUnloadOption(string arg)
        {
            return Convert.ToInt32(Session.LibraryUnloadOption.AtTermination);
        }

        private static int ApplicationInit()
        {
            WriteLog("ApplicationInit");
            return 0;
        }

        private static int ApplicationEnter()
        {
            WriteLog("ApplicationEnter");
            return 0;
        }

        private static int ApplicationExit()
        {
            WriteLog("ApplicationExit");
            return 0;
        }

        private static MenuBarManager.CallbackStatus StatusCallback(MenuButtonEvent buttonEvent)
        {
            try
            {
                listingWindow.Open();
                listingWindow.WriteLine("NXKeys Command Bridge is running.");
                listingWindow.WriteLine("Pending: " + PendingDirectory);
                listingWindow.WriteLine("Log: " + LogPath);
            }
            catch (Exception ex)
            {
                WriteLog("StatusCallback failed: " + ex.Message);
            }

            return MenuBarManager.CallbackStatus.Continue;
        }

        private static void PollTimerTick(object sender, EventArgs e)
        {
            if (isProcessing) return;
            bool fullPollDue = (DateTime.UtcNow - lastFullPollUtc).TotalSeconds >= 5;
            bool contextDue = (DateTime.UtcNow - lastContextWriteUtc).TotalSeconds >= 1;
            if (!pendingWake && !fullPollDue)
            {
                if (contextDue) WriteContext("running", null, "", "");
                return;
            }

            isProcessing = true;
            try
            {
                pendingWake = false;
                lastFullPollUtc = DateTime.UtcNow;
                ProcessPendingRequests();
                WriteContext("running", null, "", "");
            }
            finally
            {
                isProcessing = false;
            }
        }

        private static void PendingWatcherChanged(object sender, FileSystemEventArgs e)
        {
            pendingWake = true;
        }

        private static void ProcessPendingRequests()
        {
            EnsureDirectories();
            string[] files = Directory.GetFiles(PendingDirectory, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                ProcessRequestFile(file);
            }
        }

        private static void ProcessRequestFile(string path)
        {
            NxCommandRequest request;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    request = JsonSerializer.Deserialize<NxCommandRequest>(stream);
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (Exception ex)
            {
                MoveWithResult(path, FailedDirectory, "invalid_json", ex.Message);
                return;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.CommandId))
            {
                MoveWithResult(path, FailedDirectory, "invalid_request", "Missing command_id.");
                return;
            }

            try
            {
                if (string.Equals(request.Action, "switch_module", StringComparison.OrdinalIgnoreCase))
                {
                    SwitchModule(request);
                    MoveWithResult(path, CompletedDirectory, "executed", "Switched module: " + request.TargetApplicationId);
                }
                else
                {
                    ExecuteNxCommand(request);
                    MoveWithResult(path, CompletedDirectory, "executed", "OK");
                }
            }
            catch (Exception ex)
            {
                MoveWithResult(path, FailedDirectory, "execution_failed", ex.ToString());
            }
        }

        private static void SwitchModule(NxCommandRequest request)
        {
            string appId = string.IsNullOrWhiteSpace(request.TargetApplicationId)
                ? request.CommandId
                : request.TargetApplicationId;
            if (string.IsNullOrWhiteSpace(appId))
            {
                throw new InvalidOperationException("Missing target application id.");
            }
            WriteLog($"Switching NX application: {request.ModuleId} -> {appId}");
            var switchMethod = theUI.MenuBarManager.GetType().GetMethod("ApplicationSwitchRequest", new[] { typeof(string) });
            if (switchMethod != null)
            {
                switchMethod.Invoke(theUI.MenuBarManager, new object[] { appId.Trim() });
            }
            else
            {
                ExecuteNxCommand(new NxCommandRequest
                {
                    CommandId = appId.Trim(),
                    CommandName = request.CommandName,
                    Sequence = request.Sequence,
                    ModuleId = request.ModuleId
                });
            }
            WriteLog("Switch request accepted: " + appId);
        }

        private static void ExecuteNxCommand(NxCommandRequest request)
        {
            string commandId = request.CommandId.Trim();
            WriteLog($"Executing direct NX command: {request.Sequence} -> {commandId} ({request.CommandName})");

            MenuButton button = theUI.MenuBarManager.GetButtonFromName(commandId);
            if (button == null)
            {
                throw new InvalidOperationException("NX menu button was not found: " + commandId);
            }

            if (button.ButtonAvailability == MenuButton.AvailabilityStatus.Unavailable)
            {
                throw new InvalidOperationException("NX menu button is unavailable in the current context: " + commandId);
            }

            if (button.ButtonSensitivity == MenuButton.SensitivityStatus.Insensitive)
            {
                throw new InvalidOperationException("NX menu button is insensitive in the current context: " + commandId);
            }

            bool invoked = theUI.DialogTester.InvokeMenuButtonAction(button);
            if (!invoked)
            {
                throw new InvalidOperationException("NX did not accept InvokeMenuButtonAction for: " + commandId);
            }

            WriteLog("Executed direct NX command: " + commandId);
        }

        private static void MoveWithResult(string sourcePath, string destinationDirectory, string status, string message)
        {
            try
            {
                Directory.CreateDirectory(destinationDirectory);
                string name = Path.GetFileNameWithoutExtension(sourcePath);
                string destinationPath = Path.Combine(destinationDirectory, name + "." + status + ".json");
                if (File.Exists(destinationPath))
                {
                    destinationPath = Path.Combine(destinationDirectory, name + "." + status + "." + Guid.NewGuid().ToString("N") + ".json");
                }

                File.Move(sourcePath, destinationPath);
                File.WriteAllText(destinationPath + ".result.txt", DateTime.Now.ToString("O") + Environment.NewLine + message);
                WriteLog($"{status}: {Path.GetFileName(sourcePath)} - {message}");
                WriteContext("running", name, status, message);
            }
            catch (Exception ex)
            {
                WriteLog("MoveWithResult failed: " + ex);
            }
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(BridgeRoot);
            Directory.CreateDirectory(PendingDirectory);
            Directory.CreateDirectory(CompletedDirectory);
            Directory.CreateDirectory(FailedDirectory);
            Directory.CreateDirectory(LogDirectory);
        }

        private static void WriteStatus(string status)
        {
            try
            {
                EnsureDirectories();
                File.WriteAllText(
                    Path.Combine(BridgeRoot, "status.json"),
                    JsonSerializer.Serialize(new
                    {
                        status,
                        process_id = Process.GetCurrentProcess().Id,
                        updated_utc = DateTime.UtcNow.ToString("O"),
                        pending_directory = PendingDirectory,
                        log_path = LogPath
                    }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private static void WriteContext(string status, string requestId, string lastResult, string message)
        {
            try
            {
                EnsureDirectories();
                string appId = AskCurrentApplicationId();
                string moduleId = ModuleIdFromRuntimeContext(appId);
                File.WriteAllText(
                    ContextPath,
                    JsonSerializer.Serialize(new
                    {
                        status,
                        application_id = appId,
                        module_id = moduleId,
                        module_label = ModuleLabelFromModule(moduleId),
                        updated_utc = DateTime.UtcNow.ToString("O"),
                        last_request_id = requestId ?? string.Empty,
                        last_result = lastResult ?? string.Empty,
                        last_message = message ?? string.Empty,
                        pending_directory = PendingDirectory,
                        log_path = LogPath
                    }, new JsonSerializerOptions { WriteIndented = true }));
                lastContextWriteUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                WriteLog("WriteContext failed: " + ex.Message);
            }
        }

        private static string AskCurrentApplicationId()
        {
            try
            {
                if (theUfSession == null) return "UG_APP_GATEWAY";
                int currentModuleId;
                theUfSession.UF.AskApplicationModule(out currentModuleId);
                return ApplicationIdFromUfModule(currentModuleId);
            }
            catch (Exception ex)
            {
                WriteLog("AskApplicationModule failed: " + ex.Message);
                return "UG_APP_GATEWAY";
            }
        }

        private static string ApplicationIdFromUfModule(int moduleId)
        {
            string constant = TryMatchUfConstant(moduleId);
            switch (constant)
            {
                case "UF_APP_MODELING": return "UG_APP_MODELING";
                case "UF_APP_DRAFTING": return "UG_APP_DRAFTING";
                case "UF_APP_MANUFACTURING": return "UG_APP_MANUFACTURING";
                case "UF_APP_SFEM": return "UG_APP_SFEM";
                case "UF_APP_DESFEM": return "UG_APP_DESFEM";
                case "UF_APP_SHEETMETAL": return "UG_APP_SHEETMETAL";
                case "UF_APP_ROUTING": return "UG_APP_ROUTING";
                case "UF_APP_STUDIO": return "UG_APP_STUDIO";
                default: return "UG_APP_GATEWAY";
            }
        }

        private static string TryMatchUfConstant(int moduleId)
        {
            try
            {
                foreach (var field in typeof(UFConstants).GetFields())
                {
                    if (!field.Name.StartsWith("UF_APP_", StringComparison.OrdinalIgnoreCase)) continue;
                    object value = field.GetValue(null);
                    if (value is int intValue && intValue == moduleId)
                    {
                        return field.Name;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        private static string ModuleIdFromApplication(string appId)
        {
            string id = (appId ?? string.Empty).ToUpperInvariant();
            if (id.Contains("DRAFTING")) return "drafting";
            if (id.Contains("MANUFACTURING")) return "manufacturing";
            if (id.Contains("SFEM") || id.Contains("DESFEM")) return "simulation";
            if (id.Contains("SHEETMETAL")) return "sheet_metal";
            if (id.Contains("ROUTING")) return "routing";
            if (id.Contains("STUDIO")) return "surface";
            if (id.Contains("MOLD")) return "mold";
            if (id.Contains("MODEL")) return "modeling";
            return "inspect_view";
        }

        private static string ModuleIdFromRuntimeContext(string appId)
        {
            if (IsButtonReady("UG_SKETCH_FINISH") || IsButtonReady("UG_SKETCH_LINE"))
            {
                return "sketch";
            }
            return ModuleIdFromApplication(appId);
        }

        private static bool IsButtonReady(string commandId)
        {
            try
            {
                MenuButton button = theUI.MenuBarManager.GetButtonFromName(commandId);
                return button != null &&
                       button.ButtonAvailability != MenuButton.AvailabilityStatus.Unavailable &&
                       button.ButtonSensitivity != MenuButton.SensitivityStatus.Insensitive;
            }
            catch
            {
                return false;
            }
        }

        private static string ModuleLabelFromApplication(string appId)
        {
            return ModuleLabelFromModule(ModuleIdFromApplication(appId));
        }

        private static string ModuleLabelFromModule(string moduleId)
        {
            switch ((moduleId ?? string.Empty).ToLowerInvariant())
            {
                case "drafting": return "Drafting";
                case "manufacturing": return "CAM / Manufacturing";
                case "simulation": return "CAE / Simulation";
                case "sheet_metal": return "Sheet Metal";
                case "routing": return "Routing";
                case "surface": return "Surface";
                case "mold": return "Mold / Tooling";
                case "sketch": return "Sketch";
                case "modeling": return "Modeling";
                default: return "Inspect / View";
            }
        }

        private static void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Process.GetCurrentProcess().Id}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static string BridgeRoot => Path.Combine(LocalAppData, "NXKeys", "bridge");
        private static string PendingDirectory => Path.Combine(BridgeRoot, "pending");
        private static string CompletedDirectory => Path.Combine(BridgeRoot, "completed");
        private static string FailedDirectory => Path.Combine(BridgeRoot, "failed");
        private static string ContextPath => Path.Combine(BridgeRoot, "context.json");
        private static string LogDirectory => Path.Combine(LocalAppData, "NXKeys", "logs");
        private static string LogPath => Path.Combine(LogDirectory, "nx-command-bridge.log");

        private sealed class NxCommandRequest
        {
            public string RequestId { get; set; }
            public string Action { get; set; }
            public string CommandId { get; set; }
            public string CommandName { get; set; }
            public string Sequence { get; set; }
            public string ModuleId { get; set; }
            public string TargetApplicationId { get; set; }
            public string CreatedUtc { get; set; }
            public int SourceProcessId { get; set; }
        }
    }
}
