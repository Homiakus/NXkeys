using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NX2512_HotkeyStudio.Models
{
    /// <summary>
    /// Upgrades legacy positional W/E/D/C/X/Z/A/Q commands to a stable mnemonic language.
    /// Exact BUTTON IDs keep explicit paths; every other catalog command receives a deterministic
    /// path from its action/object/name tokens. Paths are scoped by the active NX module.
    /// </summary>
    public static class MnemonicPathGenerator
    {
        private sealed class MnemonicDefinition
        {
            public string[] Path { get; set; }
            public string[][] Aliases { get; set; } = Array.Empty<string[]>();
            public string[] SearchAliases { get; set; } = Array.Empty<string>();
        }

        private static readonly IReadOnlyDictionary<string, string> RootLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["C"] = "Create", ["E"] = "Edit", ["T"] = "Transform", ["X"] = "Remove",
                ["P"] = "Process", ["I"] = "Inspect", ["V"] = "View", ["S"] = "Select",
                ["A"] = "Annotate", ["M"] = "Manage", ["F"] = "File", ["G"] = "Go",
                ["U"] = "Utilities", ["H"] = "Help"
            };

        private static readonly IReadOnlyDictionary<string, string> ObjectLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["A"] = "Annotation", ["B"] = "Body/Base", ["C"] = "Component", ["D"] = "Dimension/Datum",
                ["E"] = "Edge", ["F"] = "Feature", ["G"] = "Geometry", ["H"] = "Sheet Metal",
                ["K"] = "Constraint", ["L"] = "Layer", ["M"] = "Material/Mold", ["N"] = "Simulation",
                ["O"] = "Operation", ["P"] = "Part", ["R"] = "Routing", ["S"] = "Sketch",
                ["T"] = "Tool/Template", ["U"] = "Surface", ["V"] = "View", ["W"] = "WAVE",
                ["Y"] = "Assembly", ["Z"] = "Other"
            };

        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "UG", "NX", "CREATE", "NEW", "ADD", "EDIT", "DELETE", "REMOVE", "UPDATE", "OPEN", "SHOW",
            "VIEW", "FEATURE", "COMMAND", "OBJECT", "TO", "FROM", "BY", "IN", "OF", "AND", "THE",
            "MODEL", "MODELING", "APPLICATION", "MANAGER", "TOOLS", "TOOL", "SETTINGS", "SETTING"
        };

        private static readonly IReadOnlyDictionary<string, MnemonicDefinition> Known = BuildKnown();

        public static void Apply(IEnumerable<ModuleConfig> modules)
        {
            foreach (ModuleConfig module in modules ?? Enumerable.Empty<ModuleConfig>())
            {
                if (module == null || !module.Enabled) continue;
                List<ModuleCommand> commands = module.CommandSets?
                    .Where(set => set?.Commands != null)
                    .SelectMany(set => set.Commands)
                    .Where(command => command != null && command.Enabled)
                    .OrderBy(SupportPriority)
                    .ThenBy(command => command.DisplayOrder <= 0 ? int.MaxValue : command.DisplayOrder)
                    .ThenBy(command => command.Command?.ID, StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<ModuleCommand>();

                var usedCanonical = new Dictionary<string, ModuleCommand>(StringComparer.OrdinalIgnoreCase);
                foreach (ModuleCommand command in commands)
                {
                    command.Path ??= new List<string>();
                    command.PathLabels ??= new List<string>();
                    command.Aliases ??= new List<List<string>>();
                    command.SearchAliases ??= new List<string>();

                    MnemonicDefinition definition = ResolveDefinition(command);
                    IReadOnlyList<string> requested = NormalizePath(command.Path);
                    bool locked = command.PathLocked || string.Equals(command.PathSource, "user", StringComparison.OrdinalIgnoreCase);
                    List<string> canonical;
                    if (locked)
                    {
                        if (requested.Count == 0)
                            throw new InvalidOperationException("Locked mnemonic path is empty for " + (command.Command?.ID ?? command.Command?.Name));
                        string lockedKey = string.Concat(requested);
                        if (usedCanonical.ContainsKey(lockedKey) || CreatesPrefixConflict(lockedKey, usedCanonical.Keys))
                            throw new InvalidOperationException("Locked mnemonic path conflicts in module " + module.ID + ": " + string.Join(" → ", requested));
                        canonical = requested.ToList();
                        usedCanonical[lockedKey] = command;
                    }
                    else
                    {
                        if (requested.Count == 0) requested = NormalizePath(definition?.Path);
                        if (requested.Count == 0) requested = GenerateCandidate(module, command);
                        canonical = ReserveUnique(requested, command, usedCanonical);
                    }
                    command.Path = canonical;

                    if (command.PathLabels.Count != canonical.Count)
                        command.PathLabels = BuildLabels(canonical, command.Command?.Name);

                    MergeSearchAliases(command, definition);
                    if ((command.Aliases == null || command.Aliases.Count == 0) && definition?.Aliases != null)
                        command.Aliases = definition.Aliases.Select(alias => NormalizePath(alias).ToList()).Where(alias => alias.Count > 0).ToList();
                }

                FilterAliases(commands, usedCanonical);
            }
        }

        public static IReadOnlyList<string> NormalizePath(IEnumerable<string> path)
        {
            if (path == null) return Array.Empty<string>();
            return path.Select(LeaderKeyConfig.NormalizeInputKey)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Take(5)
                .ToArray();
        }

        private static MnemonicDefinition ResolveDefinition(ModuleCommand command)
        {
            string id = command?.Command?.ID ?? string.Empty;
            return Known.TryGetValue(id, out MnemonicDefinition value) ? value : null;
        }

        private static List<string> ReserveUnique(
            IReadOnlyList<string> requested,
            ModuleCommand command,
            IDictionary<string, ModuleCommand> used)
        {
            List<string> candidate = requested.Count > 0 ? requested.ToList() : new List<string> { "M", "Z", "X" };
            if (candidate.Count < 2) candidate.Insert(0, "M");
            string key = string.Concat(candidate);
            int targetLength = TargetLength(command);
            bool support = IsSupport(command);
            if ((support || candidate.Count <= targetLength) && !used.ContainsKey(key) && !CreatesPrefixConflict(key, used.Keys))
            {
                used[key] = command;
                return candidate;
            }

            string root = candidate.FirstOrDefault() ?? "M";
            string objectToken = candidate.Count > 1 ? candidate[1] : "Z";
            string[] letters = CandidateLetters(command).ToArray();
            string[] rootAlternatives = new[] { root, "C", "E", "T", "X", "P", "I", "V", "A", "M", "F", "U", "H" }
                .Where(value => !string.Equals(value, "S", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(value, "G", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string[] objectAlternatives = new[]
            {
                objectToken, "Z", "F", "G", "B", "C", "D", "E", "H", "K", "L", "M",
                "N", "O", "P", "R", "S", "T", "U", "V", "W", "Y"
            }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (targetLength <= 2)
            {
                foreach (string alternativeRoot in rootAlternatives)
                {
                    foreach (string token in letters)
                    {
                        List<string> alternative = new List<string> { alternativeRoot, token };
                        key = string.Concat(alternative);
                        if (used.ContainsKey(key) || CreatesPrefixConflict(key, used.Keys)) continue;
                        used[key] = command;
                        return alternative;
                    }
                }
            }
            if (targetLength <= 3)
            {
                foreach (string alternativeRoot in rootAlternatives)
                {
                    foreach (string alternativeObject in objectAlternatives)
                    {
                        foreach (string token in letters)
                        {
                            List<string> alternative = new List<string> { alternativeRoot, alternativeObject, token };
                            key = string.Concat(alternative);
                            if (used.ContainsKey(key) || CreatesPrefixConflict(key, used.Keys)) continue;
                            used[key] = command;
                            return alternative;
                        }
                    }
                }
            }

            foreach (string alternativeRoot in rootAlternatives)
            {
                foreach (string alternativeObject in objectAlternatives)
                {
                    foreach (string first in letters)
                    {
                        foreach (string second in letters)
                        {
                            if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase)) continue;
                            List<string> alternative = new List<string> { alternativeRoot, alternativeObject, first, second };
                            key = string.Concat(alternative);
                            if (used.ContainsKey(key) || CreatesPrefixConflict(key, used.Keys)) continue;
                            used[key] = command;
                            return alternative;
                        }
                    }
                }
            }
            if (targetLength <= 4)
                throw new InvalidOperationException("Unable to allocate a <=" + targetLength + "-token mnemonic path for " + (command?.Command?.ID ?? command?.Command?.Name));

            foreach (string alternativeRoot in new[] { root, "M", "U", "H", "I", "E", "T", "C", "P", "V", "A", "X" }.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                for (int index = 1; index <= 99; index++)
                {
                    List<string> alternative = new[] { alternativeRoot, "Z" }
                        .Concat(index.ToString().Select(character => character.ToString())).Take(5).ToList();
                    key = string.Concat(alternative);
                    if (used.ContainsKey(key) || CreatesPrefixConflict(key, used.Keys)) continue;
                    used[key] = command;
                    return alternative;
                }
            }

            throw new InvalidOperationException("Unable to allocate mnemonic path for " + (command?.Command?.ID ?? command?.Command?.Name));
        }

        private static bool CreatesPrefixConflict(string candidate, IEnumerable<string> existing)
        {
            return existing.Any(value =>
                candidate.StartsWith(value, StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));
        }

        private static int SupportPriority(ModuleCommand command)
        {
            if (string.Equals(command?.SupportKind, "selection_filter", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(command?.SupportKind, "module_switch", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(command?.Action, "set_selection_filter", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(command?.Action, "switch_module", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        private static bool IsSupport(ModuleCommand command) => SupportPriority(command) < 2;

        private static int TargetLength(ModuleCommand command)
        {
            if (IsSupport(command)) return 2;
            switch ((command?.Frequency ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "K5": return 2;
                case "K4": return 3;
                case "K3": return 4;
                case "K2":
                case "K1": return 5;
                default: return 5;
            }
        }

        private static IReadOnlyList<string> GenerateCandidate(ModuleConfig module, ModuleCommand command)
        {
            string text = BuildText(command);
            string root = ActionRoot(text);
            string objectToken = ObjectToken(module?.ID, text, root);
            string leaf = CandidateLetters(command).FirstOrDefault() ?? "X";
            return new[] { root, objectToken, leaf };
        }

        private static string ActionRoot(string text)
        {
            if (ContainsAny(text, "UG_SEL_", "SELECT", "DESELECT", "SELECTION")) return "S";
            if (ContainsAny(text, "DELETE", "REMOVE", "DEASSIGN", "BREAK LINK", "CLEAR")) return "X";
            if (ContainsAny(text, "DIMENSION", "ANNOTATION", "NOTE", "SYMBOL", "BALLOON", "LABEL", "GD&T", "PMI")) return "A";
            if (ContainsAny(text, "MEASURE", "INFORMATION", "INFO", "ANALYSIS", "VALIDATE", "CHECK", "RESULT", "STATISTIC")) return "I";
            if (ContainsAny(text, "VIEW", "DISPLAY", "SHOW", "HIDE", "FIT", "ORIENT", "TRIMETRIC", "TRANSPARENCY")) return "V";
            if (ContainsAny(text, "GENERATE", "REGENERATE", "SOLVE", "POSTPROCESS", "SIMULATE", "VERIFY", "REFRESH", "UPDATE")) return "P";
            if (ContainsAny(text, "MOVE", "ROTATE", "MIRROR", "PATTERN", "OFFSET", "SCALE", "COPY", "ALIGN", "REORDER")) return "T";
            if (ContainsAny(text, "EDIT", "REPLACE", "TRIM", "EXTEND", "SEW", "UNTRIM", "UNBEND", "REBEND", "STYLE")) return "E";
            if (ContainsAny(text, "CREATE", "NEW", "ADD", "PLACE", "ASSIGN", "BASE", "FLANGE", "BEND", "HOLE", "EXTRUDE", "REVOLVE")) return "C";
            if (ContainsAny(text, "NAVIGATOR", "LAYER", "MATERIAL", "LIBRARY", "PREFERENCE", "SETTING", "MANAGER", "PALETTE", "EXPRESSIONS")) return "M";
            return "M";
        }

        private static string ObjectToken(string moduleId, string text, string root)
        {
            if (ContainsAny(text, "LAYER")) return "L";
            if (ContainsAny(text, "MATERIAL", "APPEARANCE")) return "M";
            if (ContainsAny(text, "WAVE", "INTERPART")) return "W";
            if (ContainsAny(text, "COMPONENT", "ASSEMBL")) return "C";
            if (ContainsAny(text, "CONSTRAINT")) return "K";
            if (ContainsAny(text, "DIMENSION", "DATUM")) return "D";
            if (ContainsAny(text, "SKETCH")) return "S";
            if (ContainsAny(text, "SURFACE", "SHEET", "FACE CURVATURE", "THROUGH CURVES", "SWEPT"))
                return ContainsAny(text, "SHEET METAL", "SBSM", "FLANGE", "BEND") ? "H" : "U";
            if (ContainsAny(text, "BODY")) return "B";
            if (ContainsAny(text, "EDGE")) return "E";
            if (ContainsAny(text, "FEATURE")) return "F";
            if (ContainsAny(text, "OPERATION", "TOOL PATH", "CAM_", "MANUFACTURING")) return "O";
            if (ContainsAny(text, "TOOL")) return "T";
            if (ContainsAny(text, "SIM_", "SIMULATION", "SOLUTION", "MESH", "LOAD")) return "N";
            if (ContainsAny(text, "ROUTE", "ROUTING", "STOCK")) return "R";
            if (ContainsAny(text, "MOLD", "EJECTOR", "GATE", "COOLING")) return "M";
            if (ContainsAny(text, "VIEW")) return "V";
            if (ContainsAny(text, "CURVE", "LINE", "RECTANGLE", "CIRCLE", "ARC", "GEOMETRY")) return "G";
            if (root == "S") return SelectionObjectToken(text);

            switch ((moduleId ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "modeling": return "F";
                case "sketch": return "G";
                case "assembly": return "C";
                case "drafting": return root == "A" ? "D" : "V";
                case "pmi": return "A";
                case "surface": return "U";
                case "sheet_metal": return "H";
                case "manufacturing": return "O";
                case "simulation": return "N";
                case "routing": return "R";
                case "mold": return "M";
                case "reuse": return "T";
                case "inspect_view": return root == "V" ? "V" : "M";
                case "selection_object": return SelectionObjectToken(text);
                default: return "Z";
            }
        }

        private static string SelectionObjectToken(string text)
        {
            if (ContainsAny(text, "BODY")) return "B";
            if (ContainsAny(text, "FACE")) return "F";
            if (ContainsAny(text, "EDGE")) return "E";
            if (ContainsAny(text, "FEATURE")) return "T";
            if (ContainsAny(text, "COMPONENT")) return "C";
            if (ContainsAny(text, "CURVE")) return "U";
            if (ContainsAny(text, "DATUM")) return "D";
            if (ContainsAny(text, "SELECT ALL")) return "A";
            if (ContainsAny(text, "DESELECT", "CLEAR")) return "N";
            if (ContainsAny(text, "RESET")) return "R";
            return "Z";
        }

        private static IEnumerable<string> CandidateLetters(ModuleCommand command)
        {
            string[] tokens = Tokens(BuildText(command)).Where(token => !StopWords.Contains(token)).ToArray();
            var result = new List<string>();
            foreach (string token in tokens.Reverse())
            {
                foreach (char character in token.Where(char.IsLetterOrDigit))
                {
                    string value = char.ToUpperInvariant(character).ToString();
                    if (!result.Contains(value, StringComparer.OrdinalIgnoreCase)) result.Add(value);
                }
            }
            foreach (string fallback in new[] { "A", "E", "I", "O", "U", "R", "N", "L", "D", "P", "G", "B", "C", "F", "H", "J", "K", "M", "Q", "S", "T", "V", "W", "X", "Y", "Z" })
                if (!result.Contains(fallback, StringComparer.OrdinalIgnoreCase)) result.Add(fallback);
            return result;
        }

        public static List<string> BuildPathLabels(IReadOnlyList<string> path, string commandName) =>
            BuildLabels(path ?? Array.Empty<string>(), commandName);

        private static List<string> BuildLabels(IReadOnlyList<string> path, string commandName)
        {
            var labels = new List<string>();
            for (int index = 0; index < path.Count; index++)
            {
                string token = path[index];
                if (index == 0 && RootLabels.TryGetValue(token, out string root)) labels.Add(root);
                else if (index == 1 && ObjectLabels.TryGetValue(token, out string obj)) labels.Add(obj);
                else labels.Add(index == path.Count - 1 && !string.IsNullOrWhiteSpace(commandName) ? commandName : token);
            }
            return labels;
        }

        private static void MergeSearchAliases(ModuleCommand command, MnemonicDefinition definition)
        {
            var aliases = new HashSet<string>(command.SearchAliases ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (string value in command.Command?.Aliases ?? new List<string>()) if (!string.IsNullOrWhiteSpace(value)) aliases.Add(value.Trim());
            foreach (string value in definition?.SearchAliases ?? Array.Empty<string>()) if (!string.IsNullOrWhiteSpace(value)) aliases.Add(value.Trim());
            if (!string.IsNullOrWhiteSpace(command.Command?.Name)) aliases.Add(command.Command.Name);
            if (!string.IsNullOrWhiteSpace(command.Command?.ID)) aliases.Add(command.Command.ID);
            command.SearchAliases = aliases.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void FilterAliases(List<ModuleCommand> commands, IDictionary<string, ModuleCommand> canonical)
        {
            var accepted = new Dictionary<string, ModuleCommand>(canonical, StringComparer.OrdinalIgnoreCase);
            foreach (ModuleCommand command in commands)
            {
                var clean = new List<List<string>>();
                foreach (List<string> raw in command.Aliases ?? new List<List<string>>())
                {
                    List<string> alias = NormalizePath(raw).ToList();
                    if (alias.Count == 0) continue;
                    string key = string.Concat(alias);
                    string canonicalKey = string.Concat(NormalizePath(command.Path));
                    if (string.Equals(key, canonicalKey, StringComparison.OrdinalIgnoreCase)) continue;
                    if (accepted.ContainsKey(key) || CreatesPrefixConflict(key, accepted.Keys)) continue;
                    accepted[key] = command;
                    clean.Add(alias);
                }
                command.Aliases = clean;
            }
        }

        private static string BuildText(ModuleCommand command) =>
            string.Join(" ", command?.Command?.ID ?? string.Empty, command?.Command?.Name ?? string.Empty,
                command?.SubmenuLabel ?? string.Empty, command?.Notes ?? string.Empty).ToUpperInvariant();

        private static string[] Tokens(string value) =>
            Regex.Split(value ?? string.Empty, "[^A-Z0-9]+")
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToArray();

        private static bool ContainsAny(string text, params string[] values) =>
            values.Any(value => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);

        private static IReadOnlyDictionary<string, MnemonicDefinition> BuildKnown()
        {
            var map = new Dictionary<string, MnemonicDefinition>(StringComparer.OrdinalIgnoreCase);
            void Add(string id, string path, string[] aliases = null, params string[] search)
            {
                map[id] = new MnemonicDefinition
                {
                    Path = Split(path),
                    Aliases = (aliases ?? Array.Empty<string>()).Select(Split).ToArray(),
                    SearchAliases = search ?? Array.Empty<string>()
                };
            }

            Add("UG_CREATE_SKETCH", "C S K", new[] { "C S" }, "создать эскиз");
            Add("UG_MODELING_EXTRUDED_FEATURE", "C F E", new[] { "C E" }, "extrude", "вытянуть", "выдавливание");
            Add("UG_MODELING_HOLE_FEATURE", "C F H", new[] { "C H" }, "hole", "отверстие");
            Add("UG_MODELING_REVOLVED_FEATURE", "C F R", new[] { "C R" }, "revolve", "вращение");
            Add("UG_MODELING_BLEND_FEATURE", "E E B", new[] { "E B" }, "blend", "скругление");
            Add("UG_MODELING_CHAMFER_FEATURE", "E E C", new[] { "E C" }, "chamfer", "фаска");
            Add("UG_MODELING_PATTERNFEATURE_FEATURE", "T F P", new[] { "T P" }, "pattern", "массив");
            Add("UG_MODELING_MIRRORFEATURE_FEATURE", "T F M", new[] { "T M" }, "mirror", "зеркало");

            Add("UG_SKETCH_LINE", "C G L");
            Add("UG_SKETCH_RECTANGLE", "C G R");
            Add("UG_SKETCH_CIRCLE", "C G C");
            Add("UG_SKETCH_ARC", "C G A");
            Add("UG_SKETCH_TRIM", "E G T");
            Add("UG_SKETCH_EXTEND", "E G E");
            Add("UG_SKETCH_OFFSET_CURVE", "T G O");
            Add("UG_SKETCH_LINE_BY_TWO_POINTS", "C G L 2");
            Add("UG_SKETCH_LINE_FROM_MIDPOINT", "C G L M");
            Add("UG_SKETCH_RECTANGLE_BY_TWO_POINTS", "C G R 2");
            Add("UG_SKETCH_RECTANGLE_FROM_CENTER", "C G R C");
            Add("UG_SKETCH_RECTANGLE_BY_THREE_POINTS", "C G R 3");
            Add("UG_SKETCH_CIRCLE_FROM_CENTER", "C G C C");
            Add("UG_SKETCH_CIRCLE_BY_THREE_POINTS", "C G C 3");
            Add("UG_SKETCH_ARC_BY_THREE_POINTS", "C G A 3");
            Add("UG_SKETCH_ARC_FROM_CENTER", "C G A C");
            Add("UG_SKETCH_RAPID_DIMENSION", "A D R");
            Add("UG_SKETCH_LINEAR_DIMENSION", "A D L");
            Add("UG_SKETCH_COINCIDENT_CONSTRAINT", "C K C");
            Add("UG_SKETCH_TANGENT_CONSTRAINT", "C K T");
            Add("UG_SKETCH_PARALLEL_CONSTRAINT", "C K P");
            Add("UG_SKETCH_PERPENDICULAR_CONSTRAINT", "C K N");
            Add("UG_SKETCH_HORIZONTAL_CONSTRAINT", "C K H");
            Add("UG_SKETCH_VERTICAL_CONSTRAINT", "C K V");
            Add("UG_SKETCH_CHECKER", "I S C");

            Add("UG_ASSEMBLIES_ADD_COMPONENT", "C C A", new[] { "C A" });
            Add("UG_ASSEMBLIES_NEW_COMPONENT", "C C N", new[] { "C N" });
            Add("UG_ASSEMBLIES_MOVE_COMPONENT", "T C M", new[] { "T M" });
            Add("UG_ASSEMBLIES_CONSTRAINTS", "C K A", new[] { "C K" });
            Add("UG_ASSEMBLIES_REPLACE_COMPONENT", "E C R", new[] { "E R" });
            Add("UG_ASSEMBLIES_REMOVE_COMPONENT", "X C R", new[] { "X C" });
            Add("UG_ASSEMBLIES_PATTERN_COMPONENT", "T C P", new[] { "T P" });
            Add("UG_ASSEMBLIES_NAVIGATOR", "M N A", new[] { "M N" });

            Add("UG_SEL_BODY_PRIORITY", "S B");
            Add("UG_SEL_FACE_PRIORITY", "S F");
            Add("UG_SEL_EDGE_PRIORITY", "S E");
            Add("UG_SEL_FEATURE_PRIORITY", "S T");
            Add("UG_SEL_COMPONENT_PRIORITY", "S C");
            Add("UG_SEL_CURVE_PRIORITY", "S U");
            Add("UG_SEL_DATUM_PRIORITY", "S D");
            Add("UG_SEL_RESET", "S R");
            Add("UG_SEL_TYPE_RESET", "S R");
            Add("UG_SEL_SELECT_ALL", "S A");
            Add("UG_SEL_DESELECT_ALL", "S N");

            Add("UG_ASSY_WAVE_LINKER", "M W L");
            Add("UG_MODELING_WAVE_LINKER", "M W L");
            Add("UG_ASSY_WAVE_INTERFACE_LINKER", "M W I");
            Add("UG_ASSY_WAVE_ASSOC_MANAGER", "M W A");
            Add("UG_ASSY_WAVE_GRAPH_BROWSER", "M W G");
            Add("UG_ASSY_WAVE_LOAD_DATA", "M W D");
            Add("UG_LAYER_SETTINGS", "M L S");
            Add("UG_LAYER_VIEW", "M L V");
            Add("UG_LAYER_CATEGORY", "M L A");
            Add("UG_LAYER_COPY", "M L C");
            Add("UG_LAYER_MOVE", "M L M");
            Add("UG_INFO_OTHER_LAYERS", "I L I");
            Add("UG_MATERIAL_ASSIGN", "M M A");
            Add("UG_MATERIAL_LIBRARY_MANAGER", "M M L");
            Add("UG_VIEW_PALETTE_SYSTEM_MATERIALS", "M M S");
            Add("UG_VIEW_PALETTE_MATERIALS_IN_PART", "M M P");
            Add("UG_DISPLAY_APPEARANCE_VISUAL_MATERIAL", "V M V");
            Add("UG_VIEW_SHED_MATERIAL_OVERRIDE_DEASSIGN", "X M V");

            Add("UG_DRAFTING_BASE_VIEW", "C V B");
            Add("UG_DRAFTING_PROJECTED_VIEW", "C V P");
            Add("UG_DRAFTING_SECTION_VIEW", "C V S");
            Add("UG_DRAFTING_DETAIL_VIEW", "C V D");
            Add("UG_DRAFTING_UPDATE_VIEWS", "P V U");
            Add("UG_DRAFTING_VIEW_STYLE", "E V S");
            Add("UG_DRAFTING_PARTS_LIST", "A P L");
            Add("UG_DRAFTING_RAPID_DIMENSION", "A D R");
            Add("UG_PMI_RAPID_DIMENSION", "A D R");
            Add("UG_PMI_DATUM_FEATURE_SYMBOL", "A G D");
            Add("UG_PMI_FEATURE_CONTROL_FRAME", "A G F");
            Add("UG_PMI_SURFACE_FINISH", "A S F");
            Add("UG_PMI_NOTE", "A N P");
            Add("UG_PMI_EDIT", "E A P");
            Add("UG_PMI_MODEL_VIEW", "V M P");
            Add("UG_PMI_VALIDATE", "I A V");

            Add("UG_MODELING_THROUGH_CURVES_FEATURE", "C U T");
            Add("UG_MODELING_SWEPT_FEATURE", "C U S");
            Add("UG_MODELING_STUDIO_SURFACE_FEATURE", "C U D");
            Add("UG_MODELING_TRIM_SHEET_FEATURE", "E U T");
            Add("UG_MODELING_SEW_FEATURE", "E U S");
            Add("UG_MODELING_UNTRIM_FEATURE", "E U U");
            Add("UG_MODELING_EXTRACT_GEOMETRY", "C G E");
            Add("UG_ANALYSIS_FACE_CURVATURE", "I U C");
            Add("UG_SHEET_METAL_BASE_TAB", "C H B");
            Add("UG_SHEET_METAL_FLANGE", "C H F");
            Add("UG_SHEET_METAL_CONTOUR_FLANGE", "C H C");
            Add("UG_SHEET_METAL_BEND", "E H B");
            Add("UG_SHEET_METAL_UNBEND", "T H U");
            Add("UG_SHEET_METAL_REBEND", "T H R");
            Add("UG_SHEET_METAL_FLAT_PATTERN", "P H F");
            Add("UG_SHEET_METAL_VALIDATE", "I H V");
            Add("UG_SBSM_SHEETMETAL_FROM_SOLID_FEATURE", "T H C");
            Add("UG_MODELING_SHEET_FEATURE", "C H S");
            Add("UG_MODELING_FF_EXTEND_SHEET", "E H E");
            Add("UG_INFO_ANALYSIS_SHEET_BOUNDARY", "I H B");

            Add("UG_CAM_CREATE_OPERATION", "C O O", new[] { "C O" });
            Add("UG_CAM_CREATE_TOOL", "C T T", new[] { "C T" });
            Add("UG_CAM_GENERATE_TOOL_PATH", "P O G", new[] { "P G" });
            Add("UG_CAM_VERIFY_TOOL_PATH", "P O V", new[] { "P V" });
            Add("UG_CAM_POSTPROCESS", "P O P", new[] { "P P" });
            Add("UG_CAM_DELETE_OPERATION", "X O D", new[] { "X O" });
            Add("UG_CAM_OPERATION_NAVIGATOR", "M N O", new[] { "M N" });
            Add("UG_CAM_INFORMATION", "I O T", new[] { "I T" });

            Add("UG_SIM_CREATE_SOLUTION", "C N S");
            Add("UG_SIM_CREATE_LOAD", "C N L");
            Add("UG_SIM_CREATE_CONSTRAINT", "C N C");
            Add("UG_SIM_MESH", "P N M");
            Add("UG_SIM_SOLVE", "P N S");
            Add("UG_SIM_DELETE", "X N D");
            Add("UG_SIM_NAVIGATOR", "M N S");
            Add("UG_SIM_RESULTS", "I N R");

            Add("UG_ROUTE_CREATE_ROUTE", "C R R");
            Add("UG_ROUTE_PLACE_PART", "C R P");
            Add("UG_ROUTE_ADD_STOCK", "C R S");
            Add("UG_ROUTE_EDIT_ROUTE", "E R R");
            Add("UG_ROUTE_DELETE", "X R D");
            Add("UG_ROUTE_REMOVE_PART", "X R P");
            Add("UG_ROUTE_NAVIGATOR", "M N R");
            Add("UG_ROUTE_VALIDATE", "I R V");

            Add("UG_MOLD_INITIALIZE_PROJECT", "C M I");
            Add("UG_MOLD_PARTING", "C M P");
            Add("UG_MOLD_MOLD_BASE", "C M B");
            Add("UG_MOLD_GATE", "C M G");
            Add("UG_MOLD_COOLING", "C M C");
            Add("UG_MOLD_EJECTOR", "C M E");
            Add("UG_MOLD_LIBRARY", "M L M");
            Add("UG_MOLD_VALIDATE", "I M V");
            Add("UG_EXPRESSIONS", "U E X");
            Add("UG_NAVIGATOR_REUSE_LIBRARY", "M L R");
            Add("UG_CREATE_FEATURE_TEMPLATE", "C T F");
            Add("UG_REPLACE_FEATURE_TEMPLATE", "E T F");
            Add("UG_NAVIGATOR_PART", "M N P");
            Add("UG_PARAMETER_TABLE", "M P T");
            Add("UG_HELP_COMMAND_FINDER", "H C F");

            Add("UG_VIEW_FIT", "V F T", new[] { "V F" });
            Add("UG_VIEW_POPUP_ORIENT_TFRTRI", "V T R");
            Add("UG_INFO_GEOMETRIC_MEASUREMENT", "I M G", new[] { "I M" });
            Add("UG_INFO_OBJECT", "I O B");
            Add("UG_EDIT_BLANK_SELECTED", "V H S");
            Add("UG_EDIT_MD_SHOWHIDE_ALL", "V S H");

            return map;
        }

        private static string[] Split(string value) =>
            (value ?? string.Empty).Split(new[] { ' ', '-', '>' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(LeaderKeyConfig.NormalizeInputKey)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToArray();
    }
}
