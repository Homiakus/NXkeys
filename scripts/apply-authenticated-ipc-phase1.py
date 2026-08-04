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


def append_once(path: str, marker: str, content: str) -> None:
    text = read(path)
    if marker in text:
        return
    write(path, text.rstrip() + "\n\n" + content.rstrip() + "\n")


# ---------------------------------------------------------------------------
# IPC schema 4: authenticated envelope and observable security state.
# ---------------------------------------------------------------------------
protocol = "NXKeys.Protocol/NxProtocol.cs"
replace_once(protocol, "public const int SchemaVersion = 3;", "public const int SchemaVersion = 4;")
replace_once(
    protocol,
    """        [JsonPropertyName("expected_application_id")]
        public string ExpectedApplicationId { get; set; } = string.Empty;

        [JsonPropertyName("destructive")]
""",
    """        [JsonPropertyName("expected_application_id")]
        public string ExpectedApplicationId { get; set; } = string.Empty;

        [JsonPropertyName("session_id")]
        public string SessionId { get; set; } = string.Empty;

        [JsonPropertyName("client_instance_id")]
        public string ClientInstanceId { get; set; } = string.Empty;

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;

        [JsonPropertyName("sequence_number")]
        public long SequenceNumber { get; set; }

        [JsonPropertyName("profile_digest")]
        public string ProfileDigest { get; set; } = string.Empty;

        [JsonPropertyName("payload_hmac")]
        public string PayloadHmac { get; set; } = string.Empty;

        [JsonPropertyName("destructive")]
""",
)
replace_once(
    protocol,
    """            RequireMaxLength(nameof(ExpectedApplicationId), ExpectedApplicationId);
            RequireMaxLength(nameof(ExpectedSelectionFingerprint), ExpectedSelectionFingerprint);
""",
    """            RequireMaxLength(nameof(ExpectedApplicationId), ExpectedApplicationId);
            RequireMaxLength(nameof(ExpectedSelectionFingerprint), ExpectedSelectionFingerprint);
            RequireMaxLength(nameof(SessionId), SessionId);
            RequireMaxLength(nameof(ClientInstanceId), ClientInstanceId);
            RequireMaxLength(nameof(Nonce), Nonce);
            RequireMaxLength(nameof(ProfileDigest), ProfileDigest);
            RequireMaxLength(nameof(PayloadHmac), PayloadHmac);
""",
)
replace_once(
    protocol,
    """        [JsonPropertyName("last_message")]
        public string LastMessage { get; set; } = string.Empty;

        [JsonIgnore]
""",
    """        [JsonPropertyName("last_message")]
        public string LastMessage { get; set; } = string.Empty;

        [JsonPropertyName("security_status")]
        public string SecurityStatus { get; set; } = string.Empty;

        [JsonPropertyName("security_session_id")]
        public string SecuritySessionId { get; set; } = string.Empty;

        [JsonPropertyName("security_profile_digest")]
        public string SecurityProfileDigest { get; set; } = string.Empty;

        [JsonIgnore]
""",
)
replace_once(
    protocol,
    """                ActiveCommandId ?? string.Empty
""",
    """                ActiveCommandId ?? string.Empty,
                SecurityStatus ?? string.Empty,
                SecuritySessionId ?? string.Empty,
                SecurityProfileDigest ?? string.Empty
""",
)

security_path = ROOT / "NXKeys.Protocol/NxBridgeSecurity.cs"
if security_path.exists():
    raise RuntimeError("NXKeys.Protocol/NxBridgeSecurity.cs already exists")
