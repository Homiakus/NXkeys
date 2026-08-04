from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, content: str) -> None:
    (ROOT / path).write_text(content, encoding="utf-8", newline="\n")


def replace_once(path: str, old: str, new: str) -> None:
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected one match, found {count}: {old[:160]!r}")
    write(path, text.replace(old, new, 1))


def replace_section(path: str, start_marker: str, end_marker: str, replacement: str) -> None:
    text = read(path)
    start = text.find(start_marker)
    if start < 0:
        raise RuntimeError(f"{path}: start marker not found: {start_marker!r}")
    end = text.find(end_marker, start)
    if end < 0:
        raise RuntimeError(f"{path}: end marker not found: {end_marker!r}")
    write(path, text[:start] + replacement + text[end:])


def append_once(path: str, marker: str, content: str) -> None:
    text = read(path)
    if marker in text:
        return
    write(path, text.rstrip() + "\n\n" + content.rstrip() + "\n")


# ---------------------------------------------------------------------------
# Proper architectural boundaries: Protocol and BridgeCore class libraries.
# ---------------------------------------------------------------------------
protocol_project = ROOT / "NXKeys.Protocol/NXKeys.Protocol.csproj"
if protocol_project.exists():
    raise RuntimeError("NXKeys.Protocol project already exists")
protocol_project.write_text("""<Project Sdk=\"Microsoft.NET.Sdk\">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <Deterministic>true</Deterministic>
    <AssemblyName>NXKeys.Protocol</AssemblyName>
    <RootNamespace>NXKeys.Protocol</RootNamespace>
  </PropertyGroup>
</Project>
""", encoding="utf-8", newline="\n")

bridge_core_dir = ROOT / "NXKeys.BridgeCore"
bridge_core_dir.mkdir(exist_ok=False)
(bridge_core_dir / "NXKeys.BridgeCore.csproj").write_text("""<Project Sdk=\"Microsoft.NET.Sdk\">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <Deterministic>true</Deterministic>
    <AssemblyName>NXKeys.BridgeCore</AssemblyName>
    <RootNamespace>NXKeys.BridgeCore</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=\"..\\NXKeys.Protocol\\NXKeys.Protocol.csproj\" />
  </ItemGroup>
</Project>
""", encoding="utf-8", newline="\n")

