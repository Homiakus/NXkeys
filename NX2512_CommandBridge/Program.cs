using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using NXKeys.Protocol;
using NXOpen;
using NXOpen.MenuBar;
using NXOpen.UF;
using Timer = System.Windows.Forms.Timer;

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
        private static volatile bool pendingWake = true;
        private static DateTime lastFullPollUtc = DateTime.MinValue;
        private static DateTime lastContextWriteUtc = DateTime.MinValue;
        private static long contextRevision;
        private static string lastContextFingerprint = string.Empty;
        private static string lastRequestId = string.Empty;
        private static string lastResult = string.Empty;
        private static string lastMessage = string.Empty;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetLastActivePopup(IntPtr hWnd);

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
                LoadPreviousContextRevision();
                RecoverInterruptedRequests();

                pollTimer = new Timer { Interval = 150 };
                pollTimer.Tick += PollTimerTick;
                pollTimer.Start();

                pendingWatcher = new FileSystemWatcher(PendingDirectory, "*.request.json")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
                };
                pendingWatcher.Created += PendingWatcherChanged;
                pendingWatcher.Renamed += PendingWatcherChanged;
                pendingWatcher.Changed += PendingWatcherChanged;
                pendingWatcher.EnableRaisingEvents = true;

                isInitialized = true;
                WriteStatus("running");
                WriteContext("initialized", "NXKeys Command Bridge initialized.");
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
                listingWindow.WriteLine("Processing: " + ProcessingDirectory);
                listingWindow.WriteLine("Context revision: " + contextRevision);
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
                if (contextDue) WriteContext(lastResult, lastMessage);
                return;
            }

            isProcessing = true;
            try
            {
                pendingWake = false;
                lastFullPollUtc = DateTime.UtcNow;
                ProcessPendingRequests();
                WriteContext(lastResult, lastMessage);
            }
            catch (Exception ex)
            {
                WriteLog("PollTimerTick failed: " + ex);
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
            string[] files = Directory.GetFiles(PendingDirectory, "*.request.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files) ProcessRequestFile(file);
        }

        private static void ProcessRequestFile(string pendingPath)
        {
            string fileName = Path.GetFileName(pendingPath);
            string requestIdFromName = fileName.EndsWith(".request.json", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - ".request.json".Length)
                : Path.GetFileNameWithoutExtension(fileName);

            if (ResultExists(requestIdFromName))
            {
                ArchiveDuplicate(pendingPath, requestIdFromName);
                return;
            }

            string processingPath = Path.Combine(ProcessingDirectory, fileName);
            try
            {
                File.Move(pendingPath, processingPath);
            }
            catch (IOException)
            {
                return;
            }

            NxCommandRequest request = null;
            try
            {
                using (FileStream stream = new FileStream(processingPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    request = JsonSerializer.Deserialize<NxCommandRequest>(stream, NxProtocolJson.ReadOptions);
                }
                if (request == null) throw new InvalidOperationException("Request JSON is empty.");
                request.Validate();
                if (!string.Equals(request.RequestId, requestIdFromName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("request_id does not match the file name.");

                NxContextSnapshot before = BuildCurrentContext();
                ValidateExpectedContext(request, before);

                if (string.Equals(request.Action, "switch_module", StringComparison.OrdinalIgnoreCase))
                {
                    SwitchModule(request);
                    CompleteClaim(processingPath, request, "executed", "Switched module: " + request.TargetApplicationId, before.Revision);
                }
                else
                {
                    ExecuteNxCommand(request);
                    NxContextSnapshot after = BuildCurrentContext();
                    CompleteClaim(processingPath, request, "executed", "OK", after.Revision);
                }
            }
            catch (Exception ex)
            {
                string requestId = request?.RequestId;
                if (string.IsNullOrWhiteSpace(requestId)) requestId = requestIdFromName;
                FailClaim(processingPath, requestId, "rejected", ex.Message, BuildCurrentContext().Revision);
            }
        }

        private static void ValidateExpectedContext(NxCommandRequest request, NxContextSnapshot current)
        {
            if (current.ModalDialogActive)
                throw new InvalidOperationException("NX has an active modal dialog.");
            if (request.ExpectedContextRevision > 0 && current.Revision != request.ExpectedContextRevision)
                throw new InvalidOperationException(
                    "NX context changed after the shortcut was accepted. Expected revision " +
                    request.ExpectedContextRevision + ", actual " + current.Revision + ".");
            if (request.ExpectedSelectionCount >= 0 && current.SelectionCount != request.ExpectedSelectionCount)
                throw new InvalidOperationException(
                    "NX selection changed after the shortcut was accepted. Expected " +
                    request.ExpectedSelectionCount + ", actual " + current.SelectionCount + ".");
            if (!string.IsNullOrWhiteSpace(request.ExpectedApplicationId) &&
                !string.Equals(current.ApplicationId, request.ExpectedApplicationId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "NX application changed after the shortcut was accepted. Expected " +
                    request.ExpectedApplicationId + ", actual " + current.ApplicationId + ".");

            if (!string.Equals(request.Action, "switch_module", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(request.ModuleId) &&
                !IsSharedModule(request.ModuleId) &&
                !string.Equals(NormalizeModule(current.ModuleId), NormalizeModule(request.ModuleId), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Command belongs to module " + request.ModuleId + ", current module is " + current.ModuleId + ".");
        }

        private static void SwitchModule(NxCommandRequest request)
        {
            string applicationId = string.IsNullOrWhiteSpace(request.TargetApplicationId)
                ? request.CommandId
                : request.TargetApplicationId;
            if (string.IsNullOrWhiteSpace(applicationId))
                throw new InvalidOperationException("Missing target application id.");

            WriteLog("Switching NX application: " + request.ModuleId + " -> " + applicationId);
            var switchMethod = theUI.MenuBarManager.GetType().GetMethod("ApplicationSwitchRequest", new[] { typeof(string) });
            if (switchMethod != null)
            {
                switchMethod.Invoke(theUI.MenuBarManager, new object[] { applicationId.Trim() });
            }
            else
            {
                ExecuteNxCommand(new NxCommandRequest
                {
                    SchemaVersion = NxProtocolConstants.SchemaVersion,
                    RequestId = request.RequestId,
                    CommandId = applicationId.Trim(),
                    CommandName = request.CommandName,
                    Sequence = request.Sequence,
                    ModuleId = request.ModuleId,
                    CreatedUtc = request.CreatedUtc,
                    ExpiresUtc = request.ExpiresUtc,
                    ConfirmationAccepted = true
                });
            }
            WriteLog("Switch request accepted: " + applicationId);
        }

        private static void ExecuteNxCommand(NxCommandRequest request)
        {
            string commandId = request.CommandId.Trim();
            WriteLog("Executing direct NX command: " + request.Sequence + " -> " + commandId + " (" + request.CommandName + ")");

            MenuButton button = theUI.MenuBarManager.GetButtonFromName(commandId);
            if (button == null)
                throw new InvalidOperationException("NX menu button was not found: " + commandId);
            if (button.ButtonAvailability == MenuButton.AvailabilityStatus.Unavailable)
                throw new InvalidOperationException("NX menu button is unavailable in the current context: " + commandId);
            if (button.ButtonSensitivity == MenuButton.SensitivityStatus.Insensitive)
                throw new InvalidOperationException("NX menu button is insensitive in the current context: " + commandId);

            bool invoked = theUI.DialogTester.InvokeMenuButtonAction(button);
            if (!invoked)
                throw new InvalidOperationException("NX did not accept InvokeMenuButtonAction for: " + commandId);
            WriteLog("Executed direct NX command: " + commandId);
        }

        private static void CompleteClaim(
            string processingPath,
            NxCommandRequest request,
            string status,
            string message,
            long revision)
        {
            string requestId = request.RequestId;
            NxCommandResult result = CreateResult(requestId, status, message, revision);
            WriteResultAtomic(CompletedDirectory, result);
            ArchiveRequest(processingPath, CompletedDirectory, requestId);
            RememberResult(requestId, status, message);
        }

        private static void FailClaim(string processingPath, string requestId, string status, string message, long revision)
        {
            try
            {
                NxCommandResult result = CreateResult(requestId, status, message, revision);
                WriteResultAtomic(FailedDirectory, result);
                ArchiveRequest(processingPath, FailedDirectory, requestId);
                RememberResult(requestId, status, message);
            }
            catch (Exception ex)
            {
                WriteLog("FailClaim failed: " + ex);
            }
        }

        private static NxCommandResult CreateResult(string requestId, string status, string message, long revision)
        {
            return new NxCommandResult
            {
                SchemaVersion = NxProtocolConstants.SchemaVersion,
                RequestId = requestId ?? string.Empty,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                ContextRevision = revision,
                CompletedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        private static void WriteResultAtomic(string directory, NxCommandResult result)
        {
            Directory.CreateDirectory(directory);
            string finalPath = Path.Combine(directory, result.RequestId + ".result.json");
            if (File.Exists(finalPath)) return;
            WriteJsonAtomic(finalPath, result);
        }

        private static void ArchiveRequest(string sourcePath, string destinationDirectory, string requestId)
        {
            if (!File.Exists(sourcePath)) return;
            Directory.CreateDirectory(destinationDirectory);
            string destination = Path.Combine(destinationDirectory, requestId + ".request.json");
            if (File.Exists(destination))
                destination = Path.Combine(destinationDirectory, requestId + ".request." + Guid.NewGuid().ToString("N") + ".json");
            File.Move(sourcePath, destination);
        }

        private static void ArchiveDuplicate(string pendingPath, string requestId)
        {
            try
            {
                string destinationDirectory = File.Exists(Path.Combine(CompletedDirectory, requestId + ".result.json"))
                    ? CompletedDirectory
                    : FailedDirectory;
                ArchiveRequest(pendingPath, destinationDirectory, requestId);
                WriteLog("Duplicate request ignored: " + requestId);
            }
            catch (Exception ex)
            {
                WriteLog("ArchiveDuplicate failed: " + ex.Message);
            }
        }

        private static bool ResultExists(string requestId)
        {
            return File.Exists(Path.Combine(CompletedDirectory, requestId + ".result.json")) ||
                   File.Exists(Path.Combine(FailedDirectory, requestId + ".result.json"));
        }

        private static void RecoverInterruptedRequests()
        {
            foreach (string path in Directory.GetFiles(ProcessingDirectory, "*.request.json"))
            {
                string name = Path.GetFileName(path);
                string requestId = name.Substring(0, name.Length - ".request.json".Length);
                try
                {
                    if (File.Exists(Path.Combine(CompletedDirectory, requestId + ".result.json")))
                    {
                        ArchiveRequest(path, CompletedDirectory, requestId);
                        continue;
                    }
                    if (File.Exists(Path.Combine(FailedDirectory, requestId + ".result.json")))
                    {
                        ArchiveRequest(path, FailedDirectory, requestId);
                        continue;
                    }

                    NxCommandResult result = CreateResult(
                        requestId,
                        "interrupted_unknown",
                        "Bridge restarted while the request was in processing. The command will not be replayed automatically.",
                        contextRevision);
                    WriteResultAtomic(FailedDirectory, result);
                    ArchiveRequest(path, FailedDirectory, requestId);
                    WriteLog("Interrupted request quarantined without replay: " + requestId);
                }
                catch (Exception ex)
                {
                    WriteLog("RecoverInterruptedRequests failed for " + path + ": " + ex.Message);
                }
            }
        }

        private static void RememberResult(string requestId, string result, string message)
        {
            lastRequestId = requestId ?? string.Empty;
            lastResult = result ?? string.Empty;
            lastMessage = message ?? string.Empty;
            WriteLog(lastResult + ": " + lastRequestId + " - " + lastMessage);
            WriteContext(lastResult, lastMessage);
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(BridgeRoot);
            Directory.CreateDirectory(PendingDirectory);
            Directory.CreateDirectory(ProcessingDirectory);
            Directory.CreateDirectory(CompletedDirectory);
            Directory.CreateDirectory(FailedDirectory);
            Directory.CreateDirectory(LogDirectory);
        }

        private static void LoadPreviousContextRevision()
        {
            try
            {
                if (!File.Exists(ContextPath)) return;
                using (FileStream stream = new FileStream(ContextPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    NxContextSnapshot previous = JsonSerializer.Deserialize<NxContextSnapshot>(stream, NxProtocolJson.ReadOptions);
                    if (previous == null) return;
                    contextRevision = Math.Max(0, previous.Revision);
                    lastContextFingerprint = previous.SemanticFingerprint();
                }
            }
            catch (Exception ex)
            {
                WriteLog("LoadPreviousContextRevision warning: " + ex.Message);
            }
        }

        private static NxContextSnapshot BuildCurrentContext()
        {
            string applicationId = AskCurrentApplicationId(out int applicationConfidence);
            string moduleId = ModuleIdFromRuntimeContext(applicationId, out int moduleConfidence);
            int selectionCount = AskSelectionCount(out List<string> selectedTypes);
            bool workPart = AskWorkPartAvailable();
            bool displayPart = AskDisplayPartAvailable();
            bool modal = IsModalDialogActive();

            var snapshot = new NxContextSnapshot
            {
                SchemaVersion = NxProtocolConstants.SchemaVersion,
                Status = "running",
                ApplicationId = applicationId,
                ModuleId = moduleId,
                ModuleLabel = ModuleLabelFromModule(moduleId),
                SelectionCount = selectionCount,
                SelectionState = selectionCount < 0 ? "unknown" : selectionCount == 0 ? "none" : selectionCount == 1 ? "single" : "multiple",
                SelectedTypes = selectedTypes,
                WorkPartAvailable = workPart,
                DisplayPartAvailable = displayPart,
                ModalDialogActive = modal,
                ActiveCommandId = string.Empty,
                ContextConfidence = Math.Min(applicationConfidence, moduleConfidence),
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                LastRequestId = lastRequestId,
                LastResult = lastResult,
                LastMessage = lastMessage
            };

            string fingerprint = snapshot.SemanticFingerprint();
            if (!string.Equals(fingerprint, lastContextFingerprint, StringComparison.Ordinal))
            {
                contextRevision++;
                lastContextFingerprint = fingerprint;
            }
            snapshot.Revision = Math.Max(1, contextRevision);
            return snapshot;
        }

        private static void WriteContext(string result, string message)
        {
            try
            {
                EnsureDirectories();
                if (result != null) lastResult = result;
                if (message != null) lastMessage = message;
                NxContextSnapshot snapshot = BuildCurrentContext();
                WriteJsonAtomic(ContextPath, snapshot);
                lastContextWriteUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                WriteLog("WriteContext failed: " + ex.Message);
            }
        }

        private static void WriteStatus(string status)
        {
            try
            {
                EnsureDirectories();
                WriteJsonAtomic(
                    Path.Combine(BridgeRoot, "status.json"),
                    new
                    {
                        schema_version = NxProtocolConstants.SchemaVersion,
                        status,
                        process_id = Process.GetCurrentProcess().Id,
                        updated_utc = DateTimeOffset.UtcNow.ToString("O"),
                        pending_directory = PendingDirectory,
                        processing_directory = ProcessingDirectory,
                        log_path = LogPath
                    });
            }
            catch { }
        }

        private static void WriteJsonAtomic<T>(string path, T value)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(value, NxProtocolJson.WriteOptions);
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }
                File.Move(temporary, path, true);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            }
        }

        private static string AskCurrentApplicationId(out int confidence)
        {
            confidence = 100;
            try
            {
                if (theUfSession == null)
                {
                    confidence = 30;
                    return "UG_APP_GATEWAY";
                }
                int currentModuleId;
                theUfSession.UF.AskApplicationModule(out currentModuleId);
                return ApplicationIdFromUfModule(currentModuleId);
            }
            catch (Exception ex)
            {
                confidence = 20;
                WriteLog("AskApplicationModule failed: " + ex.Message);
                return "UG_APP_GATEWAY";
            }
        }

        private static int AskSelectionCount(out List<string> selectedTypes)
        {
            selectedTypes = new List<string>();
            try
            {
                int count = theUI.SelectionManager.GetNumSelectedObjects();
                int inspected = Math.Min(count, 64);
                for (int index = 0; index < inspected; index++)
                {
                    TaggedObject selected = theUI.SelectionManager.GetSelectedTaggedObject(index);
                    string typeName = selected?.GetType().FullName;
                    if (!string.IsNullOrWhiteSpace(typeName) && !selectedTypes.Contains(typeName, StringComparer.Ordinal))
                        selectedTypes.Add(typeName);
                }
                return count;
            }
            catch (Exception ex)
            {
                WriteLog("AskSelectionCount failed: " + ex.Message);
                return -1;
            }
        }

        private static bool AskWorkPartAvailable()
        {
            try { return theSession?.Parts?.Work != null; }
            catch { return false; }
        }

        private static bool AskDisplayPartAvailable()
        {
            try { return theSession?.Parts?.Display != null; }
            catch { return false; }
        }

        private static bool IsModalDialogActive()
        {
            try
            {
                IntPtr mainWindow = Process.GetCurrentProcess().MainWindowHandle;
                if (mainWindow == IntPtr.Zero) return false;
                if (!IsWindowEnabled(mainWindow)) return true;
                IntPtr popup = GetLastActivePopup(mainWindow);
                return popup != IntPtr.Zero && popup != mainWindow && IsWindowVisible(popup);
            }
            catch
            {
                return false;
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
                    if (value is int intValue && intValue == moduleId) return field.Name;
                }
            }
            catch { }
            return string.Empty;
        }

        private static string ModuleIdFromApplication(string applicationId)
        {
            string id = (applicationId ?? string.Empty).ToUpperInvariant();
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

        private static string ModuleIdFromRuntimeContext(string applicationId, out int confidence)
        {
            if (IsButtonReady("UG_SKETCH_FINISH") || IsButtonReady("UG_SKETCH_LINE"))
            {
                confidence = 60;
                return "sketch";
            }
            confidence = 90;
            return ModuleIdFromApplication(applicationId);
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

        private static bool IsSharedModule(string moduleId)
        {
            string normalized = NormalizeModule(moduleId);
            return normalized == "selection_object" || normalized == "inspect_view" || normalized == "reuse";
        }

        private static string NormalizeModule(string moduleId)
        {
            string value = (moduleId ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
            switch (value)
            {
                case "view":
                case "inspect":
                case "inspect_/_view": return "inspect_view";
                case "selection_filters":
                case "selection": return "selection_object";
                case "cam_/_manufacturing":
                case "cam": return "manufacturing";
                case "cae_/_simulation":
                case "cae": return "simulation";
                case "mold_/_tooling": return "mold";
                case "reuse_/_templates": return "reuse";
                default: return value;
            }
        }

        private static void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                    " [" + Process.GetCurrentProcess().Id + "] " + message + Environment.NewLine);
            }
            catch { }
        }

        private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static string BridgeRoot => Path.Combine(LocalAppData, "NXKeys", "bridge");
        private static string PendingDirectory => Path.Combine(BridgeRoot, "pending");
        private static string ProcessingDirectory => Path.Combine(BridgeRoot, "processing");
        private static string CompletedDirectory => Path.Combine(BridgeRoot, "completed");
        private static string FailedDirectory => Path.Combine(BridgeRoot, "failed");
        private static string ContextPath => Path.Combine(BridgeRoot, "context.json");
        private static string LogDirectory => Path.Combine(LocalAppData, "NXKeys", "logs");
        private static string LogPath => Path.Combine(LogDirectory, "nx-command-bridge.log");
    }
}
