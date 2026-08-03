#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8-sig")


def write(relative: str, content: str) -> None:
    target = ROOT / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content.replace("\r\n", "\n"), encoding="utf-8", newline="\n")


def replace_once(relative: str, old: str, new: str) -> None:
    text = read(relative)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{relative}: expected one match, found {count}: {old[:120]!r}")
    write(relative, text.replace(old, new, 1))


def insert_before(relative: str, marker: str, insertion: str) -> None:
    replace_once(relative, marker, insertion + marker)


# 1. Runtime: route Sketch through a dedicated semantic policy.
path = "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs"
text = read(path)
text = text.replace(
    "public static class MnemonicPathGenerator",
    "public static partial class MnemonicPathGenerator",
    1,
)
text = text.replace(
    "MnemonicDefinition definition = ResolveDefinition(command);\n                    IReadOnlyList<string> requested = NormalizePath(command.Path);",
    "MnemonicDefinition definition = ResolveDefinitionForModule(module, command);\n                    IReadOnlyList<string> requested = ShouldRegenerateSketchPath(module, command)\n                        ? Array.Empty<string>()\n                        : NormalizePath(command.Path);",
    1,
)
text = text.replace(
    "if (requested.Count == 0) requested = GenerateCandidate(module, command);\n                        canonical = ReserveUnique(requested, command, usedCanonical);",
    "if (requested.Count == 0) requested = GenerateCandidateForModule(module, command);\n                        canonical = ReserveUniqueForModule(module, requested, command, usedCanonical);",
    1,
)
text = text.replace(
    "                    command.Path = canonical;\n\n                    if (command.PathLabels.Count != canonical.Count)\n                        command.PathLabels = BuildLabels(canonical, command.Command?.Name);",
    "                    command.Path = canonical;\n                    NormalizeSketchAliases(module, command, locked);\n\n                    if (command.PathLabels.Count != canonical.Count || IsSketchIntentCommand(module, command))\n                        command.PathLabels = BuildPathLabelsForModule(module, command, canonical);",
    1,
)
write(path, text)

sketch_policy = r'''using System;
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
'''
write("NX2512_HotkeyStudio/Models/MnemonicPathGenerator.Sketch.cs", sketch_policy)


# 2. Shared Node policy: Sketch is a structured 3-5 token language, independent of K-level compression.
path = "scripts/sequence-policy.mjs"
text = read(path)
needle = "export function targetLengthForFrequency(frequency) {\n  return FREQUENCY_TARGET_LENGTH[String(frequency ?? '').trim()] ?? 5;\n}\n"
addition = needle + r'''

export function isSketchIntentCommand(moduleId, command) {
  return String(moduleId ?? '').trim().toLowerCase() === 'sketch' && !isSupportCommand(command);
}

export function targetLengthForCommand(moduleId, command) {
  // Sketch keeps action -> object -> operation and an optional explicit variant branch.
  return isSketchIntentCommand(moduleId, command) ? 5 : targetLengthForFrequency(command?.frequency);
}
'''
if text.count(needle) != 1:
    raise RuntimeError("sequence-policy.mjs: targetLengthForFrequency marker changed")
write(path, text.replace(needle, addition, 1))


# 3. Full compiler: rebuild Sketch from its own intent scope, use stable families, and drop positional aliases.
path = "scripts/compile-full-command-map.mjs"
text = read(path)
text = text.replace(
    "  isSupportCommand,\n  pathKey as policyPathKey,\n  supportMetadata,\n  targetLengthForFrequency",
    "  isSketchIntentCommand,\n  isSupportCommand,\n  pathKey as policyPathKey,\n  supportMetadata,\n  targetLengthForCommand,\n  targetLengthForFrequency",
    1,
)

