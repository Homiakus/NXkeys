from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8-sig")


def write(relative: str, content: str) -> None:
    path = ROOT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.replace("\r\n", "\n"), encoding="utf-8", newline="\n")


def replace(relative: str, old: str, new: str, count: int = 1) -> None:
    content = read(relative)
    actual = content.count(old)
    if actual < count:
        raise RuntimeError(f"{relative}: expected at least {count} occurrence(s), found {actual}: {old[:100]!r}")
    write(relative, content.replace(old, new, count))


def regex_replace(relative: str, pattern: str, replacement: str, count: int = 1, flags: int = 0) -> None:
    content = read(relative)
    updated, actual = re.subn(pattern, replacement, content, count=count, flags=flags)
    if actual != count:
        raise RuntimeError(f"{relative}: expected {count} regex replacement(s), found {actual}: {pattern}")
    write(relative, updated)


# ---------------------------------------------------------------------------
# JavaScript sequence policy and compiler precedence.
# ---------------------------------------------------------------------------
replace("scripts/sequence-policy.mjs", "export const SEQUENCE_POLICY_VERSION = 6;", "export const SEQUENCE_POLICY_VERSION = 7;")
replace(
    "scripts/sequence-policy.mjs",
    "  { id: 'UG_SEL_DATUM_PRIORITY', name: 'Datum Selection Priority', path: ['S', 'D'], alias: ['A'], selectionType: 'datum', iconHint: 'selection' },\n  { id: 'UG_SEL_TYPE_RESET', name: 'Reset Selection Filter', path: ['S', 'R'], alias: ['Q'], selectionType: 'reset', iconHint: 'sel_deselect' }",
    "  { id: 'UG_SEL_DATUM_PRIORITY', name: 'Datum Selection Priority', path: ['S', 'D'], alias: ['A'], selectionType: 'datum', iconHint: 'selection' },\n  { id: 'UG_SEL_SELECT_ALL', name: 'Select All', path: ['S', 'A'], alias: [], selectionType: 'all', iconHint: 'selection' },\n  { id: 'UG_SEL_DESELECT_ALL', name: 'Deselect All', path: ['S', 'N'], alias: [], selectionType: 'none', iconHint: 'sel_deselect' },\n  { id: 'UG_SEL_TYPE_RESET', name: 'Reset Selection Filter', path: ['S', 'R'], alias: ['Q'], selectionType: 'reset', iconHint: 'sel_deselect' }",
)

replace(
    "scripts/compile-full-command-map.mjs",
    "const probePathValue = arg('--probe', 'docs/audit/runtime-command-probe-2026-07-28.json');\nconst probePath = probePathValue ? absolute(probePathValue) : '';",
    "const probePathValue = arg('--probe', 'docs/audit/runtime-command-probe-2026-07-28.json');\nconst probePath = probePathValue ? absolute(probePathValue) : '';\nconst overridesPathValue = arg('--overrides', 'config/nxkeys.user-overrides.json');\nconst overridesPath = overridesPathValue ? absolute(overridesPathValue) : '';",
)
replace(
    "scripts/compile-full-command-map.mjs",
    "  C: 'Create', E: 'Edit', T: 'Transform', X: 'Remove', P: 'Process', I: 'Inspect',\n  V: 'View', S: 'Select', A: 'Annotate', M: 'Manage', F: 'File', G: 'Go', U: 'Utilities', H: 'Help'",
    "  C: 'Create', E: 'Edit', T: 'Transform', X: 'Remove', P: 'Process', I: 'Inspect',\n  V: 'View', S: 'Select', A: 'Annotate', M: 'Manage', L: 'Layers', D: 'Dimension', K: 'Constraint',\n  F: 'File', G: 'Go', U: 'Utilities', H: 'Help'",
)
replace(
    "scripts/compile-full-command-map.mjs",
    "  const rootAlternatives = [root, 'C', 'E', 'T', 'X', 'P', 'I', 'V', 'A', 'M', 'F', 'U', 'H']",
    "  const rootAlternatives = [root, 'C', 'E', 'T', 'X', 'P', 'I', 'V', 'A', 'M', 'L', 'D', 'K', 'F', 'U', 'H']",
)
replace(
    "scripts/compile-full-command-map.mjs",
    "const profile = clone(readJson(profilePath));\nconst catalog = loadNxCatalog(catalogDir, probePath);",
    "const profile = clone(readJson(profilePath));\nconst userOverrides = overridesPath && fs.existsSync(overridesPath) ? readJson(overridesPath) : {};\nconst catalog = loadNxCatalog(catalogDir, probePath);",
)
replace(
    "scripts/compile-full-command-map.mjs",
    "// Allocate canonical paths for every command, preserving exact known BUTTON ID paths first.\nfor (const module of modules) {",
    "// Apply stable user overrides before path allocation. Overrides are keyed by exact BUTTON ID and\n// survive profile regeneration. A module_id field can narrow an override to one NX module.\nconst overrideCommands = new Map(Object.entries(userOverrides.commands ?? {})\n  .map(([id, value]) => [String(id).trim().toUpperCase(), value ?? {}]));\nfor (const module of modules) {\n  for (const { command } of allModuleCommands(module)) {\n    const id = String(command.command?.id ?? '').trim().toUpperCase();\n    const override = overrideCommands.get(id);\n    if (!override || (override.module_id && override.module_id !== module.id)) continue;\n    const overridePath = normalizePath(override.path);\n    if (overridePath.length >= 2) command.path = overridePath;\n    if (Array.isArray(override.aliases)) command.aliases = override.aliases.map(normalizePath).filter(path => path.length > 0);\n    command.path_locked = override.locked !== false;\n    command.path_source = 'user';\n  }\n}\n\n// Allocate canonical paths. Locked user paths win, then curated BUTTON ID mappings, then profile/generated paths.\nfor (const module of modules) {",
)
replace(
    "scripts/compile-full-command-map.mjs",
    "    const id = String(command.command.id ?? '').toUpperCase();\n    const legacy = command.submenu_key ? [command.submenu_key, command.input_key] : [command.input_key];\n    command.__preferred = command.path?.length ? command.path : knownPaths.get(id) ?? legacy;\n    command.__priority = isSupportCommand(command) ? -1 : (knownPaths.has(id) ? 0 : (String(command.fallback ?? '').startsWith('catalog:') ? 2 : 1));",
    "    const id = String(command.command.id ?? '').toUpperCase();\n    const legacy = command.submenu_key ? [command.submenu_key, command.input_key] : [command.input_key];\n    const existingPath = normalizePath(command.path);\n    const curatedPath = knownPaths.get(id);\n    const locked = command.path_locked === true || String(command.path_source ?? '').toLowerCase() === 'user';\n    command.__preferred = locked && existingPath.length ? existingPath\n      : curatedPath?.length ? curatedPath\n        : existingPath.length ? existingPath : legacy;\n    command.__pathSource = locked ? 'user' : curatedPath?.length ? 'curated' : existingPath.length ? 'profile' : 'generated';\n    command.__priority = isSupportCommand(command) ? -2 : locked ? -1 : curatedPath?.length ? 0\n      : (String(command.fallback ?? '').startsWith('catalog:') ? 2 : 1);",
)
replace(
    "scripts/compile-full-command-map.mjs",
    "    command.path = reservePath(command.__preferred, command.command?.name ?? command.fallback, used, command.frequency);\n    command.path_labels = buildPathLabels(command.path, command.command?.name ?? 'Command');\n    delete command.__preferred;\n    delete command.__priority;",
    "    command.path = reservePath(command.__preferred, command.command?.name ?? command.fallback, used, command.frequency);\n    command.path_labels = buildPathLabels(command.path, command.command?.name ?? 'Command');\n    command.path_source = command.__pathSource;\n    command.path_locked = command.__pathSource === 'user';\n    delete command.__preferred;\n    delete command.__priority;\n    delete command.__pathSource;",
)

