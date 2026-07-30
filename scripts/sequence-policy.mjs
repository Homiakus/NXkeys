export const SEQUENCE_POLICY_VERSION = 7;

export const FREQUENCY_TARGET_LENGTH = {
  K5: 2,
  K4: 3,
  K3: 4,
  K2: 5,
  K1: 5,
  support: 2
};

export const CANONICAL_SELECTION_FILTERS = [
  { id: 'UG_SEL_BODY_PRIORITY', name: 'Body Selection Priority', path: ['S', 'B'], alias: ['W'], selectionType: 'body', iconHint: 'sel_body' },
  { id: 'UG_SEL_FACE_PRIORITY', name: 'Face Selection Priority', path: ['S', 'F'], alias: ['E'], selectionType: 'face', iconHint: 'sel_face' },
  { id: 'UG_SEL_EDGE_PRIORITY', name: 'Edge Selection Priority', path: ['S', 'E'], alias: ['D'], selectionType: 'edge', iconHint: 'sel_edge' },
  { id: 'UG_SEL_FEATURE_PRIORITY', name: 'Feature Selection Priority', path: ['S', 'T'], alias: ['C'], selectionType: 'feature', iconHint: 'selection' },
  { id: 'UG_SEL_COMPONENT_PRIORITY', name: 'Component Selection Priority', path: ['S', 'C'], alias: ['X'], selectionType: 'component', iconHint: 'assembly' },
  { id: 'UG_SEL_CURVE_PRIORITY', name: 'Curve Selection Priority', path: ['S', 'U'], alias: ['Z'], selectionType: 'curve', iconHint: 'selection' },
  { id: 'UG_SEL_DATUM_PRIORITY', name: 'Datum Selection Priority', path: ['S', 'D'], alias: ['A'], selectionType: 'datum', iconHint: 'selection' },
  { id: 'UG_SEL_TYPE_RESET', name: 'Reset Selection Filter', path: ['S', 'R'], alias: ['Q'], selectionType: 'reset', iconHint: 'sel_deselect' },
  { id: 'UG_SEL_SELECT_ALL', name: 'Select All', path: ['S', 'A'], alias: [], selectionType: 'all', iconHint: 'selection' },
  { id: 'UG_SEL_DESELECT_ALL', name: 'Deselect All', path: ['S', 'N'], alias: [], selectionType: 'none', iconHint: 'sel_deselect' }
];

export const MODULE_SWITCH_PATHS = {
  modeling: ['G', 'M'],
  assembly: ['G', 'A'],
  drafting: ['G', 'D'],
  pmi: ['G', 'P'],
  surface: ['G', 'U'],
  sheet_metal: ['G', 'H'],
  manufacturing: ['G', 'C'],
  simulation: ['G', 'N'],
  routing: ['G', 'R'],
  mold: ['G', 'O'],
  reuse: ['G', 'L'],
  inspect_view: ['G', 'V']
};

export const SWITCHABLE_MODULE_IDS = Object.keys(MODULE_SWITCH_PATHS);
export const DEFAULT_MODULE_CYCLE = ['modeling', 'assembly', 'drafting', 'manufacturing'];

export function normalizeToken(value) {
  return String(value ?? '').trim().match(/[A-Za-z0-9]/)?.[0]?.toUpperCase() ?? '';
}

export function normalizePath(value) {
  const raw = Array.isArray(value) ? value : String(value ?? '').split(/[\s>\-/]+/);
  return raw.map(normalizeToken).filter(Boolean).slice(0, 5);
}

export function pathKey(value) {
  return normalizePath(value).join('');
}

export function pathsConflict(left, right) {
  const a = typeof left === 'string' ? left : pathKey(left);
  const b = typeof right === 'string' ? right : pathKey(right);
  return !!a && !!b && (a.startsWith(b) || b.startsWith(a));
}

export function targetLengthForFrequency(frequency) {
  return FREQUENCY_TARGET_LENGTH[String(frequency ?? '').trim()] ?? 5;
}

export function isSelectionSupportCommand(command) {
  const canonicalIds = new Set(CANONICAL_SELECTION_FILTERS.map(filter => filter.id));
  return command?.support_kind === 'selection_filter' ||
    canonicalIds.has(String(command?.command?.id ?? '').toUpperCase());
}

export function isModuleSwitchSupportCommand(command) {
  return command?.support_kind === 'module_switch' ||
    command?.action === 'switch_module';
}

export function isSupportCommand(command) {
  return isSelectionSupportCommand(command) || isModuleSwitchSupportCommand(command);
}

export function commandRows(module) {
  return (module?.command_sets ?? []).flatMap(set => (set?.commands ?? []).map(command => ({ module, set, command })));
}

export function findOrCreateSet(module, id, label) {
  module.command_sets ??= [];
  let set = module.command_sets.find(item => item && item.id === id);
  if (!set) {
    set = { id, label, commands: [] };
    module.command_sets.push(set);
  }
  set.commands ??= [];
  return set;
}

