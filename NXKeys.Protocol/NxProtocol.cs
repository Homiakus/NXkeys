using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NXKeys.Protocol
{
    public static class NxProtocolConstants
    {
        public const int SchemaVersion = 4;
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
        public const string RunCapability = "run_capability";

        public static bool IsSupported(string action)
        {
            return string.Equals(action, ExecuteCommand, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, SwitchModule, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, SetSelectionFilter, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, ProbeCommand, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, RunCapability, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class NxCommandRequest
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = NxProtocolConstants.SchemaVersion;

        [JsonPropertyName("request_id")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = "execute_command";

        [JsonPropertyName("command_id")]
        public string CommandId { get; set; } = string.Empty;

        [JsonPropertyName("command_name")]
        public string CommandName { get; set; } = string.Empty;

        [JsonPropertyName("sequence")]
        public string Sequence { get; set; } = string.Empty;

        [JsonPropertyName("module_id")]
        public string ModuleId { get; set; } = string.Empty;

        [JsonPropertyName("target_application_id")]
        public string TargetApplicationId { get; set; } = string.Empty;

        [JsonPropertyName("selection_filter")]
        public string SelectionFilter { get; set; } = string.Empty;

        [JsonPropertyName("created_utc")]
        public string CreatedUtc { get; set; } = string.Empty;

        [JsonPropertyName("expires_utc")]
        public string ExpiresUtc { get; set; } = string.Empty;

        [JsonPropertyName("source_process_id")]
        public int SourceProcessId { get; set; }

        [JsonPropertyName("expected_context_revision")]
        public long ExpectedContextRevision { get; set; }

        [JsonPropertyName("expected_selection_count")]
        public int ExpectedSelectionCount { get; set; } = -1;

        [JsonPropertyName("expected_selection_fingerprint")]
        public string ExpectedSelectionFingerprint { get; set; } = string.Empty;

        [JsonPropertyName("expected_application_id")]
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
        public bool Destructive { get; set; }

        [JsonPropertyName("confirmation_accepted")]
        public bool ConfirmationAccepted { get; set; }

        // Capability Pack Integration Fields (Schema 4 Envelope)
        [JsonPropertyName("capability_id")]
        public string CapabilityId { get; set; } = string.Empty;

        [JsonPropertyName("workflow_id")]
        public string WorkflowId { get; set; } = string.Empty;

        [JsonPropertyName("component_id")]
        public string ComponentId { get; set; } = string.Empty;

        [JsonPropertyName("component_version")]
        public string ComponentVersion { get; set; } = string.Empty;

        [JsonPropertyName("payload_name")]
        public string PayloadName { get; set; } = string.Empty;

        [JsonPropertyName("payload_sha256")]
        public string PayloadSha256 { get; set; } = string.Empty;

        [JsonPropertyName("payload_schema_version")]
        public int PayloadSchemaVersion { get; set; }

        [JsonPropertyName("expected_part_id")]
        public string ExpectedPartId { get; set; } = string.Empty;

        [JsonPropertyName("expected_model_revision")]
        public string ExpectedModelRevision { get; set; } = string.Empty;

        [JsonPropertyName("profile_id")]
        public string ProfileId { get; set; } = string.Empty;

        [JsonPropertyName("profile_sha256")]
        public string ProfileSha256 { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsExpired
        {
            get
            {
                return !DateTimeOffset.TryParse(ExpiresUtc, out DateTimeOffset expires) ||
                       DateTimeOffset.UtcNow >= expires.ToUniversalTime();
            }
        }

        public void Validate()
        {
            if (SchemaVersion != NxProtocolConstants.SchemaVersion)
                throw new InvalidOperationException("Unsupported NXKeys protocol schema: " + SchemaVersion);
            if (string.IsNullOrWhiteSpace(RequestId))
                throw new InvalidOperationException("request_id is required.");
            if (string.IsNullOrWhiteSpace(Action))
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
            RequireMaxLength(nameof(SessionId), SessionId);
            RequireMaxLength(nameof(ClientInstanceId), ClientInstanceId);
            RequireMaxLength(nameof(Nonce), Nonce);
            RequireMaxLength(nameof(ProfileDigest), ProfileDigest);
            RequireMaxLength(nameof(PayloadHmac), PayloadHmac);
            RequireMaxLength(nameof(CapabilityId), CapabilityId);
            RequireMaxLength(nameof(WorkflowId), WorkflowId);
            RequireMaxLength(nameof(ComponentId), ComponentId);
            RequireMaxLength(nameof(ComponentVersion), ComponentVersion);
            RequireMaxLength(nameof(PayloadName), PayloadName);
            RequireMaxLength(nameof(PayloadSha256), PayloadSha256);
            RequireMaxLength(nameof(ExpectedPartId), ExpectedPartId);
            RequireMaxLength(nameof(ExpectedModelRevision), ExpectedModelRevision);
            RequireMaxLength(nameof(ProfileId), ProfileId);
            RequireMaxLength(nameof(ProfileSha256), ProfileSha256);

            if (string.Equals(Action, NxProtocolActions.RunCapability, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(CapabilityId))
                    throw new InvalidOperationException("capability_id is required for run_capability.");
                if (!string.IsNullOrWhiteSpace(PayloadName))
                {
                    if (Path.IsPathRooted(PayloadName) || PayloadName.Contains("..") || PayloadName.Contains(':'))
                        throw new InvalidOperationException("payload_name must be a relative file name without path traversal.");
                }
            }
            else if (!string.Equals(Action, NxProtocolActions.SwitchModule, StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(Action, NxProtocolActions.SetSelectionFilter, StringComparison.OrdinalIgnoreCase) &&
                     string.IsNullOrWhiteSpace(CommandId))
            {
                throw new InvalidOperationException("command_id is required for execute_command.");
            }

            if (string.Equals(Action, "switch_module", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(TargetApplicationId) && string.IsNullOrWhiteSpace(CommandId))
                throw new InvalidOperationException("target_application_id is required for switch_module.");
            if (string.Equals(Action, "set_selection_filter", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(SelectionFilter) && string.IsNullOrWhiteSpace(CommandId))
                throw new InvalidOperationException("selection_filter or command_id is required for set_selection_filter.");
            if (IsExpired)
                throw new InvalidOperationException("Request has expired.");
            if (Destructive && !ConfirmationAccepted)
                throw new InvalidOperationException("Destructive request has no explicit confirmation.");
        }

        private static void RequireMaxLength(string field, string value)
        {
            if ((value ?? string.Empty).Length > NxProtocolConstants.MaxTextFieldLength)
                throw new InvalidOperationException(field + " exceeds the protocol length limit.");
        }
    }

    public class NxContextSnapshot
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = NxProtocolConstants.SchemaVersion;

        [JsonPropertyName("revision")]
        public long Revision { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("application_id")]
        public string ApplicationId { get; set; } = string.Empty;

        [JsonPropertyName("module_id")]
        public string ModuleId { get; set; } = string.Empty;

        [JsonPropertyName("module_label")]
        public string ModuleLabel { get; set; } = string.Empty;

        [JsonPropertyName("selection_count")]
        public int SelectionCount { get; set; } = -1;

        [JsonPropertyName("selection_state")]
        public string SelectionState { get; set; } = "unknown";

        [JsonPropertyName("selected_types")]
        public List<string> SelectedTypes { get; set; } = new List<string>();

        [JsonPropertyName("selection_fingerprint")]
        public string SelectionFingerprint { get; set; } = string.Empty;

        [JsonPropertyName("work_part_available")]
        public bool WorkPartAvailable { get; set; }

        [JsonPropertyName("display_part_available")]
        public bool DisplayPartAvailable { get; set; }

        [JsonPropertyName("modal_dialog_active")]
        public bool ModalDialogActive { get; set; }

        [JsonPropertyName("active_command_id")]
        public string ActiveCommandId { get; set; } = string.Empty;

        [JsonPropertyName("context_confidence")]
        public int ContextConfidence { get; set; }

        [JsonPropertyName("updated_utc")]
        public string UpdatedUtc { get; set; } = string.Empty;

        [JsonPropertyName("last_request_id")]
        public string LastRequestId { get; set; } = string.Empty;

        [JsonPropertyName("last_result")]
        public string LastResult { get; set; } = string.Empty;

        [JsonPropertyName("last_message")]
        public string LastMessage { get; set; } = string.Empty;

        [JsonPropertyName("security_status")]
        public string SecurityStatus { get; set; } = string.Empty;

        [JsonPropertyName("security_session_id")]
        public string SecuritySessionId { get; set; } = string.Empty;

        [JsonPropertyName("security_profile_digest")]
        public string SecurityProfileDigest { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsFresh => IsFreshFor(NxProtocolConstants.DefaultContextFreshness);

        public bool IsFreshFor(TimeSpan maximumAge)
        {
            if (!DateTimeOffset.TryParse(UpdatedUtc, out DateTimeOffset updated)) return false;
            TimeSpan age = DateTimeOffset.UtcNow - updated.ToUniversalTime();
            return age >= TimeSpan.Zero && age <= maximumAge;
        }

        public string SemanticFingerprint()
        {
            return string.Join("|", new[]
            {
                Status ?? string.Empty,
                ApplicationId ?? string.Empty,
                ModuleId ?? string.Empty,
                SelectionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SelectionState ?? string.Empty,
                SelectionFingerprint ?? string.Empty,
                WorkPartAvailable ? "1" : "0",
                DisplayPartAvailable ? "1" : "0",
                ModalDialogActive ? "1" : "0",
                ActiveCommandId ?? string.Empty,
                SecurityStatus ?? string.Empty,
                SecuritySessionId ?? string.Empty,
                SecurityProfileDigest ?? string.Empty
            });
        }
    }

    public class NxCommandResult
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = NxProtocolConstants.SchemaVersion;

        [JsonPropertyName("request_id")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("workflow_id")]
        public string WorkflowId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("phase")]
        public string Phase { get; set; } = string.Empty;

        [JsonPropertyName("percent")]
        public int Percent { get; set; }

        [JsonPropertyName("issue_code")]
        public string IssueCode { get; set; } = string.Empty;

        [JsonPropertyName("recommended_action")]
        public string RecommendedAction { get; set; } = string.Empty;

        [JsonPropertyName("preview_hash")]
        public string PreviewHash { get; set; } = string.Empty;

        [JsonPropertyName("report_name")]
        public string ReportName { get; set; } = string.Empty;

        [JsonPropertyName("report_sha256")]
        public string ReportSha256 { get; set; } = string.Empty;

        [JsonPropertyName("rollback_attempted")]
        public bool RollbackAttempted { get; set; }

        [JsonPropertyName("rollback_verified")]
        public bool RollbackVerified { get; set; }

        [JsonPropertyName("manual_review_required")]
        public bool ManualReviewRequired { get; set; }

        [JsonPropertyName("context_revision")]
        public long ContextRevision { get; set; }

        [JsonPropertyName("completed_utc")]
        public string CompletedUtc { get; set; } = string.Empty;

        [JsonIgnore]
        public bool Success => string.Equals(Status, "executed", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase);
    }

    public static class NxProtocolJson
    {
        public static JsonSerializerOptions CreateOptions(bool writeIndented = false)
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = writeIndented,
                AllowTrailingCommas = false,
                ReadCommentHandling = JsonCommentHandling.Disallow
            };
        }

        public static readonly JsonSerializerOptions ReadOptions = CreateOptions(false);
        public static readonly JsonSerializerOptions WriteOptions = CreateOptions(true);
    }
}