replace(
    "scripts/compile-main-command-map.mjs",
    "const probe = valueOf('--probe', 'docs/audit/runtime-command-probe-2026-07-28.json');\nconst output = absolute(valueOf('--out', 'config/nx2512-pro-main.generated.json'));",
    "const probe = valueOf('--probe', 'docs/audit/runtime-command-probe-2026-07-28.json');\nconst overrides = valueOf('--overrides', 'config/nxkeys.user-overrides.json');\nconst output = absolute(valueOf('--out', 'config/nx2512-pro-main.generated.json'));",
)
replace(
    "scripts/compile-main-command-map.mjs",
    "  if (probe) args.push('--probe', absolute(probe));\n  if (has('--no-global-duplication')) args.push('--no-global-duplication');",
    "  if (probe) args.push('--probe', absolute(probe));\n  if (overrides && fs.existsSync(absolute(overrides))) args.push('--overrides', absolute(overrides));\n  if (has('--no-global-duplication')) args.push('--no-global-duplication');",
)

# ---------------------------------------------------------------------------
# C# config model: path ownership, preferred module cycle and workflow gestures.
# ---------------------------------------------------------------------------
replace(
    "NX2512_HotkeyStudio/Models/ModuleConfigTypesV5.cs",
    "        [JsonPropertyName(\"aliases\")] public List<List<string>> Aliases { get; set; } = new List<List<string>>();\n        [JsonPropertyName(\"search_aliases\")] public List<string> SearchAliases { get; set; } = new List<string>();",
    "        [JsonPropertyName(\"aliases\")] public List<List<string>> Aliases { get; set; } = new List<List<string>>();\n        [JsonPropertyName(\"path_locked\")] public bool PathLocked { get; set; }\n        [JsonPropertyName(\"path_source\")] public string PathSource { get; set; } = string.Empty;\n        [JsonPropertyName(\"search_aliases\")] public List<string> SearchAliases { get; set; } = new List<string>();",
)

replace(
    "NX2512_HotkeyStudio/Models/LeaderConfigV5.cs",
    "namespace NX2512_HotkeyStudio.Models\n{\n    public sealed class LeaderKeyConfig",
    "namespace NX2512_HotkeyStudio.Models\n{\n    public sealed class WorkflowGestureConfig\n    {\n        [JsonPropertyName(\"finish\")] public string Finish { get; set; } = \"Ctrl+MiddleMouse\";\n        [JsonPropertyName(\"apply\")] public string Apply { get; set; } = \"MiddleMouse\";\n        [JsonPropertyName(\"cancel\")] public string Cancel { get; set; } = \"Escape\";\n        [JsonPropertyName(\"back\")] public string Back { get; set; } = \"Backspace\";\n\n        public void ApplyDefaults()\n        {\n            if (string.IsNullOrWhiteSpace(Finish)) Finish = \"Ctrl+MiddleMouse\";\n            if (string.IsNullOrWhiteSpace(Apply)) Apply = \"MiddleMouse\";\n            if (string.IsNullOrWhiteSpace(Cancel)) Cancel = \"Escape\";\n            if (string.IsNullOrWhiteSpace(Back)) Back = \"Backspace\";\n        }\n    }\n\n    public sealed class LeaderKeyConfig",
)
replace(
    "NX2512_HotkeyStudio/Models/LeaderConfigV5.cs",
    "        [JsonPropertyName(\"hook_only_when_nx_active\")] public bool HookOnlyWhenNXActive { get; set; } = true;\n        [JsonPropertyName(\"slot_key_map\")]",
    "        [JsonPropertyName(\"hook_only_when_nx_active\")] public bool HookOnlyWhenNXActive { get; set; } = true;\n        [JsonPropertyName(\"module_cycle_order\")] public List<string> ModuleCycleOrder { get; set; } = new List<string>\n        {\n            \"modeling\", \"assembly\", \"drafting\", \"manufacturing\"\n        };\n        [JsonPropertyName(\"workflow_gestures\")] public WorkflowGestureConfig WorkflowGestures { get; set; } = new WorkflowGestureConfig();\n        [JsonPropertyName(\"slot_key_map\")]",
)
replace(
    "NX2512_HotkeyStudio/Models/LeaderConfigV5.cs",
    "            if (HudOpacity <= 0 || HudOpacity > 1.0) HudOpacity = 0.95;\n            if (SlotKeyMap != null)",
    "            if (HudOpacity <= 0 || HudOpacity > 1.0) HudOpacity = 0.95;\n            ModuleCycleOrder ??= new List<string>();\n            ModuleCycleOrder = ModuleCycleOrder.Where(value => !string.IsNullOrWhiteSpace(value))\n                .Select(ContextGuardEvaluator.NormalizeModule).Distinct(StringComparer.OrdinalIgnoreCase).ToList();\n            if (ModuleCycleOrder.Count == 0) ModuleCycleOrder.AddRange(new[] { \"modeling\", \"assembly\", \"drafting\", \"manufacturing\" });\n            WorkflowGestures ??= new WorkflowGestureConfig();\n            WorkflowGestures.ApplyDefaults();\n            if (SlotKeyMap != null)",
)

