using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace NX2512_HotkeyStudio.Models
{
    /// <summary>
    /// System.Text.Json callback that preserves v8 secondary_aliases without
    /// requiring a schema bump. The legacy runtime translator understands one
    /// canonical path per operation, so aliases are expanded into equivalent
    /// operation contracts before Config.ApplyDefaults translates the profile.
    /// </summary>
    public sealed partial class Config : IJsonOnDeserialized
    {
        private static readonly IReadOnlyDictionary<string, string> AliasApplicationPrefixes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["modeling"] = "M",
                ["UG_APP_MODELING"] = "M",
                ["sketch"] = "S",
                ["UG_APP_SKETCH"] = "S",
                ["assemblies"] = "A",
                ["assembly"] = "A",
                ["UG_APP_ASSEMBLIES"] = "A",
                ["drafting"] = "D",
                ["UG_APP_DRAFTING"] = "D",
                ["pmi"] = "P",
                ["UG_APP_PMI"] = "P",
                ["surface"] = "U",
                ["UG_APP_STUDIO"] = "U",
                ["sheetmetal"] = "H",
                ["sheet_metal"] = "H",
                ["UG_APP_SHEETMETAL"] = "H",
                ["manufacturing"] = "N",
                ["UG_APP_MANUFACTURING"] = "N",
                ["simulation"] = "I",
                ["UG_APP_SFEM"] = "I",
                ["UG_APP_DESFEM"] = "I",
                ["routing"] = "R",
                ["UG_APP_ROUTING"] = "R",
                ["mold"] = "L",
                ["UG_APP_MOLDWIZARD"] = "L"
            };

        void IJsonOnDeserialized.OnDeserialized()
        {
            ExpandSecondaryAliasesForLegacyRuntime();
        }

        private void ExpandSecondaryAliasesForLegacyRuntime()
        {
            if (Operations == null || Operations.Count == 0) return;

            List<OperationContract> source = Operations.Where(operation => operation != null).ToList();
            var expanded = new List<OperationContract>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (OperationContract operation in source)
            {
                if (operation.Paths == null || operation.Paths.SecondaryAliases == null ||
                    operation.Paths.SecondaryAliases.Count == 0)
                    continue;

                string application = operation.Availability?.Applications?.FirstOrDefault() ?? string.Empty;
                bool applicationSpecific = !string.IsNullOrWhiteSpace(application) &&
                                           !string.Equals(application, "global", StringComparison.OrdinalIgnoreCase);
                AliasApplicationPrefixes.TryGetValue(application, out string applicationPrefix);

                IReadOnlyList<string> canonical = NormalizeAliasTokens(operation.Paths.Leader);
                string canonicalKey = string.Join("", canonical);

                int aliasIndex = 0;
                foreach (string rawAlias in operation.Paths.SecondaryAliases)
                {
                    aliasIndex++;
                    List<string> tokens = ParseAlias(rawAlias);
                    if (tokens.Count == 0) continue;

                    // App-specific v8 operations already get their module prefix from
                    // availability.applications. If a human-readable alias contains
                    // that prefix (M->..., S->..., H->...), strip it before translation
                    // so the runtime path does not become M->M->... .
                    if (applicationSpecific && !string.IsNullOrWhiteSpace(applicationPrefix) &&
                        tokens.Count > 1 && string.Equals(tokens[0], applicationPrefix, StringComparison.OrdinalIgnoreCase))
                        tokens.RemoveAt(0);

                    if (tokens.Count == 0) continue;
                    string aliasKey = string.Join("", tokens);
                    if (!string.IsNullOrWhiteSpace(canonicalKey) &&
                        string.Equals(aliasKey, canonicalKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string dedupeKey = (operation.Adapter?.Value ?? operation.OperationID ?? string.Empty) + "|" +
                                       application + "|" + aliasKey;
                    if (!seen.Add(dedupeKey)) continue;

                    OperationContract alias = CloneOperation(operation);
                    alias.OperationID = (operation.OperationID ?? "operation") + "#secondary_alias_" + aliasIndex;
                    alias.Paths.SecondaryAliases.Clear();
                    alias.Paths.Direct = null;
                    alias.Paths.WorkspaceKey = null;
                    alias.Paths.Leader = new List<string>();

                    if (tokens.Count == 1)
                        alias.Paths.Direct = tokens[0];
                    else
                        alias.Paths.Leader = tokens;

                    expanded.Add(alias);
                }
            }

            if (expanded.Count > 0)
                Operations.AddRange(expanded);
        }

        private static OperationContract CloneOperation(OperationContract source)
        {
            return new OperationContract
            {
                OperationID = source.OperationID ?? string.Empty,
                CommandName = source.CommandName ?? string.Empty,
                Paths = new OperationPaths
                {
                    Direct = source.Paths?.Direct,
                    WorkspaceKey = source.Paths?.WorkspaceKey,
                    Leader = source.Paths?.Leader?.ToList() ?? new List<string>(),
                    SecondaryAliases = source.Paths?.SecondaryAliases?.ToList() ?? new List<string>()
                },
                Adapter = new OperationAdapter
                {
                    Kind = source.Adapter?.Kind ?? string.Empty,
                    Value = source.Adapter?.Value ?? string.Empty,
                    Status = source.Adapter?.Status ?? string.Empty
                },
                Availability = new OperationAvailability
                {
                    Applications = source.Availability?.Applications?.ToList() ?? new List<string>(),
                    RequiresWorkPart = source.Availability?.RequiresWorkPart ?? false,
                    BlockedInTextInput = source.Availability?.BlockedInTextInput ?? false
                }
            };
        }

        private static List<string> ParseAlias(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<string>();
            string normalized = value
                .Replace("→", " ", StringComparison.Ordinal)
                .Replace("->", " ", StringComparison.Ordinal)
                .Replace("/", " ", StringComparison.Ordinal)
                .Replace("\\", " ", StringComparison.Ordinal)
                .Replace(">", " ", StringComparison.Ordinal)
                .Replace("-", " ", StringComparison.Ordinal);

            return normalized.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(LeaderKeyConfig.NormalizeInputKey)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Take(5)
                .ToList();
        }

        private static IReadOnlyList<string> NormalizeAliasTokens(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Select(LeaderKeyConfig.NormalizeInputKey)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Take(5)
                .ToArray();
        }
    }
}
