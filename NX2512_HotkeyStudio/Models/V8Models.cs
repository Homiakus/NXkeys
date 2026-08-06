using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NX2512_HotkeyStudio.Models
{
    // V8 Schema Models
    public sealed class OperationContract
    {
        [JsonPropertyName("operation_id")] public string OperationID { get; set; } = string.Empty;
        [JsonPropertyName("command_name")] public string CommandName { get; set; } = string.Empty;
        [JsonPropertyName("paths")] public OperationPaths Paths { get; set; } = new OperationPaths();
        [JsonPropertyName("adapter")] public OperationAdapter Adapter { get; set; } = new OperationAdapter();
        [JsonPropertyName("availability")] public OperationAvailability Availability { get; set; } = new OperationAvailability();
    }

    public sealed class OperationPaths
    {
        [JsonPropertyName("direct")] public string Direct { get; set; }
        [JsonPropertyName("workspace_key")] public string WorkspaceKey { get; set; }
        [JsonPropertyName("leader")] public List<string> Leader { get; set; }
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