security_path.write_text(r'''using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NXKeys.Protocol
{
    public static class NxBridgeSecurityEnvironment
    {
        public const string SessionIdVariable = "NXKEYS_SESSION_ID";
        public const string SessionSecretVariable = "NXKEYS_SESSION_SECRET";
        public const string ConfigPathVariable = "NXKEYS_CONFIG_PATH";
        public const string ClientExecutableVariable = "NXKEYS_CLIENT_EXE";

        public static bool TryRead(
            out string sessionId,
            out byte[] secret,
            out string configPath,
            out string clientExecutable,
            out string error)
        {
            sessionId = (Environment.GetEnvironmentVariable(SessionIdVariable) ?? string.Empty).Trim();
            string encodedSecret = (Environment.GetEnvironmentVariable(SessionSecretVariable) ?? string.Empty).Trim();
            configPath = (Environment.GetEnvironmentVariable(ConfigPathVariable) ?? string.Empty).Trim();
            clientExecutable = (Environment.GetEnvironmentVariable(ClientExecutableVariable) ?? string.Empty).Trim();
            secret = Array.Empty<byte>();
            error = string.Empty;

            if (!Guid.TryParseExact(sessionId, "N", out _))
            {
                error = "NXKeys secure session id is missing or invalid.";
                return false;
            }
            try { secret = Convert.FromBase64String(encodedSecret); }
            catch (FormatException)
            {
                error = "NXKeys secure session secret is not valid Base64.";
                return false;
            }
            if (secret.Length < 32)
            {
                error = "NXKeys secure session secret is shorter than 256 bits.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                error = "NXKeys secure profile path is missing or does not exist.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(clientExecutable) || !File.Exists(clientExecutable))
            {
                error = "NXKeys trusted client executable is missing or does not exist.";
                return false;
            }

            configPath = Path.GetFullPath(configPath);
            clientExecutable = Path.GetFullPath(clientExecutable);
            return true;
        }
    }

    public sealed class NxCommandPermission
    {
        public string Action { get; set; } = string.Empty;
        public string CommandId { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string TargetApplicationId { get; set; } = string.Empty;
        public string SelectionFilter { get; set; } = string.Empty;
        public bool Destructive { get; set; }
        public bool ConfirmationRequired { get; set; }

        public string Key => NxBridgePermissionSet.PermissionKey(
            Action, CommandId, ModuleId, TargetApplicationId, SelectionFilter);

        public string PolicyLine => string.Join("|", new[]
        {
            Key,
            Destructive ? "destructive" : "safe",
            ConfirmationRequired ? "confirm" : "direct"
        });
    }

    public sealed class NxBridgePermissionSet
    {
        private readonly Dictionary<string, NxCommandPermission> permissions;

        public string ProfileDigest { get; }
        public IReadOnlyList<NxCommandPermission> Permissions { get; }

        private NxBridgePermissionSet(IEnumerable<NxCommandPermission> source)
        {
            permissions = new Dictionary<string, NxCommandPermission>(StringComparer.OrdinalIgnoreCase);
            foreach (NxCommandPermission permission in source ?? Enumerable.Empty<NxCommandPermission>())
            {
                if (permission == null || string.IsNullOrWhiteSpace(permission.Action)) continue;
                permissions[permission.Key] = permission;
            }
            Permissions = permissions.Values.OrderBy(item => item.PolicyLine, StringComparer.Ordinal).ToList();
            string material = string.Join("\n", Permissions.Select(item => item.PolicyLine));
            using (SHA256 sha256 = SHA256.Create())
                ProfileDigest = Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
        }

        public static NxBridgePermissionSet FromProfileFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("NXKeys security profile was not found.", path);
            return FromProfileJson(File.ReadAllText(path, Encoding.UTF8));
        }

        public static NxBridgePermissionSet FromProfileJson(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object ||
                    !document.RootElement.TryGetProperty("modules", out JsonElement modulesElement) ||
                    modulesElement.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("NXKeys profile does not contain a modules array.");

                List<JsonElement> modules = modulesElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object && ReadEnabled(item))
                    .ToList();
                var applications = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (JsonElement module in modules)
                {
                    string moduleId = ReadString(module, "id");
                    string applicationId = ReadFirstString(module, "nx_application_ids");
                    if (!string.IsNullOrWhiteSpace(moduleId) && !string.IsNullOrWhiteSpace(applicationId))
                        applications[moduleId] = applicationId;
                }

                var result = new List<NxCommandPermission>();
                foreach (JsonElement module in modules)
                {
                    string moduleId = ReadString(module, "id");
                    string moduleApplication = ReadFirstString(module, "nx_application_ids");
                    string switchCommandId = ReadNestedString(module, "switch_command", "id");
                    if (!string.IsNullOrWhiteSpace(moduleApplication))
                    {
                        result.Add(new NxCommandPermission
                        {
                            Action = NxProtocolActions.SwitchModule,
                            CommandId = switchCommandId,
                            ModuleId = moduleId,
                            TargetApplicationId = moduleApplication
                        });
                    }

                    if (!module.TryGetProperty("command_sets", out JsonElement sets) || sets.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (JsonElement set in sets.EnumerateArray())
                    {
                        if (set.ValueKind != JsonValueKind.Object ||
                            !set.TryGetProperty("commands", out JsonElement commands) ||
                            commands.ValueKind != JsonValueKind.Array) continue;
                        foreach (JsonElement command in commands.EnumerateArray())
                        {
                            if (command.ValueKind != JsonValueKind.Object || !ReadEnabled(command)) continue;
                            string commandId = ReadNestedString(command, "command", "id");
                            string commandName = ReadNestedString(command, "command", "name");
                            string action = ReadString(command, "action");
                            if (string.IsNullOrWhiteSpace(action))
                                action = commandId.StartsWith("UG_SEL_", StringComparison.OrdinalIgnoreCase)
                                    ? NxProtocolActions.SetSelectionFilter
                                    : NxProtocolActions.ExecuteCommand;
                            if (!NxProtocolActions.IsSupported(action))
                                throw new InvalidOperationException("Unsupported action in NXKeys profile: " + action);

                            string targetApplication = string.Empty;
                            if (string.Equals(action, NxProtocolActions.SwitchModule, StringComparison.OrdinalIgnoreCase))
                            {
                                string targetModule = ReadString(command, "target_module_id");
                                applications.TryGetValue(targetModule, out targetApplication);
                            }
                            string selectionFilter = ReadString(command, "selection_type");
                            if (string.IsNullOrWhiteSpace(selectionFilter) &&
                                string.Equals(action, NxProtocolActions.SetSelectionFilter, StringComparison.OrdinalIgnoreCase))
                            {
                                selectionFilter = InferSelectionType(commandId, commandName, ReadString(command, "notes"));
                            }

                            result.Add(new NxCommandPermission
                            {
                                Action = action,
                                CommandId = commandId,
                                ModuleId = moduleId,
                                TargetApplicationId = targetApplication ?? string.Empty,
                                SelectionFilter = selectionFilter,
                                Destructive = ReadBoolean(command, "destructive"),
                                ConfirmationRequired = ReadBoolean(command, "confirm_before_execute") ||
                                                       ReadBoolean(command, "destructive")
                            });
                        }
                    }
                }
                if (result.Count == 0)
                    throw new InvalidOperationException("NXKeys profile produced an empty Bridge permission set.");
                return new NxBridgePermissionSet(result);
            }
        }

        public bool TryGetPermission(NxCommandRequest request, out NxCommandPermission permission)
        {
            permission = null;
            if (request == null) return false;
            return permissions.TryGetValue(PermissionKey(
                request.Action,
                request.CommandId,
                request.ModuleId,
                request.TargetApplicationId,
                request.SelectionFilter), out permission);
        }

        public static string PermissionKey(
            string action,
            string commandId,
            string moduleId,
            string targetApplicationId,
            string selectionFilter)
        {
            return string.Join("|", new[]
            {
                Normalize(action),
                Normalize(commandId),
                Normalize(moduleId),
                Normalize(targetApplicationId),
                Normalize(selectionFilter)
            });
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();

        private static bool ReadEnabled(JsonElement element) =>
            !element.TryGetProperty("enabled", out JsonElement value) ||
            value.ValueKind != JsonValueKind.False;

        private static bool ReadBoolean(JsonElement element, string property) =>
            element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.True;

        private static string ReadString(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.String)
                return string.Empty;
            return value.GetString()?.Trim() ?? string.Empty;
        }

        private static string ReadNestedString(JsonElement element, string parent, string property)
        {
            if (!element.TryGetProperty(parent, out JsonElement nested) || nested.ValueKind != JsonValueKind.Object)
                return string.Empty;
            return ReadString(nested, property);
        }

        private static string ReadFirstString(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
                return string.Empty;
            foreach (JsonElement item in array.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    return item.GetString().Trim();
            return string.Empty;
        }

        private static string InferSelectionType(string commandId, string commandName, string notes)
        {
            string text = string.Join(" ", commandId ?? string.Empty, commandName ?? string.Empty, notes ?? string.Empty)
                .ToUpperInvariant();
            if (text.Contains("DESELECT")) return "none";
            if (text.Contains("SELECT_ALL")) return "all";
            if (text.Contains("RESET")) return "reset";
            if (text.Contains("EDGE")) return "edge";
            if (text.Contains("FACE") || text.Contains("SURFACE") || text.Contains("SHEET_BOUNDARY")) return "face";
            if (text.Contains("BODY") || text.Contains("SOLID") || text.Contains("SHEET_METAL")) return "body";
            if (text.Contains("COMPONENT") || text.Contains("ASSEMBL")) return "component";
            if (text.Contains("CURVE") || text.Contains("LINE") || text.Contains("ARC") || text.Contains("CIRCLE")) return "curve";
            if (text.Contains("DATUM") || text.Contains("COORDINATE_SYSTEM")) return "datum";
            if (text.Contains("FEATURE") || text.Contains("TEMPLATE")) return "feature";
            if (text.Contains("OPERATION") || text.Contains("TOOL_PATH") || text.Contains("CAM_")) return "operation";
            return string.Empty;
        }
    }

    public static class NxRequestAuthenticator
    {
        public static void Sign(
            NxCommandRequest request,
            string sessionId,
            string clientInstanceId,
            byte[] secret,
            string profileDigest,
            long sequenceNumber)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.SessionId = sessionId ?? string.Empty;
            request.ClientInstanceId = clientInstanceId ?? string.Empty;
            request.Nonce = Guid.NewGuid().ToString("N");
            request.SequenceNumber = sequenceNumber;
            request.ProfileDigest = profileDigest ?? string.Empty;
            request.PayloadHmac = ComputeHmac(request, secret);
            ValidateEnvelope(request);
        }

        public static bool Verify(
            NxCommandRequest request,
            string expectedSessionId,
            byte[] secret,
            string expectedProfileDigest,
            out string error)
        {
            error = string.Empty;
            try { ValidateEnvelope(request); }
            catch (Exception exception) { error = exception.Message; return false; }
            if (!string.Equals(request.SessionId, expectedSessionId, StringComparison.Ordinal))
            { error = "NXKeys request session does not match the active Bridge session."; return false; }
            if (!string.Equals(request.ProfileDigest, expectedProfileDigest, StringComparison.OrdinalIgnoreCase))
            { error = "NXKeys request profile digest does not match the Bridge allowlist."; return false; }

            byte[] supplied;
            byte[] expected;
            try
            {
                supplied = Convert.FromHexString(request.PayloadHmac);
                expected = Convert.FromHexString(ComputeHmac(request, secret));
            }
            catch (FormatException)
            { error = "NXKeys request HMAC is not valid hexadecimal data."; return false; }
            if (supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(supplied, expected))
            { error = "NXKeys request HMAC verification failed."; return false; }
            return true;
        }

        public static void ValidateEnvelope(NxCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!Guid.TryParseExact(request.SessionId, "N", out _))
                throw new InvalidOperationException("session_id is required and must be a compact GUID.");
            if (!Guid.TryParseExact(request.ClientInstanceId, "N", out _))
                throw new InvalidOperationException("client_instance_id is required and must be a compact GUID.");
            if (!Guid.TryParseExact(request.Nonce, "N", out _))
                throw new InvalidOperationException("nonce is required and must be a compact GUID.");
            if (request.SequenceNumber <= 0)
                throw new InvalidOperationException("sequence_number must be positive.");
            if (!IsSha256(request.ProfileDigest))
                throw new InvalidOperationException("profile_digest must be a SHA-256 value.");
            if (!IsSha256(request.PayloadHmac))
                throw new InvalidOperationException("payload_hmac must be a SHA-256 HMAC value.");
        }

        public static string ComputeHmac(NxCommandRequest request, byte[] secret)
        {
            if (secret == null || secret.Length < 32)
                throw new InvalidOperationException("NXKeys session secret must contain at least 256 bits.");
            using (var hmac = new HMACSHA256(secret))
                return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(CanonicalPayload(request))))
                    .ToLowerInvariant();
        }

        public static string CanonicalPayload(NxCommandRequest request)
        {
            var builder = new StringBuilder();
            Append(builder, request.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            Append(builder, request.RequestId);
            Append(builder, request.Action);
            Append(builder, request.CommandId);
            Append(builder, request.CommandName);
            Append(builder, request.Sequence);
            Append(builder, request.ModuleId);
            Append(builder, request.TargetApplicationId);
            Append(builder, request.SelectionFilter);
            Append(builder, request.CreatedUtc);
            Append(builder, request.ExpiresUtc);
            Append(builder, request.SourceProcessId.ToString(CultureInfo.InvariantCulture));
            Append(builder, request.ExpectedContextRevision.ToString(CultureInfo.InvariantCulture));
            Append(builder, request.ExpectedSelectionCount.ToString(CultureInfo.InvariantCulture));
            Append(builder, request.ExpectedSelectionFingerprint);
            Append(builder, request.ExpectedApplicationId);
            Append(builder, request.Destructive ? "1" : "0");
            Append(builder, request.ConfirmationAccepted ? "1" : "0");
            Append(builder, request.SessionId);
            Append(builder, request.ClientInstanceId);
            Append(builder, request.Nonce);
            Append(builder, request.SequenceNumber.ToString(CultureInfo.InvariantCulture));
            Append(builder, request.ProfileDigest);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            string text = value ?? string.Empty;
            builder.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text).Append(';');
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            return value.All(character => Uri.IsHexDigit(character));
        }
    }

    public sealed class NxReplayGuard
    {
        private readonly int maximumNonces;
        private readonly HashSet<string> nonces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> nonceOrder = new Queue<string>();
        private readonly Dictionary<string, long> lastSequenceByClient =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly object sync = new object();

        public NxReplayGuard(int maximumRememberedNonces = 4096)
        {
            maximumNonces = Math.Max(128, maximumRememberedNonces);
        }

        public bool TryAccept(NxCommandRequest request, out string error)
        {
            error = string.Empty;
            if (request == null) { error = "Request is null."; return false; }
            lock (sync)
            {
                if (nonces.Contains(request.Nonce))
                { error = "NXKeys request nonce has already been used."; return false; }
                if (lastSequenceByClient.TryGetValue(request.ClientInstanceId, out long last) &&
                    request.SequenceNumber <= last)
                { error = "NXKeys request sequence is not monotonic."; return false; }

                nonces.Add(request.Nonce);
                nonceOrder.Enqueue(request.Nonce);
                lastSequenceByClient[request.ClientInstanceId] = request.SequenceNumber;
                while (nonceOrder.Count > maximumNonces)
                    nonces.Remove(nonceOrder.Dequeue());
                return true;
            }
        }
    }
}
''', encoding="utf-8", newline="\n")

