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
        raise RuntimeError(f"{path}: expected one match, found {count}: {old[:120]!r}")
    write(path, text.replace(old, new, 1))


def append_once(path: str, marker: str, content: str) -> None:
    text = read(path)
    if marker in text:
        return
    write(path, text.rstrip() + "\n\n" + content.rstrip() + "\n")


# ---------------------------------------------------------------------------
# Shared protocol: strict actions, bounded payloads and selection fingerprint.
# ---------------------------------------------------------------------------
protocol = "NXKeys.Protocol/NxProtocol.cs"
replace_once(
    protocol,
    """    public static class NxProtocolConstants
    {
        public const int SchemaVersion = 3;
        public static readonly TimeSpan DefaultContextFreshness = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan DefaultRequestLifetime = TimeSpan.FromSeconds(15);
    }

    public class NxCommandRequest
""",
    """    public static class NxProtocolConstants
    {
        public const int SchemaVersion = 3;
        public const int MaxRequestPayloadBytes = 64 * 1024;
        public const int MaxPendingRequestCount = 256;
        public const int MaxRequestsPerPoll = 8;
        public const int MaxTextFieldLength = 1024;
        public static readonly TimeSpan DefaultContextFreshness = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan DefaultRequestLifetime = TimeSpan.FromSeconds(15);
    }

    public static class NxProtocolActions
    {
        public const string ExecuteCommand = "execute_command";
        public const string SwitchModule = "switch_module";
        public const string SetSelectionFilter = "set_selection_filter";
        public const string ProbeCommand = "probe_command";

        public static bool IsSupported(string action)
        {
            return string.Equals(action, ExecuteCommand, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, SwitchModule, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, SetSelectionFilter, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, ProbeCommand, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class NxCommandRequest
""",
)
replace_once(
    protocol,
    """        [JsonPropertyName("expected_selection_count")]
        public int ExpectedSelectionCount { get; set; } = -1;

        [JsonPropertyName("expected_application_id")]
""",
    """        [JsonPropertyName("expected_selection_count")]
        public int ExpectedSelectionCount { get; set; } = -1;

        [JsonPropertyName("expected_selection_fingerprint")]
        public string ExpectedSelectionFingerprint { get; set; } = string.Empty;

        [JsonPropertyName("expected_application_id")]
""",
)
replace_once(
    protocol,
    """            if (string.IsNullOrWhiteSpace(Action))
                throw new InvalidOperationException("action is required.");
            if (!string.Equals(Action, "switch_module", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(CommandId))
""",
    """            if (string.IsNullOrWhiteSpace(Action))
                throw new InvalidOperationException("action is required.");
            if (!NxProtocolActions.IsSupported(Action))
                throw new InvalidOperationException("Unsupported NXKeys action: " + Action);
            RequireMaxLength(nameof(RequestId), RequestId);
            RequireMaxLength(nameof(Action), Action);
            RequireMaxLength(nameof(CommandId), CommandId);
            RequireMaxLength(nameof(CommandName), CommandName);
            RequireMaxLength(nameof(Sequence), Sequence);
            RequireMaxLength(nameof(ModuleId), ModuleId);
            RequireMaxLength(nameof(TargetApplicationId), TargetApplicationId);
            RequireMaxLength(nameof(SelectionFilter), SelectionFilter);
            RequireMaxLength(nameof(ExpectedApplicationId), ExpectedApplicationId);
            RequireMaxLength(nameof(ExpectedSelectionFingerprint), ExpectedSelectionFingerprint);
            if (!string.Equals(Action, NxProtocolActions.SwitchModule, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(CommandId))
""",
)
replace_once(
    protocol,
    """            if (Destructive && !ConfirmationAccepted)
                throw new InvalidOperationException("Destructive request has no explicit confirmation.");
        }
    }

    public class NxContextSnapshot
""",
    """            if (Destructive && !ConfirmationAccepted)
                throw new InvalidOperationException("Destructive request has no explicit confirmation.");
        }

        private static void RequireMaxLength(string field, string value)
        {
            if ((value ?? string.Empty).Length > NxProtocolConstants.MaxTextFieldLength)
                throw new InvalidOperationException(field + " exceeds the protocol length limit.");
        }
    }

    public class NxContextSnapshot
""",
)
replace_once(
    protocol,
    """        [JsonPropertyName("selected_types")]
        public List<string> SelectedTypes { get; set; } = new List<string>();

        [JsonPropertyName("work_part_available")]
""",
    """        [JsonPropertyName("selected_types")]
        public List<string> SelectedTypes { get; set; } = new List<string>();

        [JsonPropertyName("selection_fingerprint")]
        public string SelectionFingerprint { get; set; } = string.Empty;

        [JsonPropertyName("work_part_available")]
""",
)
replace_once(
    protocol,
    """                SelectionState ?? string.Empty,
                WorkPartAvailable ? "1" : "0",
""",
    """                SelectionState ?? string.Empty,
                SelectionFingerprint ?? string.Empty,
                WorkPartAvailable ? "1" : "0",
""",
)