marker = "function reservePath(preferred, name, used, frequency = '') {"
sketch_js = r'''const SKETCH_KNOWN_PATHS = new Map([
  ['UG_SKETCH_LINE', ['C', 'G', 'L']],
  ['UG_SKETCH_RECTANGLE', ['C', 'G', 'R']],
  ['UG_SKETCH_CIRCLE', ['C', 'G', 'C']],
  ['UG_SKETCH_ARC', ['C', 'G', 'A']],
  ['UG_SKETCH_TRIM', ['E', 'G', 'T']],
  ['UG_SKETCH_EXTEND', ['E', 'G', 'E']],
  ['UG_SKETCH_OFFSET_CURVE', ['T', 'G', 'O']],
  ['UG_SKETCH_LINE_BY_TWO_POINTS', ['C', 'G', 'V', 'L', '2']],
  ['UG_SKETCH_LINE_FROM_MIDPOINT', ['C', 'G', 'V', 'L', 'M']],
  ['UG_SKETCH_RECTANGLE_BY_TWO_POINTS', ['C', 'G', 'V', 'R', '2']],
  ['UG_SKETCH_RECTANGLE_FROM_CENTER', ['C', 'G', 'V', 'R', 'C']],
  ['UG_SKETCH_RECTANGLE_BY_THREE_POINTS', ['C', 'G', 'V', 'R', '3']],
  ['UG_SKETCH_CIRCLE_FROM_CENTER', ['C', 'G', 'V', 'C', 'C']],
  ['UG_SKETCH_CIRCLE_BY_THREE_POINTS', ['C', 'G', 'V', 'C', '3']],
  ['UG_SKETCH_ARC_BY_THREE_POINTS', ['C', 'G', 'V', 'A', '3']],
  ['UG_SKETCH_ARC_FROM_CENTER', ['C', 'G', 'V', 'A', 'C']],
  ['UG_SKETCH_RAPID_DIMENSION', ['A', 'D', 'R']],
  ['UG_SKETCH_LINEAR_DIMENSION', ['A', 'D', 'L']],
  ['UG_SKETCH_COINCIDENT_CONSTRAINT', ['C', 'K', 'C']],
  ['UG_SKETCH_TANGENT_CONSTRAINT', ['C', 'K', 'T']],
  ['UG_SKETCH_PARALLEL_CONSTRAINT', ['C', 'K', 'P']],
  ['UG_SKETCH_PERPENDICULAR_CONSTRAINT', ['C', 'K', 'N']],
  ['UG_SKETCH_HORIZONTAL_CONSTRAINT', ['C', 'K', 'H']],
  ['UG_SKETCH_VERTICAL_CONSTRAINT', ['C', 'K', 'V']],
  ['UG_SKETCH_CHECKER', ['I', 'S', 'C']],
  ['UG_MODELING_BLEND_FEATURE', ['E', 'G', 'F']],
  ['UG_MODELING_CHAMFER_FEATURE', ['E', 'G', 'H']]
]);

function sketchText(command) {
  return `${command?.command?.id ?? ''} ${command?.command?.name ?? ''} ${command?.submenu_label ?? ''} ${command?.notes ?? ''}`.toUpperCase();
}

function sketchLeaf(text, command) {
  const preferred = [
    [['RECTANGLE'], 'R'], [['CIRCLE'], 'C'], [['ARC'], 'A'], [['LINE'], 'L'],
    [['SPLINE'], 'S'], [['ELLIPSE'], 'E'], [['POINT'], 'P'], [['POLYGON'], 'Y'],
    [['OFFSET'], 'O'], [['TRIM'], 'T'], [['EXTEND'], 'E'], [['FILLET', 'BLEND'], 'F'],
    [['CHAMFER'], 'H'], [['COINCIDENT'], 'C'], [['TANGENT'], 'T'], [['PARALLEL'], 'P'],
    [['PERPENDICULAR'], 'N'], [['HORIZONTAL'], 'H'], [['VERTICAL'], 'V'],
    [['RAPID'], 'R'], [['LINEAR'], 'L'], [['CHECK'], 'C']
  ];
  for (const [terms, leaf] of preferred) if (terms.some(term => text.includes(term))) return leaf;
  return candidateLetters(command?.command?.name ?? command?.fallback ?? 'Sketch').find(value => value !== 'V') ?? 'X';
}

function sketchPreferredPath(command) {
  const id = String(command?.command?.id ?? '').toUpperCase();
  if (SKETCH_KNOWN_PATHS.has(id)) return SKETCH_KNOWN_PATHS.get(id);
  const text = sketchText(command);
  let family;
  if (/DIMENSION|RADIUS DIM|DIAMETER DIM/.test(text)) family = ['A', 'D'];
  else if (/CONSTRAINT|COINCIDENT|TANGENT|PARALLEL|PERPENDICULAR|HORIZONTAL|VERTICAL|\bFIX\b/.test(text)) family = ['C', 'K'];
  else if (/TRIM|EXTEND|FILLET|BLEND|CHAMFER|BREAK|JOIN|CORNER/.test(text)) family = ['E', 'G'];
  else if (/OFFSET|MIRROR|MOVE|ROTATE|SCALE|PATTERN|COPY/.test(text)) family = ['T', 'G'];
  else if (/DELETE|REMOVE/.test(text)) family = ['X', 'G'];
  else if (/CHECK|VALIDATE|INSPECT|ANALYSIS|SOLVE/.test(text)) family = ['I', 'S'];
  else if (/SETTING|PREFERENCE|MANAGE/.test(text)) family = ['M', 'S'];
  else family = ['C', 'G'];
  return [...family, sketchLeaf(text, command)];
}

function reserveSketchPath(preferred, command, used) {
  const requested = normalizePath(preferred);
  if (requested.length >= 3 && requested.length <= 5 && !conflicts(requested, used)) {
    used.add(pathKey(requested));
    return requested;
  }
  const generated = sketchPreferredPath(command);
  const root = generated[0] ?? 'C';
  const area = generated[1] ?? 'G';
  const first = generated[2] ?? 'X';
  const letters = [first, ...candidateLetters(command?.command?.name ?? command?.fallback ?? 'Sketch')]
    .filter(value => !(root === 'C' && area === 'G' && value === 'V'))
    .filter((value, index, array) => array.indexOf(value) === index);
  for (const leaf of letters) {
    const candidate = [root, area, leaf];
    if (!conflicts(candidate, used)) { used.add(pathKey(candidate)); return candidate; }
  }
  const variants = [...candidateLetters(command?.command?.name ?? 'Sketch'), '2', '3', 'C', 'M', 'A', 'B']
    .filter((value, index, array) => array.indexOf(value) === index);
  for (const leaf of letters) {
    for (const variant of variants) {
      const candidate = [root, area, 'V', leaf, variant];
      if (!conflicts(candidate, used)) { used.add(pathKey(candidate)); return candidate; }
    }
  }
  throw new Error(`Unable to allocate a semantic Sketch path for ${command?.command?.name ?? command?.fallback}`);
}

function buildSketchPathLabels(tokens, name) {
  return tokens.map((token, index) => {
    if (index === 0) return ROOT_LABELS[token] ?? token;
    if (index === 1) return ({ G: 'Geometry', K: 'Constraint', D: 'Dimension', S: 'Sketch' }[token] ?? OBJECT_LABELS[token] ?? token);
    if (index === 2 && token === 'V') return 'Variant';
    return index === tokens.length - 1 ? name : token;
  });
}

'''
if marker not in text:
    raise RuntimeError("compile-full-command-map.mjs: reservePath marker changed")