# ---------------------------------------------------------------------------
# C# mnemonic generator: semantic roots and stable precedence.
# ---------------------------------------------------------------------------
replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    "                [\"A\"] = \"Annotate\", [\"M\"] = \"Manage\", [\"F\"] = \"File\", [\"G\"] = \"Go\",\n                [\"U\"] = \"Utilities\", [\"H\"] = \"Help\"",
    "                [\"A\"] = \"Annotate\", [\"M\"] = \"Manage\", [\"L\"] = \"Layers\",\n                [\"D\"] = \"Dimension\", [\"K\"] = \"Constraint\", [\"F\"] = \"File\", [\"G\"] = \"Go\",\n                [\"U\"] = \"Utilities\", [\"H\"] = \"Help\"",
)
replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    "                    MnemonicDefinition definition = ResolveDefinition(command);\n                    IReadOnlyList<string> requested = NormalizePath(command.Path);\n                    if (requested.Count == 0) requested = NormalizePath(definition?.Path);\n                    if (requested.Count == 0) requested = GenerateCandidate(module, command);\n                    List<string> canonical = ReserveUnique(requested, command, usedCanonical);\n                    command.Path = canonical;",
    "                    MnemonicDefinition definition = ResolveDefinition(command);\n                    IReadOnlyList<string> profilePath = NormalizePath(command.Path);\n                    bool locked = command.PathLocked || string.Equals(command.PathSource, \"user\", StringComparison.OrdinalIgnoreCase);\n                    IReadOnlyList<string> requested = locked ? profilePath : NormalizePath(definition?.Path);\n                    if (requested.Count == 0) requested = profilePath;\n                    if (requested.Count == 0) requested = GenerateCandidate(module, command);\n                    string requestedKey = string.Concat(requested);\n                    if (locked && (usedCanonical.ContainsKey(requestedKey) || CreatesPrefixConflict(requestedKey, usedCanonical.Keys)))\n                        throw new InvalidOperationException(\"Locked user path conflicts in module \" + module.ID + \": \" + requestedKey);\n                    List<string> canonical = ReserveUnique(requested, command, usedCanonical);\n                    command.Path = canonical;\n                    command.PathSource = locked ? \"user\" : definition != null ? \"curated\" : profilePath.Count > 0 ? \"profile\" : \"generated\";\n                    command.PathLocked = locked;",
)
replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    "            string[] rootAlternatives = new[] { root, \"C\", \"E\", \"T\", \"X\", \"P\", \"I\", \"V\", \"A\", \"M\", \"F\", \"U\", \"H\" }",
    "            string[] rootAlternatives = new[] { root, \"C\", \"E\", \"T\", \"X\", \"P\", \"I\", \"V\", \"A\", \"M\", \"L\", \"D\", \"K\", \"F\", \"U\", \"H\" }",
)
replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    "            if (ContainsAny(text, \"NAVIGATOR\", \"LAYER\", \"MATERIAL\", \"LIBRARY\", \"PREFERENCE\", \"SETTING\", \"MANAGER\", \"PALETTE\", \"EXPRESSIONS\")) return \"M\";",
    "            if (ContainsAny(text, \"LAYER\")) return \"L\";\n            if (ContainsAny(text, \"SKETCH\") && ContainsAny(text, \"DIMENSION\")) return \"D\";\n            if (ContainsAny(text, \"SKETCH\") && ContainsAny(text, \"CONSTRAINT\")) return \"K\";\n            if (ContainsAny(text, \"NAVIGATOR\", \"MATERIAL\", \"LIBRARY\", \"PREFERENCE\", \"SETTING\", \"MANAGER\", \"PALETTE\", \"EXPRESSIONS\")) return \"M\";",
)

