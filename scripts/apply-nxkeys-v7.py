#!/usr/bin/env python3
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def read(path):
    return (ROOT / path).read_text(encoding='utf-8')

def write(path, text):
    (ROOT / path).write_text(text, encoding='utf-8', newline='\n')

def replace(path, old, new, required=True):
    text = read(path)
    if old not in text:
        if required:
            raise RuntimeError(f'Pattern not found in {path}: {old[:100]}')
        return False
    write(path, text.replace(old, new))
    return True

# 1. Sequence policy v7: complete selection grammar and preferred module cycle.
p = 'scripts/sequence-policy.mjs'
t = read(p)
t = t.replace('export const SEQUENCE_POLICY_VERSION = 6;', 'export const SEQUENCE_POLICY_VERSION = 7;')
t = t.replace(
"  { id: 'UG_SEL_TYPE_RESET', name: 'Reset Selection Filter', path: ['S', 'R'], alias: ['Q'], selectionType: 'reset', iconHint: 'sel_deselect' }\n];",
"  { id: 'UG_SEL_TYPE_RESET', name: 'Reset Selection Filter', path: ['S', 'R'], alias: ['Q'], selectionType: 'reset', iconHint: 'sel_deselect' },\n  { id: 'UG_SEL_SELECT_ALL', name: 'Select All', path: ['S', 'A'], alias: [], selectionType: 'all', iconHint: 'selection' },\n  { id: 'UG_SEL_DESELECT_ALL', name: 'Deselect All', path: ['S', 'N'], alias: [], selectionType: 'none', iconHint: 'sel_deselect' }\n];")
t = t.replace(
"export const SWITCHABLE_MODULE_IDS = Object.keys(MODULE_SWITCH_PATHS);",
"export const SWITCHABLE_MODULE_IDS = Object.keys(MODULE_SWITCH_PATHS);\n\nexport const DEFAULT_MODULE_CYCLE = ['modeling', 'assembly', 'drafting', 'manufacturing'];")
t = t.replace(
"    set.commands = set.commands.filter(command => !/^UG_SEL_(RESET|SELECT_ALL|DESELECT_ALL)$/i.test(String(command?.command?.id ?? '')));",
"    set.commands = set.commands.filter(command => !/^UG_SEL_RESET$/i.test(String(command?.command?.id ?? '')));")
write(p, t)