text = text.replace(marker, sketch_js + marker, 1)
text = text.replace(
    "const globalTargets = modules.filter(module => module.id !== 'selection_object').map(module => module.id);",
    "const globalTargets = modules.filter(module => !['selection_object', 'sketch'].includes(module.id)).map(module => module.id);\n\n// Sketch is rebuilt only from intents whose runtime scope is Sketch. Global/file/modeling rows stay\n// available through direct shortcuts or their own modules instead of polluting the Sketch tree.\nconst sketchModule = modulesById.get('sketch');\nif (sketchModule) {\n  for (const set of sketchModule.command_sets ?? [])\n    set.commands = (set.commands ?? []).filter(command => isSupportCommand(command));\n  sketchModule.command_sets = (sketchModule.command_sets ?? []).filter(set => (set.commands ?? []).length > 0);\n}",
    1,
)
text = text.replace(
    "    command.__preferred = command.path?.length ? command.path : knownPaths.get(id) ?? legacy;\n    command.__priority = isSupportCommand(command) ? -1 : (knownPaths.has(id) ? 0 : (String(command.fallback ?? '').startsWith('catalog:') ? 2 : 1));",
    "    const sketchIntent = isSketchIntentCommand(module.id, command);\n    command.__preferred = sketchIntent\n      ? sketchPreferredPath(command)\n      : (command.path?.length ? command.path : knownPaths.get(id) ?? legacy);\n    command.__priority = isSupportCommand(command) ? -1 : ((sketchIntent && SKETCH_KNOWN_PATHS.has(id)) || knownPaths.has(id) ? 0 : (String(command.fallback ?? '').startsWith('catalog:') ? 2 : 1));",
    1,
)
text = text.replace(
    "    command.path = reservePath(command.__preferred, command.command?.name ?? command.fallback, used, command.frequency);\n    command.path_labels = buildPathLabels(command.path, command.command?.name ?? 'Command');",
    "    const sketchIntent = isSketchIntentCommand(module.id, command);\n    command.path = sketchIntent\n      ? reserveSketchPath(command.__preferred, command, used)\n      : reservePath(command.__preferred, command.command?.name ?? command.fallback, used, command.frequency);\n    command.path_labels = sketchIntent\n      ? buildSketchPathLabels(command.path, command.command?.name ?? 'Sketch command')\n      : buildPathLabels(command.path, command.command?.name ?? 'Command');\n    if (sketchIntent) command.path_source = SKETCH_KNOWN_PATHS.has(String(command.command?.id ?? '').toUpperCase())\n      ? 'sketch_curated' : 'sketch_generated';",
    1,
)
text = text.replace(
    "    const candidates = [];\n    for (const alias of command.aliases ?? []) candidates.push(normalizePath(alias));\n    if (command.input_key) candidates.push(normalizePath([command.input_key]));\n    if (command.submenu_key && command.input_key) candidates.push(normalizePath([command.submenu_key, command.input_key]));",
    "    const candidates = [];\n    const sketchIntent = isSketchIntentCommand(module.id, command);\n    if (!sketchIntent) {\n      for (const alias of command.aliases ?? []) candidates.push(normalizePath(alias));\n      if (command.input_key) candidates.push(normalizePath([command.input_key]));\n      if (command.submenu_key && command.input_key) candidates.push(normalizePath([command.submenu_key, command.input_key]));\n    }",
    1,
)
write(path, text)


