using System;
using System.Collections.Generic;
using System.Linq;
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
        private List<string> canonicalLeader = new List<string>();

        [JsonPropertyName("direct")] public string Direct { get; set; }
        [JsonPropertyName("workspace_key")] public string WorkspaceKey { get; set; }

        [JsonPropertyName("leader")]
        public List<string> Leader
        {
            get
            {
                // The v8 profile already defines compact Modeling aliases for the
                // management/workspace family (M->L, M->W, M->E, ...), but the old
                // runtime discarded secondary_aliases entirely.  Prefer the compact
                // M route when one is explicitly authored in the profile.  This is
                // intentionally profile-driven: no command IDs are guessed here.
                string modelingAlias = (SecondaryAliases ?? new List<string>())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) &&
                        value.TrimStart().StartsWith("M->", StringComparison.OrdinalIgnoreCase));
                List<string> parsed = ParseAlias(modelingAlias);
                return parsed.Count > 0 ? parsed : canonicalLeader;
            }
            set => canonicalLeader = value ?? new List<string>();
        }

        // v8 profiles use aliases such as "M->L" and "M->W" for fast access
        // from the current Modeling workspace.  Older runtime models silently
        // discarded this field during JSON deserialization, making those routes
        // impossible to compile or validate.
        [JsonPropertyName("secondary_aliases")]
        public List<string> SecondaryAliases { get; set; } = new List<string>();

        [JsonIgnore]
        public IReadOnlyList<string> CanonicalLeader => canonicalLeader;

        private static List<string> ParseAlias(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<string>();
            return value.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim().ToUpperInvariant())
                .Where(token => token.Length > 0)
                .ToList();
        }
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