# ---------------------------------------------------------------------------
# Profile loading and saving: fail closed on schema, write atomically.
# ---------------------------------------------------------------------------
config = "NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs"
replace_once(
    config,
    """            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8)) json = reader.ReadToEnd();
            Config config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
""",
    """            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8)) json = reader.ReadToEnd();
            ValidateSourceSchemaVersion(json);
            Config config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
""",
)
replace_once(
    config,
    """        public void Save(string path)
        {
""",
    """        private static void ValidateSourceSchemaVersion(string json)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                }))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object ||
                        !document.RootElement.TryGetProperty("schema_version", out JsonElement schemaElement) ||
                        !schemaElement.TryGetInt32(out int sourceVersion))
                        throw new InvalidOperationException("Configuration schema_version is required and must be an integer.");
                    if (sourceVersion < MinimumSupportedSchemaVersion || sourceVersion > CurrentSchemaVersion)
                        throw new InvalidOperationException(
                            $"Unsupported configuration schema_version {sourceVersion}. Supported range is " +
                            $"{MinimumSupportedSchemaVersion}..{CurrentSchemaVersion}.");
                }
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Configuration JSON is invalid: " + exception.Message, exception);
            }
        }

        public void Save(string path)
        {
""",
)
replace_once(
    config,
    """            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, json, new UTF8Encoding(false));
""",
    """            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            NX2512_HotkeyStudio.Services.AtomicFileWriter.WriteAllText(
                path, json, true, new UTF8Encoding(false));
""",
)
replace_once(
    config,
    """            if (SchemaVersion < MinimumSupportedSchemaVersion || SchemaVersion > CurrentSchemaVersion)
                SchemaVersion = CurrentSchemaVersion;
""",
    """            if (SchemaVersion < MinimumSupportedSchemaVersion || SchemaVersion > CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Unsupported configuration schema_version {SchemaVersion}. Supported range is " +
                    $"{MinimumSupportedSchemaVersion}..{CurrentSchemaVersion}.");
""",
)

# ---------------------------------------------------------------------------
# Typed transport reads, testable bridge root and bounded client queue.
# ---------------------------------------------------------------------------
transport_type = ROOT / "NX2512_HotkeyStudio/Services/NxTransportReadResult.cs"
if transport_type.exists():
    raise RuntimeError("NxTransportReadResult.cs already exists")
transport_type.write_text(
    """using System;

namespace NX2512_HotkeyStudio.Services
{
    public enum NxTransportReadStatus
    {
        Success,
        NotFound,
        InvalidRequest,
        Corrupt,
        SchemaMismatch,
        AccessDenied,
        IoError
    }

    public sealed class NxTransportReadResult<T> where T : class
    {
        public NxTransportReadStatus Status { get; }
        public T Value { get; }
        public string Message { get; }
        public bool IsSuccess => Status == NxTransportReadStatus.Success && Value != null;

        private NxTransportReadResult(NxTransportReadStatus status, T value, string message)
        {
            Status = status;
            Value = value;
            Message = message ?? string.Empty;
        }

        public static NxTransportReadResult<T> Success(T value) =>
            new NxTransportReadResult<T>(NxTransportReadStatus.Success, value, string.Empty);

        public static NxTransportReadResult<T> Failure(NxTransportReadStatus status, string message) =>
            new NxTransportReadResult<T>(status, null, message);
    }
}
""",
    encoding="utf-8",
    newline="\n",
)