# 4. Validators and sequence audit use a command-aware target length.
path = "scripts/validate-main-command-map.mjs"
text = read(path)
text = text.replace("  targetLengthForFrequency\n", "  targetLengthForCommand\n", 1)
text = text.replace(
    "        const targetLength = targetLengthForFrequency(command.frequency);",
    "        const targetLength = targetLengthForCommand(module.id, command);",
    1,
)
write(path, text)

path = "scripts/audit-command-sequences.mjs"
text = read(path)
text = text.replace(
    "  targetLengthForFrequency\n",
    "  targetLengthForCommand\n",
    1,
)
text = text.replace(
    "        catalog_refs: command.catalog_refs ?? []",
    "        catalog_refs: command.catalog_refs ?? [],\n        target_length: targetLengthForCommand(module.id, command)",
    1,
)
text = text.replace(
    "    if (length > targetLengthForFrequency(frequency)) bucket.over_target += 1;",
    "    if (length > row.target_length) bucket.over_target += 1;",
    1,
)
text = text.replace(
    "  for (const [frequency, value] of overTarget) fail(`${frequency} has ${value.over_target} paths over target length ${targetLengthForFrequency(frequency)}`);",
    "  for (const [frequency, value] of overTarget) fail(`${frequency} has ${value.over_target} paths over its command-specific target length`);",
    1,
)
write(path, text)


# 5. Bootstrap profile: remove unrelated Sketch rows and seed the verified core BUTTON IDs.
profile_path = "config/nx2512-pro-hybrid.json"
profile = json.loads(read(profile_path))
sketch_module = next((item for item in profile.get("modules", []) if item.get("id") == "sketch"), None)
if sketch_module is None:
    raise RuntimeError("Bootstrap profile has no sketch module")

# Keep runtime selection support only; the compiler repopulates the module from Sketch-scoped intents.
for command_set in sketch_module.get("command_sets", []):
    command_set["commands"] = [
        command for command in command_set.get("commands", [])
        if command.get("support_kind") == "selection_filter" or command.get("action") == "set_selection_filter"
    ]
sketch_module["command_sets"] = [item for item in sketch_module.get("command_sets", []) if item.get("commands")]

