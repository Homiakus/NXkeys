using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using NXKeys.BridgeCore;
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
        private static NxContextMonitor contextMonitor;
        private static NxMenuCommandExecutor executor;
        private static string lastRequestId = string.Empty;
        private static string lastResult = string.Empty;
        private static string lastMessage = string.Empty;
        private static BridgeSecurityGate securityGate;
        private static BridgeRequestInbox requestInbox;

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

        public static int ufsta(string[] args)
        {
            return Startup();
        }

        public static int ufusr(string[] args)
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
                contextMonitor = new NxContextMonitor(theSession, theUfSession, theUI, WriteLog);
                executor = new NxMenuCommandExecutor(theUI, WriteLog, ProcessingDirectory);

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
                securityGate = BridgeSecurityGate.CreateFromEnvironment(WriteLog);
                contextMonitor.LoadPreviousContextRevision();
                RecoverInterruptedRequests();
                requestInbox = new BridgeRequestInbox(
                    PendingDirectory,
                    ProcessingDirectory,
                    ResultExists,
                    ArchiveDuplicate,
                    securityGate.Validate,
                    WriteLog);
                requestInbox.Start();
                AppDomain.CurrentDomain.ProcessExit += (_, _) => requestInbox?.Dispose();

                pollTimer = new Timer { Interval = 100 };
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
                requestInbox.Signal();

                isInitialized = true;
                WriteStatus("running");
                PublishContext("initialized", "NXKeys Command Bridge initialized.");
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
                listingWindow.WriteLine("Context revision: " + contextMonitor.CurrentRevision);
                listingWindow.WriteLine("Security: " + (securityGate?.Status ?? "not_initialized"));
                listingWindow.WriteLine("Admitted queue: " + (requestInbox?.ReadyCount ?? 0));
                listingWindow.WriteLine("Rejected queue: " + (requestInbox?.RejectedCount ?? 0));
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
            bool contextDue = (DateTime.UtcNow - contextMonitor.LastContextWriteUtc).TotalSeconds >= 1;

            if (requestInbox != null && requestInbox.TryDequeueRejected(out BridgeRequestRejection rejection) && rejection != null)
            {
                isProcessing = true;
                try
                {
                    FailClaim(
                        rejection.ProcessingPath,
                        rejection.RequestId,
                        "rejected",
                        rejection.Message,
                        contextMonitor.GetCurrent()?.Revision ?? 0);
                    PublishContext(lastResult, lastMessage);
                }
                catch (Exception exception)
                {
                    WriteLog("Rejected request finalization failed: " + exception);
                }
                finally
                {
                    isProcessing = false;
                }
                return;
            }

            if (requestInbox != null && requestInbox.TryDequeue(out BridgeRequestClaim claim) && claim != null)
            {
                isProcessing = true;
                try
                {
                    ProcessClaim(claim);
                    PublishContext(lastResult, lastMessage);
                }
                catch (Exception exception)
                {
                    WriteLog("NX request dispatch failed: " + exception);
                }
                finally
                {
                    isProcessing = false;
                }
                return;
            }

            if (contextDue) PublishContext(lastResult, lastMessage);
        }

        private static void PendingWatcherChanged(object sender, FileSystemEventArgs e)
        {
            requestInbox?.Signal();
        }

        private static void ProcessClaim(BridgeRequestClaim claim)
        {
            NxCommandRequest request = claim.Request;
            try
            {
                NxContextSnapshot before = contextMonitor.GetCurrent()!;
                ValidateExpectedContext(request, before);

                if (string.Equals(request.Action, NxProtocolActions.SwitchModule, StringComparison.OrdinalIgnoreCase))
                {
                    executor.SwitchModule(request);
                    CompleteClaim(claim.ProcessingPath, request, "executed",
                        "Switched module: " + request.TargetApplicationId, before.Revision);
                }
                else if (string.Equals(request.Action, NxProtocolActions.SetSelectionFilter, StringComparison.OrdinalIgnoreCase))
                {
                    string message = executor.ApplySelectionCommand(request);
                    CompleteClaim(claim.ProcessingPath, request, "executed", message, before.Revision);
                }
                else if (string.Equals(request.Action, NxProtocolActions.ProbeCommand, StringComparison.OrdinalIgnoreCase))
                {
                    string message = executor.ProbeNxCommand(request.CommandId);
                    CompleteClaim(claim.ProcessingPath, request, "completed", message, before.Revision);
                }
                else if (string.Equals(request.Action, NxProtocolActions.RunCapability, StringComparison.OrdinalIgnoreCase))
                {
                    NxCommandResult capResult = executor.ExecuteCapabilityRequest(request);
                    NxContextSnapshot after = contextMonitor.GetCurrent()!;
                    CompleteClaimWithResult(claim.ProcessingPath, request, capResult, after.Revision);
                }
                else if (string.Equals(request.Action, NxProtocolActions.ExecuteCommand, StringComparison.OrdinalIgnoreCase))
                {
                    executor.ExecuteNxCommand(request);
                    NxContextSnapshot after = contextMonitor.GetCurrent()!;
                    CompleteClaim(claim.ProcessingPath, request, "executed", "OK", after.Revision);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported NXKeys action: " + request.Action);
                }
            }
            catch (Exception exception)
            {
                FailClaim(
                    claim.ProcessingPath,
                    request?.RequestId ?? claim.RequestId,
                    "rejected",
                    exception.Message,
                    contextMonitor.GetCurrent()?.Revision ?? 0);
            }
        }

        private static void ValidateExpectedContext(NxCommandRequest request, NxContextSnapshot current)
        {
            bool allowInDialog = string.Equals(request.Action, "set_selection_intent", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(request.Action, "set_selection_filter", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(request.Action, "cancel_dialog", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(request.Action, "view_fit", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(request.Action, "view_refresh", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(request.Action, "set_layer_settings", StringComparison.OrdinalIgnoreCase) ||
                                 (request.CommandId != null && (
                                     request.CommandId.StartsWith("UG_VIEW_", StringComparison.OrdinalIgnoreCase) ||
                                     request.CommandId.StartsWith("UG_SEL_", StringComparison.OrdinalIgnoreCase) ||
                                     request.CommandId.StartsWith("UG_LAYER_", StringComparison.OrdinalIgnoreCase)));

            if (current.ModalDialogActive && !allowInDialog)
                throw new InvalidOperationException("NX has an active modal dialog.");
            if (request.ExpectedContextRevision > 0 && current.Revision != request.ExpectedContextRevision)
                throw new InvalidOperationException(
                    "NX context changed after the shortcut was accepted. Expected revision " +
                    request.ExpectedContextRevision + ", actual " + current.Revision + ".");
            if (request.ExpectedSelectionCount >= 0 && current.SelectionCount != request.ExpectedSelectionCount)
                throw new InvalidOperationException(
                    "NX selection changed after the shortcut was accepted. Expected " +
                    request.ExpectedSelectionCount + ", actual " + current.SelectionCount + ".");
            if (!string.IsNullOrWhiteSpace(request.ExpectedSelectionFingerprint) &&
                !string.Equals(current.SelectionFingerprint, request.ExpectedSelectionFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "NX selected objects changed after the shortcut was accepted.");
            if (!string.IsNullOrWhiteSpace(request.ExpectedApplicationId) &&
                !string.Equals(current.ApplicationId, request.ExpectedApplicationId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "NX application changed after the shortcut was accepted. Expected " +
                    request.ExpectedApplicationId + ", actual " + current.ApplicationId + ".");

            if (!string.Equals(request.Action, "switch_module", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(request.ModuleId) &&
                !NxContextMonitor.IsSharedModule(request.ModuleId) &&
                !string.Equals(NxContextMonitor.NormalizeModule(current.ModuleId), NxContextMonitor.NormalizeModule(request.ModuleId), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Command belongs to module " + request.ModuleId + ", current module is " + current.ModuleId + ".");
        }





        private static void CompleteClaimWithResult(
            string processingPath,
            NxCommandRequest request,
            NxCommandResult result,
            long revision)
        {
            string requestId = request.RequestId;
            result.ContextRevision = revision;
            string destinationDir = result.Success ? CompletedDirectory : FailedDirectory;
            WriteResultAtomic(destinationDir, result);
            ArchiveRequest(processingPath, destinationDir, requestId);
            RememberResult(requestId, result.Status, result.Message);
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
                        contextMonitor.CurrentRevision);
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
            PublishContext(lastResult, lastMessage);
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

        private static void PublishContext(string result, string message)
        {
            contextMonitor.LastRequestId = lastRequestId;
            contextMonitor.SecurityStatus = securityGate?.Status ?? "not_initialized";
            contextMonitor.SecuritySessionId = securityGate?.SessionId ?? string.Empty;
            contextMonitor.SecurityProfileDigest = securityGate?.ProfileDigest ?? string.Empty;
            contextMonitor.Refresh(result, message);
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
                        log_path = LogPath,
                        security_status = securityGate?.Status ?? "not_initialized",
                        security_session_id = securityGate?.SessionId ?? string.Empty,
                        security_profile_digest = securityGate?.ProfileDigest ?? string.Empty,
                        admitted_queue = requestInbox?.ReadyCount ?? 0,
                        rejected_queue = requestInbox?.RejectedCount ?? 0
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
        private static string LogDirectory => Path.Combine(LocalAppData, "NXKeys", "logs");
        private static string LogPath => Path.Combine(LogDirectory, "nx-command-bridge.log");
    }
}