regex_replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    r'''            Add\("UG_CREATE_SKETCH", "C S K",.*?            Add\("UG_SKETCH_CHECKER", "I S C"\);''',
    '''            Add("UG_CREATE_SKETCH", "C S", null, "создать эскиз");
            Add("UG_MODELING_EXTRUDED_FEATURE", "C E", null, "extrude", "вытянуть", "выдавливание");
            Add("UG_MODELING_HOLE_FEATURE", "C H", null, "hole", "отверстие");
            Add("UG_MODELING_REVOLVED_FEATURE", "C R", null, "revolve", "вращение");
            Add("UG_MODELING_BLEND_FEATURE", "E B", null, "blend", "скругление");
            Add("UG_MODELING_CHAMFER_FEATURE", "E C", null, "chamfer", "фаска");
            Add("UG_MODELING_PATTERNFEATURE_FEATURE", "T P", null, "pattern", "массив");
            Add("UG_MODELING_MIRRORFEATURE_FEATURE", "T M", null, "mirror", "зеркало");

            // Sketch: default geometry is two keys; alternative construction modes use a separate
            // non-terminal semantic branch so the DFA remains prefix-free.
            Add("UG_SKETCH_LINE_BY_TWO_POINTS", "C L");
            Add("UG_SKETCH_LINE_FROM_MIDPOINT", "C M L");
            Add("UG_SKETCH_RECTANGLE_BY_TWO_POINTS", "C R");
            Add("UG_SKETCH_RECTANGLE_FROM_CENTER", "C M R");
            Add("UG_SKETCH_RECTANGLE_BY_THREE_POINTS", "C T R");
            Add("UG_SKETCH_CIRCLE_FROM_CENTER", "C C");
            Add("UG_SKETCH_CIRCLE_BY_THREE_POINTS", "C T C");
            Add("UG_SKETCH_ARC_FROM_CENTER", "C A");
            Add("UG_SKETCH_ARC_BY_THREE_POINTS", "C T A");
            Add("UG_SKETCH_TRIM", "E T");
            Add("UG_SKETCH_EXTEND", "E E");
            Add("UG_SKETCH_OFFSET_CURVE", "T O");
            Add("UG_SKETCH_RAPID_DIMENSION", "D Q");
            Add("UG_SKETCH_LINEAR_DIMENSION", "D L");
            Add("UG_SKETCH_COINCIDENT_CONSTRAINT", "K C");
            Add("UG_SKETCH_TANGENT_CONSTRAINT", "K T");
            Add("UG_SKETCH_PARALLEL_CONSTRAINT", "K P");
            Add("UG_SKETCH_PERPENDICULAR_CONSTRAINT", "K N");
            Add("UG_SKETCH_HORIZONTAL_CONSTRAINT", "K H");
            Add("UG_SKETCH_VERTICAL_CONSTRAINT", "K V");
            Add("UG_SKETCH_CHECKER", "I C");''',
    flags=re.S,
)
regex_replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    r'''            Add\("UG_ASSEMBLIES_ADD_COMPONENT", "C C A",.*?            Add\("UG_ASSEMBLIES_NAVIGATOR", "M N A",.*?\);''',
    '''            Add("UG_ASSEMBLIES_ADD_COMPONENT", "C A");
            Add("UG_ASSEMBLIES_NEW_COMPONENT", "C N");
            Add("UG_ASSEMBLIES_MOVE_COMPONENT", "T M");
            Add("UG_ASSEMBLIES_CONSTRAINTS", "K A");
            Add("UG_ASSEMBLIES_REPLACE_COMPONENT", "E R");
            Add("UG_ASSEMBLIES_REMOVE_COMPONENT", "X C");
            Add("UG_ASSEMBLIES_PATTERN_COMPONENT", "T P");
            Add("UG_ASSEMBLIES_NAVIGATOR", "M N");''',
    flags=re.S,
)
replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    "            Add(\"UG_LAYER_SETTINGS\", \"M L S\");\n            Add(\"UG_LAYER_VIEW\", \"M L V\");\n            Add(\"UG_LAYER_CATEGORY\", \"M L A\");\n            Add(\"UG_LAYER_COPY\", \"M L C\");\n            Add(\"UG_LAYER_MOVE\", \"M L M\");\n            Add(\"UG_INFO_OTHER_LAYERS\", \"I L I\");",
    "            Add(\"UG_LAYER_SETTINGS\", \"L S\");\n            Add(\"UG_LAYER_VIEW\", \"L V\");\n            Add(\"UG_LAYER_CATEGORY\", \"L A\");\n            Add(\"UG_LAYER_COPY\", \"L C\");\n            Add(\"UG_LAYER_MOVE\", \"L M\");\n            Add(\"UG_INFO_OTHER_LAYERS\", \"L I\");",
)
replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    "            Add(\"UG_CAM_CREATE_OPERATION\", \"C O O\", new[] { \"C O\" });\n            Add(\"UG_CAM_CREATE_TOOL\", \"C T T\", new[] { \"C T\" });\n            Add(\"UG_CAM_GENERATE_TOOL_PATH\", \"P O G\", new[] { \"P G\" });\n            Add(\"UG_CAM_VERIFY_TOOL_PATH\", \"P O V\", new[] { \"P V\" });\n            Add(\"UG_CAM_POSTPROCESS\", \"P O P\", new[] { \"P P\" });\n            Add(\"UG_CAM_DELETE_OPERATION\", \"X O D\", new[] { \"X O\" });\n            Add(\"UG_CAM_OPERATION_NAVIGATOR\", \"M N O\", new[] { \"M N\" });\n            Add(\"UG_CAM_INFORMATION\", \"I O T\", new[] { \"I T\" });",
    "            Add(\"UG_CAM_CREATE_OPERATION\", \"C O\");\n            Add(\"UG_CAM_CREATE_TOOL\", \"C T\");\n            Add(\"UG_CAM_GENERATE_TOOL_PATH\", \"P G\");\n            Add(\"UG_CAM_VERIFY_TOOL_PATH\", \"P V\");\n            Add(\"UG_CAM_POSTPROCESS\", \"P P\");\n            Add(\"UG_CAM_DELETE_OPERATION\", \"X O\");\n            Add(\"UG_CAM_OPERATION_NAVIGATOR\", \"M N\");\n            Add(\"UG_CAM_INFORMATION\", \"I T\");",
)