core = [
    ("UG_SKETCH_LINE", "Line", "Линия", ["C", "G", "L"], "K5", False),
    ("UG_SKETCH_RECTANGLE", "Rectangle", "Прямоугольник", ["C", "G", "R"], "K5", False),
    ("UG_SKETCH_CIRCLE", "Circle", "Окружность", ["C", "G", "C"], "K5", False),
    ("UG_SKETCH_ARC", "Arc", "Дуга", ["C", "G", "A"], "K5", False),
    ("UG_SKETCH_TRIM", "Trim", "Обрезать", ["E", "G", "T"], "K5", True),
    ("UG_SKETCH_EXTEND", "Extend", "Удлинить", ["E", "G", "E"], "K4", True),
    ("UG_SKETCH_OFFSET_CURVE", "Offset Curve", "Смещение кривой", ["T", "G", "O"], "K4", True),
    ("UG_SKETCH_LINE_BY_TWO_POINTS", "Line by Two Points", "Линия по двум точкам", ["C", "G", "V", "L", "2"], "K3", False),
    ("UG_SKETCH_LINE_FROM_MIDPOINT", "Line from Midpoint", "Линия от середины", ["C", "G", "V", "L", "M"], "K3", False),
    ("UG_SKETCH_RECTANGLE_BY_TWO_POINTS", "Rectangle by Two Points", "Прямоугольник по двум точкам", ["C", "G", "V", "R", "2"], "K3", False),
    ("UG_SKETCH_RECTANGLE_FROM_CENTER", "Rectangle from Center", "Прямоугольник из центра", ["C", "G", "V", "R", "C"], "K3", False),
    ("UG_SKETCH_RECTANGLE_BY_THREE_POINTS", "Rectangle by Three Points", "Прямоугольник по трём точкам", ["C", "G", "V", "R", "3"], "K3", False),
    ("UG_SKETCH_CIRCLE_FROM_CENTER", "Circle from Center", "Окружность из центра", ["C", "G", "V", "C", "C"], "K3", False),
    ("UG_SKETCH_CIRCLE_BY_THREE_POINTS", "Circle by Three Points", "Окружность по трём точкам", ["C", "G", "V", "C", "3"], "K3", False),
    ("UG_SKETCH_ARC_BY_THREE_POINTS", "Arc by Three Points", "Дуга по трём точкам", ["C", "G", "V", "A", "3"], "K3", False),
    ("UG_SKETCH_ARC_FROM_CENTER", "Arc from Center", "Дуга из центра", ["C", "G", "V", "A", "C"], "K3", False),
    ("UG_SKETCH_RAPID_DIMENSION", "Rapid Dimension", "Быстрый размер", ["A", "D", "R"], "K5", False),
    ("UG_SKETCH_LINEAR_DIMENSION", "Linear Dimension", "Линейный размер", ["A", "D", "L"], "K4", False),
    ("UG_SKETCH_COINCIDENT_CONSTRAINT", "Coincident Constraint", "Совпадение", ["C", "K", "C"], "K3", True),
    ("UG_SKETCH_TANGENT_CONSTRAINT", "Tangent Constraint", "Касательность", ["C", "K", "T"], "K3", True),
    ("UG_SKETCH_PARALLEL_CONSTRAINT", "Parallel Constraint", "Параллельность", ["C", "K", "P"], "K3", True),
    ("UG_SKETCH_PERPENDICULAR_CONSTRAINT", "Perpendicular Constraint", "Перпендикулярность", ["C", "K", "N"], "K3", True),
    ("UG_SKETCH_HORIZONTAL_CONSTRAINT", "Horizontal Constraint", "Горизонтальность", ["C", "K", "H"], "K3", True),
    ("UG_SKETCH_VERTICAL_CONSTRAINT", "Vertical Constraint", "Вертикальность", ["C", "K", "V"], "K3", True),
    ("UG_SKETCH_CHECKER", "Sketch Checker", "Проверка эскиза", ["I", "S", "C"], "K4", False),
    ("UG_MODELING_BLEND_FEATURE", "Sketch Fillet", "Скругление эскиза", ["E", "G", "F"], "K3", True),
    ("UG_MODELING_CHAMFER_FEATURE", "Sketch Chamfer", "Фаска эскиза", ["E", "G", "H"], "K3", True),
]

