using System;
using System.Collections.Generic;

namespace NX2512_HotkeyStudio.Models
{
    public sealed class CommandItem
    {
        public string ID { get; set; } = string.Empty;
        public List<string> Labels { get; set; } = new List<string>();
        public List<string> Synonyms { get; set; } = new List<string>();
        public List<string> Messages { get; set; } = new List<string>();
        public List<string> Accelerators { get; set; } = new List<string>();
        public List<string> Sources { get; set; } = new List<string>();
        public List<ApiCrosswalkItem> ApiCandidates { get; set; } = new List<ApiCrosswalkItem>();

        public string DisplayLabel => Labels.Count > 0 ? Labels[0] : ID;
    }

    public sealed class ApiCrosswalkItem
    {
        public string ButtonID { get; set; } = string.Empty;
        public string UiLabel { get; set; } = string.Empty;
        public string ApiKind { get; set; } = string.Empty; // NXOpen or UFUN
        public string ApiTarget { get; set; } = string.Empty; // Class/Member/Function
        public string Confidence { get; set; } = string.Empty; // HIGH, MEDIUM, LOW
        public double Score { get; set; }
    }

    public enum ResolutionStatus
    {
        Resolved,
        Ambiguous,
        Unresolved
    }

    public sealed class CandidateMatch
    {
        public string ID { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Score { get; set; }
        public string ApiMatch { get; set; } = string.Empty;
    }

    public sealed class ResolutionResult
    {
        public Binding Binding { get; set; }
        public ResolutionStatus Status { get; set; }
        public string CommandID { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<CandidateMatch> Candidates { get; set; } = new List<CandidateMatch>();
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class ConflictItem
    {
        public string Shortcut { get; set; } = string.Empty;
        public CommandItem Command { get; set; }
    }

    public sealed class CatalogIndex
    {
        public Dictionary<string, CommandItem> Commands { get; } =
            new Dictionary<string, CommandItem>(StringComparer.OrdinalIgnoreCase);

        public List<ApiCrosswalkItem> CrosswalkEntries { get; } = new List<ApiCrosswalkItem>();

        public void AddOrMerge(CommandItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ID)) return;

            string key = item.ID.Trim().ToUpperInvariant();
            if (Commands.TryGetValue(key, out CommandItem existing))
            {
                MergeLists(existing.Labels, item.Labels);
                MergeLists(existing.Synonyms, item.Synonyms);
                MergeLists(existing.Messages, item.Messages);
                MergeLists(existing.Accelerators, item.Accelerators);
                MergeLists(existing.Sources, item.Sources);
            }
            else
            {
                Commands[key] = item;
            }
        }

        private static void MergeLists(List<string> target, List<string> source)
        {
            if (source == null) return;
            foreach (string s in source)
            {
                if (string.IsNullOrWhiteSpace(s)) continue;
                if (!target.Exists(x => string.Equals(x, s, StringComparison.OrdinalIgnoreCase)))
                {
                    target.Add(s);
                }
            }
        }
    }
}
