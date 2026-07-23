using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NXKeys.Protocol
{
    public static class NxProtocolConstants
    {
        public const int SchemaVersion = 3;
        public static readonly TimeSpan DefaultContextFreshness = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan DefaultRequestLifetime = TimeSpan.FromSeconds(15);
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

        [JsonPropertyName("expected_application_id")]
        public string ExpectedApplicationId { get; set; } = string.Empty;

        [JsonPropertyName("destructive")]
        public bool Destructive { get; set; }

        [JsonPropertyName("confirmation_accepted")]
        public bool ConfirmationAccepted { get; set; }

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
            if (!string.Equals(Action, "switch_module", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(CommandId))
                throw new InvalidOperationException("command_id is required for execute_command.");
            if (string.Equals(Action, "switch_module", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(TargetApplicationId) && string.IsNullOrWhiteSpace(CommandId))
                throw new InvalidOperationException("target_application_id is required for switch_module.");
            if (IsExpired)
                throw new InvalidOperationException("Request has expired.");
            if (Destructive && !ConfirmationAccepted)
                throw new InvalidOperationException("Destructive request has no explicit confirmation.");
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
                WorkPartAvailable ? "1" : "0",
                DisplayPartAvailable ? "1" : "0",
                ModalDialogActive ? "1" : "0",
                ActiveCommandId ?? string.Empty
            });
        }
    }

    public class NxCommandResult
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = NxProtocolConstants.SchemaVersion;

        [JsonPropertyName("request_id")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

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