(bridge_core_dir / "BridgeSecurityGate.cs").write_text(r'''using System;
using System.Diagnostics;
using System.IO;
using NXKeys.Protocol;

namespace NXKeys.BridgeCore
{
    public sealed class BridgeSecurityGate
    {
        private readonly Action<string> log;
        private readonly object sync = new object();
        private readonly byte[] secret;
        private readonly string profilePath;
        private readonly string expectedClientExecutable;
        private readonly NxReplayGuard replayGuard = new NxReplayGuard();
        private NxBridgePermissionSet permissions;

        public string Status { get; private set; }
        public string SessionId { get; }
        public string ProfileDigest => permissions?.ProfileDigest ?? string.Empty;
        public bool IsAuthenticated => string.Equals(Status, "authenticated", StringComparison.OrdinalIgnoreCase);

        private BridgeSecurityGate(
            string status,
            string sessionId,
            byte[] sessionSecret,
            string securityProfilePath,
            string clientExecutable,
            NxBridgePermissionSet permissionSet,
            Action<string> logger)
        {
            Status = status ?? "authentication_required";
            SessionId = sessionId ?? string.Empty;
            secret = sessionSecret ?? Array.Empty<byte>();
            profilePath = securityProfilePath ?? string.Empty;
            expectedClientExecutable = clientExecutable ?? string.Empty;
            permissions = permissionSet;
            log = logger ?? (_ => { });
        }

        public static BridgeSecurityGate CreateFromEnvironment(Action<string> log = null)
        {
            Action<string> logger = log ?? (_ => { });
            if (!NxBridgeSecurityEnvironment.TryRead(
                    out string sessionId,
                    out byte[] secret,
                    out string profilePath,
                    out string clientExecutable,
                    out string error))
            {
                logger("Secure IPC is unavailable: " + error);
                return new BridgeSecurityGate(
                    "authentication_required", sessionId, secret, profilePath, clientExecutable, null, logger);
            }

            try
            {
                NxBridgePermissionSet permissions = NxBridgePermissionSet.FromProfileFile(profilePath);
                logger("Secure IPC initialized. Profile digest=" + permissions.ProfileDigest);
                return new BridgeSecurityGate(
                    "authenticated", sessionId, secret, profilePath, clientExecutable, permissions, logger);
            }
            catch (Exception exception)
            {
                logger("Secure IPC profile load failed: " + exception.Message);
                return new BridgeSecurityGate(
                    "profile_invalid", sessionId, secret, profilePath, clientExecutable, null, logger);
            }
        }

        public void Validate(NxCommandRequest request)
        {
            if (!IsAuthenticated || permissions == null)
                throw new InvalidOperationException(
                    "NXKeys authenticated session is not ready. Start NX through the managed NXKeys launcher.");

            RefreshProfileIfNeeded(request?.ProfileDigest);
            if (!NxRequestAuthenticator.Verify(
                    request,
                    SessionId,
                    secret,
                    permissions.ProfileDigest,
                    out string authenticationError))
                throw new InvalidOperationException(authenticationError);

            ValidateSourceProcess(request.SourceProcessId);
            if (!permissions.TryGetPermission(request, out NxCommandPermission permission))
                throw new InvalidOperationException("NX command/action is not present in the active profile allowlist.");
            if (request.Destructive != permission.Destructive)
                throw new InvalidOperationException("Request destructive policy differs from the active profile.");
            if (permission.ConfirmationRequired && !request.ConfirmationAccepted)
                throw new InvalidOperationException("Request requires confirmation according to the active profile.");
            if (!replayGuard.TryAccept(request, out string replayError))
                throw new InvalidOperationException(replayError);
        }

        private void RefreshProfileIfNeeded(string requestedDigest)
        {
            if (permissions != null &&
                string.Equals(permissions.ProfileDigest, requestedDigest, StringComparison.OrdinalIgnoreCase)) return;
            lock (sync)
            {
                NxBridgePermissionSet refreshed = NxBridgePermissionSet.FromProfileFile(profilePath);
                if (!string.Equals(refreshed.ProfileDigest, requestedDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Request profile digest does not match the installed NXKeys profile.");
                permissions = refreshed;
                Status = "authenticated";
                log("Secure IPC permission set reloaded. Digest=" + refreshed.ProfileDigest);
            }
        }

        private void ValidateSourceProcess(int processId)
        {
            if (processId <= 0) throw new InvalidOperationException("Request source_process_id is invalid.");
            try
            {
                using (Process source = Process.GetProcessById(processId))
                {
                    string actual = Path.GetFullPath(source.MainModule?.FileName ?? string.Empty);
                    if (!string.Equals(actual, expectedClientExecutable, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "Request source process is not the trusted managed HotkeyStudio executable.");
                }
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException("Request source process is no longer running.");
            }
        }
    }
}
''', encoding="utf-8", newline="\n")