# Link the shared security contract into both executables.
for project in (
    "NX2512_HotkeyStudio/NX2512_HotkeyStudio.csproj",
    "NX2512_CommandBridge/NX2512_CommandBridge.csproj",
):
    replace_once(
        project,
        """    <Compile Include="..\\NXKeys.Protocol\\NxProtocol.cs" Link="Protocol\\NxProtocol.cs" />
""",
        """    <Compile Include="..\\NXKeys.Protocol\\NxProtocol.cs" Link="Protocol\\NxProtocol.cs" />
    <Compile Include="..\\NXKeys.Protocol\\NxBridgeSecurity.cs" Link="Protocol\\NxBridgeSecurity.cs" />
""",
    )

# ---------------------------------------------------------------------------
# Secure launch: one ephemeral secret inherited by HotkeyStudio and NX.
# ---------------------------------------------------------------------------
runtime = "NX2512_HotkeyStudio/Services/NxRuntimeService.cs"
replace_once(runtime, "using System.Linq;\n", "using System.Linq;\nusing System.Security.Cryptography;\n")
replace_once(
    runtime,
    """                string studioExe = Path.Combine(managedRoot, "NX2512_HotkeyStudio.exe");
                if (File.Exists(studioExe))
                {
                    var leader = new ProcessStartInfo(studioExe)
""",
    """                string studioExe = Path.GetFullPath(Path.Combine(managedRoot, "NX2512_HotkeyStudio.exe"));
                string fullConfigPath = Path.GetFullPath(configPath);
                string sessionId = Guid.NewGuid().ToString("N");
                string sessionSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                StopOtherHotkeyStudioProcesses(studioExe);
                if (File.Exists(studioExe))
                {
                    var leader = new ProcessStartInfo(studioExe)
""",
)
replace_once(
    runtime,
    """                    leader.ArgumentList.Add(configPath);
                    Process.Start(leader);
""",
    """                    leader.ArgumentList.Add(fullConfigPath);
                    ApplySecurityEnvironment(leader, sessionId, sessionSecret, fullConfigPath, studioExe);
                    Process.Start(leader);
""",
)
replace_once(
    runtime,
    """                start.Environment["UGII_CUSTOM_DIRECTORY_FILE"] = customDirs;
                foreach (string argument in nxArguments ?? Array.Empty<string>()) start.ArgumentList.Add(argument);
""",
    """                start.Environment["UGII_CUSTOM_DIRECTORY_FILE"] = customDirs;
                ApplySecurityEnvironment(start, sessionId, sessionSecret, fullConfigPath, studioExe);
                foreach (string argument in nxArguments ?? Array.Empty<string>()) start.ArgumentList.Add(argument);
""",
)
replace_once(
    runtime,
    """        private static bool IsNxProcess(string processName, string path, string description)
""",
    """        private static void ApplySecurityEnvironment(
            ProcessStartInfo start,
            string sessionId,
            string sessionSecret,
            string configPath,
            string clientExecutable)
        {
            start.Environment[NXKeys.Protocol.NxBridgeSecurityEnvironment.SessionIdVariable] = sessionId;
            start.Environment[NXKeys.Protocol.NxBridgeSecurityEnvironment.SessionSecretVariable] = sessionSecret;
            start.Environment[NXKeys.Protocol.NxBridgeSecurityEnvironment.ConfigPathVariable] = configPath;
            start.Environment[NXKeys.Protocol.NxBridgeSecurityEnvironment.ClientExecutableVariable] = clientExecutable;
        }

        private static void StopOtherHotkeyStudioProcesses(string expectedExecutable)
        {
            int currentPid = Process.GetCurrentProcess().Id;
            foreach (Process process in Process.GetProcessesByName("NX2512_HotkeyStudio"))
            {
                using (process)
                {
                    if (process.Id == currentPid) continue;
                    try
                    {
                        string path = process.MainModule?.FileName ?? string.Empty;
                        if (!string.Equals(Path.GetFullPath(path), expectedExecutable, StringComparison.OrdinalIgnoreCase)) continue;
                        process.Kill(true);
                        process.WaitForExit(2000);
                    }
                    catch { }
                }
            }
        }

        private static bool IsNxProcess(string processName, string path, string description)
""",
)