function upsertCommand(set, match, factory) {
  const existing = set.commands.find(match);
  if (existing) return Object.assign(existing, factory(existing));
  const created = factory(null);
  set.commands.push(created);
  return created;
}

export function ensureUniversalSelectionFilters(modules) {
  const canonicalIds = new Set(CANONICAL_SELECTION_FILTERS.map(filter => filter.id));
  for (const module of modules ?? []) {
    if (!module || module.enabled === false) continue;
    for (const existingSet of module.command_sets ?? []) {
      if (existingSet?.id === 'selection_filters') continue;
      existingSet.commands = (existingSet.commands ?? []).filter(command => !canonicalIds.has(String(command?.command?.id ?? '').toUpperCase()));
    }
    const set = findOrCreateSet(module, 'selection_filters', 'Selection Filters');
    set.commands = set.commands.filter(command => !/^UG_SEL_RESET$/i.test(String(command?.command?.id ?? '')));
    for (const [index, filter] of CANONICAL_SELECTION_FILTERS.entries()) {
      upsertCommand(
        set,
        command => String(command?.command?.id ?? '').toUpperCase() === filter.id,
        () => ({
          slot: '',
          submenu_key: '',
          submenu_label: 'Selection Filters',
          input_key: filter.path[1],
          path: [...filter.path],
          path_labels: ['Select', filter.name],
          aliases: [],
          search_aliases: [filter.name, filter.id],
          icon_hint: filter.iconHint,
          display_order: 9000 + index,
          command: { id: filter.id, name: filter.name },
          action: 'set_selection_filter',
          selection_type: filter.selectionType,
          enabled: true,
          requires_selection: false,
          destructive: false,
          confirm_before_execute: false,
          fallback: '',
          notes: 'Universal runtime selection filter',
          catalog_refs: [],
          frequency: 'support',
          resolution_status: 'existing',
          resolution_candidates: [],
          support_kind: 'selection_filter',
          path_locked: false,
          path_source: 'curated'
        })
      );
    }
  }
}

export function ensureUniversalModuleSwitches(modules) {
  const byId = new Map((modules ?? []).filter(module => module && module.enabled !== false).map(module => [module.id, module]));
  for (const module of byId.values()) {
    if (module.id === 'sketch' || module.id === 'selection_object') continue;
    const set = findOrCreateSet(module, 'module_switches', 'Module Switches');
    set.commands = set.commands.filter(command => command?.support_kind !== 'module_switch' && command?.action !== 'switch_module');
    let order = 9100;
    for (const targetId of SWITCHABLE_MODULE_IDS) {
      if (targetId === module.id || !byId.has(targetId)) continue;
      const target = byId.get(targetId);
      const commandId = target.switch_command?.id || target.nx_application_ids?.[0] || '';
      const applicationId = target.nx_application_ids?.[0] || commandId;
      if (!commandId && !applicationId) continue;
      set.commands.push({
        slot: '',
        submenu_key: '',
        submenu_label: 'Module Switches',
        input_key: MODULE_SWITCH_PATHS[targetId][1],
        path: [...MODULE_SWITCH_PATHS[targetId]],
        path_labels: ['Go', target.label || targetId],
        aliases: [],
        search_aliases: [`Switch to ${target.label || targetId}`, targetId, commandId, applicationId].filter(Boolean),
        icon_hint: 'menu',
        display_order: order++,
        command: { id: commandId || applicationId, name: `Switch to ${target.label || targetId}` },
        action: 'switch_module',
        target_module_id: targetId,
        selection_type: '',
        enabled: true,
        requires_selection: false,
        destructive: false,
        confirm_before_execute: false,
        fallback: '',
        notes: 'Universal runtime module switch',
        catalog_refs: [],
        frequency: 'support',
        resolution_status: 'existing',
        resolution_candidates: [],
        support_kind: 'module_switch',
        path_locked: false,
        path_source: 'curated'
      });
    }
  }

  const sketch = byId.get('sketch');
  if (sketch?.command_sets) {
    for (const set of sketch.command_sets) {
      set.commands = (set.commands ?? []).filter(command => command?.support_kind !== 'module_switch' && command?.action !== 'switch_module');
    }
    sketch.command_sets = sketch.command_sets.filter(set => set.id !== 'module_switches');
  }
}

export function ensureUniversalSupport(modules) {
  ensureUniversalSelectionFilters(modules);
  ensureUniversalModuleSwitches(modules);
}

export function supportMetadata(modules) {
  let selectionFilters = 0;
  let moduleSwitches = 0;
  for (const module of modules ?? []) {
    for (const { command } of commandRows(module)) {
      if (isSelectionSupportCommand(command)) selectionFilters += 1;
      if (isModuleSwitchSupportCommand(command)) moduleSwitches += 1;
    }
  }
  return {
    support_commands: selectionFilters + moduleSwitches,
    selection_filter_support_commands: selectionFilters,
    module_switch_support_commands: moduleSwitches
  };
}