(bridge_core_dir / "BridgeRequestInbox.cs").write_text(r'''using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using NXKeys.Protocol;

namespace NXKeys.BridgeCore
{
    public sealed class BridgeRequestClaim
    {
        public string ProcessingPath { get; }
        public string RequestId { get; }
        public NxCommandRequest Request { get; }

        public BridgeRequestClaim(string processingPath, string requestId, NxCommandRequest request)
        {
            ProcessingPath = processingPath ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }
    }

    public sealed class BridgeRequestRejection
    {
        public string ProcessingPath { get; }
        public string RequestId { get; }
        public string Message { get; }

        public BridgeRequestRejection(string processingPath, string requestId, string message)
        {
            ProcessingPath = processingPath ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public sealed class BridgeRequestInbox : IDisposable
    {
        private readonly string pendingDirectory;
        private readonly string processingDirectory;
        private readonly Func<string, bool> resultExists;
        private readonly Action<string, string> duplicateHandler;
        private readonly Action<NxCommandRequest> admission;
        private readonly Action<string> log;
        private readonly ConcurrentQueue<BridgeRequestClaim> ready = new ConcurrentQueue<BridgeRequestClaim>();
        private readonly ConcurrentQueue<BridgeRequestRejection> rejected = new ConcurrentQueue<BridgeRequestRejection>();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Thread worker;
        private int started;

        public int ReadyCount => ready.Count;
        public int RejectedCount => rejected.Count;
        public bool IsRunning => worker != null && worker.IsAlive && !cancellation.IsCancellationRequested;

        public BridgeRequestInbox(
            string pendingPath,
            string processingPath,
            Func<string, bool> resultExistsCallback,
            Action<string, string> duplicateCallback,
            Action<NxCommandRequest> admissionCallback,
            Action<string> logger = null)
        {
            pendingDirectory = Path.GetFullPath(pendingPath ?? throw new ArgumentNullException(nameof(pendingPath)));
            processingDirectory = Path.GetFullPath(processingPath ?? throw new ArgumentNullException(nameof(processingPath)));
            resultExists = resultExistsCallback ?? (_ => false);
            duplicateHandler = duplicateCallback ?? ((_, _) => { });
            admission = admissionCallback ?? throw new ArgumentNullException(nameof(admissionCallback));
            log = logger ?? (_ => { });
        }

        public void Start()
        {
            if (Interlocked.Exchange(ref started, 1) == 1) return;
            Directory.CreateDirectory(pendingDirectory);
            Directory.CreateDirectory(processingDirectory);
            worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "NXKeys Bridge Inbox"
            };
            worker.Start();
            Signal();
        }

        public void Signal()
        {
            if (!cancellation.IsCancellationRequested) wake.Set();
        }

        public bool TryDequeue(out BridgeRequestClaim claim) => ready.TryDequeue(out claim);
        public bool TryDequeueRejected(out BridgeRequestRejection rejection) => rejected.TryDequeue(out rejection);

        private void WorkerLoop()
        {
            WaitHandle[] handles = { wake, cancellation.Token.WaitHandle };
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    int signaled = WaitHandle.WaitAny(handles, TimeSpan.FromSeconds(1));
                    if (signaled == 1 || cancellation.IsCancellationRequested) break;
                    ScanOnce();
                }
            }
            catch (Exception exception)
            {
                log("Bridge inbox worker stopped after an unexpected error: " + exception);
            }
        }

        private void ScanOnce()
        {
            Directory.CreateDirectory(pendingDirectory);
            Directory.CreateDirectory(processingDirectory);
            string[] files = Directory.GetFiles(pendingDirectory, "*.request.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (files.Length > NxProtocolConstants.MaxPendingRequestCount)
                log("Pending queue exceeds limit: " + files.Length + ". Background admission remains bounded.");

            int capacity = Math.Max(0, NxProtocolConstants.MaxPendingRequestCount - ready.Count);
            foreach (string pendingPath in files.Take(Math.Min(NxProtocolConstants.MaxRequestsPerPoll, capacity)))
                ClaimAndValidate(pendingPath);
        }

        private void ClaimAndValidate(string pendingPath)
        {
            string fileName = Path.GetFileName(pendingPath);
            string requestId = fileName.EndsWith(".request.json", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - ".request.json".Length)
                : Path.GetFileNameWithoutExtension(fileName);

            if (resultExists(requestId))
            {
                duplicateHandler(pendingPath, requestId);
                return;
            }

            string processingPath = Path.Combine(processingDirectory, fileName);
            try
            {
                File.Move(pendingPath, processingPath);
            }
            catch (IOException)
            {
                return;
            }

            try
            {
                long payloadLength = new FileInfo(processingPath).Length;
                if (payloadLength <= 0 || payloadLength > NxProtocolConstants.MaxRequestPayloadBytes)
                    throw new InvalidOperationException(
                        "Request payload size is outside the allowed range: " + payloadLength + " bytes.");
                NxCommandRequest request;
                using (FileStream stream = new FileStream(processingPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    request = JsonSerializer.Deserialize<NxCommandRequest>(stream, NxProtocolJson.ReadOptions);
                if (request == null) throw new InvalidOperationException("Request JSON is empty.");
                request.Validate();
                if (!string.Equals(request.RequestId, requestId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("request_id does not match the file name.");
                admission(request);
                ready.Enqueue(new BridgeRequestClaim(processingPath, requestId, request));
            }
            catch (Exception exception)
            {
                rejected.Enqueue(new BridgeRequestRejection(processingPath, requestId, exception.Message));
            }
        }

        public void Dispose()
        {
            if (cancellation.IsCancellationRequested) return;
            cancellation.Cancel();
            wake.Set();
            try { worker?.Join(2000); } catch { }
            wake.Dispose();
            cancellation.Dispose();
        }
    }
}
''', encoding="utf-8", newline="\n")