# ---------------------------------------------------------------------------
# Desktop client: load allowlist, sign every request, refresh after profile save.
# ---------------------------------------------------------------------------
client = "NX2512_HotkeyStudio/Services/NxCommandBridgeClient.cs"
replace_once(client, "using System.Text.Json;\n", "using System.Text.Json;\nusing System.Threading;\n")
replace_once(
    client,
    """    public static class NxCommandBridgeClient
    {
""",
    """    public static class NxCommandBridgeClient
    {
        private static readonly string ClientInstanceId = Guid.NewGuid().ToString("N");
        private static readonly object SecuritySync = new object();
        private static NxBridgePermissionSet securityPermissions;
        private static string securityProfilePath = string.Empty;
        private static DateTime securityProfileWriteUtc = DateTime.MinValue;
        private static long securitySequence;

        public static void ConfigureSecurity(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentException("NXKeys security profile path is required.", nameof(configPath));
            string fullPath = Path.GetFullPath(configPath);
            NxBridgePermissionSet permissions = NxBridgePermissionSet.FromProfileFile(fullPath);
            lock (SecuritySync)
            {
                securityPermissions = permissions;
                securityProfilePath = fullPath;
                securityProfileWriteUtc = File.GetLastWriteTimeUtc(fullPath);
            }
        }
""",
)
replace_once(
    client,
    """            if (!string.Equals(context.Status, "running", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("NXKeys Bridge не готов: " + context.Status + ". В NX нажмите Start NXKeys Bridge.");
            return context;
""",
    """            if (!string.Equals(context.Status, "running", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("NXKeys Bridge не готов: " + context.Status + ". В NX нажмите Start NXKeys Bridge.");
            if (!string.Equals(context.SecurityStatus, "authenticated", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "NXKeys Bridge не имеет authenticated session. Запустите NX через managed launcher NXKeys.");
            return context;
""",
)
replace_once(
    client,
    """        private static void WriteRequest(NxCommandRequest request)
        {
            Directory.CreateDirectory(PendingDirectory);
""",
    """        private static void WriteRequest(NxCommandRequest request)
        {
            PrepareAuthenticatedRequest(request);
            Directory.CreateDirectory(PendingDirectory);
""",
)
replace_once(
    client,
    """        private static void WriteRequest(NxCommandRequest request)
""",
    """        private static void PrepareAuthenticatedRequest(NxCommandRequest request)
        {
            if (!NxBridgeSecurityEnvironment.TryRead(
                    out string sessionId,
                    out byte[] secret,
                    out string environmentProfilePath,
                    out string clientExecutable,
                    out string error))
                throw new InvalidOperationException(error + " Запустите NX через launch-nx2512-with-nxkeys.cmd.");

            string actualClient = Path.GetFullPath(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
            if (!string.Equals(actualClient, clientExecutable, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("NXKeys request source is not the trusted managed HotkeyStudio executable.");

            NxBridgePermissionSet permissions;
            lock (SecuritySync)
            {
                string fullEnvironmentProfile = Path.GetFullPath(environmentProfilePath);
                if (securityPermissions == null ||
                    !string.Equals(securityProfilePath, fullEnvironmentProfile, StringComparison.OrdinalIgnoreCase) ||
                    File.GetLastWriteTimeUtc(fullEnvironmentProfile) != securityProfileWriteUtc)
                {
                    securityPermissions = NxBridgePermissionSet.FromProfileFile(fullEnvironmentProfile);
                    securityProfilePath = fullEnvironmentProfile;
                    securityProfileWriteUtc = File.GetLastWriteTimeUtc(fullEnvironmentProfile);
                }
                permissions = securityPermissions;
            }

            if (!permissions.TryGetPermission(request, out NxCommandPermission permission))
                throw new InvalidOperationException(
                    "NXKeys request is not present in the active profile allowlist: " +
                    NxBridgePermissionSet.PermissionKey(request.Action, request.CommandId, request.ModuleId,
                        request.TargetApplicationId, request.SelectionFilter));
            if (request.Destructive != permission.Destructive)
                throw new InvalidOperationException("NXKeys request destructive policy differs from the active profile.");
            if (permission.ConfirmationRequired && !request.ConfirmationAccepted)
                throw new InvalidOperationException("NXKeys request has not passed the confirmation policy.");

            NxRequestAuthenticator.Sign(
                request,
                sessionId,
                ClientInstanceId,
                secret,
                permissions.ProfileDigest,
                Interlocked.Increment(ref securitySequence));
            request.Validate();
        }

        private static void WriteRequest(NxCommandRequest request)
""",
)