# ---------------------------------------------------------------------------
# Runtime: favorite module cycle and context-safe NX workflow gestures.
# ---------------------------------------------------------------------------
replace(
    "NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs",
    "        private const uint KEYEVENTF_KEYUP = 0x0002;",
    "        private const uint KEYEVENTF_KEYUP = 0x0002;\n        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;\n        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;",
)
replace(
    "NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs",
    "        [DllImport(\"user32.dll\")]\n        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);",
    "        [DllImport(\"user32.dll\")]\n        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);\n        [DllImport(\"user32.dll\")]\n        private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);",
)
replace(
    "NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs",
    "            uint key = input.VirtualKey;\n            if (key == VK_ESCAPE) { Apply(stateMachine.Cancel(\"Отменено клавишей Esc.\")); return; }\n            if (key == VK_TAB) { CycleModule(input.Shift ? -1 : 1); return; }\n            if (key == VK_RETURN)\n            {\n                if (stateMachine.State == LeaderState.Search) ExecuteFirstSearchResult();\n                else Apply(stateMachine.Confirm());\n                return;\n            }\n            if (key == VK_BACK)\n            {\n                if (stateMachine.State == LeaderState.Prefix) Apply(stateMachine.Cancel(\"Leader закрыт.\"));\n                else Apply(stateMachine.Backspace());\n                return;\n            }",
    "            uint key = input.VirtualKey;\n            if (key == VK_TAB) { CycleModule(input.Shift ? -1 : 1); return; }\n            if (key == VK_RETURN)\n            {\n                if (AtModuleRoot()) ExecuteWorkflowGesture(input.Shift ? \"apply\" : \"finish\");\n                else if (stateMachine.State == LeaderState.Search) ExecuteFirstSearchResult();\n                else Apply(stateMachine.Confirm());\n                return;\n            }\n            if (key == VK_ESCAPE)\n            {\n                if (AtModuleRoot()) ExecuteWorkflowGesture(\"cancel\");\n                else Apply(stateMachine.Cancel(\"Отменено клавишей Esc.\"));\n                return;\n            }\n            if (key == VK_BACK)\n            {\n                if (AtModuleRoot()) ExecuteWorkflowGesture(\"back\");\n                else Apply(stateMachine.Backspace());\n                return;\n            }",
)
replace(
    "NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs",
    "        private void ProcessTrigger(DateTime timestampUtc)",
    "        private bool AtModuleRoot()\n        {\n            return stateMachine.State == LeaderState.Prefix &&\n                   SequenceAutomaton.TokenizeSequence(stateMachine.Prefix).Count == 1;\n        }\n\n        private void ExecuteWorkflowGesture(string action)\n        {\n            WorkflowGestureConfig gestures = config.WorkflowGestures ?? new WorkflowGestureConfig();\n            gestures.ApplyDefaults();\n            string gesture = string.Equals(action, \"finish\", StringComparison.OrdinalIgnoreCase) ? gestures.Finish\n                : string.Equals(action, \"apply\", StringComparison.OrdinalIgnoreCase) ? gestures.Apply\n                : string.Equals(action, \"cancel\", StringComparison.OrdinalIgnoreCase) ? gestures.Cancel\n                : gestures.Back;\n            Apply(stateMachine.Cancel(\"NX workflow: \" + action));\n            InjectGesture(gesture);\n            StatusChanged?.Invoke(\"NX workflow gesture: \" + action + \" → \" + gesture);\n        }\n\n        private static void InjectGesture(string gesture)\n        {\n            string normalized = new string((gesture ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());\n            bool control = normalized.Contains(\"CTRL\");\n            if (control) keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);\n            try\n            {\n                if (normalized.Contains(\"MIDDLEMOUSE\"))\n                {\n                    mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, UIntPtr.Zero);\n                    mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, UIntPtr.Zero);\n                }\n                else if (normalized.Contains(\"ESCAPE\")) SendKey(VK_ESCAPE);\n                else if (normalized.Contains(\"BACKSPACE\")) SendKey(VK_BACK);\n                else if (normalized.Contains(\"ENTER\")) SendKey(VK_RETURN);\n            }\n            finally\n            {\n                if (control) keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);\n            }\n        }\n\n        private static void SendKey(byte virtualKey)\n        {\n            keybd_event(virtualKey, 0, 0, UIntPtr.Zero);\n            keybd_event(virtualKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);\n        }\n\n        private void ProcessTrigger(DateTime timestampUtc)",
)
replace(
    "NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs",
    "            List<ModuleConfig> modules = config.RuntimeModules.Where(module => module != null && module.Enabled).ToList();\n            if (modules.Count == 0) return;",
    "            List<ModuleConfig> available = config.RuntimeModules.Where(module => module != null && module.Enabled).ToList();\n            var byId = available.ToDictionary(module => ContextGuardEvaluator.NormalizeModule(module.ID), StringComparer.OrdinalIgnoreCase);\n            List<ModuleConfig> modules = (config.ModuleCycleOrder ?? new List<string>())\n                .Select(ContextGuardEvaluator.NormalizeModule)\n                .Where(byId.ContainsKey).Select(id => byId[id]).Distinct().ToList();\n            if (modules.Count == 0) modules = available.Where(module => module.ID != \"sketch\" && module.ID != \"selection_object\").ToList();\n            if (modules.Count == 0) return;",
)

