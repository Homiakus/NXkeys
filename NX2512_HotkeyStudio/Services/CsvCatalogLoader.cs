using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public static class CsvCatalogLoader
    {
        public static void LoadFromDirectory(CatalogIndex index, string catalogDir)
        {
            if (index == null || string.IsNullOrWhiteSpace(catalogDir) || !Directory.Exists(catalogDir))
            {
                return;
            }

            string buttonsCsv = Path.Combine(catalogDir, "06_ui_commands_buttons.csv");
            if (File.Exists(buttonsCsv))
            {
                LoadButtonsCsv(index, buttonsCsv);
            }

            string candidatesCsv = Path.Combine(catalogDir, "08_ui_command_api_candidates.csv");
            if (File.Exists(candidatesCsv))
            {
                LoadApiCandidatesCsv(index, candidatesCsv);
            }
        }

        private static void LoadButtonsCsv(CatalogIndex index, string csvPath)
        {
            using (var reader = new StreamReader(csvPath, Encoding.UTF8))
            {
                string header = reader.ReadLine();
                if (header == null) return;

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    List<string> fields = ParseCsvLine(line);
                    if (fields.Count < 5) continue;

                    string buttonId = fields[0].Trim();
                    if (string.IsNullOrWhiteSpace(buttonId)) continue;

                    string label = CleanValue(fields[1]);
                    string synonyms = CleanValue(fields[2]);
                    string accelerator = CleanValue(fields[4]);
                    string sourceFile = fields.Count > 12 ? fields[12].Trim() : string.Empty;

                    CommandItem item = new CommandItem
                    {
                        ID = buttonId
                    };

                    if (!string.IsNullOrWhiteSpace(label)) item.Labels.Add(label);
                    if (!string.IsNullOrWhiteSpace(synonyms))
                    {
                        string[] parts = synonyms.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string p in parts)
                        {
                            string s = p.Trim();
                            if (!string.IsNullOrEmpty(s)) item.Synonyms.Add(s);
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(accelerator)) item.Accelerators.Add(accelerator);
                    if (!string.IsNullOrWhiteSpace(sourceFile)) item.Sources.Add(sourceFile);

                    index.AddOrMerge(item);
                }
            }
        }

        private static void LoadApiCandidatesCsv(CatalogIndex index, string csvPath)
        {
            using (var reader = new StreamReader(csvPath, Encoding.UTF8))
            {
                string header = reader.ReadLine();
                if (header == null) return;

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    List<string> fields = ParseCsvLine(line);
                    if (fields.Count < 11) continue;

                    string buttonId = fields[0].Trim();
                    if (string.IsNullOrWhiteSpace(buttonId)) continue;

                    string uiLabel = CleanValue(fields[1]);
                    string candidateKind = fields[6].Trim();
                    string apiName = fields[7].Trim();

                    double.TryParse(fields[9], out double score);
                    string confidence = fields[10].Trim();

                    string key = buttonId.ToUpperInvariant();
                    if (index.Commands.TryGetValue(key, out CommandItem cmd))
                    {
                        var entry = new ApiCrosswalkItem
                        {
                            ButtonID = buttonId,
                            UiLabel = uiLabel,
                            ApiKind = candidateKind,
                            ApiTarget = apiName,
                            Confidence = confidence,
                            Score = score
                        };

                        if (!cmd.ApiCandidates.Exists(x => x.ApiTarget == apiName))
                        {
                            cmd.ApiCandidates.Add(entry);
                        }
                    }
                }
            }
        }

        private static string CleanValue(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return string.Empty;
            val = val.Trim().Trim('"', '\'');
            val = val.Replace("&", "");
            return val.Trim();
        }

        public static List<string> ParseCsvLine(string line)
        {
            List<string> result = new List<string>();
            if (line == null) return result;

            bool inQuotes = false;
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString());

            return result;
        }
    }
}