# Project references replace linked source.
replace_once(
    "NX2512_HotkeyStudio/NX2512_HotkeyStudio.csproj",
    """  <ItemGroup>
    <Compile Include="..\\NXKeys.Protocol\\NxProtocol.cs" Link="Protocol\\NxProtocol.cs" />
    <Compile Include="..\\NXKeys.Protocol\\NxBridgeSecurity.cs" Link="Protocol\\NxBridgeSecurity.cs" />
    <Compile Include="..\\NXKeys.StateMachines\\LeaderBehaviorProfile.cs" Link="StateMachines\\LeaderBehaviorProfile.cs" />
""",
    """  <ItemGroup>
    <ProjectReference Include="..\\NXKeys.Protocol\\NXKeys.Protocol.csproj" />
    <Compile Include="..\\NXKeys.StateMachines\\LeaderBehaviorProfile.cs" Link="StateMachines\\LeaderBehaviorProfile.cs" />
""",
)
replace_once(
    "NX2512_CommandBridge/NX2512_CommandBridge.csproj",
    """  <ItemGroup>
    <Compile Include="..\\NXKeys.Protocol\\NxProtocol.cs" Link="Protocol\\NxProtocol.cs" />
    <Compile Include="..\\NXKeys.Protocol\\NxBridgeSecurity.cs" Link="Protocol\\NxBridgeSecurity.cs" />
  </ItemGroup>
""",
    """  <ItemGroup>
    <ProjectReference Include="..\\NXKeys.Protocol\\NXKeys.Protocol.csproj" />
    <ProjectReference Include="..\\NXKeys.BridgeCore\\NXKeys.BridgeCore.csproj" />
  </ItemGroup>
""",
)
replace_once(
    "NX2512_HotkeyStudio.Tests/NX2512_HotkeyStudio.Tests.csproj",
    """  <ItemGroup>
    <ProjectReference Include="..\\NX2512_HotkeyStudio\\NX2512_HotkeyStudio.csproj" />
  </ItemGroup>
""",
    """  <ItemGroup>
    <ProjectReference Include="..\\NX2512_HotkeyStudio\\NX2512_HotkeyStudio.csproj" />
    <ProjectReference Include="..\\NXKeys.BridgeCore\\NXKeys.BridgeCore.csproj" />
  </ItemGroup>
""",
)