# ---------------------------------------------------------------------------
# UI: expose canonical paths, aliases, lock/source and frequency.
# ---------------------------------------------------------------------------
replace(
    "NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs",
    "            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = \"Key\", HeaderText = \"Key\", Width = 54 });\n            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = \"Icon\", HeaderText = \"Icon\", Width = 72 });",
    "            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = \"Key\", HeaderText = \"Key\", Width = 54 });\n            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = \"Path\", HeaderText = \"Canonical Path\", Width = 120 });\n            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = \"Aliases\", HeaderText = \"Aliases (; separated)\", Width = 170 });\n            moduleGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = \"PathLocked\", HeaderText = \"Locked\", Width = 62 });\n            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = \"PathSource\", HeaderText = \"Source\", Width = 78 });\n            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = \"Frequency\", HeaderText = \"K\", Width = 48 });\n            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = \"Icon\", HeaderText = \"Icon\", Width = 72 });",
)
replace(
    "NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs",
    "                        command.InputKey,\n                        string.IsNullOrWhiteSpace(command.IconHint)",
    "                        command.InputKey,\n                        string.Join(\" \u2192 \", command.Path ?? new List<string>()),\n                        string.Join(\"; \", (command.Aliases ?? new List<List<string>>()).Select(alias => string.Join(\" \", alias))),\n                        command.PathLocked,\n                        command.PathSource ?? string.Empty,\n                        command.Frequency ?? string.Empty,\n                        string.IsNullOrWhiteSpace(command.IconHint)",
)
replace(
    "NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs",
    "                command.InputKey = LeaderKeyConfig.NormalizeInputKey(ReadText(row, \"Key\"));\n                command.IconHint = ReadText(row, \"Icon\").Trim();",
    "                command.InputKey = LeaderKeyConfig.NormalizeInputKey(ReadText(row, \"Key\"));\n                command.Path = ParsePath(ReadText(row, \"Path\"));\n                command.Aliases = ParseAliases(ReadText(row, \"Aliases\"));\n                command.PathLocked = ReadBool(row, \"PathLocked\");\n                command.PathSource = command.PathLocked ? \"user\" : ReadText(row, \"PathSource\").Trim();\n                command.Frequency = ReadText(row, \"Frequency\").Trim().ToUpperInvariant();\n                command.IconHint = ReadText(row, \"Icon\").Trim();",
)
replace(
    "NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs",
    "        private static string ReadText(DataGridViewRow row, string column) => Convert.ToString(row.Cells[column].Value) ?? string.Empty;",
    "        private static List<string> ParsePath(string value) => MnemonicPathGenerator.NormalizePath(value).ToList();\n\n        private static List<List<string>> ParseAliases(string value)\n        {\n            return (value ?? string.Empty).Split(new[] { ';', '\\n' }, StringSplitOptions.RemoveEmptyEntries)\n                .Select(MnemonicPathGenerator.NormalizePath).Where(path => path.Count > 0)\n                .Select(path => path.ToList()).ToList();\n        }\n\n        private static string ReadText(DataGridViewRow row, string column) => Convert.ToString(row.Cells[column].Value) ?? string.Empty;",
)
replace(
    "NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs",
    "                \"Tab / Shift+Tab запрашивают смену приложения NX. Space включает поиск только внутри текущего модуля. Опасные команды требуют Enter.\";",
    "                \"Tab / Shift+Tab переключают избранный цикл Modeling → Assembly → Drafting → Manufacturing. G+буква открывает любой модуль. \" +\n                \"В корне HUD: Enter = Finish/OK, Shift+Enter = Apply, Esc = Cancel, Backspace = Previous Step. Space включает поиск.\";",
)

# ---------------------------------------------------------------------------
# Bootstrap configuration: universal selection actions, cycle and curated paths.
# ---------------------------------------------------------------------------
config_path = ROOT / "config/nx2512-pro-hybrid.json"
config = json.loads(config_path.read_text(encoding="utf-8-sig"))
leader = config.setdefault("leader_key", {})
leader["module_cycle_order"] = ["modeling", "assembly", "drafting", "manufacturing"]
leader["workflow_gestures"] = {
    "finish": "Ctrl+MiddleMouse",
    "apply": "MiddleMouse",
    "cancel": "Escape",
    "back": "Backspace",
}

curated = {
    "UG_CREATE_SKETCH": ["C", "S"],
    "UG_MODELING_EXTRUDED_FEATURE": ["C", "E"],
    "UG_MODELING_HOLE_FEATURE": ["C", "H"],
    "UG_MODELING_REVOLVED_FEATURE": ["C", "R"],
    "UG_MODELING_BLEND_FEATURE": ["E", "B"],
    "UG_MODELING_CHAMFER_FEATURE": ["E", "C"],
    "UG_MODELING_PATTERNFEATURE_FEATURE": ["T", "P"],
    "UG_MODELING_MIRRORFEATURE_FEATURE": ["T", "M"],
    "UG_SKETCH_LINE_BY_TWO_POINTS": ["C", "L"],
    "UG_SKETCH_LINE_FROM_MIDPOINT": ["C", "M", "L"],
    "UG_SKETCH_RECTANGLE_BY_TWO_POINTS": ["C", "R"],
    "UG_SKETCH_RECTANGLE_FROM_CENTER": ["C", "M", "R"],
    "UG_SKETCH_RECTANGLE_BY_THREE_POINTS": ["C", "T", "R"],
    "UG_SKETCH_CIRCLE_FROM_CENTER": ["C", "C"],
    "UG_SKETCH_CIRCLE_BY_THREE_POINTS": ["C", "T", "C"],
    "UG_SKETCH_ARC_FROM_CENTER": ["C", "A"],
    "UG_SKETCH_ARC_BY_THREE_POINTS": ["C", "T", "A"],
    "UG_SKETCH_TRIM": ["E", "T"],
    "UG_SKETCH_EXTEND": ["E", "E"],
    "UG_SKETCH_OFFSET_CURVE": ["T", "O"],
    "UG_SKETCH_RAPID_DIMENSION": ["D", "Q"],
    "UG_SKETCH_LINEAR_DIMENSION": ["D", "L"],
    "UG_SKETCH_COINCIDENT_CONSTRAINT": ["K", "C"],
    "UG_SKETCH_TANGENT_CONSTRAINT": ["K", "T"],
    "UG_SKETCH_PARALLEL_CONSTRAINT": ["K", "P"],
    "UG_SKETCH_PERPENDICULAR_CONSTRAINT": ["K", "N"],
    "UG_SKETCH_HORIZONTAL_CONSTRAINT": ["K", "H"],
    "UG_SKETCH_VERTICAL_CONSTRAINT": ["K", "V"],
    "UG_SKETCH_CHECKER": ["I", "C"],
    "UG_LAYER_SETTINGS": ["L", "S"],
    "UG_LAYER_VIEW": ["L", "V"],
    "UG_LAYER_CATEGORY": ["L", "A"],
    "UG_LAYER_COPY": ["L", "C"],
    "UG_LAYER_MOVE": ["L", "M"],
    "UG_INFO_OTHER_LAYERS": ["L", "I"],
    "UG_ASSEMBLIES_ADD_COMPONENT": ["C", "A"],
    "UG_ASSEMBLIES_NEW_COMPONENT": ["C", "N"],
    "UG_ASSEMBLIES_MOVE_COMPONENT": ["T", "M"],
    "UG_ASSEMBLIES_CONSTRAINTS": ["K", "A"],
    "UG_ASSEMBLIES_REPLACE_COMPONENT": ["E", "R"],
    "UG_ASSEMBLIES_REMOVE_COMPONENT": ["X", "C"],
    "UG_ASSEMBLIES_PATTERN_COMPONENT": ["T", "P"],
    "UG_ASSEMBLIES_NAVIGATOR": ["M", "N"],
    "UG_CAM_CREATE_OPERATION": ["C", "O"],
    "UG_CAM_CREATE_TOOL": ["C", "T"],
    "UG_CAM_GENERATE_TOOL_PATH": ["P", "G"],
    "UG_CAM_VERIFY_TOOL_PATH": ["P", "V"],
    "UG_CAM_POSTPROCESS": ["P", "P"],
    "UG_CAM_DELETE_OPERATION": ["X", "O"],
    "UG_CAM_OPERATION_NAVIGATOR": ["M", "N"],
    "UG_CAM_INFORMATION": ["I", "T"],
}

