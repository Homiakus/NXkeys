using System;
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
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("NXKeys profile must be a JSON object.");

                if (!document.RootElement.TryGetProperty("modules", out JsonElement modulesElement) || modulesElement.ValueKind != JsonValueKind.Array)
                {
                    if (document.RootElement.TryGetProperty("operations", out JsonElement opsElement) && opsElement.ValueKind == JsonValueKind.Array)
                    {
                        var v8Result = new List<NxCommandPermission>();
                        foreach (JsonElement op in opsElement.EnumerateArray())
                        {
                            if (op.ValueKind != JsonValueKind.Object) continue;
                            string opId = ReadString(op, "operation_id");
                            string adapterKind = ReadNestedString(op, "adapter", "kind");
                            string adapterVal = ReadNestedString(op, "adapter", "value");
                            string commandId = string.Equals(adapterKind, "button_id", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(adapterVal)
                                ? adapterVal
                                : opId;

                            // Determine v8 module ID: prefer availability.applications[0] for
                            // application-specific operations; fall back to first leader token
                            // for global operations.
                            string appId = ReadFirstNestedArrayElement(op, "availability", "applications");
                            string firstLeaderToken = ReadFirstNestedArrayElement(op, "paths", "leader");
                            string v8ModuleId;

                            bool hasSpecificApp = !string.IsNullOrWhiteSpace(appId) &&
                                !string.Equals(appId, "global", StringComparison.OrdinalIgnoreCase);

                            if (hasSpecificApp)
                            {
                                // Modeling, Sketch, Drafting, etc. → mapped to v8_m, v8_s, v8_d, ...
                                v8ModuleId = "v8_" + NxAppToV8Prefix(appId);
                            }
                            else if (!string.IsNullOrWhiteSpace(firstLeaderToken))
                            {
                                // Global operations: first leader token IS the module prefix
                                // (e.g. leader ["M","L"] → module v8_m)
                                v8ModuleId = "v8_" + firstLeaderToken.Trim().ToLowerInvariant();
                            }
                            else
                            {
                                v8ModuleId = "v8_m"; // fallback: modeling
                            }

                            if (!string.IsNullOrWhiteSpace(commandId))
                            {
                                v8Result.Add(new NxCommandPermission
                                {
                                    Action = NxProtocolActions.ExecuteCommand,
                                    CommandId = commandId,
                                    ModuleId = v8ModuleId,
                                    TargetApplicationId = string.Empty,
                                    SelectionFilter = string.Empty,
                                    Destructive = false,
                                    ConfirmationRequired = false
                                });
                            }
                        }
                        if (v8Result.Count == 0)
                            throw new InvalidOperationException("NXKeys v8 profile produced an empty Bridge permission set.");
                        return new NxBridgePermissionSet(v8Result);
                    }
                    throw new InvalidOperationException("NXKeys profile does not contain a modules or operations array.");
                }

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
                                if (applications.TryGetValue(targetModule, out string? resolvedApplication))
                                    targetApplication = resolvedApplication ?? string.Empty;
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

        public bool TryGetPermission(NxCommandRequest request, out NxCommandPermission? permission)
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

        private static string ReadFirstNestedArrayElement(JsonElement element, string parent, string property)
        {
            if (!element.TryGetProperty(parent, out JsonElement nested) || nested.ValueKind != JsonValueKind.Object)
                return string.Empty;
            if (!nested.TryGetProperty(property, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
                return string.Empty;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string? value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return string.Empty;
        }

        private static string ReadFirstString(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
                return string.Empty;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string? value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return string.Empty;
        }

        // Mirrors ConfigRuntimeV5.NxAppIdToModulePrefix — maps NX application IDs
        // to single-letter v8 module prefixes so allowlist keys match runtime requests.
        private static string NxAppToV8Prefix(string appId)
        {
            switch ((appId ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "UG_APP_MODELING": return "m";
                case "UG_APP_SKETCH": return "s";
                case "UG_APP_ASSEMBLIES": return "a";
                case "UG_APP_DRAFTING": return "d";
                case "UG_APP_PMI": return "p";
                case "UG_APP_STUDIO": return "u";
                case "UG_APP_SHEETMETAL": return "h";
                case "UG_APP_MANUFACTURING": return "n";
                case "UG_APP_SFEM":
                case "UG_APP_DESFEM": return "i";
                case "UG_APP_ROUTING": return "r";
                case "UG_APP_MOLDWIZARD": return "l";
                case "UG_APP_GATEWAY": return "g";

                // V8 profile availability.applications uses short app names
                // (e.g. "modeling"), not UG_APP_* IDs.  Alias them to the same
                // prefixes so FromProfileJson can honor availability for
                // application-specific operations (modeling.revolve → v8_m).
                // Labels are UPPERCASE because the switch input is ToUpperInvariant().
                case "MODELING": return "m";
                case "SKETCH": return "s";
                case "ASSEMBLIES": return "a";
                case "DRAFTING": return "d";
                case "PMI": return "p";
                case "SURFACE": return "u";
                case "SHEETMETAL": return "h";
                case "MANUFACTURING": return "n";
                case "SIMULATION": return "i";
                case "ROUTING": return "r";
                default: return "m"; // fallback: modeling
            }
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
            NxCommandRequest? request,
            string expectedSessionId,
            byte[] secret,
            string expectedProfileDigest,
            out string error)
        {
            error = string.Empty;
            if (request == null) { error = "Request is null."; return false; }
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
