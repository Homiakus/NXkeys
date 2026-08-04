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
        private static DateTime lastContextWriteUtc = DateTime.MinValue;
        private static long contextRevision;
        private static string lastContextFingerprint = string.Empty;
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
                securityGate = BridgeSecurityGate.CreateFromEnvironment(WriteLog);
                LoadPreviousContextRevision();
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
            bool contextDue = (DateTime.UtcNow - lastContextWriteUtc).TotalSeconds >= 1;

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
                        BuildCurrentContext().Revision);
                    WriteContext(lastResult, lastMessage);
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
                    WriteContext(lastResult, lastMessage);
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

            if (contextDue) WriteContext(lastResult, lastMessage);
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
                NxContextSnapshot before = BuildCurrentContext();
                ValidateExpectedContext(request, before);

                if (string.Equals(request.Action, NxProtocolActions.SwitchModule, StringComparison.OrdinalIgnoreCase))
                {
                    SwitchModule(request);
                    CompleteClaim(claim.ProcessingPath, request, "executed",
                        "Switched module: " + request.TargetApplicationId, before.Revision);
                }
                else if (string.Equals(request.Action, NxProtocolActions.SetSelectionFilter, StringComparison.OrdinalIgnoreCase))
                {
                    string message = ApplySelectionCommand(request);
                    CompleteClaim(claim.ProcessingPath, request, "executed", message, before.Revision);
                }
                else if (string.Equals(request.Action, NxProtocolActions.ProbeCommand, StringComparison.OrdinalIgnoreCase))
                {
                    string message = ProbeNxCommand(request.CommandId);
                    CompleteClaim(claim.ProcessingPath, request, "completed", message, before.Revision);
                }
                else if (string.Equals(request.Action, NxProtocolActions.ExecuteCommand, StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteNxCommand(request);
                    NxContextSnapshot after = BuildCurrentContext();
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
                    BuildCurrentContext().Revision);
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

            if (!string.IsNullOrWhiteSpace(request.SelectionFilter))
                ApplyGlobalSelectionFilter(request.SelectionFilter);

            MenuButton button = RequireRunnableButton(commandId);

            bool invoked = theUI.DialogTester.InvokeMenuButtonAction(button);
            if (!invoked)
                throw new InvalidOperationException("NX did not accept InvokeMenuButtonAction for: " + commandId);
            WriteLog("Executed direct NX command: " + commandId);
        }

        private static string ProbeNxCommand(string commandId)
        {
            string id = (commandId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id)) return "missing id";
            try
            {
                MenuButton button = theUI.MenuBarManager.GetButtonFromName(id);
                if (button == null) return "missing: " + id;
                return "availability=" + button.ButtonAvailability + "; sensitivity=" + button.ButtonSensitivity;
            }
            catch (Exception ex)
            {
                return "missing: " + id + "; " + ex.Message;
            }
        }

        private static MenuButton RequireRunnableButton(string commandId)
        {
            string id = (commandId ?? string.Empty).Trim();
            MenuButton button;
            try
            {
                button = theUI.MenuBarManager.GetButtonFromName(id);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("NX menu button was not found: " + id + ". " + ex.Message);
            }
            if (button == null)
                throw new InvalidOperationException("NX menu button was not found: " + id);
            if (button.ButtonAvailability == MenuButton.AvailabilityStatus.Unavailable)
                throw new InvalidOperationException("NX menu button is unavailable in the current context: " + id);
            if (button.ButtonSensitivity == MenuButton.SensitivityStatus.Insensitive)
                throw new InvalidOperationException("NX menu button is insensitive in the current context: " + id);
            return button;
        }

        private static string ApplySelectionCommand(NxCommandRequest request)
        {
            string filter = !string.IsNullOrWhiteSpace(request.SelectionFilter)
                ? request.SelectionFilter
                : SelectionFilterFromCommandId(request.CommandId);
            if (string.IsNullOrWhiteSpace(filter))
                throw new InvalidOperationException("Selection filter is not defined for: " + request.CommandId);

            string normalized = NormalizeSelectionFilter(filter);
            if (normalized == "none")
            {
                ClearGlobalSelectionList();
                return "Selection cleared";
            }
            if (normalized == "all")
            {
                ExecuteRelaxedMenuButton(request.CommandId);
                return "Select all requested";
            }
            if (normalized == "reset")
            {
                theUI.SelectionManager.ResetEnabledGlobalFilterMembers();
                return "Selection filters reset";
            }

            ApplyGlobalSelectionFilter(normalized);
            return "Selection filter set: " + normalized;
        }

        private static void ApplyGlobalSelectionFilter(string filter)
        {
            string normalized = NormalizeSelectionFilter(filter);
            if (string.IsNullOrWhiteSpace(normalized) || normalized == "all") return;
            if (normalized == "none") { ClearGlobalSelectionList(); return; }
            if (normalized == "reset") { theUI.SelectionManager.ResetEnabledGlobalFilterMembers(); return; }

            NXOpen.Select.FilterMember[] members = FilterMembersFor(normalized);
            if (members.Length == 0)
            {
                WriteLog("No global NX selection filter mapping for: " + filter);
                return;
            }
            theUI.SelectionManager.SetEnabledGlobalFilterMembers(members);
            WriteLog("Applied global selection filter: " + normalized + " => " + string.Join(",", members.Select(value => value.ToString())));
        }

        private static NXOpen.Select.FilterMember[] FilterMembersFor(string filter)
        {
            switch (NormalizeSelectionFilter(filter))
            {
                case "edge": return ParseFilterMembers("AllEdges");
                case "face": return ParseFilterMembers("AllFaces");
                case "body": return ParseFilterMembers("AllSolidBodies", "AllSheetBodies", "AllFacetBodies");
                case "component": return ParseFilterMembers("Component");
                case "curve": return ParseFilterMembers("AllCurves", "AllConicCurves");
                case "datum": return ParseFilterMembers("DatumAxis", "DatumPlane", "CoordinateSystem");
                case "feature": return ParseFilterMembers("AllBasicFeatures", "SolidFeature", "CurveFeature", "DatumPlaneFeature", "DatumAxisFeature");
                case "operation": return ParseFilterMembers("CAMOperation");
                default: return Array.Empty<NXOpen.Select.FilterMember>();
            }
        }

        private static NXOpen.Select.FilterMember[] ParseFilterMembers(params string[] names)
        {
            var values = new List<NXOpen.Select.FilterMember>();
            foreach (string name in names ?? Array.Empty<string>())
            {
                try
                {
                    values.Add((NXOpen.Select.FilterMember)Enum.Parse(typeof(NXOpen.Select.FilterMember), name, true));
                }
                catch { }
            }
            return values.Distinct().ToArray();
        }

        private static string SelectionFilterFromCommandId(string commandId)
        {
            string id = (commandId ?? string.Empty).ToUpperInvariant();
            if (id.Contains("DESELECT")) return "none";
            if (id.Contains("SELECT_ALL")) return "all";
            if (id.Contains("RESET")) return "reset";
            if (id.Contains("EDGE")) return "edge";
            if (id.Contains("FACE")) return "face";
            if (id.Contains("BODY")) return "body";
            if (id.Contains("COMPONENT")) return "component";
            if (id.Contains("CURVE")) return "curve";
            if (id.Contains("DATUM")) return "datum";
            if (id.Contains("FEATURE")) return "feature";
            return string.Empty;
        }

        private static string NormalizeSelectionFilter(string value)
        {
            string normalized = new string((value ?? string.Empty).Trim().ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
            while (normalized.Contains("__", StringComparison.Ordinal)) normalized = normalized.Replace("__", "_");
            return normalized.Trim('_');
        }

        private static void ClearGlobalSelectionList()
        {
            try
            {
                theUI.SelectionManager.ClearGlobalSelectionList();
            }
            catch
            {
                ExecuteRelaxedMenuButton("UG_SEL_DESELECT_ALL");
            }
        }

        private static void ExecuteRelaxedMenuButton(string commandId)
        {
            string id = (commandId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id)) return;
            try
            {
                MenuButton button = theUI.MenuBarManager.GetButtonFromName(id);
                if (button != null) theUI.DialogTester.InvokeMenuButtonAction(button);
            }
            catch (Exception ex)
            {
                WriteLog("Relaxed menu action failed for " + id + ": " + ex.Message);
            }
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
            int selectionCount = AskSelectionSnapshot(
                out List<string> selectedTypes, out string selectionFingerprint);
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
                SelectionFingerprint = selectionFingerprint,
                WorkPartAvailable = workPart,
                DisplayPartAvailable = displayPart,
                ModalDialogActive = modal,
                ActiveCommandId = string.Empty,
                ContextConfidence = Math.Min(applicationConfidence, moduleConfidence),
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                LastRequestId = lastRequestId,
                LastResult = lastResult,
                LastMessage = lastMessage,
                SecurityStatus = securityGate?.Status ?? "not_initialized",
                SecuritySessionId = securityGate?.SessionId ?? string.Empty,
                SecurityProfileDigest = securityGate?.ProfileDigest ?? string.Empty
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

        private static int AskSelectionSnapshot(
            out List<string> selectedTypes,
            out string selectionFingerprint)
        {
            selectedTypes = new List<string>();
            selectionFingerprint = string.Empty;
            try
            {
                int count = theUI.SelectionManager.GetNumSelectedObjects();
                int inspected = Math.Min(count, 64);
                var identities = new List<string>(inspected);
                for (int index = 0; index < inspected; index++)
                {
                    object selected = AskSelectedObject(index);
                    AddSelectedType(selectedTypes, selected);
                    identities.Add(SelectedObjectIdentity(selected, index));
                }
                identities.Sort(StringComparer.Ordinal);
                string material = count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                                  string.Join("|", identities);
                using (SHA256 sha256 = SHA256.Create())
                    selectionFingerprint = Convert.ToHexString(
                        sha256.ComputeHash(Encoding.UTF8.GetBytes(material)));
                return count;
            }
            catch (Exception ex)
            {
                WriteLog("AskSelectionSnapshot failed: " + ex.Message);
                selectionFingerprint = string.Empty;
                return -1;
            }
        }

        private static string SelectedObjectIdentity(object selected, int index)
        {
            if (selected == null) return "null@" + index;
            Type type = selected.GetType();
            foreach (string propertyName in new[] { "Tag", "JournalIdentifier", "Name" })
            {
                try
                {
                    PropertyInfo property = type.GetProperty(propertyName);
                    object value = property?.GetValue(selected);
                    string text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(text))
                        return (type.FullName ?? type.Name) + "#" + propertyName + "=" + text;
                }
                catch
                {
                    // Identity probing is best effort; NX Tag is used when available.
                }
            }
            return (type.FullName ?? type.Name) + "#index=" + index;
        }

        private static object AskSelectedObject(int index)
        {
            object manager = theUI?.SelectionManager;
            if (manager == null) return null;
            Type type = manager.GetType();
            foreach (string methodName in new[] { "GetSelectedObject", "GetSelectedTaggedObject" })
            {
                try
                {
                    var method = type.GetMethod(methodName, new[] { typeof(int) });
                    if (method == null) continue;
                    return method.Invoke(manager, new object[] { index });
                }
                catch (Exception ex)
                {
                    Exception targetEx = (ex is TargetInvocationException tie) ? (tie.InnerException ?? tie) : ex;
                    WriteLog("Selection object probe failed for method " + methodName + "(" + index + "): " + targetEx.Message);
                }
            }
            return null;
        }

        private static void AddSelectedType(List<string> selectedTypes, object selected)
        {
            if (selected == null) return;
            for (Type type = selected.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                string typeName = type.FullName;
                if (!string.IsNullOrWhiteSpace(typeName) && !selectedTypes.Contains(typeName, StringComparer.Ordinal))
                    selectedTypes.Add(typeName);
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