for module in config.get("modules", []):
    all_commands = [command for command_set in module.get("command_sets", []) for command in command_set.get("commands", [])]
    for command in all_commands:
        command_id = command.get("command", {}).get("id", "")
        if command_id in curated:
            command["path"] = curated[command_id]
            command["path_labels"] = []
            command["path_source"] = "curated"
            command["path_locked"] = False

    selection_set = next((item for item in module.get("command_sets", []) if item.get("id") == "selection_filters"), None)
    if selection_set is None:
        selection_set = {"id": "selection_filters", "label": "Selection Filters", "commands": []}
        module.setdefault("command_sets", []).append(selection_set)
    existing = {item.get("command", {}).get("id") for item in selection_set.get("commands", [])}
    support_rows = [
        ("UG_SEL_SELECT_ALL", "Select All", ["S", "A"], "all", "selection", 9008),
        ("UG_SEL_DESELECT_ALL", "Deselect All", ["S", "N"], "none", "sel_deselect", 9009),
    ]
    for command_id, name, path_tokens, selection_type, icon, order in support_rows:
        if command_id in existing:
            continue
        selection_set.setdefault("commands", []).append({
            "slot": "", "submenu_key": "", "submenu_label": "Selection Filters",
            "input_key": path_tokens[1], "path": path_tokens,
            "path_labels": ["Select", name], "aliases": [], "path_locked": False,
            "path_source": "support", "search_aliases": [name, command_id],
            "icon_hint": icon, "display_order": order,
            "command": {"id": command_id, "name": name},
            "action": "set_selection_filter", "selection_type": selection_type,
            "enabled": True, "requires_selection": False, "destructive": False,
            "confirm_before_execute": False, "fallback": "", "notes": "Universal runtime selection action",
            "catalog_refs": [], "frequency": "support", "resolution_status": "existing",
            "resolution_candidates": [], "support_kind": "selection_filter",
        })