client = "NX2512_HotkeyStudio/Services/NxCommandBridgeClient.cs"
replace_once(
    client,
    """        public static string BridgeRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NXKeys",
            "bridge");
""",
    """        public static string BridgeRoot
        {
            get
            {
                string overrideRoot = Environment.GetEnvironmentVariable("NXKEYS_BRIDGE_ROOT");
                return string.IsNullOrWhiteSpace(overrideRoot)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "bridge")
                    : Path.GetFullPath(overrideRoot);
            }
        }
""",
)
old_reads = """        public static NxBridgeContext ReadContext()
        {
            try
            {
                if (!File.Exists(ContextPath)) return null;
                using (FileStream stream = new FileStream(ContextPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    return JsonSerializer.Deserialize<NxBridgeContext>(stream, NxProtocolJson.ReadOptions);
                }
            }
            catch
            {
                return null;
            }
        }

        public static bool TryReadResult(string requestId, out NxBridgeResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(requestId)) return false;
            foreach (string directory in new[] { CompletedDirectory, FailedDirectory })
            {
                string path = Path.Combine(directory, requestId + ".result.json");
                if (!File.Exists(path)) continue;
                try
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        result = JsonSerializer.Deserialize<NxBridgeResult>(stream, NxProtocolJson.ReadOptions);
                    }
                    return result != null;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

"""
new_reads = """        public static NxTransportReadResult<NxBridgeContext> ReadContextDetailed()
        {
            if (!File.Exists(ContextPath))
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.NotFound, "Bridge context file was not found.");
            try
            {
                using (FileStream stream = new FileStream(ContextPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    NxBridgeContext context = JsonSerializer.Deserialize<NxBridgeContext>(stream, NxProtocolJson.ReadOptions);
                    if (context == null)
                        return NxTransportReadResult<NxBridgeContext>.Failure(
                            NxTransportReadStatus.Corrupt, "Bridge context JSON is empty.");
                    if (context.SchemaVersion != NxProtocolConstants.SchemaVersion)
                        return NxTransportReadResult<NxBridgeContext>.Failure(
                            NxTransportReadStatus.SchemaMismatch,
                            "Unsupported Bridge context schema: " + context.SchemaVersion + ".");
                    return NxTransportReadResult<NxBridgeContext>.Success(context);
                }
            }
            catch (JsonException exception)
            {
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.Corrupt, exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.AccessDenied, exception.Message);
            }
            catch (IOException exception)
            {
                return NxTransportReadResult<NxBridgeContext>.Failure(
                    NxTransportReadStatus.IoError, exception.Message);
            }
        }

        public static NxBridgeContext ReadContext()
        {
            NxTransportReadResult<NxBridgeContext> read = ReadContextDetailed();
            return read.IsSuccess ? read.Value : null;
        }

        public static NxTransportReadResult<NxBridgeResult> ReadResultDetailed(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return NxTransportReadResult<NxBridgeResult>.Failure(
                    NxTransportReadStatus.InvalidRequest, "requestId is required.");
            foreach (string directory in new[] { CompletedDirectory, FailedDirectory })
            {
                string path = Path.Combine(directory, requestId + ".result.json");
                if (!File.Exists(path)) continue;
                try
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        NxBridgeResult result = JsonSerializer.Deserialize<NxBridgeResult>(stream, NxProtocolJson.ReadOptions);
                        if (result == null)
                            return NxTransportReadResult<NxBridgeResult>.Failure(
                                NxTransportReadStatus.Corrupt, "Bridge result JSON is empty.");
                        if (result.SchemaVersion != NxProtocolConstants.SchemaVersion)
                            return NxTransportReadResult<NxBridgeResult>.Failure(
                                NxTransportReadStatus.SchemaMismatch,
                                "Unsupported Bridge result schema: " + result.SchemaVersion + ".");
                        return NxTransportReadResult<NxBridgeResult>.Success(result);
                    }
                }
                catch (JsonException exception)
                {
                    return NxTransportReadResult<NxBridgeResult>.Failure(
                        NxTransportReadStatus.Corrupt, exception.Message);
                }
                catch (UnauthorizedAccessException exception)
                {
                    return NxTransportReadResult<NxBridgeResult>.Failure(
                        NxTransportReadStatus.AccessDenied, exception.Message);
                }
                catch (IOException exception)
                {
                    return NxTransportReadResult<NxBridgeResult>.Failure(
                        NxTransportReadStatus.IoError, exception.Message);
                }
            }
            return NxTransportReadResult<NxBridgeResult>.Failure(
                NxTransportReadStatus.NotFound, "Bridge result file was not found.");
        }

        public static bool TryReadResult(string requestId, out NxBridgeResult result)
        {
            NxTransportReadResult<NxBridgeResult> read = ReadResultDetailed(requestId);
            result = read.IsSuccess ? read.Value : null;
            return read.IsSuccess;
        }

"""
replace_once(client, old_reads, new_reads)
replace_once(
    client,
    """        private static NxBridgeContext RequireFreshContext()
        {
            NxBridgeContext context = ReadContext();
            if (context == null)
                throw new InvalidOperationException("NXKeys Bridge не загружен: в NX нажмите Start NXKeys Bridge, затем повторите команду.");
""",
    """        private static NxBridgeContext RequireFreshContext()
        {
            NxTransportReadResult<NxBridgeContext> read = ReadContextDetailed();
            if (!read.IsSuccess)
                throw new InvalidOperationException(
                    "NXKeys Bridge context недоступен [" + read.Status + "]: " + read.Message);
            NxBridgeContext context = read.Value;
""",
)
replace_once(
    client,
    """                ExpectedContextRevision = context?.Revision ?? 0,
                ExpectedSelectionCount = context?.SelectionCount ?? -1,
                ExpectedApplicationId = context?.ApplicationId ?? string.Empty
""",
    """                ExpectedContextRevision = context?.Revision ?? 0,
                ExpectedSelectionCount = context?.SelectionCount ?? -1,
                ExpectedSelectionFingerprint = context?.SelectionFingerprint ?? string.Empty,
                ExpectedApplicationId = context?.ApplicationId ?? string.Empty
""",
)
replace_once(
    client,
    """            string finalPath = Path.Combine(PendingDirectory, request.RequestId + ".request.json");
            string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(request, NxProtocolJson.WriteOptions);
            try
""",
    """            int pendingCount = Directory.GetFiles(PendingDirectory, "*.request.json").Length;
            if (pendingCount >= NxProtocolConstants.MaxPendingRequestCount)
                throw new InvalidOperationException(
                    "NXKeys Bridge queue limit reached: " + pendingCount + ".");

            string finalPath = Path.Combine(PendingDirectory, request.RequestId + ".request.json");
            string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(request, NxProtocolJson.WriteOptions);
            if (payload.Length > NxProtocolConstants.MaxRequestPayloadBytes)
                throw new InvalidOperationException(
                    "NXKeys request payload exceeds " + NxProtocolConstants.MaxRequestPayloadBytes + " bytes.");
            try
""",
)