# 2. Stable semantic paths: layers become first-class; Sketch uses compact contextual grammar.
p = 'NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs'
t = read(p)
replacements = {
'Add("UG_SKETCH_LINE", "C G L");':'Add("UG_SKETCH_LINE", "C L");',
'Add("UG_SKETCH_RECTANGLE", "C G R");':'Add("UG_SKETCH_RECTANGLE", "C R");',
'Add("UG_SKETCH_CIRCLE", "C G C");':'Add("UG_SKETCH_CIRCLE", "C C");',
'Add("UG_SKETCH_ARC", "C G A");':'Add("UG_SKETCH_ARC", "C A");',
'Add("UG_SKETCH_TRIM", "E G T");':'Add("UG_SKETCH_TRIM", "E T");',
'Add("UG_SKETCH_EXTEND", "E G E");':'Add("UG_SKETCH_EXTEND", "E E");',
'Add("UG_SKETCH_OFFSET_CURVE", "T G O");':'Add("UG_SKETCH_OFFSET_CURVE", "T O");',
'Add("UG_SKETCH_LINE_BY_TWO_POINTS", "C G L 2");':'Add("UG_SKETCH_LINE_BY_TWO_POINTS", "C L 2");',
'Add("UG_SKETCH_LINE_FROM_MIDPOINT", "C G L M");':'Add("UG_SKETCH_LINE_FROM_MIDPOINT", "C L M");',
'Add("UG_SKETCH_RECTANGLE_BY_TWO_POINTS", "C G R 2");':'Add("UG_SKETCH_RECTANGLE_BY_TWO_POINTS", "C R 2");',
'Add("UG_SKETCH_RECTANGLE_FROM_CENTER", "C G R C");':'Add("UG_SKETCH_RECTANGLE_FROM_CENTER", "C R C");',
'Add("UG_SKETCH_RECTANGLE_BY_THREE_POINTS", "C G R 3");':'Add("UG_SKETCH_RECTANGLE_BY_THREE_POINTS", "C R 3");',
'Add("UG_SKETCH_CIRCLE_FROM_CENTER", "C G C C");':'Add("UG_SKETCH_CIRCLE_FROM_CENTER", "C C C");',
'Add("UG_SKETCH_CIRCLE_BY_THREE_POINTS", "C G C 3");':'Add("UG_SKETCH_CIRCLE_BY_THREE_POINTS", "C C 3");',
'Add("UG_SKETCH_ARC_BY_THREE_POINTS", "C G A 3");':'Add("UG_SKETCH_ARC_BY_THREE_POINTS", "C A 3");',
'Add("UG_SKETCH_ARC_FROM_CENTER", "C G A C");':'Add("UG_SKETCH_ARC_FROM_CENTER", "C A C");',
'Add("UG_SKETCH_RAPID_DIMENSION", "A D R");':'Add("UG_SKETCH_RAPID_DIMENSION", "D Q");',
'Add("UG_SKETCH_LINEAR_DIMENSION", "A D L");':'Add("UG_SKETCH_LINEAR_DIMENSION", "D L");',
'Add("UG_SKETCH_COINCIDENT_CONSTRAINT", "C K C");':'Add("UG_SKETCH_COINCIDENT_CONSTRAINT", "K C");',
'Add("UG_SKETCH_TANGENT_CONSTRAINT", "C K T");':'Add("UG_SKETCH_TANGENT_CONSTRAINT", "K T");',
'Add("UG_SKETCH_PARALLEL_CONSTRAINT", "C K P");':'Add("UG_SKETCH_PARALLEL_CONSTRAINT", "K P");',
'Add("UG_SKETCH_PERPENDICULAR_CONSTRAINT", "C K N");':'Add("UG_SKETCH_PERPENDICULAR_CONSTRAINT", "K N");',
'Add("UG_SKETCH_HORIZONTAL_CONSTRAINT", "C K H");':'Add("UG_SKETCH_HORIZONTAL_CONSTRAINT", "K H");',
'Add("UG_SKETCH_VERTICAL_CONSTRAINT", "C K V");':'Add("UG_SKETCH_VERTICAL_CONSTRAINT", "K V");',
'Add("UG_SKETCH_CHECKER", "I S C");':'Add("UG_SKETCH_CHECKER", "I C");',
'Add("UG_LAYER_SETTINGS", "M L S");':'Add("UG_LAYER_SETTINGS", "L S");',
'Add("UG_LAYER_VIEW", "M L V");':'Add("UG_LAYER_VIEW", "L V");',
'Add("UG_LAYER_CATEGORY", "M L A");':'Add("UG_LAYER_CATEGORY", "L A");',
'Add("UG_LAYER_COPY", "M L C");':'Add("UG_LAYER_COPY", "L C");',
'Add("UG_LAYER_MOVE", "M L M");':'Add("UG_LAYER_MOVE", "L M");',
'Add("UG_INFO_OTHER_LAYERS", "I L I");':'Add("UG_INFO_OTHER_LAYERS", "L I");'
}
for old, new in replacements.items():
    if old not in t:
        raise RuntimeError(f'Missing mnemonic mapping: {old}')
    t = t.replace(old, new)
# Semantic roots L/D/K are allowed and must not be displaced by collision fallback.
t = t.replace(
'new[] { root, "C", "E", "T", "X", "P", "I", "V", "A", "M", "F", "U", "H" }',
'new[] { root, "C", "E", "T", "X", "P", "I", "V", "A", "M", "L", "D", "K", "F", "U", "H" }')
write(p, t)

# 3. Persist path ownership/locking metadata for safe customization.
p = 'NX2512_HotkeyStudio/Models/ModuleConfigTypesV5.cs'
t = read(p)
t = t.replace(
'        [JsonPropertyName("frequency")] public string Frequency { get; set; } = string.Empty;\n',
'        [JsonPropertyName("frequency")] public string Frequency { get; set; } = string.Empty;\n        [JsonPropertyName("path_locked")] public bool PathLocked { get; set; }\n        [JsonPropertyName("path_source")] public string PathSource { get; set; } = string.Empty;\n')
write(p, t)

# 4. User-locked paths have priority over generated and curated reallocation.
p = 'NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs'
t = read(p)
t = t.replace(
'                    IReadOnlyList<string> requested = NormalizePath(command.Path);\n                    if (requested.Count == 0) requested = NormalizePath(definition?.Path);',
'                    IReadOnlyList<string> requested = command.PathLocked ? NormalizePath(command.Path) : NormalizePath(definition?.Path);\n                    if (requested.Count == 0) requested = NormalizePath(command.Path);')
t = t.replace(
'                    command.Path = canonical;\n',
'                    command.Path = canonical;\n                    if (string.IsNullOrWhiteSpace(command.PathSource)) command.PathSource = command.PathLocked ? "user" : (definition != null ? "curated" : "generated");\n')
write(p, t)