config_path.write_text(json.dumps(config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")

# ---------------------------------------------------------------------------
# Documentation and ergonomic validation.
# ---------------------------------------------------------------------------
for relative in ["README.md", "FULL_COMMAND_MAP.md", "docs/CONFIGURATION.md"]:
    content = read(relative)
    content = content.replace("sequence_policy_version = 6", "sequence_policy_version = 7")
    content = content.replace("sequence_policy_version: 6", "sequence_policy_version: 7")
    content = content.replace("112 универсальных фильтров выбора", "140 универсальных действий выбора")
    content = content.replace("selection_filter_support_commands = 112", "selection_filter_support_commands = 140")
    content = content.replace("selection_filter_support_commands: 112", "selection_filter_support_commands: 140")
    content = content.replace("support_commands = 244", "support_commands = 272")
    content = content.replace("support_commands: 244", "support_commands: 272")
    content = content.replace("`SB`, `SF`, `SE`, `ST`, `SC`, `SU`, `SD`, `SR`", "`SB`, `SF`, `SE`, `ST`, `SC`, `SU`, `SD`, `SA`, `SN`, `SR`")
    write(relative, content)

write("config/nxkeys.user-overrides.example.json", '''{
  "schema_version": 1,
  "commands": {
    "UG_MODELING_EXTRUDED_FEATURE": {
      "path": ["C", "E"],
      "locked": true
    },
    "UG_SKETCH_TRIM": {
      "module_id": "sketch",
      "path": ["E", "T"],
      "aliases": [["T", "R"]],
      "locked": true
    }
  }
}
''')

write("docs/ERGONOMIC_COMMAND_MAP.md", '''# NXKeys ergonomic command map v7

The runtime keeps the existing prefix-free DFA while changing path precedence to:

```text
user locked override -> curated BUTTON ID path -> profile path -> generated fallback
```

## Always-available controls

```text
S B/F/E/T/C/U/D  selection filter
S A              select all
S N              deselect all
S R              reset filter
G <module>       direct module switch
Tab / Shift+Tab  preferred module cycle
```

The default cycle is `Modeling -> Assembly -> Drafting -> Manufacturing` and is configured by
`leader_key.module_cycle_order`.

At the module root of the HUD:

```text
Enter        Finish / OK       (Ctrl + Middle Mouse)
Shift+Enter  Apply / Continue   (Middle Mouse)
Esc          Cancel             (Escape)
Backspace    Previous Step      (Backspace)
```

The gestures are configurable in `leader_key.workflow_gestures`.

## Layers

`LS` settings, `LV` visible layers, `LA` categories, `LM` move, `LC` copy, `LI` information.

## Sketch

Default geometry is two keys: `CL` line, `CR` rectangle, `CC` circle, `CA` arc. Alternative
construction methods use non-terminal semantic branches (`CML`, `CMR`, `CTR`, `CTC`, `CTA`) so
no command is also a prefix. Editing uses `ET`, `EE`, `TO`; dimensions use `DQ`, `DL`; constraints
use `KC`, `KT`, `KP`, `KN`, `KH`, `KV`; checker is `IC`.

## User overrides

Copy `config/nxkeys.user-overrides.example.json` to `config/nxkeys.user-overrides.json`. The compiler
loads it automatically. Locked paths are never silently reassigned; a conflict stops compilation.
The Studio editor exposes Canonical Path, Aliases, Locked, Source and Frequency columns.
''')

write("scripts/validate-ergonomic-map.mjs", '''import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { CANONICAL_SELECTION_FILTERS, SEQUENCE_POLICY_VERSION } from './sequence-policy.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const profile = JSON.parse(fs.readFileSync(path.join(root, 'config', 'nx2512-pro-main.generated.json'), 'utf8'));
let failed = false;
const fail = message => { failed = true; console.error(`[ergonomic-map] ERROR: ${message}`); };
const key = value => (value ?? []).join('').toUpperCase();
const rows = module => (module.command_sets ?? []).flatMap(set => set.commands ?? []);
const byModule = new Map((profile.modules ?? []).map(module => [module.id, module]));
const find = (moduleId, commandId) => rows(byModule.get(moduleId) ?? {}).find(command => command.command?.id === commandId);

if (SEQUENCE_POLICY_VERSION !== 7) fail(`sequence policy is ${SEQUENCE_POLICY_VERSION}, expected 7`);
if (profile.full_command_catalog?.sequence_policy_version !== 7) fail('generated metadata is not policy v7');
const cycle = profile.leader_key?.module_cycle_order ?? [];
if (cycle.join(',') !== 'modeling,assembly,drafting,manufacturing') fail(`unexpected module cycle: ${cycle.join(',')}`);
for (const field of ['finish', 'apply', 'cancel', 'back']) if (!profile.leader_key?.workflow_gestures?.[field]) fail(`missing workflow gesture ${field}`);

for (const module of byModule.values()) {
  const ids = new Map(rows(module).map(command => [command.command?.id, command]));
  for (const filter of CANONICAL_SELECTION_FILTERS) {
    const command = ids.get(filter.id);
    if (!command) fail(`${module.id} missing ${filter.id}`);
    else if (key(command.path) !== key(filter.path)) fail(`${module.id}/${filter.id} path drift: ${key(command.path)}`);
  }
}

const required = {
  modeling: {
    UG_CREATE_SKETCH: 'CS', UG_MODELING_EXTRUDED_FEATURE: 'CE', UG_MODELING_HOLE_FEATURE: 'CH',
    UG_MODELING_REVOLVED_FEATURE: 'CR', UG_MODELING_BLEND_FEATURE: 'EB', UG_MODELING_CHAMFER_FEATURE: 'EC'
  },
  sketch: {
    UG_SKETCH_LINE_BY_TWO_POINTS: 'CL', UG_SKETCH_LINE_FROM_MIDPOINT: 'CML',
    UG_SKETCH_RECTANGLE_BY_TWO_POINTS: 'CR', UG_SKETCH_RECTANGLE_FROM_CENTER: 'CMR',
    UG_SKETCH_RECTANGLE_BY_THREE_POINTS: 'CTR', UG_SKETCH_CIRCLE_FROM_CENTER: 'CC',
    UG_SKETCH_CIRCLE_BY_THREE_POINTS: 'CTC', UG_SKETCH_ARC_FROM_CENTER: 'CA',
    UG_SKETCH_ARC_BY_THREE_POINTS: 'CTA', UG_SKETCH_TRIM: 'ET', UG_SKETCH_EXTEND: 'EE',
    UG_SKETCH_OFFSET_CURVE: 'TO', UG_SKETCH_RAPID_DIMENSION: 'DQ', UG_SKETCH_LINEAR_DIMENSION: 'DL',
    UG_SKETCH_COINCIDENT_CONSTRAINT: 'KC', UG_SKETCH_TANGENT_CONSTRAINT: 'KT',
    UG_SKETCH_PARALLEL_CONSTRAINT: 'KP', UG_SKETCH_PERPENDICULAR_CONSTRAINT: 'KN',
    UG_SKETCH_HORIZONTAL_CONSTRAINT: 'KH', UG_SKETCH_VERTICAL_CONSTRAINT: 'KV', UG_SKETCH_CHECKER: 'IC'
  }
};
for (const [moduleId, commands] of Object.entries(required)) {
  for (const [commandId, expected] of Object.entries(commands)) {
    const command = find(moduleId, commandId);
    if (!command) fail(`${moduleId} missing curated command ${commandId}`);
    else if (key(command.path) !== expected) fail(`${moduleId}/${commandId}: ${key(command.path)}, expected ${expected}`);
  }
}

const compiler = fs.readFileSync(path.join(root, 'scripts', 'compile-full-command-map.mjs'), 'utf8');
if (!compiler.includes("path_source = 'user'") || !compiler.includes("arg('--overrides'")) fail('compiler lacks durable user overrides');
const engine = fs.readFileSync(path.join(root, 'NX2512_HotkeyStudio', 'Services', 'LeaderKeyEngine.cs'), 'utf8');
for (const marker of ['ExecuteWorkflowGesture', 'module_cycle_order', 'MOUSEEVENTF_MIDDLEDOWN']) if (!engine.includes(marker)) fail(`runtime marker missing: ${marker}`);
const editor = fs.readFileSync(path.join(root, 'NX2512_HotkeyStudio', 'UI', 'HotkeyStudioForm.cs'), 'utf8');
for (const marker of ['Canonical Path', 'PathLocked', 'ParseAliases']) if (!editor.includes(marker)) fail(`editor marker missing: ${marker}`);

if (!failed) console.log(`[ergonomic-map] OK: policy v7, ${CANONICAL_SELECTION_FILTERS.length} selection actions, curated Sketch/layer workflow and durable overrides.`);
if (failed) process.exitCode = 1;
''')

# Add ergonomic validation to the documented validation list.
replace(
    "README.md",
    "node .\\scripts\\audit-command-sequences.mjs\ndotnet run",
    "node .\\scripts\\audit-command-sequences.mjs\nnode .\\scripts\\validate-ergonomic-map.mjs\ndotnet run",
)

# Remove the one-shot migration machinery from the resulting commit.
(ROOT / "scripts/apply-ergonomic-v7.py").unlink(missing_ok=True)
(ROOT / ".github/workflows/apply-ergonomic-v7.yml").unlink(missing_ok=True)

print("Ergonomic command map v7 applied.")