# ---------------------------------------------------------------------------
# Command Bridge becomes a thin NX-thread dispatcher over background BridgeCore.
# ---------------------------------------------------------------------------
bridge = "NX2512_CommandBridge/Program.cs"
replace_once(bridge, "using NXKeys.Protocol;\n", "using NXKeys.BridgeCore;\nusing NXKeys.Protocol;\n")
replace_once(
    bridge,
    """        private static bool isInitialized;
        private static bool isProcessing;
        private static volatile bool pendingWake = true;
        private static DateTime lastFullPollUtc = DateTime.MinValue;
        private static DateTime lastContextWriteUtc = DateTime.MinValue;
""",
    """        private static bool isInitialized;
        private static bool isProcessing;
        private static DateTime lastContextWriteUtc = DateTime.MinValue;
""",
)
replace_once(
    bridge,
    """        private static string lastMessage = string.Empty;
        private static string securityStatus = "authentication_required";
        private static string securitySessionId = string.Empty;
        private static string securityProfilePath = string.Empty;
        private static string expectedClientExecutable = string.Empty;
        private static byte[] securitySecret = Array.Empty<byte>();
        private static NxBridgePermissionSet securityPermissions;
        private static readonly NxReplayGuard replayGuard = new NxReplayGuard();
        private static readonly object securitySync = new object();
""",
    """        private static string lastMessage = string.Empty;
        private static BridgeSecurityGate securityGate;
        private static BridgeRequestInbox requestInbox;
""",
)
replace_once(
    bridge,
    """                EnsureDirectories();
                InitializeSecurity();
                LoadPreviousContextRevision();
                RecoverInterruptedRequests();

                pollTimer = new Timer { Interval = 150 };
""",
    """                EnsureDirectories();
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
""",
)
replace_once(
    bridge,
    """                pendingWatcher.EnableRaisingEvents = true;

                isInitialized = true;
""",
    """                pendingWatcher.EnableRaisingEvents = true;
                requestInbox.Signal();

                isInitialized = true;
""",
)
replace_once(
    bridge,
    """                listingWindow.WriteLine("Context revision: " + contextRevision);
                listingWindow.WriteLine("Log: " + LogPath);
""",
    """                listingWindow.WriteLine("Context revision: " + contextRevision);
                listingWindow.WriteLine("Security: " + (securityGate?.Status ?? "not_initialized"));
                listingWindow.WriteLine("Admitted queue: " + (requestInbox?.ReadyCount ?? 0));
                listingWindow.WriteLine("Rejected queue: " + (requestInbox?.RejectedCount ?? 0));
                listingWindow.WriteLine("Log: " + LogPath);
""",
)
replace_section(
    bridge,
    "        private static void PollTimerTick(object sender, EventArgs e)\n",
    "        private static void ValidateExpectedContext(NxCommandRequest request, NxContextSnapshot current)\n",
    r'''        private static void PollTimerTick(object sender, EventArgs e)
        {
            if (isProcessing) return;
            bool contextDue = (DateTime.UtcNow - lastContextWriteUtc).TotalSeconds >= 1;

            if (requestInbox != null && requestInbox.TryDequeueRejected(out BridgeRequestRejection rejection))
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

            if (requestInbox != null && requestInbox.TryDequeue(out BridgeRequestClaim claim))
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

''',
)
replace_once(
    bridge,
    """                SecurityStatus = securityStatus,
                SecuritySessionId = securitySessionId,
                SecurityProfileDigest = securityPermissions?.ProfileDigest ?? string.Empty
""",
    """                SecurityStatus = securityGate?.Status ?? "not_initialized",
                SecuritySessionId = securityGate?.SessionId ?? string.Empty,
                SecurityProfileDigest = securityGate?.ProfileDigest ?? string.Empty
""",
)
replace_once(
    bridge,
    """                        security_status = securityStatus,
                        security_session_id = securitySessionId,
                        security_profile_digest = securityPermissions?.ProfileDigest ?? string.Empty
""",
    """                        security_status = securityGate?.Status ?? "not_initialized",
                        security_session_id = securityGate?.SessionId ?? string.Empty,
                        security_profile_digest = securityGate?.ProfileDigest ?? string.Empty,
                        admitted_queue = requestInbox?.ReadyCount ?? 0,
                        rejected_queue = requestInbox?.RejectedCount ?? 0
""",
)