# 5. Tab cycles only through the high-value application loop, falling back to enabled modules.
p = 'NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs'
t = read(p)
old = '            List<ModuleConfig> modules = config.RuntimeModules.Where(module => module != null && module.Enabled).ToList();\n            if (modules.Count == 0) return;'
new = '            string[] preferred = { "modeling", "assembly", "drafting", "manufacturing" };\n            List<ModuleConfig> enabled = config.RuntimeModules.Where(module => module != null && module.Enabled).ToList();\n            List<ModuleConfig> modules = preferred.Select(id => enabled.FirstOrDefault(module => string.Equals(ContextGuardEvaluator.NormalizeModule(module.ID), ContextGuardEvaluator.NormalizeModule(id), StringComparison.OrdinalIgnoreCase)))\n                .Where(module => module != null).ToList();\n            if (modules.Count == 0) modules = enabled;\n            if (modules.Count == 0) return;'
if old not in t: raise RuntimeError('CycleModule block not found')
t = t.replace(old, new)
write(p, t)

# 6. Editor exposes canonical path, aliases, lock, source and frequency.
p = 'NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs'
t = read(p)
t = t.replace(
'            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = "Key", Width = 54 });',
'            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = "Key", Width = 54 });\n            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "Canonical Path", Width = 120 });\n            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Aliases", HeaderText = "Aliases", Width = 150 });\n            moduleGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "PathLocked", HeaderText = "Locked", Width = 62 });\n            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PathSource", HeaderText = "Source", Width = 74, ReadOnly = true });\n            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Frequency", HeaderText = "Freq", Width = 52 });')
t = t.replace(
'                        command.InputKey,\n                        string.IsNullOrWhiteSpace(command.IconHint)',
'                        command.InputKey,\n                        string.Join(" ", command.Path ?? new List<string>()),\n                        string.Join("; ", (command.Aliases ?? new List<List<string>>()).Select(alias => string.Join(" ", alias))),\n                        command.PathLocked,\n                        command.PathSource,\n                        command.Frequency,\n                        string.IsNullOrWhiteSpace(command.IconHint)')
t = t.replace(
'                command.InputKey = LeaderKeyConfig.NormalizeInputKey(ReadText(row, "Key"));\n                command.IconHint = ReadText(row, "Icon").Trim();',
'                command.InputKey = LeaderKeyConfig.NormalizeInputKey(ReadText(row, "Key"));\n                command.Path = MnemonicPathGenerator.NormalizePath(ReadText(row, "Path").Split(new[] { \' \', \'-\', \'>\' }, StringSplitOptions.RemoveEmptyEntries)).ToList();\n                command.Aliases = ReadText(row, "Aliases").Split(new[] { \';\' }, StringSplitOptions.RemoveEmptyEntries)\n                    .Select(value => MnemonicPathGenerator.NormalizePath(value.Split(new[] { \' \', \'-\', \'>\' }, StringSplitOptions.RemoveEmptyEntries)).ToList())\n                    .Where(alias => alias.Count > 0).ToList();\n                command.PathLocked = ReadBool(row, "PathLocked");\n                command.PathSource = command.PathLocked ? "user" : ReadText(row, "PathSource").Trim();\n                command.Frequency = ReadText(row, "Frequency").Trim().ToUpperInvariant();\n                command.IconHint = ReadText(row, "Icon").Trim();')
write(p, t)