program = "NX2512_HotkeyStudio/Program.cs"
replace_once(
    program,
    """            Config config = Config.Load(activeConfigPath);
            if (config.SchemaVersion != Config.CurrentSchemaVersion || !config.LeaderKey.AdaptiveModuleMode)
""",
    """            Config config = Config.Load(activeConfigPath);
            NxCommandBridgeClient.ConfigureSecurity(activeConfigPath);
            if (config.SchemaVersion != Config.CurrentSchemaVersion || !config.LeaderKey.AdaptiveModuleMode)
""",
)
replace_once(
    program,
    """                Config config = Config.Load(configPath);

                switch (command)
""",
    """                Config config = Config.Load(configPath);
                NxCommandBridgeClient.ConfigureSecurity(configPath);

                switch (command)
""",
)

form = "NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs"
replace_once(
    form,
    """                config.Save(configPath);
                dirty = false;
""",
    """                config.Save(configPath);
                NxCommandBridgeClient.ConfigureSecurity(configPath);
                dirty = false;
""",
)

# ---------------------------------------------------------------------------
# NX Bridge: independently load policy, verify HMAC/source/nonce/sequence.
# ---------------------------------------------------------------------------
bridge = "NX2512_CommandBridge/Program.cs"
replace_once(
    bridge,
    """        private static string lastMessage = string.Empty;
""",
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
)
replace_once(
    bridge,
    """                EnsureDirectories();
                LoadPreviousContextRevision();
""",
    """                EnsureDirectories();
                InitializeSecurity();
                LoadPreviousContextRevision();
""",
)
replace_once(
    bridge,
    """                request.Validate();
                if (!string.Equals(request.RequestId, requestIdFromName, StringComparison.OrdinalIgnoreCase))
""",
    """                request.Validate();
                ValidateAuthenticatedRequest(request);
                if (!string.Equals(request.RequestId, requestIdFromName, StringComparison.OrdinalIgnoreCase))
""",
)
replace_once(
    bridge,
    """        private static void ValidateExpectedContext(NxCommandRequest request, NxContextSnapshot current)
""",
    r'''        private static void InitializeSecurity()
        {
            if (!NxBridgeSecurityEnvironment.TryRead(
                    out securitySessionId,
                    out securitySecret,
                    out securityProfilePath,
                    out expectedClientExecutable,
                    out string error))
            {
                securityStatus = "authentication_required";
                WriteLog("Secure IPC is unavailable: " + error);
                return;
            }
            try
            {
                securityPermissions = NxBridgePermissionSet.FromProfileFile(securityProfilePath);
                securityStatus = "authenticated";
                WriteLog("Secure IPC initialized. Profile digest=" + securityPermissions.ProfileDigest);
            }
            catch (Exception exception)
            {
                securityStatus = "profile_invalid";
                WriteLog("Secure IPC profile load failed: " + exception.Message);
            }
        }

        private static void ValidateAuthenticatedRequest(NxCommandRequest request)
        {
            if (!string.Equals(securityStatus, "authenticated", StringComparison.OrdinalIgnoreCase) ||
                securityPermissions == null)
                throw new InvalidOperationException(
                    "NXKeys authenticated session is not ready. Start NX through the managed NXKeys launcher.");

            RefreshSecurityProfileIfNeeded(request.ProfileDigest);
            if (!NxRequestAuthenticator.Verify(
                    request,
                    securitySessionId,
                    securitySecret,
                    securityPermissions.ProfileDigest,
                    out string authenticationError))
                throw new InvalidOperationException(authenticationError);

            ValidateSourceProcess(request.SourceProcessId);
            if (!securityPermissions.TryGetPermission(request, out NxCommandPermission permission))
                throw new InvalidOperationException("NX command/action is not present in the active profile allowlist.");
            if (request.Destructive != permission.Destructive)
                throw new InvalidOperationException("Request destructive policy differs from the active profile.");
            if (permission.ConfirmationRequired && !request.ConfirmationAccepted)
                throw new InvalidOperationException("Request requires confirmation according to the active profile.");
            if (!replayGuard.TryAccept(request, out string replayError))
                throw new InvalidOperationException(replayError);
        }

        private static void RefreshSecurityProfileIfNeeded(string requestedDigest)
        {
            if (securityPermissions != null &&
                string.Equals(securityPermissions.ProfileDigest, requestedDigest, StringComparison.OrdinalIgnoreCase)) return;
            lock (securitySync)
            {
                NxBridgePermissionSet refreshed = NxBridgePermissionSet.FromProfileFile(securityProfilePath);
                if (!string.Equals(refreshed.ProfileDigest, requestedDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Request profile digest does not match the installed NXKeys profile.");
                securityPermissions = refreshed;
                WriteLog("Secure IPC permission set reloaded. Digest=" + refreshed.ProfileDigest);
            }
        }

        private static void ValidateSourceProcess(int processId)
        {
            if (processId <= 0) throw new InvalidOperationException("Request source_process_id is invalid.");
            try
            {
                using (Process source = Process.GetProcessById(processId))
                {
                    string actual = Path.GetFullPath(source.MainModule?.FileName ?? string.Empty);
                    if (!string.Equals(actual, expectedClientExecutable, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Request source process is not the trusted managed HotkeyStudio executable.");
                }
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException("Request source process is no longer running.");
            }
        }

        private static void ValidateExpectedContext(NxCommandRequest request, NxContextSnapshot current)
''',
)
replace_once(
    bridge,
    """                LastRequestId = lastRequestId,
                LastResult = lastResult,
                LastMessage = lastMessage
""",
    """                LastRequestId = lastRequestId,
                LastResult = lastResult,
                LastMessage = lastMessage,
                SecurityStatus = securityStatus,
                SecuritySessionId = securitySessionId,
                SecurityProfileDigest = securityPermissions?.ProfileDigest ?? string.Empty
""",
)
replace_once(
    bridge,
    """                        processing_directory = ProcessingDirectory,
                        log_path = LogPath
""",
    """                        processing_directory = ProcessingDirectory,
                        log_path = LogPath,
                        security_status = securityStatus,
                        security_session_id = securitySessionId,
                        security_profile_digest = securityPermissions?.ProfileDigest ?? string.Empty
""",
)