# ---------------------------------------------------------------------------
# Profile-scoped Local single instance and cancellable signal threads.
# ---------------------------------------------------------------------------
program = "NX2512_HotkeyStudio/Program.cs"
replace_once(program, "using System.Linq;\n", "using System.Linq;\nusing System.Security.Cryptography;\nusing System.Text;\n")
replace_once(
    program,
    """        private const string SingleInstanceMutexName = @"Global\\NXKeys_HotkeyStudio_SingleInstance";
        private const string ShowUiEventName = @"Global\\NXKeys_HotkeyStudio_ShowUI";
        private const string ToggleEventName = @"Global\\NXKeys_HotkeyStudio_ToggleEngine";
        private const string StartEventName = @"Global\\NXKeys_HotkeyStudio_StartEngine";

""",
    """        private static string singleInstanceMutexName = string.Empty;
        private static string showUiEventName = string.Empty;
        private static string toggleEventName = string.Empty;
        private static string startEventName = string.Empty;

""",
)
replace_once(
    program,
    """        private static Control uiInvoker;
        private static string activeConfigPath = string.Empty;
""",
    """        private static Control uiInvoker;
        private static string activeConfigPath = string.Empty;
        private static readonly CancellationTokenSource signalCancellation = new CancellationTokenSource();
        private static readonly List<Thread> signalThreads = new List<Thread>();
""",
)
replace_once(
    program,
    """            singleMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                SignalExisting(openGui ? ShowUiEventName : toggle ? ToggleEventName : StartEventName);
                return;
            }

            activeConfigPath = ResolveConfigPath(GetArgValue(args, "--config"));
            Config config = Config.Load(activeConfigPath);
""",
    """            activeConfigPath = ResolveConfigPath(GetArgValue(args, "--config"));
            ConfigureInstanceScope(activeConfigPath);
            singleMutex = new Mutex(true, singleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                SignalExisting(openGui ? showUiEventName : toggle ? toggleEventName : startEventName);
                return;
            }

            Config config = Config.Load(activeConfigPath);
""",
)
replace_once(
    program,
    """            showUiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowUiEventName);
            toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ToggleEventName);
            startEvent = new EventWaitHandle(false, EventResetMode.AutoReset, StartEventName);
""",
    """            showUiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, showUiEventName);
            toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, toggleEventName);
            startEvent = new EventWaitHandle(false, EventResetMode.AutoReset, startEventName);
""",
)
replace_once(
    program,
    """        private static void SetupTrayIcon()
""",
    """        private static void ConfigureInstanceScope(string configPath)
        {
            string normalized = Path.GetFullPath(configPath).ToUpperInvariant();
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
                .Substring(0, 16);
            string scope = "Local\\\\NXKeys_" + digest;
            singleInstanceMutexName = scope + "_HotkeyStudio";
            showUiEventName = scope + "_ShowUI";
            toggleEventName = scope + "_ToggleEngine";
            startEventName = scope + "_StartEngine";
        }

        private static void SetupTrayIcon()
""",
)
replace_section(
    program,
    "        private static void StartSignalThread(EventWaitHandle handle, Action action)\n",
    "        private static void SignalExisting(string eventName)\n",
    r'''        private static void StartSignalThread(EventWaitHandle handle, Action action)
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

''',
)
replace_once(
    program,
    """        private static void Cleanup()
        {
            try { globalEngine?.Stop(); globalEngine?.Dispose(); } catch { }
""",
    """        private static void Cleanup()
        {
            try { signalCancellation.Cancel(); } catch { }
            lock (signalThreads)
            {
                foreach (Thread thread in signalThreads)
                    try { if (thread.IsAlive) thread.Join(500); } catch { }
                signalThreads.Clear();
            }
            try { globalEngine?.Stop(); globalEngine?.Dispose(); } catch { }
""",
)

# ---------------------------------------------------------------------------
# Preserve the user's original CapsLock state instead of forcing it off.
# ---------------------------------------------------------------------------
engine = "NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs"
replace_once(
    engine,
    """        private int capsLockRestoreAttempts;
        private NxBridgeContext currentContext;
""",
    """        private int capsLockRestoreAttempts;
        private bool capsLockStateBeforeTrigger;
        private NxBridgeContext currentContext;
""",
)
replace_once(
    engine,
    """            if (triggerVk != VK_CAPITAL) return;
            capsLockRestoreAttempts = 0;
""",
    """            if (triggerVk != VK_CAPITAL) return;
            capsLockStateBeforeTrigger = IsCapsLockOn();
            capsLockRestoreAttempts = 0;
""",
)
replace_once(
    engine,
    """            capsLockRestoreAttempts++;
            EnsureCapsLockOff();
            if (capsLockRestoreAttempts >= 4 || !IsCapsLockOn()) capsLockRestore.Stop();
        }

        private static bool IsCapsLockOn() => (GetKeyState(VK_CAPITAL) & 0x0001) != 0;

        private static void EnsureCapsLockOff()
        {
            if (!IsCapsLockOn()) return;
            keybd_event(VK_CAPITAL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_CAPITAL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
""",
    """            capsLockRestoreAttempts++;
            EnsureCapsLockState(capsLockStateBeforeTrigger);
            if (capsLockRestoreAttempts >= 4 || IsCapsLockOn() == capsLockStateBeforeTrigger)
                capsLockRestore.Stop();
        }

        private static bool IsCapsLockOn() => (GetKeyState(VK_CAPITAL) & 0x0001) != 0;

        private static void EnsureCapsLockState(bool desiredState)
        {
            if (IsCapsLockOn() == desiredState) return;
            keybd_event(VK_CAPITAL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_CAPITAL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
""",
)