# ---------------------------------------------------------------------------
# NX-loaded bridge: bounded poll, explicit dispatch and selection TOCTOU guard.
# ---------------------------------------------------------------------------
bridge = "NX2512_CommandBridge/Program.cs"
replace_once(
    bridge,
    """using System.Runtime.InteropServices;
using System.Text.Json;
""",
    """using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
""",
)
replace_once(
    bridge,
    """        private static void ProcessPendingRequests()
        {
            EnsureDirectories();
            string[] files = Directory.GetFiles(PendingDirectory, "*.request.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files) ProcessRequestFile(file);
        }
""",
    """        private static void ProcessPendingRequests()
        {
            EnsureDirectories();
            string[] files = Directory.GetFiles(PendingDirectory, "*.request.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (files.Length > NxProtocolConstants.MaxPendingRequestCount)
                WriteLog("Pending queue exceeds limit: " + files.Length + ". Processing remains bounded.");
            foreach (string file in files.Take(NxProtocolConstants.MaxRequestsPerPoll))
                ProcessRequestFile(file);
        }
""",
)
replace_once(
    bridge,
    """            NxCommandRequest request = null;
            try
            {
                using (FileStream stream = new FileStream(processingPath, FileMode.Open, FileAccess.Read, FileShare.None))
""",
    """            NxCommandRequest request = null;
            try
            {
                long payloadLength = new FileInfo(processingPath).Length;
                if (payloadLength <= 0 || payloadLength > NxProtocolConstants.MaxRequestPayloadBytes)
                    throw new InvalidOperationException(
                        "Request payload size is outside the allowed range: " + payloadLength + " bytes.");
                using (FileStream stream = new FileStream(processingPath, FileMode.Open, FileAccess.Read, FileShare.None))
""",
)
replace_once(
    bridge,
    """                else
                {
                    ExecuteNxCommand(request);
                    NxContextSnapshot after = BuildCurrentContext();
                    CompleteClaim(processingPath, request, "executed", "OK", after.Revision);
                }
""",
    """                else if (string.Equals(request.Action, NxProtocolActions.ExecuteCommand, StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteNxCommand(request);
                    NxContextSnapshot after = BuildCurrentContext();
                    CompleteClaim(processingPath, request, "executed", "OK", after.Revision);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported NXKeys action: " + request.Action);
                }
""",
)
replace_once(
    bridge,
    """            if (request.ExpectedSelectionCount >= 0 && current.SelectionCount != request.ExpectedSelectionCount)
                throw new InvalidOperationException(
                    "NX selection changed after the shortcut was accepted. Expected " +
                    request.ExpectedSelectionCount + ", actual " + current.SelectionCount + ".");
            if (!string.IsNullOrWhiteSpace(request.ExpectedApplicationId) &&
""",
    """            if (request.ExpectedSelectionCount >= 0 && current.SelectionCount != request.ExpectedSelectionCount)
                throw new InvalidOperationException(
                    "NX selection changed after the shortcut was accepted. Expected " +
                    request.ExpectedSelectionCount + ", actual " + current.SelectionCount + ".");
            if (!string.IsNullOrWhiteSpace(request.ExpectedSelectionFingerprint) &&
                !string.Equals(current.SelectionFingerprint, request.ExpectedSelectionFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "NX selected objects changed after the shortcut was accepted.");
            if (!string.IsNullOrWhiteSpace(request.ExpectedApplicationId) &&
""",
)
replace_once(
    bridge,
    """            int selectionCount = AskSelectionCount(out List<string> selectedTypes);
            bool workPart = AskWorkPartAvailable();
""",
    """            int selectionCount = AskSelectionSnapshot(
                out List<string> selectedTypes, out string selectionFingerprint);
            bool workPart = AskWorkPartAvailable();
""",
)
replace_once(
    bridge,
    """                SelectionState = selectionCount < 0 ? "unknown" : selectionCount == 0 ? "none" : selectionCount == 1 ? "single" : "multiple",
                SelectedTypes = selectedTypes,
                WorkPartAvailable = workPart,
""",
    """                SelectionState = selectionCount < 0 ? "unknown" : selectionCount == 0 ? "none" : selectionCount == 1 ? "single" : "multiple",
                SelectedTypes = selectedTypes,
                SelectionFingerprint = selectionFingerprint,
                WorkPartAvailable = workPart,
""",
)
old_selection = """        private static int AskSelectionCount(out List<string> selectedTypes)
        {
            selectedTypes = new List<string>();
            try
            {
                int count = theUI.SelectionManager.GetNumSelectedObjects();
                int inspected = Math.Min(count, 64);
                for (int index = 0; index < inspected; index++)
                {
                    object selected = AskSelectedObject(index);
                    AddSelectedType(selectedTypes, selected);
                }
                return count;
            }
            catch (Exception ex)
            {
                WriteLog("AskSelectionCount failed: " + ex.Message);
                return -1;
            }
        }
"""
new_selection = """        private static int AskSelectionSnapshot(
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
"""
replace_once(bridge, old_selection, new_selection)