# 7. Bootstrap profile: normalize curated paths, add selection all/none, mark curated paths.
p = 'config/nx2512-pro-hybrid.json'
data = json.loads(read(p))
path_map = {
'UG_SKETCH_LINE_BY_TWO_POINTS':['C','L','2'], 'UG_SKETCH_LINE_FROM_MIDPOINT':['C','L','M'],
'UG_SKETCH_RECTANGLE_BY_TWO_POINTS':['C','R','2'], 'UG_SKETCH_RECTANGLE_FROM_CENTER':['C','R','C'],
'UG_SKETCH_RECTANGLE_BY_THREE_POINTS':['C','R','3'], 'UG_SKETCH_CIRCLE_FROM_CENTER':['C','C','C'],
'UG_SKETCH_CIRCLE_BY_THREE_POINTS':['C','C','3'], 'UG_SKETCH_ARC_BY_THREE_POINTS':['C','A','3'],
'UG_SKETCH_ARC_FROM_CENTER':['C','A','C'], 'UG_SKETCH_TRIM':['E','T'], 'UG_SKETCH_EXTEND':['E','E'],
'UG_SKETCH_OFFSET_CURVE':['T','O'], 'UG_SKETCH_RAPID_DIMENSION':['D','Q'], 'UG_SKETCH_LINEAR_DIMENSION':['D','L'],
'UG_SKETCH_COINCIDENT_CONSTRAINT':['K','C'], 'UG_SKETCH_TANGENT_CONSTRAINT':['K','T'],
'UG_SKETCH_PARALLEL_CONSTRAINT':['K','P'], 'UG_SKETCH_PERPENDICULAR_CONSTRAINT':['K','N'],
'UG_SKETCH_HORIZONTAL_CONSTRAINT':['K','H'], 'UG_SKETCH_VERTICAL_CONSTRAINT':['K','V'], 'UG_SKETCH_CHECKER':['I','C'],
'UG_LAYER_SETTINGS':['L','S'], 'UG_LAYER_VIEW':['L','V'], 'UG_LAYER_CATEGORY':['L','A'],
'UG_LAYER_COPY':['L','C'], 'UG_LAYER_MOVE':['L','M'], 'UG_INFO_OTHER_LAYERS':['L','I']
}
for module in data.get('modules', []):
    for command_set in module.get('command_sets', []):
        for command in command_set.get('commands', []):
            cid = command.get('command', {}).get('id', '')
            if cid in path_map:
                command['path'] = path_map[cid]
                command['path_locked'] = False
                command['path_source'] = 'curated'
            elif command.get('path'):
                command.setdefault('path_source', 'curated' if not command.get('fallback') else 'generated')
    # complete universal selection grammar in every module
    selection = next((s for s in module.get('command_sets', []) if s.get('id') == 'selection_filters'), None)
    if selection is not None:
        existing = {c.get('command', {}).get('id') for c in selection.get('commands', [])}
        extras = [
            ('UG_SEL_SELECT_ALL','Select All',['S','A'],'all','selection'),
            ('UG_SEL_DESELECT_ALL','Deselect All',['S','N'],'none','sel_deselect')
        ]
        for idx, (cid, name, pathv, stype, icon) in enumerate(extras, start=8):
            if cid in existing: continue
            selection['commands'].append({
                'slot':'','submenu_key':'','submenu_label':'Selection Filters','input_key':pathv[1],
                'path':pathv,'path_labels':['Select',name],'aliases':[],'search_aliases':[name,cid],
                'icon_hint':icon,'display_order':9000+idx,'command':{'id':cid,'name':name},
                'action':'set_selection_filter','selection_type':stype,'enabled':True,'requires_selection':False,
                'destructive':False,'confirm_before_execute':False,'fallback':'','notes':'Universal runtime selection action',
                'catalog_refs':[],'frequency':'support','resolution_status':'existing','resolution_candidates':[],
                'support_kind':'selection_filter','path_source':'curated','path_locked':False
            })
data.setdefault('leader_key', {})['module_cycle_order'] = ['modeling','assembly','drafting','manufacturing']
data.setdefault('full_command_catalog', {})['sequence_policy_version'] = 7
write(p, json.dumps(data, ensure_ascii=False, indent=2) + '\n')

# 8. Documentation of the implemented interaction contract.
doc = '''# NXKeys v7 — ergonomic command map\n\n## Core rules\n\n- `CapsLock` opens the active NX context.\n- `S B/F/E/T/C/U/D/R/A/N` controls selection: Body, Face, Edge, Feature, Component, Curve, Datum, Reset, All, None.\n- `G <module>` directly switches application. `Tab` cycles Modeling → Assembly → Drafting → Manufacturing; `Shift+Tab` reverses.\n- Frequent layer operations use the first-class `L*` namespace: `LS`, `LV`, `LA`, `LM`, `LC`, `LI`.\n- User paths can be locked. Locked paths have priority over curated and generated paths and survive profile rebuilding.\n\n## Sketch\n\nCreation: `CL` Line, `CR` Rectangle, `CC` Circle, `CA` Arc. Variants add `2`, `3`, `C`, or `M`.\n\nEditing: `ET` Trim, `EE` Extend, `TO` Offset.\n\nDimensions: `DQ` Rapid Dimension, `DL` Linear Dimension.\n\nConstraints: `KC` Coincident, `KT` Tangent, `KP` Parallel, `KN` Perpendicular, `KH` Horizontal, `KV` Vertical.\n\nInspection: `IC` Sketch Checker.\n\n## Customization\n\nThe module editor exposes Canonical Path, Aliases, Locked, Source, and Frequency. Set `Locked` for personal paths that must not be reassigned by the generator.\n'''
write('docs/NXKEYS_V7_ERGONOMIC_MAP.md', doc)

# Update visible policy version references where exact and safe.
for path in ['README.md','docs/CONFIGURATION.md','docs/MNEMONIC_COMMAND_LANGUAGE.md']:
    text = read(path)
    text = text.replace('sequence_policy_version = 6', 'sequence_policy_version = 7')
    text = text.replace('sequence_policy_version: 6', 'sequence_policy_version: 7')
    text = text.replace('"sequence_policy_version": 6', '"sequence_policy_version": 7')
    write(path, text)

print('NXKeys v7 ergonomic migration applied.')