# ---------------------------------------------------------------------------
# Regression tests for the background inbox and architectural project boundary.
# ---------------------------------------------------------------------------
tests = "NX2512_HotkeyStudio.Tests/Program.cs"
replace_once(
    tests,
    """        VerifyAuthenticatedIpc();

        Console.WriteLine("[OK] Canonical profile editor, command menus, Sketch grammar, authenticated IPC and single NX ribbon regressions.");
""",
    """        VerifyAuthenticatedIpc();
        VerifyBridgeInbox();

        Console.WriteLine("[OK] Canonical profile editor, Sketch grammar, authenticated IPC and background Bridge inbox regressions.");
""",
)
inbox_test = r'''
    private static void VerifyBridgeInbox()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "nxkeys-inbox-" + Guid.NewGuid().ToString("N"));
        string pending = Path.Combine(tempRoot, "pending");
        string processing = Path.Combine(tempRoot, "processing");
        Directory.CreateDirectory(pending);
        Directory.CreateDirectory(processing);
        int admitted = 0;
        try
        {
            using (var inbox = new NXKeys.BridgeCore.BridgeRequestInbox(
                pending,
                processing,
                _ => false,
                (_, _) => { },
                _ => System.Threading.Interlocked.Increment(ref admitted)))
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                var request = new NXKeys.Protocol.NxCommandRequest
                {
                    RequestId = "inbox-test",
                    Action = NXKeys.Protocol.NxProtocolActions.ExecuteCommand,
                    CommandId = "UG_TEST",
                    CommandName = "Inbox test",
                    ModuleId = "modeling",
                    CreatedUtc = now.ToString("O"),
                    ExpiresUtc = now.AddMinutes(1).ToString("O"),
                    ConfirmationAccepted = true
                };
                string requestPath = Path.Combine(pending, request.RequestId + ".request.json");
                File.WriteAllText(requestPath,
                    System.Text.Json.JsonSerializer.Serialize(request, NXKeys.Protocol.NxProtocolJson.WriteOptions));
                inbox.Start();
                inbox.Signal();

                NXKeys.BridgeCore.BridgeRequestClaim claim = null;
                bool claimed = System.Threading.SpinWait.SpinUntil(
                    () => inbox.TryDequeue(out claim), TimeSpan.FromSeconds(5));
                Assert(claimed && claim != null, "Background inbox must claim a valid request.");
                Assert(claim.RequestId == request.RequestId && File.Exists(claim.ProcessingPath),
                    "Claimed request must be atomically moved to processing.");
                Assert(admitted == 1, "Admission callback must run exactly once.");

                string oversizedId = "oversized";
                File.WriteAllText(
                    Path.Combine(pending, oversizedId + ".request.json"),
                    new string('X', NXKeys.Protocol.NxProtocolConstants.MaxRequestPayloadBytes + 1));
                inbox.Signal();
                NXKeys.BridgeCore.BridgeRequestRejection rejection = null;
                bool rejected = System.Threading.SpinWait.SpinUntil(
                    () => inbox.TryDequeueRejected(out rejection), TimeSpan.FromSeconds(5));
                Assert(rejected && rejection != null && rejection.RequestId == oversizedId,
                    "Oversized requests must be rejected off the NX UI thread.");
            }
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

'''
replace_once(tests, "    private static void VerifyAuthenticatedIpc()\n", inbox_test + "    private static void VerifyAuthenticatedIpc()\n")

