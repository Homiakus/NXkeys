using System;
using System.Collections.Generic;
using System.Linq;

namespace NX2512_HotkeyStudio.Models
{
    /// <summary>
    /// Semantic grammar for the Sketch workspace.
    /// Sketch paths always keep the action -> object -> operation hierarchy and never spill into
    /// unrelated fallback roots. User-locked paths remain untouched.
    /// </summary>
    public static partial class MnemonicPathGenerator
    {
        private static readonly IReadOnlyDictionary<string, MnemonicDefinition> SketchKnown = BuildSketchKnown();

        private static readonly IReadOnlyDictionary<string, string> SketchOperationLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["G"] = "Geometry",
                ["K"] = "Constraint",
                ["D"] = "Dimension",
                ["S"] = "Sketch",
                ["V"] = "Variant"
            };

        private static MnemonicDefinition ResolveDefinitionForModule(ModuleConfig module, ModuleCommand command)
        {
            string id = command?.Command?.ID ?? string.Empty;
            if (IsSketchIntentCommand(module, command) && SketchKnown.TryGetValue(id, out MnemonicDefinition sketch))
                return sketch;
            return ResolveDefinition(command);
        }

        private static bool ShouldRegenerateSketchPath(ModuleConfig module, ModuleCommand command)
        {
            if (!IsSketchIntentCommand(module, command)) return false;
            if (command?.PathLocked == true) return false;
            return !string.Equals(command?.PathSource, "user", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<string> GenerateCandidateForModule(ModuleConfig module, ModuleCommand command) =>
            IsSketchIntentCommand(module, command) ? GenerateSketchCandidate(command) : GenerateCandidate(module, command);

        private static List<string> ReserveUniqueForModule(
            ModuleConfig module,
            IReadOnlyList<string> requested,
            ModuleCommand command,
            IDictionary<string, ModuleCommand> used) =>
            IsSketchIntentCommand(module, command)
                ? ReserveSketchPath(requested, command, used)
                : ReserveUnique(requested, command, used);

        private static List<string> BuildPathLabelsForModule(
            ModuleConfig module,
            ModuleCommand command,
            IReadOnlyList<string> path)
        {
            if (!IsSketchIntentCommand(module, command)) return BuildLabels(path, command?.Command?.Name);

            var labels = new List<string>();
            for (int index = 0; index < path.Count; index++)
            {
                string token = path[index];
                if (index == 0 && RootLabels.TryGetValue(token, out string root)) labels.Add(root);
                else if (index == 1 && SketchOperationLabels.TryGetValue(token, out string area)) labels.Add(area);
                else if (index == 2 && string.Equals(token, "V", StringComparison.OrdinalIgnoreCase)) labels.Add("Variant");
                else if (index == path.Count - 1 && !string.IsNullOrWhiteSpace(command?.Command?.Name)) labels.Add(command.Command.Name);
                else labels.Add(token);
            }
            return labels;
        }

        private static void NormalizeSketchAliases(ModuleConfig module, ModuleCommand command, bool locked)
        {
            if (!IsSketchIntentCommand(module, command) || locked) return;
            // Positional W/E/D/C/X/Z/A/Q aliases were the main source of ambiguity in Sketch.
            command.Aliases = new List<List<string>>();
            string id = command?.Command?.ID ?? string.Empty;
            command.PathSource = SketchKnown.ContainsKey(id) ? "sketch_curated" : "sketch_generated";
        }

        private static bool IsSketchIntentCommand(ModuleConfig module, ModuleCommand command)
        {
            if (!string.Equals(module?.ID, "sketch", StringComparison.OrdinalIgnoreCase)) return false;
            if (command == null || IsSupport(command)) return false;
            return true;
        }

        private static IReadOnlyList<string> GenerateSketchCandidate(ModuleCommand command)
        {
            string id = command?.Command?.ID ?? string.Empty;
            if (SketchKnown.TryGetValue(id, out MnemonicDefinition known)) return known.Path;

            string text = BuildText(command);
            string[] family;
            if (ContainsAny(text, "DIMENSION", "RADIUS DIM", "DIAMETER DIM")) family = new[] { "A", "D" };
            else if (ContainsAny(text, "CONSTRAINT", "COINCIDENT", "TANGENT", "PARALLEL", "PERPENDICULAR", "HORIZONTAL", "VERTICAL", "FIX")) family = new[] { "C", "K" };
            else if (ContainsAny(text, "TRIM", "EXTEND", "FILLET", "BLEND", "CHAMFER", "BREAK", "JOIN", "CORNER")) family = new[] { "E", "G" };
            else if (ContainsAny(text, "OFFSET", "MIRROR", "MOVE", "ROTATE", "SCALE", "PATTERN", "COPY")) family = new[] { "T", "G" };
            else if (ContainsAny(text, "DELETE", "REMOVE")) family = new[] { "X", "G" };
            else if (ContainsAny(text, "CHECK", "VALIDATE", "INSPECT", "ANALYSIS", "SOLVE")) family = new[] { "I", "S" };
            else if (ContainsAny(text, "SETTING", "PREFERENCE", "MANAGE")) family = new[] { "M", "S" };
            else family = new[] { "C", "G" };

            string leaf = SketchLeaf(text, command);
            return new[] { family[0], family[1], leaf };
        }

        private static string SketchLeaf(string text, ModuleCommand command)
        {
            var preferred = new[]
            {
                (new[] { "RECTANGLE" }, "R"), (new[] { "CIRCLE" }, "C"), (new[] { "ARC" }, "A"),
                (new[] { "LINE" }, "L"), (new[] { "SPLINE" }, "S"), (new[] { "ELLIPSE" }, "E"),
                (new[] { "POINT" }, "P"), (new[] { "POLYGON" }, "Y"), (new[] { "OFFSET" }, "O"),
                (new[] { "TRIM" }, "T"), (new[] { "EXTEND" }, "E"), (new[] { "FILLET", "BLEND" }, "F"),
                (new[] { "CHAMFER" }, "H"), (new[] { "COINCIDENT" }, "C"), (new[] { "TANGENT" }, "T"),
                (new[] { "PARALLEL" }, "P"), (new[] { "PERPENDICULAR" }, "N"),
                (new[] { "HORIZONTAL" }, "H"), (new[] { "VERTICAL" }, "V"),
                (new[] { "RAPID" }, "R"), (new[] { "LINEAR" }, "L"), (new[] { "CHECK" }, "C")
            };
            foreach (var item in preferred)
                if (item.Item1.Any(value => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0))
                    return item.Item2;

            foreach (string candidate in CandidateLetters(command))
            {
                // C-G-V is reserved as the explicit variant branch.
                if (string.Equals(candidate, "V", StringComparison.OrdinalIgnoreCase)) continue;
                return candidate;
            }
            return "X";
        }

        private static List<string> ReserveSketchPath(
            IReadOnlyList<string> requested,
            ModuleCommand command,
            IDictionary<string, ModuleCommand> used)
        {
            List<string> preferred = NormalizePath(requested).ToList();
            if (preferred.Count >= 3 && preferred.Count <= 5 && TryReserveSketch(preferred, command, used))
                return preferred;

            IReadOnlyList<string> generated = GenerateSketchCandidate(command);
            string root = generated.ElementAtOrDefault(0) ?? "C";
            string area = generated.ElementAtOrDefault(1) ?? "G";
            string requestedLeaf = generated.ElementAtOrDefault(2) ?? "X";
            var letters = new[] { requestedLeaf }
                .Concat(CandidateLetters(command))
                .Where(value => !(root == "C" && area == "G" && value == "V"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (string leaf in letters)
            {
                var candidate = new List<string> { root, area, leaf };
                if (TryReserveSketch(candidate, command, used)) return candidate;
            }

            foreach (string leaf in letters)
            {
                foreach (string variant in CandidateLetters(command).Concat(new[] { "2", "3", "C", "M", "A", "B" }).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var candidate = new List<string> { root, area, "V", leaf, variant };
                    if (TryReserveSketch(candidate, command, used)) return candidate;
                }
            }

            throw new InvalidOperationException("Unable to allocate a semantic Sketch path for " +
                (command?.Command?.ID ?? command?.Command?.Name));
        }

        private static bool TryReserveSketch(
            IReadOnlyList<string> candidate,
            ModuleCommand command,
            IDictionary<string, ModuleCommand> used)
        {
            string key = string.Concat(candidate);
            if (used.ContainsKey(key) || CreatesPrefixConflict(key, used.Keys)) return false;
            used[key] = command;
            return true;
        }

        private static IReadOnlyDictionary<string, MnemonicDefinition> BuildSketchKnown()
        {
            var map = new Dictionary<string, MnemonicDefinition>(StringComparer.OrdinalIgnoreCase);
            void Add(string id, string path, params string[] search) => map[id] = new MnemonicDefinition
            {
                Path = Split(path),
                Aliases = Array.Empty<string[]>(),
                SearchAliases = search ?? Array.Empty<string>()
            };

            Add("UG_SKETCH_LINE", "C G L", "линия");
            Add("UG_SKETCH_RECTANGLE", "C G R", "прямоугольник");
            Add("UG_SKETCH_CIRCLE", "C G C", "окружность");
            Add("UG_SKETCH_ARC", "C G A", "дуга");
            Add("UG_SKETCH_TRIM", "E G T", "обрезать");
            Add("UG_SKETCH_EXTEND", "E G E", "удлинить");
            Add("UG_SKETCH_OFFSET_CURVE", "T G O", "смещение кривой");

            Add("UG_SKETCH_LINE_BY_TWO_POINTS", "C G V L 2", "линия по двум точкам");
            Add("UG_SKETCH_LINE_FROM_MIDPOINT", "C G V L M", "линия от середины");
            Add("UG_SKETCH_RECTANGLE_BY_TWO_POINTS", "C G V R 2", "прямоугольник по двум точкам");
            Add("UG_SKETCH_RECTANGLE_FROM_CENTER", "C G V R C", "прямоугольник из центра");
            Add("UG_SKETCH_RECTANGLE_BY_THREE_POINTS", "C G V R 3", "прямоугольник по трём точкам");
            Add("UG_SKETCH_CIRCLE_FROM_CENTER", "C G V C C", "окружность из центра");
            Add("UG_SKETCH_CIRCLE_BY_THREE_POINTS", "C G V C 3", "окружность по трём точкам");
            Add("UG_SKETCH_ARC_BY_THREE_POINTS", "C G V A 3", "дуга по трём точкам");
            Add("UG_SKETCH_ARC_FROM_CENTER", "C G V A C", "дуга из центра");

            Add("UG_SKETCH_RAPID_DIMENSION", "A D R", "быстрый размер");
            Add("UG_SKETCH_LINEAR_DIMENSION", "A D L", "линейный размер");
            Add("UG_SKETCH_COINCIDENT_CONSTRAINT", "C K C", "совпадение");
            Add("UG_SKETCH_TANGENT_CONSTRAINT", "C K T", "касательность");
            Add("UG_SKETCH_PARALLEL_CONSTRAINT", "C K P", "параллельность");
            Add("UG_SKETCH_PERPENDICULAR_CONSTRAINT", "C K N", "перпендикулярность");
            Add("UG_SKETCH_HORIZONTAL_CONSTRAINT", "C K H", "горизонтальность");
            Add("UG_SKETCH_VERTICAL_CONSTRAINT", "C K V", "вертикальность");
            Add("UG_SKETCH_CHECKER", "I S C", "проверка эскиза");

            // NX exposes Sketch fillet/chamfer through shared Modeling BUTTON IDs.
            Add("UG_MODELING_BLEND_FEATURE", "E G F", "скругление эскиза");
            Add("UG_MODELING_CHAMFER_FEATURE", "E G H", "фаска эскиза");
            return map;
        }
    }
}