# ---------------------------------------------------------------------------
# Regression tests for phase-zero invariants.
# ---------------------------------------------------------------------------
tests = "NX2512_HotkeyStudio.Tests/Program.cs"
replace_once(
    tests,
    """        VerifySketchIntentGrammar();

        Console.WriteLine("[OK] Canonical profile editor, command menus, Sketch intent grammar and single NX ribbon regressions.");
""",
    """        VerifySketchIntentGrammar();
        VerifyPhaseZeroHardening();

        Console.WriteLine("[OK] Canonical profile editor, command menus, Sketch grammar, runtime hardening and single NX ribbon regressions.");
""",
)
phase_tests = r'''
    private static void VerifyPhaseZeroHardening()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = new NXKeys.Protocol.NxCommandRequest
        {
            RequestId = "phase-zero-test",
            Action = "unexpected_action",
            CommandId = "UG_TEST",
            CreatedUtc = now.ToString("O"),
            ExpiresUtc = now.AddMinutes(1).ToString("O"),
            ConfirmationAccepted = true
        };
        AssertThrows<InvalidOperationException>(() => request.Validate(),
            "Unknown protocol actions must be rejected fail-closed.");

        request.Action = NXKeys.Protocol.NxProtocolActions.ExecuteCommand;
        request.Validate();
        request.CommandName = new string('X', NXKeys.Protocol.NxProtocolConstants.MaxTextFieldLength + 1);
        AssertThrows<InvalidOperationException>(() => request.Validate(),
            "Oversized protocol fields must be rejected.");

        var firstContext = new NXKeys.Protocol.NxContextSnapshot
        {
            Status = "running",
            ModuleId = "modeling",
            SelectionCount = 1,
            SelectionState = "single",
            SelectionFingerprint = "AAA"
        };
        var secondContext = new NXKeys.Protocol.NxContextSnapshot
        {
            Status = "running",
            ModuleId = "modeling",
            SelectionCount = 1,
            SelectionState = "single",
            SelectionFingerprint = "BBB"
        };
        Assert(firstContext.SemanticFingerprint() != secondContext.SemanticFingerprint(),
            "Selection identity must participate in the semantic context revision.");

        string sourceConfig = FindRepositoryFile(Path.Combine("config", "nx2512-pro-hybrid.json"));
        string sourceJson = File.ReadAllText(sourceConfig);
        string futureJson = sourceJson.Replace("\"schema_version\": 6", "\"schema_version\": 999");
        Assert(futureJson != sourceJson, "Test profile schema marker was not found.");

        string tempRoot = Path.Combine(Path.GetTempPath(), "nxkeys-phase-zero-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            string futurePath = Path.Combine(tempRoot, "future.json");
            File.WriteAllText(futurePath, futureJson);
            AssertThrows<InvalidOperationException>(() => Config.Load(futurePath),
                "Future profile schema must be rejected before migration.");

            Config loaded = Config.Load(sourceConfig);
            string savedPath = Path.Combine(tempRoot, "saved.json");
            loaded.Save(savedPath);
            Config roundTrip = Config.Load(savedPath);
            Assert(roundTrip.SchemaVersion == Config.CurrentSchemaVersion,
                "Atomic profile save must produce a readable current-schema profile.");
            Assert(!Directory.EnumerateFiles(tempRoot, ".nxkeys-*.tmp").Any(),
                "Atomic profile save must not leave temporary files.");

            string previousBridgeRoot = Environment.GetEnvironmentVariable("NXKEYS_BRIDGE_ROOT");
            string isolatedBridgeRoot = Path.Combine(tempRoot, "bridge");
            Environment.SetEnvironmentVariable("NXKEYS_BRIDGE_ROOT", isolatedBridgeRoot);
            try
            {
                Directory.CreateDirectory(isolatedBridgeRoot);
                File.WriteAllText(Path.Combine(isolatedBridgeRoot, "context.json"), "{ broken json");
                NxTransportReadResult<NxBridgeContext> read = NxCommandBridgeClient.ReadContextDetailed();
                Assert(read.Status == NxTransportReadStatus.Corrupt,
                    "Corrupt context must be distinguishable from an offline Bridge.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("NXKEYS_BRIDGE_ROOT", previousBridgeRoot);
            }
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static string FindRepositoryFile(string relativePath)
    {
        string current = Directory.GetCurrentDirectory();
        for (int depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            string candidate = Path.Combine(current, relativePath);
            if (File.Exists(candidate)) return candidate;
            current = Path.GetDirectoryName(current);
        }
        throw new FileNotFoundException("Repository file was not found.", relativePath);
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

'''
replace_once(
    tests,
    """

    private static void VerifySketchIntentGrammar()
""",
    "\n" + phase_tests + "    private static void VerifySketchIntentGrammar()\n",
)