# ---------------------------------------------------------------------------
# CI and documentation.
# ---------------------------------------------------------------------------
workflow = ".github/workflows/runtime-hardening.yml"
text = read(workflow)
text = text.replace("      - 'NXKeys.Protocol/**'\n", "      - 'NXKeys.Protocol/**'\n      - 'NXKeys.BridgeCore/**'\n")
text = text.replace(
    "          $security = Get-Content .\\NXKeys.Protocol\\NxBridgeSecurity.cs -Raw\n",
    "          $security = Get-Content .\\NXKeys.Protocol\\NxBridgeSecurity.cs -Raw\n          $gate = Get-Content .\\NXKeys.BridgeCore\\BridgeSecurityGate.cs -Raw\n          $inbox = Get-Content .\\NXKeys.BridgeCore\\BridgeRequestInbox.cs -Raw\n",
)
text = text.replace(
    "          foreach ($required in @(\n            'NxRequestAuthenticator',\n",
    "          foreach ($required in @('BridgeSecurityGate', 'ValidateSourceProcess', 'NxReplayGuard')) {\n            if ($gate -notmatch [regex]::Escape($required)) {\n              throw \"Missing BridgeCore security invariant: $required\"\n            }\n          }\n          foreach ($required in @('BridgeRequestInbox', 'AutoResetEvent', 'MaxRequestsPerPoll', 'TryDequeueRejected')) {\n            if ($inbox -notmatch [regex]::Escape($required)) {\n              throw \"Missing background inbox invariant: $required\"\n            }\n          }\n          foreach ($required in @(\n            'NxRequestAuthenticator',\n",
)
text = text.replace(
    "            'ValidateSourceProcess',\n            'NxRequestAuthenticator.Verify',\n            'replayGuard.TryAccept'\n",
    "            'BridgeRequestInbox',\n            'ProcessClaim'\n",
)
write(workflow, text)

append_once(
    "docs/ARCHITECTURE.md",
    "## BridgeCore and bounded NX UI thread",
    """## BridgeCore and bounded NX UI thread

`NXKeys.Protocol` is now a real class library rather than linked source. `NXKeys.BridgeCore` owns
transport admission that does not require NXOpen:

- authenticated session and profile permission validation;
- source process verification and replay protection;
- background enumeration, atomic claim, payload parsing and rejection;
- bounded ready/rejected queues.

The NX-loaded assembly is now the UI-thread adapter. Its WinForms timer dequeues at most one
admitted request per tick and performs only current-context validation plus the actual NX call.
Directory enumeration, JSON parsing, HMAC verification and process inspection no longer execute on
the NX UI thread.

```mermaid
flowchart LR
    Files[(pending files)] --> Inbox[BridgeRequestInbox background thread]
    Inbox --> Security[BridgeSecurityGate]
    Security --> Ready[(bounded admitted queue)]
    Ready -->|one request per tick| Adapter[NX CommandBridge UI thread]
    Adapter --> NX[Siemens NX]
```
""",
)
append_once(
    "docs/NX_PLUGIN_FRAGILITY_ARCHITECTURE_UI_AUDIT.md",
    "## 19. Статус реализации архитектурной фазы",
    """## 19. Статус реализации архитектурной фазы

Реализованы следующие пункты аудита:

- `NXK-FR-006` — security/admission и inbox вынесены из static NX entrypoint в `NXKeys.BridgeCore`;
- `NXK-FR-007` — filesystem scan, claim, JSON и HMAC выполняются background worker; NX UI thread получает один admitted request за tick;
- `NXK-FR-014` — `NXKeys.Protocol` стал отдельной class library, linked-source dependency удалена из основных executables;
- `NXK-FR-016` — single instance использует `Local\\` и digest активного профиля;
- `NXK-FR-017` — instance signal threads получили cancellation и bounded join;
- `NXK-FR-018` — Leader восстанавливает исходное состояние CapsLock, а не принудительно выключает его.
""",
)
replace_once(
    "CHANGELOG.md",
    """### Security

- IPC повышен до schema 4: ephemeral 256-bit launch capability и HMAC-SHA-256 для каждого request;
""",
    """### Architecture

- добавлены отдельные class libraries `NXKeys.Protocol` и `NXKeys.BridgeCore`;
- filesystem admission вынесен с NX UI thread в bounded background inbox;
- NX adapter исполняет не более одного admitted request за UI tick;
- single-instance scope привязан к local session и активному профилю;
- signal threads получили cancellation, а CapsLock сохраняет исходное состояние пользователя.

### Security

- IPC повышен до schema 4: ephemeral 256-bit launch capability и HMAC-SHA-256 для каждого request;
""",
)

print("BridgeCore architecture migration applied successfully.")