# ---------------------------------------------------------------------------
# Regression tests for HMAC, profile permission set and anti-replay.
# ---------------------------------------------------------------------------
tests = "NX2512_HotkeyStudio.Tests/Program.cs"
replace_once(
    tests,
    """        VerifyPhaseZeroHardening();

        Console.WriteLine("[OK] Canonical profile editor, command menus, Sketch grammar, runtime hardening and single NX ribbon regressions.");
""",
    """        VerifyPhaseZeroHardening();
        VerifyAuthenticatedIpc();

        Console.WriteLine("[OK] Canonical profile editor, command menus, Sketch grammar, authenticated IPC and single NX ribbon regressions.");
""",
)
auth_test = r'''
    private static void VerifyAuthenticatedIpc()
    {
        Assert(NXKeys.Protocol.NxProtocolConstants.SchemaVersion == 4,
            "Authenticated transport requires IPC schema 4.");
        string sourceConfig = FindRepositoryFile(Path.Combine("config", "nx2512-pro-hybrid.json"));
        NXKeys.Protocol.NxBridgePermissionSet permissions =
            NXKeys.Protocol.NxBridgePermissionSet.FromProfileFile(sourceConfig);
        Assert(permissions.Permissions.Count > 0, "Profile permission set must not be empty.");
        NXKeys.Protocol.NxCommandPermission permission = permissions.Permissions
            .First(item => string.Equals(item.Action, NXKeys.Protocol.NxProtocolActions.ExecuteCommand,
                StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.CommandId));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = new NXKeys.Protocol.NxCommandRequest
        {
            RequestId = "authenticated-test",
            Action = permission.Action,
            CommandId = permission.CommandId,
            CommandName = "Authenticated test",
            ModuleId = permission.ModuleId,
            TargetApplicationId = permission.TargetApplicationId,
            SelectionFilter = permission.SelectionFilter,
            CreatedUtc = now.ToString("O"),
            ExpiresUtc = now.AddMinutes(1).ToString("O"),
            SourceProcessId = Environment.ProcessId,
            Destructive = permission.Destructive,
            ConfirmationAccepted = permission.ConfirmationRequired
        };
        byte[] secret = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        string sessionId = Guid.NewGuid().ToString("N");
        string clientId = Guid.NewGuid().ToString("N");
        NXKeys.Protocol.NxRequestAuthenticator.Sign(
            request, sessionId, clientId, secret, permissions.ProfileDigest, 1);
        request.Validate();
        Assert(NXKeys.Protocol.NxRequestAuthenticator.Verify(
                request, sessionId, secret, permissions.ProfileDigest, out string verificationError),
            "Signed request must verify: " + verificationError);
        Assert(permissions.TryGetPermission(request, out NXKeys.Protocol.NxCommandPermission resolved) &&
               resolved.CommandId == permission.CommandId,
            "Signed request must be admitted by the exact profile allowlist.");

        string originalName = request.CommandName;
        request.CommandName = originalName + " tampered";
        Assert(!NXKeys.Protocol.NxRequestAuthenticator.Verify(
                request, sessionId, secret, permissions.ProfileDigest, out _),
            "Any signed payload mutation must invalidate the HMAC.");
        request.CommandName = originalName;
        NXKeys.Protocol.NxRequestAuthenticator.Sign(
            request, sessionId, clientId, secret, permissions.ProfileDigest, 2);

        var replay = new NXKeys.Protocol.NxReplayGuard(128);
        Assert(replay.TryAccept(request, out _), "First authenticated request must pass anti-replay.");
        Assert(!replay.TryAccept(request, out string replayError) &&
               replayError.IndexOf("nonce", StringComparison.OrdinalIgnoreCase) >= 0,
            "Repeated nonce must be rejected.");

        var unauthorized = new NXKeys.Protocol.NxCommandRequest
        {
            Action = NXKeys.Protocol.NxProtocolActions.ExecuteCommand,
            CommandId = "UG_NOT_IN_PROFILE",
            ModuleId = permission.ModuleId
        };
        Assert(!permissions.TryGetPermission(unauthorized, out _),
            "Unknown command must not be admitted by the profile allowlist.");
    }

'''
replace_once(tests, "    private static void VerifyPhaseZeroHardening()\n", auth_test + "    private static void VerifyPhaseZeroHardening()\n")