core_set = {
    "id": "sketch_core_intents",
    "label": "Sketch Core Intents",
    "commands": [],
}
for order, (command_id, name, russian, mnemonic, frequency, requires_selection) in enumerate(core, start=1):
    labels = []
    for index, token in enumerate(mnemonic):
        if index == 0:
            labels.append({"C": "Create", "E": "Edit", "T": "Transform", "A": "Annotate", "I": "Inspect"}.get(token, token))
        elif index == 1:
            labels.append({"G": "Geometry", "D": "Dimension", "K": "Constraint", "S": "Sketch"}.get(token, token))
        elif index == 2 and token == "V":
            labels.append("Variant")
        elif index == len(mnemonic) - 1:
            labels.append(name)
        else:
            labels.append(token)
    core_set["commands"].append({
        "slot": "",
        "submenu_key": "",
        "submenu_label": "Sketch Core",
        "input_key": "",
        "path": mnemonic,
        "path_labels": labels,
        "aliases": [],
        "search_aliases": [name, russian, command_id],
        "icon_hint": "sketch",
        "display_order": 100 + order,
        "command": {"id": command_id, "name": name, "aliases": [russian]},
        "action": "execute_command",
        "target_module_id": "",
        "support_kind": "",
        "selection_type": "curve" if requires_selection else "",
        "enabled": True,
        "requires_selection": requires_selection,
        "destructive": False,
        "confirm_before_execute": False,
        "fallback": "curated:sketch-core",
        "notes": "Verified Sketch core BUTTON ID; semantic action-object-operation path",
        "catalog_refs": [],
        "frequency": frequency,
        "resolution_status": "existing",
        "resolution_candidates": [],
        "catalog_backed_support": False,
        "path_locked": False,
        "path_source": "sketch_curated",
    })
sketch_module.setdefault("command_sets", []).insert(0, core_set)
write(profile_path, json.dumps(profile, ensure_ascii=False, indent=2) + "\n")


# 6. Regression coverage for the runtime policy.
path = "NX2512_HotkeyStudio.Tests/Program.cs"
text = read(path)
text = text.replace(
    "        Console.WriteLine(\"[OK] Canonical profile editor, command menus and single NX ribbon regressions.\");",
    "        VerifySketchIntentGrammar();\n\n        Console.WriteLine(\"[OK] Canonical profile editor, command menus, Sketch intent grammar and single NX ribbon regressions.\");",
    1,
)
methods = r'''
    private static void VerifySketchIntentGrammar()
    {
        var commands = new List<ModuleCommand>
        {
            SketchCommand("UG_SKETCH_LINE", "Line", new[] { "C", "L" }, "K5", 1),
            SketchCommand("UG_SKETCH_LINE_BY_TWO_POINTS", "Line by Two Points", new[] { "C", "L", "2" }, "K3", 2),
            SketchCommand("UG_SKETCH_RECTANGLE", "Rectangle", new[] { "C", "R" }, "K5", 3),
            SketchCommand("UG_SKETCH_TRIM", "Trim", new[] { "E", "T" }, "K5", 4),
            SketchCommand("UG_MODELING_CHAMFER_FEATURE", "Sketch Chamfer", new[] { "C", "G", "C", "H" }, "K3", 5),
            SketchCommand("UG_SKETCH_STUDIO_SPLINE", "Studio Spline", new[] { "M", "Z", "S" }, "K4", 6),
            SketchCommand("UG_SKETCH_USER_CUSTOM", "User Custom", new[] { "U", "S", "X" }, "K3", 7, true)
        };
        commands[0].Aliases = new List<List<string>> { new List<string> { "Q", "W" } };
        var module = new ModuleConfig
        {
            ID = "sketch",
            Label = "Sketch",
            Enabled = true,
            CommandSets = new List<ModuleCommandSet>
            {
                new ModuleCommandSet { ID = "sketch", Label = "Sketch", Commands = commands }
            }
        };

        MnemonicPathGenerator.Apply(new[] { module });
        Assert(PathOf(commands, "UG_SKETCH_LINE").SequenceEqual(new[] { "C", "G", "L" }),
            "Line must use Create -> Geometry -> Line.");
        Assert(PathOf(commands, "UG_SKETCH_LINE_BY_TWO_POINTS").SequenceEqual(new[] { "C", "G", "V", "L", "2" }),
            "Line variants must live under the explicit variant branch.");
        Assert(PathOf(commands, "UG_SKETCH_RECTANGLE").SequenceEqual(new[] { "C", "G", "R" }),
            "Rectangle must use Create -> Geometry -> Rectangle.");
        Assert(PathOf(commands, "UG_SKETCH_TRIM").SequenceEqual(new[] { "E", "G", "T" }),
            "Trim must use Edit -> Geometry -> Trim.");
        Assert(PathOf(commands, "UG_MODELING_CHAMFER_FEATURE").SequenceEqual(new[] { "E", "G", "H" }),
            "The shared NX chamfer BUTTON ID must keep Sketch semantics in Sketch context.");
        Assert(PathOf(commands, "UG_SKETCH_STUDIO_SPLINE").Take(2).SequenceEqual(new[] { "C", "G" }),
            "Unknown Sketch geometry must remain in the Create -> Geometry family.");
        Assert(PathOf(commands, "UG_SKETCH_USER_CUSTOM").SequenceEqual(new[] { "U", "S", "X" }),
            "User-locked paths must remain untouched.");
        Assert(commands.First(item => item.Command.ID == "UG_SKETCH_LINE").Aliases.Count == 0,
            "Legacy positional aliases must be removed from generated Sketch intents.");

        List<string> paths = commands.Select(item => string.Concat(item.Path)).OrderBy(value => value.Length).ToList();
        for (int left = 0; left < paths.Count; left++)
            for (int right = left + 1; right < paths.Count; right++)
                Assert(!paths[right].StartsWith(paths[left], StringComparison.OrdinalIgnoreCase),
                    "Sketch paths must remain prefix-free: " + paths[left] + " / " + paths[right]);
    }

    private static ModuleCommand SketchCommand(
        string id,
        string name,
        IEnumerable<string> path,
        string frequency,
        int order,
        bool locked = false) => new ModuleCommand
    {
        Enabled = true,
        Path = path.ToList(),
        PathLocked = locked,
        PathSource = locked ? "user" : "generated",
        Frequency = frequency,
        DisplayOrder = order,
        Command = new CommandRef { ID = id, Name = name }
    };

    private static IReadOnlyList<string> PathOf(IEnumerable<ModuleCommand> commands, string id) =>
        commands.First(item => string.Equals(item.Command.ID, id, StringComparison.OrdinalIgnoreCase)).Path;

'''
marker = "    private static ModuleCommand Command(string id, string name, IEnumerable<string> path) => new ModuleCommand"
if marker not in text:
    raise RuntimeError("HotkeyStudio test insertion marker changed")
