using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NX2512_HotkeyStudio.Models
{
    // V8 Schema Models
    public sealed class OperationContract
    {
        [JsonPropertyName("operation_id")] public string OperationID { get; set; } = string.Empty;
        [JsonPropertyName("command_name")] public string CommandName { get; set; } = string.Empty;
        [JsonPropertyName("action")] public string Action { get; set; } = "execute_command";
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("risk")] public string Risk { get; set; } = "safe";
        [JsonPropertyName("confirmation_required")] public bool ConfirmationRequired { get; set; }
        [JsonPropertyName("requires_selection")] public bool RequiresSelection { get; set; }
        [JsonPropertyName("minimum_selection_count")] public int MinimumSelectionCount { get; set; }
        [JsonPropertyName("selection_types")] public List<string> SelectionTypes { get; set; } = new List<string>();
        [JsonPropertyName("unavailable_reason")] public string UnavailableReason { get; set; } = string.Empty;
        [JsonPropertyName("target_application_id")] public string TargetApplicationId { get; set; } = string.Empty;
        [JsonPropertyName("selection_filter")] public string SelectionFilter { get; set; } = string.Empty;
        [JsonPropertyName("paths")] public OperationPaths Paths { get; set; } = new OperationPaths();
        [JsonPropertyName("adapter")] public OperationAdapter Adapter { get; set; } = new OperationAdapter();
        [JsonPropertyName("availability")] public OperationAvailability Availability { get; set; } = new OperationAvailability();
    }

    public sealed class OperationPaths
    {
        [JsonPropertyName("direct")] public string Direct { get; set; }
        [JsonPropertyName("workspace_key")] public string WorkspaceKey { get; set; }
        [JsonPropertyName("leader")] public List<string> Leader { get; set; } = new List<string>();
        [JsonPropertyName("secondary_aliases")] public List<string> SecondaryAliases { get; set; } = new List<string>();
    }

    public sealed class OperationAdapter
    {
        [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
        [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    }

    public sealed class OperationAvailability
    {
        [JsonPropertyName("applications")] public List<string> Applications { get; set; } = new List<string>();
        [JsonPropertyName("requires_work_part")] public bool RequiresWorkPart { get; set; }
        [JsonPropertyName("blocked_in_text_input")] public bool BlockedInTextInput { get; set; }
    }
}