# ---------------------------------------------------------------------------
# Permanent CI extends the hardening gate to authenticated IPC.
# ---------------------------------------------------------------------------
workflow = ".github/workflows/runtime-hardening.yml"
text = read(workflow)
text = text.replace("      - 'NXKeys.Protocol/**'\n", "      - 'NXKeys.Protocol/**'\n      - 'NX2512_HotkeyStudio/Services/NxRuntimeService.cs'\n      - 'NX2512_HotkeyStudio/Program.cs'\n")
text = text.replace("            'MaxRequestsPerPoll'\n", "            'MaxRequestsPerPoll',\n            'session_id',\n            'client_instance_id',\n            'payload_hmac'\n")
text = text.replace(
    "          $bridge = Get-Content .\\NX2512_CommandBridge\\Program.cs -Raw\n",
    "          $bridge = Get-Content .\\NX2512_CommandBridge\\Program.cs -Raw\n          $security = Get-Content .\\NXKeys.Protocol\\NxBridgeSecurity.cs -Raw\n          $runtime = Get-Content .\\NX2512_HotkeyStudio\\Services\\NxRuntimeService.cs -Raw\n",
)
text = text.replace(
    "          foreach ($required in @(\n            'Take(NxProtocolConstants.MaxRequestsPerPoll)',\n",
    "          foreach ($required in @('NxRequestAuthenticator', 'NxBridgePermissionSet', 'NxReplayGuard', 'FixedTimeEquals')) {\n            if ($security -notmatch [regex]::Escape($required)) {\n              throw \"Missing authenticated IPC invariant: $required\"\n            }\n          }\n          foreach ($required in @('NXKEYS_SESSION_SECRET', 'RandomNumberGenerator.GetBytes(32)', 'ApplySecurityEnvironment')) {\n            if ($runtime -notmatch [regex]::Escape($required)) {\n              throw \"Missing secure launch invariant: $required\"\n            }\n          }\n          foreach ($required in @(\n            'Take(NxProtocolConstants.MaxRequestsPerPoll)',\n",
)
text = text.replace(
    "            'AskSelectionSnapshot'\n",
    "            'AskSelectionSnapshot',\n            'ValidateAuthenticatedRequest',\n            'ValidateSourceProcess'\n",
)
write(workflow, text)

# ---------------------------------------------------------------------------
# Canonical documentation, ADR and changelog.
# ---------------------------------------------------------------------------
canonical_docs = [
    "README.md",
    "CONTRIBUTING.md",
    "docs/README.md",
    "docs/ARCHITECTURE.md",
    "docs/CONFIGURATION.md",
    "docs/DOCUMENTATION_AUDIT.md",
    "docs/SAFETY_MODEL.md",
    "docs/STATE_MACHINE_ARCHITECTURE.md",
    "docs/api.md",
    "NX2512_CommandBridge/README.md",
    "NX2512_HotkeyStudio/README.md",
]
for path in canonical_docs:
    text = read(path)
    text = text.replace("IPC schema 3", "IPC schema 4")
    text = text.replace("IPC schema | 3", "IPC schema | 4")
    text = text.replace("protocol schema 3", "protocol schema 4")
    text = text.replace("schema 3 round-trip", "schema 4 authenticated round-trip")
    write(path, text)