write(path, text.replace(marker, methods + marker, 1))


# 7. CI must execute the HotkeyStudio regressions, not only compile the application.
path = ".github/workflows/ci.yml"
text = read(path)
marker = "      - name: Build Hotkey Studio\n"
step = "      - name: Run Hotkey Studio regressions\n        shell: pwsh\n        run: |\n          dotnet run --project .\\NX2512_HotkeyStudio.Tests\\NX2512_HotkeyStudio.Tests.csproj -c Release -p:Platform=x64 --nologo *> hotkey-tests.log\n          $code = $LASTEXITCODE\n          Get-Content hotkey-tests.log -Tail 180\n          exit $code\n\n"
if marker not in text:
    raise RuntimeError("CI Hotkey Studio marker changed")
text = text.replace(marker, step + marker, 1)
text = text.replace(
    "            state-machine-tests.log\n            hotkey-build.log",
    "            state-machine-tests.log\n            hotkey-tests.log\n            hotkey-build.log",
    1,
)
write(path, text)


# 8. Documentation: describe the finite Sketch vocabulary and its deliberate exception.
sketch_doc = r'''# Язык намерений Sketch

Sketch использует отдельную, предсказуемую ветвь мнемонического языка NXKeys. Цель — чтобы пользователь сначала выбирал **действие**, затем **область**, а затем **операцию**, не запоминая случайные позиции клавиш.

```text
CapsLock -> действие -> объект/область -> операция -> вариант
```

## Основные семейства

| Семейство | Назначение | Примеры |
|---|---|---|
| `C -> G -> …` | создание геометрии | `L` линия, `R` прямоугольник, `C` окружность, `A` дуга |
| `E -> G -> …` | редактирование геометрии | `T` обрезать, `E` удлинить, `F` скруглить, `H` фаска |
| `T -> G -> …` | преобразования | `O` смещение, зеркало, перенос, поворот, массив |
| `C -> K -> …` | геометрические ограничения | совпадение, касательность, параллельность, перпендикулярность, горизонтальность, вертикальность |
| `A -> D -> …` | размеры | быстрый и линейный размер; остальные размеры распределяются в этой же ветви |
| `I -> S -> …` | проверка эскиза | Sketch Checker и другие проверки |
| `X -> G -> …` | удаление геометрии | операции удаления и разрыва, когда они присутствуют в каталоге NX |
| `M -> S -> …` | управление Sketch | настройки и служебные команды Sketch |

## Базовые команды

| Путь | Намерение |
|---|---|
| `C -> G -> L` | линия |
| `C -> G -> R` | прямоугольник |
| `C -> G -> C` | окружность |
| `C -> G -> A` | дуга |
| `E -> G -> T` | обрезать |
| `E -> G -> E` | удлинить |
| `T -> G -> O` | смещение кривой |
| `A -> D -> R` | быстрый размер |
| `I -> S -> C` | проверка эскиза |

## Варианты построения

Варианты не продолжают путь базовой команды, потому что терминальная команда не может одновременно быть префиксом другой команды. Для них выделена ветвь `C -> G -> V`:

- `C -> G -> V -> L -> 2` — линия по двум точкам;
- `C -> G -> V -> L -> M` — линия от середины;
- `C -> G -> V -> R -> C` — прямоугольник из центра;
- `C -> G -> V -> C -> 3` — окружность по трём точкам;
- `C -> G -> V -> A -> C` — дуга из центра.

Sketch намеренно допускает пути длиной до пяти токенов независимо от K-частоты. Это сохраняет семантику и prefix-free инвариант; случайное сокращение до `C -> L`, `K -> C` или `D -> Q` запрещено.

## Границы контекста

В Sketch-компилятор добавляет только намерения с `runtime_module: sketch` и универсальные фильтры выбора. Глобальные файловые команды, навигатор сборки, материалы, сшивка поверхностей и переходы между приложениями не дублируются в дерево Sketch. Они остаются доступны через прямые сочетания или свои модули.

## Алиасы и расширение каталога

Старые позиционные алиасы `W/E/D/C/X/Z/A/Q` для Sketch автоматически удаляются. Пользовательский путь с `path_locked: true` или `path_source: user` сохраняется без изменений.

Новая команда `UG_SKETCH_*`, найденная в каталоге целевой установки NX, получает семейство по назначению и остаётся внутри него при разрешении коллизий. Неоднозначный или неразрешённый `BUTTON ID` остаётся видимым, но отключённым: NXKeys не подставляет выдуманные идентификаторы.
'''
write("docs/SKETCH_INTENT_LANGUAGE.md", sketch_doc)