# ---------------------------------------------------------------------------
# Documentation and traceability.
# ---------------------------------------------------------------------------
append_once(
    "docs/api.md",
    "## Runtime hardening limits",
    """## Runtime hardening limits

Protocol schema 3 now rejects unknown `action` values fail-closed. Supported actions are
`execute_command`, `switch_module`, `set_selection_filter`, and `probe_command`.

The transport enforces:

- request payload up to 64 KiB;
- at most 256 pending request files;
- at most 8 requests admitted per Bridge poll;
- text fields up to 1024 characters;
- exact schema checks for context and result reads;
- typed read states: `NotFound`, `Corrupt`, `SchemaMismatch`, `AccessDenied`, and `IoError`;
- `expected_selection_fingerprint` verification immediately before NX invocation.

These controls reduce accidental and malformed input. They do not authenticate the local sender;
session capability/HMAC or a protected named pipe remains the next security phase.
""",
)
append_once(
    "docs/SAFETY_MODEL.md",
    "## Phase-zero runtime hardening",
    """## Phase-zero runtime hardening

The first remediation phase closes the immediate fail-open paths:

- unsupported protocol actions are rejected rather than dispatched as NX commands;
- profile schemas outside the known migration range are rejected before defaults are applied;
- profile saves use the existing atomic writer;
- queue depth, payload size, field length, and work per poll are bounded;
- the selected-object fingerprint participates in context revision and dispatch validation;
- transport read failures are classified instead of being collapsed into `null`.

The file queue is still a same-user local trust boundary, not authenticated IPC. Command allowlisting,
session capabilities and anti-replay protection remain mandatory before the bridge is treated as a
hardened production boundary.
""",
)
append_once(
    "docs/NX_PLUGIN_FRAGILITY_ARCHITECTURE_UI_AUDIT.md",
    "## 17. Статус реализации фазы 0",
    """## 17. Статус реализации фазы 0

Реализованы первичные меры снижения хрупкости:

- `NXK-FR-002` — неизвестные protocol actions теперь отклоняются fail-closed;
- `NXK-FR-003` — добавлен selection fingerprint в context revision и повторную проверку Bridge;
- `NXK-FR-004` — загрузка неподдерживаемой schema отклоняется до migration/defaults;
- `NXK-FR-005` — профиль сохраняется через `AtomicFileWriter`;
- частично `NXK-FR-008` — введены лимиты payload, pending queue и requests per poll;
- `NXK-FR-010` — добавлены typed transport read results.

`NXK-FR-001` и `NXK-FR-009` остаются открытыми: файловый IPC пока не аутентифицирует sender,
а Bridge ещё не проверяет подписанный allowlist установленного профиля. Это следующий обязательный этап.
""",
)
replace_once(
    "CHANGELOG.md",
    """### Fixed

- universal selection normalization сохраняет catalog traceability заменяемой команды;
""",
    """### Fixed

- protocol actions проверяются fail-closed; неизвестное действие больше не может попасть в обычный NX dispatch;
- profile schema проверяется до migration, а сохранение выполняется атомарно;
- IPC получил typed read errors, payload/queue limits и selection fingerprint против TOCTOU;
- universal selection normalization сохраняет catalog traceability заменяемой команды;
""",
)

print("Phase-zero runtime hardening applied successfully.")