append_once(
    "docs/api.md",
    "## Authenticated request envelope (schema 4)",
    """## Authenticated request envelope (schema 4)

Every command request now carries `session_id`, `client_instance_id`, `nonce`,
`sequence_number`, `profile_digest`, and `payload_hmac`.

The managed launcher creates a random 256-bit secret and passes it only through the inherited
environment of the trusted HotkeyStudio and Siemens NX processes. The secret is never written to
the bridge queue. HotkeyStudio signs a length-prefixed canonical representation of all
security-relevant request fields with HMAC-SHA-256.

Before invoking NX, Command Bridge independently verifies:

1. protocol schema and request expiry;
2. active session id and HMAC using constant-time comparison;
3. exact path of `source_process_id` against the managed HotkeyStudio executable;
4. monotonic sequence and previously unseen nonce;
5. `profile_digest` against a permission set rebuilt from the active profile;
6. exact action, command id, module, target application and selection filter;
7. destructive and confirmation policy from the profile;
8. the current NX context and selected-object fingerprint.

Unsigned schema-3 requests and requests produced outside the managed launch session are rejected.
Changing the profile causes both sides to rebuild the permission digest before the next command.
""",
)
append_once(
    "docs/SAFETY_MODEL.md",
    "## Authenticated local IPC",
    """## Authenticated local IPC

IPC schema 4 closes the former same-user file-injection path. Queue files are transport artifacts,
not authorities: a request is admitted only when its HMAC, session, source executable, anti-replay
state and profile permission all agree.

The secure session is created by `NX2512_HotkeyStudio.exe launch`. Starting NX and HotkeyStudio
independently does not create a shared capability and therefore leaves Bridge in
`authentication_required`; commands are rejected until NX is restarted through the managed
launcher.

This protects against ordinary local processes writing forged JSON into `%LOCALAPPDATA%\\NXKeys\\bridge`.
It is not a defence against an attacker who can inject code into Siemens NX or the trusted
HotkeyStudio process, replace managed binaries while bypassing package integrity, or read their
memory with equivalent/higher privileges.
""",
)
append_once(
    "docs/ARCHITECTURE.md",
    "### Authenticated admission pipeline",
    """### Authenticated admission pipeline

```mermaid
flowchart LR
    L[Managed launcher] -->|ephemeral secret in child environment| H[HotkeyStudio]
    L -->|same session secret| N[Siemens NX / Bridge]
    H -->|schema 4 + HMAC + nonce + sequence| Q[(file queue)]
    Q --> V[Bridge authentication]
    V --> P[Profile allowlist]
    P --> C[Context and selection guards]
    C --> X[NX command invocation]
```

The profile permission digest is derived from enabled command/action/module/selection rows and
confirmation policy. Bridge does not trust the digest supplied by the request; it rebuilds the same
permission set from the configured profile and compares it before dispatch.
""",
)

adr = ROOT / "docs/adr/0003-authenticated-file-ipc.md"
if adr.exists():
    raise RuntimeError("ADR 0003 already exists")
adr.write_text("""# ADR-0003: Authenticated file IPC with ephemeral launch capability

## Status

Accepted — August 2026.

## Context

Atomic file moves protected requests from partial writes but did not prove who created a request.
Any process running as the same Windows user could previously construct a valid JSON request,
claim confirmation and provide an arbitrary runnable NX `BUTTON ID`.

## Decision

Retain the recoverable file queue, but treat it only as transport. IPC schema 4 adds:

- an ephemeral 256-bit secret generated by the managed launcher;
- a session id shared only through inherited child-process environment;
- HMAC-SHA-256 over a deterministic length-prefixed request representation;
- client instance id, monotonic sequence and nonce replay protection;
- exact source-process executable verification;
- a permission digest rebuilt independently from the active profile;
- enforcement of action/command/module/target/selection and confirmation policy in Bridge.

No secret is persisted in the queue, context file, profile or package manifest.

## Consequences

- NX must be launched through the NXKeys managed launcher for commands to execute.
- Existing schema-3 producers are intentionally incompatible and fail closed.
- Restarting the secure launch session rotates the capability and invalidates queued requests from
  the previous session.
- A profile edit changes the permission digest and is reloaded by both sides.
- The design does not protect against process injection or a compromise of the trusted binaries.

## Alternatives rejected

- ACL-only queue directory: same-user processes remain authorized.
- Secret stored beside the queue: same-user processes can read it.
- Trusting `source_process_id`: a PID is metadata, not authentication.
- Removing persistence entirely: loses at-most-once recovery and diagnostics.
""", encoding="utf-8", newline="\n")

append_once(
    "docs/NX_PLUGIN_FRAGILITY_ARCHITECTURE_UI_AUDIT.md",
    "## 18. Статус реализации фазы 1",
    """## 18. Статус реализации фазы 1

Закрыты два главных риска границы доверия:

- `NXK-FR-001` — запросы подписываются ephemeral HMAC-сессией, проверяется источник процесса и anti-replay;
- `NXK-FR-009` — Bridge независимо строит allowlist из активного профиля и проверяет его digest,
  action/command/module/target/selection и confirmation policy.

IPC повышен до schema 4. Старая schema 3 намеренно отклоняется fail-closed. Секрет создаётся
managed launcher и не сохраняется на диск.
""",
)
replace_once(
    "CHANGELOG.md",
    """### Fixed

- protocol actions проверяются fail-closed; неизвестное действие больше не может попасть в обычный NX dispatch;
""",
    """### Security

- IPC повышен до schema 4: ephemeral 256-bit launch capability и HMAC-SHA-256 для каждого request;
- добавлены client instance, nonce, monotonic sequence и replay protection;
- Bridge проверяет точный source process и профильный allowlist/digest до NX dispatch;
- unsigned requests и запуск вне managed launcher отклоняются fail-closed.

### Fixed

- protocol actions проверяются fail-closed; неизвестное действие больше не может попасть в обычный NX dispatch;
""",
)

print("Authenticated IPC phase one applied successfully.")