path = "README.md"
text = read(path)
needle = "- [Мнемонический язык](docs/MNEMONIC_COMMAND_LANGUAGE.md)\n"
if needle not in text:
    raise RuntimeError("README documentation marker changed")
write(path, text.replace(needle, needle + "- [Язык намерений Sketch](docs/SKETCH_INTENT_LANGUAGE.md)\n", 1))

path = "docs/README.md"
text = read(path)
# Add near the mnemonic language entry when present, otherwise append a compact section.
if "MNEMONIC_COMMAND_LANGUAGE.md" in text:
    text = text.replace(
        "[MNEMONIC_COMMAND_LANGUAGE.md](MNEMONIC_COMMAND_LANGUAGE.md)",
        "[MNEMONIC_COMMAND_LANGUAGE.md](MNEMONIC_COMMAND_LANGUAGE.md) и [SKETCH_INTENT_LANGUAGE.md](SKETCH_INTENT_LANGUAGE.md)",
        1,
    )
else:
    text += "\n- [Язык намерений Sketch](SKETCH_INTENT_LANGUAGE.md)\n"
write(path, text)

path = "scripts/generate-mnemonic-command-language-v7.mjs"
text = read(path)
needle = "  '- Short aliases are retained only when they cannot shadow a command or submenu.',"
if needle in text:
    text = text.replace(
        needle,
        needle + "\n  '- Sketch uses a dedicated action -> object -> operation grammar; its variant paths may contain up to five tokens regardless of K frequency.',",
        1,
    )
write(path, text)


# 9. Remove the obsolete one-shot migration that reintroduced compact, positional Sketch paths.
for obsolete in ["scripts/apply-nxkeys-v7.py", ".github/workflows/apply-nxkeys-v7.yml"]:
    target = ROOT / obsolete
    if target.exists():
        target.unlink()

print("[sketch-intent-fix] Source, compiler, bootstrap, tests and documentation updated.")
