using System;
using System.IO;
using System.Text;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public static class MenuScriptParser
    {
        public static void ParseFile(CatalogIndex index, string filePath)
        {
            if (index == null || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs, Encoding.UTF8, true))
                {
                    CommandItem current = null;
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("!") || trimmed.StartsWith("#"))
                        {
                            continue;
                        }

                        SplitDirective(trimmed, out string key, out string val);
                        string upperKey = key.ToUpperInvariant();

                        switch (upperKey)
                        {
                            case "BUTTON":
                            case "TOGGLE_BUTTON":
                                string id = FirstToken(val);
                                if (!string.IsNullOrEmpty(id) && !id.Contains("\"") && !id.Contains("'"))
                                {
                                    current = new CommandItem { ID = id };
                                    current.Sources.Add(Path.GetFullPath(filePath));
                                    index.AddOrMerge(current);
                                    current = index.Commands[id.ToUpperInvariant()];
                                }
                                else
                                {
                                    current = null;
                                }
                                break;

                            case "LABEL":
                            case "TOOLBAR_LABEL":
                                if (current != null)
                                {
                                    string label = CleanValue(val);
                                    if (!string.IsNullOrEmpty(label) && !current.Labels.Exists(x => string.Equals(x, label, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        current.Labels.Add(label);
                                    }
                                }
                                break;

                            case "SYNONYMS":
                                if (current != null)
                                {
                                    string synonyms = CleanValue(val);
                                    string[] parts = synonyms.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (string p in parts)
                                    {
                                        string s = p.Trim();
                                        if (!string.IsNullOrEmpty(s) && !current.Synonyms.Exists(x => string.Equals(x, s, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            current.Synonyms.Add(s);
                                        }
                                    }
                                }
                                break;

                            case "MESSAGE":
                                if (current != null)
                                {
                                    string msg = CleanValue(val);
                                    if (!string.IsNullOrEmpty(msg) && !current.Messages.Exists(x => string.Equals(x, msg, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        current.Messages.Add(msg);
                                    }
                                }
                                break;

                            case "ACCELERATOR":
                                if (current != null)
                                {
                                    string accel = CleanValue(val);
                                    if (!string.IsNullOrEmpty(accel) && !current.Accelerators.Exists(x => string.Equals(x, accel, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        current.Accelerators.Add(accel);
                                    }
                                }
                                break;
                        }
                    }
                }
            }
            catch
            {
                // Skip unreadable files safely
            }
        }

        private static void SplitDirective(string line, out string key, out string val)
        {
            int firstSpace = line.IndexOfAny(new[] { ' ', '\t' });
            if (firstSpace < 0)
            {
                key = line;
                val = string.Empty;
            }
            else
            {
                key = line.Substring(0, firstSpace);
                val = line.Substring(firstSpace).Trim();
            }
        }

        private static string FirstToken(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return string.Empty;
            string[] parts = val.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0].Trim() : string.Empty;
        }

        private static string CleanValue(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return string.Empty;
            val = val.Trim().Trim('"', '\'');
            val = val.Replace("&", "");
            return val.Trim();
        }
    }
}
