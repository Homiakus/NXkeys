using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class CommandResolver
    {
        private readonly CatalogIndex catalog;
        private readonly Dictionary<string, List<CommandItem>> tokenIndex =
            new Dictionary<string, List<CommandItem>>(StringComparer.OrdinalIgnoreCase);
        private bool tokenIndexBuilt;

        public CommandResolver(CatalogIndex catalog)
        {
            this.catalog = catalog ?? new CatalogIndex();
        }

        public List<ResolutionResult> ResolveBindings(IEnumerable<Binding> bindings)
        {
            var results = new List<ResolutionResult>();
            if (bindings == null) return results;

            foreach (Binding b in bindings)
            {
                results.Add(Resolve(b));
            }
            return results;
        }

        public ResolutionResult Resolve(Binding binding)
        {
            var result = new ResolutionResult { Binding = binding };

            if (binding == null || !binding.Enabled)
            {
                result.Status = ResolutionStatus.Unresolved;
                result.Reason = "binding disabled";
                return result;
            }

            string explicitId = (binding.Command?.ID ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(explicitId))
            {
                string key = explicitId.ToUpperInvariant();
                if (catalog.Commands.TryGetValue(key, out CommandItem cmd))
                {
                    result.Status = ResolutionStatus.Resolved;
                    result.CommandID = cmd.ID;
                    result.Label = cmd.DisplayLabel;
                    return result;
                }

                // Explicit ID fallback (e.g. system ribbon buttons)
                result.Status = ResolutionStatus.Resolved;
                result.CommandID = explicitId;
                result.Label = !string.IsNullOrEmpty(binding.Command?.Name) ? binding.Command.Name : explicitId;
                return result;
            }

            List<string> queries = new List<string>();
            if (!string.IsNullOrWhiteSpace(binding.Command?.Name)) queries.Add(binding.Command.Name);
            if (binding.Command?.Aliases != null)
            {
                foreach (string alias in binding.Command.Aliases)
                {
                    if (!string.IsNullOrWhiteSpace(alias)) queries.Add(alias);
                }
            }

            List<CandidateMatch> matches = new List<CandidateMatch>();

            foreach (CommandItem cmd in CandidateUniverse(queries))
            {
                double score = ScoreCommand(queries, cmd);
                if (score <= 0) continue;

                string topApi = cmd.ApiCandidates.Count > 0
                    ? $"{cmd.ApiCandidates[0].ApiKind}: {cmd.ApiCandidates[0].ApiTarget}"
                    : string.Empty;

                matches.Add(new CandidateMatch
                {
                    ID = cmd.ID,
                    Label = cmd.DisplayLabel,
                    Score = score,
                    ApiMatch = topApi
                });
            }

            matches.Sort((a, b) =>
            {
                int cmp = b.Score.CompareTo(a.Score);
                return cmp != 0 ? cmp : string.Compare(a.ID, b.ID, StringComparison.OrdinalIgnoreCase);
            });

            if (matches.Count == 0 || matches[0].Score < 0.62)
            {
                result.Status = ResolutionStatus.Unresolved;
                result.Reason = "no sufficiently strong label/id match";
                result.Candidates = matches.Take(5).ToList();
                return result;
            }

            if (matches.Count > 1 && (matches[0].Score - matches[1].Score) < 0.08)
            {
                result.Status = ResolutionStatus.Ambiguous;
                result.Reason = "multiple NX commands have similar labels; set command.id explicitly in JSON";
                result.Candidates = matches.Take(5).ToList();
                return result;
            }

            result.Status = ResolutionStatus.Resolved;
            result.CommandID = matches[0].ID;
            result.Label = matches[0].Label;
            result.Candidates = matches.Take(3).ToList();
            return result;
        }

        private IEnumerable<CommandItem> CandidateUniverse(List<string> queries)
        {
            EnsureTokenIndex();
            var result = new Dictionary<string, CommandItem>(StringComparer.OrdinalIgnoreCase);
            foreach (string query in queries ?? new List<string>())
            {
                foreach (string token in GetTokenSet(NormalizeText(query)))
                {
                    if (tokenIndex.TryGetValue(token, out List<CommandItem> bucket))
                    {
                        foreach (CommandItem item in bucket)
                        {
                            result[item.ID] = item;
                        }
                    }
                }
            }
            if (result.Count == 0)
            {
                return catalog.Commands.Values;
            }
            return result.Values;
        }

        private void EnsureTokenIndex()
        {
            if (tokenIndexBuilt) return;
            foreach (CommandItem cmd in catalog.Commands.Values)
            {
                List<string> haystacks = new List<string> { cmd.ID };
                haystacks.AddRange(cmd.Labels);
                haystacks.AddRange(cmd.Synonyms);
                foreach (string haystack in haystacks)
                {
                    foreach (string token in GetTokenSet(NormalizeText(haystack)))
                    {
                        if (!tokenIndex.TryGetValue(token, out List<CommandItem> bucket))
                        {
                            bucket = new List<CommandItem>();
                            tokenIndex[token] = bucket;
                        }
                        if (!bucket.Any(x => string.Equals(x.ID, cmd.ID, StringComparison.OrdinalIgnoreCase)))
                        {
                            bucket.Add(cmd);
                        }
                    }
                }
            }
            tokenIndexBuilt = true;
        }

        public List<ConflictItem> FindConflicts(string shortcut, string exceptID)
        {
            string needle = NormalizeShortcut(shortcut);
            List<ConflictItem> conflicts = new List<ConflictItem>();
            if (string.IsNullOrEmpty(needle)) return conflicts;

            foreach (var kvp in catalog.Commands)
            {
                CommandItem cmd = kvp.Value;
                if (string.Equals(cmd.ID, exceptID, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (string accel in cmd.Accelerators)
                {
                    if (NormalizeShortcut(accel) == needle)
                    {
                        conflicts.Add(new ConflictItem
                        {
                            Shortcut = shortcut,
                            Command = cmd
                        });
                        break;
                    }
                }
            }

            conflicts.Sort((a, b) => string.Compare(a.Command.ID, b.Command.ID, StringComparison.OrdinalIgnoreCase));
            return conflicts;
        }

        public static double ScoreCommand(List<string> queries, CommandItem cmd)
        {
            List<string> haystacks = new List<string> { cmd.ID };
            haystacks.AddRange(cmd.Labels);
            haystacks.AddRange(cmd.Synonyms);

            double maxScore = 0.0;
            foreach (string q in queries)
            {
                foreach (string h in haystacks)
                {
                    double score = CalculateSimilarity(q, h);
                    if (score > maxScore) maxScore = score;
                }
            }
            return maxScore;
        }

        public static double CalculateSimilarity(string a, string b)
        {
            string normA = NormalizeText(a);
            string normB = NormalizeText(b);

            if (string.IsNullOrEmpty(normA) || string.IsNullOrEmpty(normB)) return 0.0;
            if (normA == normB) return 1.0;

            bool isSub = false;
            int shorterLen = 0, longerLen = 0;

            if (normA.Length < normB.Length)
            {
                if (ContainsWords(normB, normA))
                {
                    isSub = true;
                    shorterLen = normA.Length;
                    longerLen = normB.Length;
                }
            }
            else
            {
                if (ContainsWords(normA, normB))
                {
                    isSub = true;
                    shorterLen = normB.Length;
                    longerLen = normA.Length;
                }
            }

            if (isSub && longerLen > 0)
            {
                return 0.78 + 0.2 * ((double)shorterLen / longerLen);
            }

            HashSet<string> tokensA = GetTokenSet(normA);
            HashSet<string> tokensB = GetTokenSet(normB);

            int intersection = 0;
            foreach (string token in tokensA)
            {
                if (tokensB.Contains(token)) intersection++;
            }

            int union = tokensA.Count + tokensB.Count - intersection;
            double jaccard = union > 0 ? (double)intersection / union : 0.0;

            int distance = LevenshteinDistance(normA, normB);
            int maxLen = Math.Max(normA.Length, normB.Length);
            double levScore = maxLen > 0 ? 1.0 - ((double)distance / maxLen) : 0.0;

            return 0.58 * jaccard + 0.42 * levScore;
        }

        private static bool ContainsWords(string longer, string shorter)
        {
            string[] wordsL = longer.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string[] wordsS = shorter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (wordsS.Length == 0 || wordsS.Length > wordsL.Length) return false;

            for (int i = 0; i <= wordsL.Length - wordsS.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < wordsS.Length; j++)
                {
                    if (wordsL[i + j] != wordsS[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }

        public static string NormalizeText(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.ToLowerInvariant();
            char[] chars = s.Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ').ToArray();
            return string.Join(" ", new string(chars).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static HashSet<string> GetTokenSet(string s)
        {
            return new HashSet<string>(s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        }

        public static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            int[] previous = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                int[] current = new int[b.Length + 1];
                current[0] = i;

                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                    current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                }
                previous = current;
            }

            return previous[b.Length];
        }

        public static string NormalizeShortcut(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.ToLowerInvariant().Replace(" ", "").Replace("-", "");
        }
    }
}
