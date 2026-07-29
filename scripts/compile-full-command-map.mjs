import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';
import {
  SEQUENCE_POLICY_VERSION,
  ensureUniversalSupport,
  isSupportCommand,
  pathKey as policyPathKey,
  supportMetadata,
  targetLengthForFrequency
} from './sequence-policy.mjs';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const argv = process.argv.slice(2);
const arg = (name, fallback = '') => {
  const index = argv.indexOf(name);
  return index >= 0 && index + 1 < argv.length ? argv[index + 1] : fallback;
};
const has = name => argv.includes(name);
const absolute = value => path.isAbsolute(value) ? value : path.resolve(repoRoot, value);
const profilePath = absolute(arg('--profile', 'config/nx2512-pro-hybrid.json'));
const intentsDir = absolute(arg('--intents', 'config/full-command-map'));
const catalogDirValue = arg('--catalog-dir', '');
const catalogDir = catalogDirValue ? absolute(catalogDirValue) : '';
const probePathValue = arg('--probe', 'docs/audit/runtime-command-probe-2026-07-28.json');
const probePath = probePathValue ? absolute(probePathValue) : '';
const outputPath = absolute(arg('--out', 'config/nx2512-pro-full.generated.json'));
const reportPath = absolute(arg('--report', 'docs/generated/full-command-resolution.md'));
const duplicateGlobal = !has('--no-global-duplication');

const readText = file => fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '');
const readJson = file => JSON.parse(readText(file));
const clone = value => JSON.parse(JSON.stringify(value));
const normalizeKey = value => String(value ?? '').trim().match(/[A-Za-z0-9]/)?.[0]?.toUpperCase() ?? '';
const normalizePath = value => {
  const raw = Array.isArray(value) ? value : String(value ?? '').split(/[\s>\-/]+/);
  return raw.map(normalizeKey).filter(Boolean).slice(0, 5);
};
const pathKey = value => normalizePath(value).join('');
const normalizeText = value => String(value ?? '')
  .toLowerCase()
  .replace(/&/g, '')
  .replace(/[^\p{L}\p{N}]+/gu, ' ')
  .trim()
  .replace(/\s+/g, ' ');
const englishTokens = value => String(value ?? '').toUpperCase().match(/[A-Z0-9]+/g) ?? [];
const unescapeField = value => String(value ?? '')
  .replace(/\\t/g, '\t')
  .replace(/\\n/g, '\n')
  .replace(/\\\\/g, '\\');

const ROOT_LABELS = {
  C: 'Create', E: 'Edit', T: 'Transform', X: 'Remove', P: 'Process', I: 'Inspect',
  V: 'View', S: 'Select', A: 'Annotate', M: 'Manage', F: 'File', G: 'Go', U: 'Utilities', H: 'Help'
};
const OBJECT_LABELS = {
  A: 'Annotation/Additive', B: 'Body/Base', C: 'Component', D: 'Dimension/Datum', E: 'Edge',
  F: 'Feature/Frame', G: 'Geometry', H: 'Sheet Metal', I: 'Inspection', J: 'Fixture', K: 'Constraint',
  L: 'Layout/Layer', M: 'Material/Mold', N: 'Simulation', O: 'Operation', P: 'Part/Data',
  Q: 'Quality', R: 'Routing', S: 'Sketch/Selection', T: 'Tool/Template', U: 'Surface',
  V: 'View', W: 'WAVE', Y: 'Assembly/Ship', Z: 'Other'
};
const STOP_WORDS = new Set([
  'UG', 'NX', 'THE', 'A', 'AN', 'AND', 'OR', 'TO', 'FROM', 'BY', 'IN', 'ON', 'OF', 'FOR', 'WITH',
  'WITHOUT', 'INTO', 'THROUGH', 'ALONG', 'AT', 'AS', 'COMMAND', 'OBJECT', 'MODEL', 'MODELING',
  'APPLICATION', 'MANAGER', 'TOOLS', 'TOOL', 'SETTINGS', 'SETTING'
]);

function parseCsvLine(line) {
  const result = [];
  let value = '';
  let quoted = false;
  for (let index = 0; index < line.length; index += 1) {
    const char = line[index];
    if (char === '"') {
      if (quoted && line[index + 1] === '"') { value += '"'; index += 1; }
      else quoted = !quoted;
    } else if (char === ',' && !quoted) {
      result.push(value); value = '';
    } else value += char;
  }
  result.push(value);
  return result;
}

function loadIntents(directory) {
  const names = fs.readdirSync(directory).sort((a, b) => a.localeCompare(b));
  const tsvFiles = names.filter(name => /^nx2512-full-.*\.tsv$/i.test(name));
  let header = [];
  let rows = [];
  if (tsvFiles.length) {
    for (const file of tsvFiles) {
      const lines = readText(path.join(directory, file)).split(/\r?\n/).filter(line => line && !line.startsWith('#'));
      const currentHeader = lines.shift()?.split('\t') ?? [];
      if (!header.length) header = currentHeader;
      for (const line of lines) rows.push(line.split('\t').map(unescapeField));
    }
  } else {
    const single = names.find(name => /^nx2512-full-command-map\.json\.gz\.b64$/i.test(name));
    const parts = names.filter(name => /^nx2512-full-command-map\.json\.gz\.b64\.part\d+$/i.test(name));
    if (!single && !parts.length) throw new Error(`No full-command-map data files found in ${directory}`);
    const encoded = single
      ? readText(path.join(directory, single)).trim()
      : parts.map(file => readText(path.join(directory, file)).trim()).join('');
    const payload = JSON.parse(gunzipSync(Buffer.from(encoded, 'base64')).toString('utf8'));
    header = payload.fields ?? [];
    rows = payload.rows ?? [];
    if (payload.count !== rows.length) throw new Error(`Compressed catalog count mismatch: ${payload.count} != ${rows.length}`);
  }
  const index = Object.fromEntries(header.map((name, position) => [name, position]));
  return rows.map(fields => {
    const entry = {
      intent_id: fields[index.intent_id],
      source_index: Number(fields[index.source_index]),
      runtime_module: fields[index.runtime_module],
      frequency: fields[index.frequency],
      source_module: fields[index.source_module],
      group: fields[index.group],
      name_en: fields[index.name_en],
      name_ru: fields[index.name_ru],
      path_hint: normalizePath(fields[index.path])
    };
    if (!entry.intent_id || !entry.name_en || !entry.runtime_module || entry.path_hint.length < 2)
      throw new Error(`Invalid intent row: ${JSON.stringify(fields)}`);
    return entry;
  });
}

function addCatalogItem(index, item) {
  if (!item?.id) return;
  const key = item.id.trim().toUpperCase();
  const current = index.byId.get(key) ?? { id: item.id.trim(), labels: [], synonyms: [], source: [] };
  for (const label of item.labels ?? []) if (label && !current.labels.some(x => normalizeText(x) === normalizeText(label))) current.labels.push(label.trim());
  for (const synonym of item.synonyms ?? []) if (synonym && !current.synonyms.some(x => normalizeText(x) === normalizeText(synonym))) current.synonyms.push(synonym.trim());
  for (const source of item.source ?? []) if (source && !current.source.includes(source)) current.source.push(source);
  index.byId.set(key, current);
}

function loadNxCatalog(directory, probeFile) {
  const index = { byId: new Map() };
  if (directory) {
    const csv = path.join(directory, '06_ui_commands_buttons.csv');
    if (!fs.existsSync(csv)) throw new Error(`NX button catalog not found: ${csv}`);
    const lines = readText(csv).split(/\r?\n/);
    lines.shift();
    for (const line of lines) {
      if (!line.trim()) continue;
      const fields = parseCsvLine(line);
      const id = fields[0]?.trim();
      if (!id) continue;
      const label = fields[1]?.trim().replace(/^['"]|['"]$/g, '').replaceAll('&', '');
      const synonyms = (fields[2] ?? '').split(/[;,]/).map(x => x.trim()).filter(Boolean);
      addCatalogItem(index, { id, labels: label ? [label] : [], synonyms, source: [csv] });
    }
  }
  if (probeFile && fs.existsSync(probeFile)) {
    const probe = readJson(probeFile);
    for (const row of probe.results ?? []) addCatalogItem(index, {
      id: row.id,
      labels: row.name ? [row.name] : [],
      synonyms: [],
      source: [probeFile]
    });
  }
  index.items = [...index.byId.values()];
  return index;
}

function addProfileCommandsToCatalog(profile, index) {
  const addRef = (command, source) => {
    const id = String(command?.id ?? command?.ID ?? '').trim();
    const name = String(command?.name ?? command?.Name ?? '').trim();
    const aliases = command?.aliases ?? command?.Aliases ?? [];
    if (!id) return;
    addCatalogItem(index, { id, labels: name ? [name] : [], synonyms: aliases, source: [source] });
  };
  for (const binding of profile.keyboard ?? []) addRef(binding?.command, 'source-profile:keyboard');
  for (const module of profile.modules ?? []) {
    addRef(module?.switch_command, `source-profile:${module?.id ?? 'module'}:switch`);
    for (const set of module?.command_sets ?? [])
      for (const row of set?.commands ?? []) addRef(row?.command, `source-profile:${module?.id ?? 'module'}`);
  }
  for (const command of Object.values(profile.workflow_controls ?? {})) addRef(command, 'source-profile:workflow');
  index.items = [...index.byId.values()];
}

function levenshtein(a, b) {
  if (!a.length) return b.length;
  if (!b.length) return a.length;
  let previous = Array.from({ length: b.length + 1 }, (_, index) => index);
  for (let i = 1; i <= a.length; i += 1) {
    const current = [i];
    for (let j = 1; j <= b.length; j += 1) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      current[j] = Math.min(previous[j] + 1, current[j - 1] + 1, previous[j - 1] + cost);
    }
    previous = current;
  }
  return previous[b.length];
}

function similarity(a, b) {
  const left = normalizeText(a);
  const right = normalizeText(b);
  if (!left || !right) return 0;
  if (left === right) return 1;
  const leftWords = left.split(' ');
  const rightWords = right.split(' ');
  const smaller = leftWords.length <= rightWords.length ? leftWords : rightWords;
  const larger = leftWords.length <= rightWords.length ? rightWords : leftWords;
  const contiguous = larger.some((_, start) => smaller.every((word, offset) => larger[start + offset] === word));
  if (contiguous) return 0.78 + 0.2 * (smaller.join(' ').length / larger.join(' ').length);
  const leftSet = new Set(leftWords);
  const rightSet = new Set(rightWords);
  const intersection = [...leftSet].filter(token => rightSet.has(token)).length;
  const union = leftSet.size + rightSet.size - intersection;
  const jaccard = union ? intersection / union : 0;
  const maxLength = Math.max(left.length, right.length);
  const lev = maxLength ? 1 - levenshtein(left, right) / maxLength : 0;
  return 0.58 * jaccard + 0.42 * lev;
}

function moduleBoost(runtimeModule, id) {
  const value = String(id ?? '').toUpperCase();
  const patterns = {
    modeling: ['MODEL', 'FEATURE', 'ANALYSIS'], sketch: ['SKETCH'], assembly: ['ASSEMB', 'ASSY'],
    drafting: ['DRAFT'], pmi: ['PMI'], surface: ['SURFACE', 'STUDIO'], sheet_metal: ['SHEET', 'SBSM'],
    manufacturing: ['CAM', 'MANUFACTUR', 'CMM', 'ADDITIVE'], simulation: ['SIM', 'MOTION', 'MCD'],
    routing: ['ROUTE', 'ROUTING', 'HARNESS'], mold: ['MOLD', 'DIE'], reuse: ['REUSE', 'TEMPLATE', 'TEAMCENTER'],
    inspect_view: ['INFO', 'VIEW', 'DISPLAY', 'ANALYSIS', 'FILE']
  };
  return (patterns[runtimeModule] ?? []).some(pattern => value.includes(pattern)) ? 0.035 : 0;
}

function resolveIntent(intent, catalog) {
  if (!catalog.items.length) return { status: 'unresolved', reason: 'NX command catalog is empty', candidates: [] };
  const queries = [intent.name_en, intent.name_ru].filter(Boolean);
  const scored = [];
  for (const item of catalog.items) {
    let score = 0;
    for (const query of queries) {
      for (const candidate of [item.id, ...item.labels, ...item.synonyms]) score = Math.max(score, similarity(query, candidate));
    }
    score += moduleBoost(intent.runtime_module, item.id);
    if (score > 0.25) scored.push({ item, score: Math.min(score, 1) });
  }
  scored.sort((a, b) => b.score - a.score || a.item.id.localeCompare(b.item.id));
  if (!scored.length || scored[0].score < 0.62) return {
    status: 'unresolved', reason: 'no sufficiently strong BUTTON ID match',
    candidates: scored.slice(0, 5).map(row => ({ id: row.item.id, label: row.item.labels[0] ?? '', score: row.score }))
  };
  if (scored[1] && scored[0].score - scored[1].score < 0.08) return {
    status: 'ambiguous', reason: 'multiple BUTTON IDs have similar labels',
    candidates: scored.slice(0, 5).map(row => ({ id: row.item.id, label: row.item.labels[0] ?? '', score: row.score }))
  };
  return {
    status: 'resolved', id: scored[0].item.id, label: scored[0].item.labels[0] ?? intent.name_en,
    score: scored[0].score,
    candidates: scored.slice(0, 3).map(row => ({ id: row.item.id, label: row.item.labels[0] ?? '', score: row.score }))
  };
}

function parseKnownPaths(source) {
  const result = new Map();
  const expression = /Add\(\s*"([^"]+)"\s*,\s*"([^"]+)"/g;
  for (const match of source.matchAll(expression)) result.set(match[1].toUpperCase(), normalizePath(match[2]));
  return result;
}

function inferIcon(intent) {
  const value = `${intent.name_en} ${intent.source_module} ${intent.group}`.toUpperCase();
  if (value.includes('WAVE')) return 'wave';
  if (value.includes('LAYER')) return 'layer';
  if (value.includes('MATERIAL')) return 'material';
  if (value.includes('SHEET') || value.includes('FLANGE') || value.includes('BEND')) return 'sheet_metal';
  if (value.includes('ASSEMB') || value.includes('COMPONENT') || value.includes('FIXTURE')) return 'assembly';
  if (value.includes('SKETCH') || /\b(LINE|RECTANGLE|CIRCLE|ARC)\b/.test(value)) return 'sketch';
  if (value.includes('SELECT')) return 'selection';
  if (/\b(VIEW|DISPLAY|SHOW|HIDE|RENDER)\b/.test(value)) return 'view';
  if (/\b(MEASURE|INFO|ANALYSIS|CHECK|INSPECT)\b/.test(value)) return 'inspect';
  if (/\b(MIRROR|PATTERN|ARRAY)\b/.test(value)) return 'pattern';
  if (/\b(EXTRUDE|REVOLVE|HOLE|BLEND|CHAMFER|FEATURE)\b/.test(value)) return 'feature';
  return 'command';
}

function selectionType(intent, id = '') {
  const value = `${id} ${intent.name_en} ${intent.group}`.toUpperCase();
  if (value.includes('DESELECT') || value.includes('CLEAR SELECTION')) return 'none';
  if (value.includes('SELECT ALL')) return 'all';
  if (value.includes('COMPONENT')) return 'component';
  if (value.includes('BODY')) return 'body';
  if (value.includes('FACE')) return 'face';
  if (value.includes('EDGE')) return 'edge';
  if (value.includes('CURVE') || value.includes('LINE') || value.includes('ARC')) return 'curve';
  if (value.includes('DATUM')) return 'datum';
  if (value.includes('FEATURE')) return 'feature';
  if (value.includes('OPERATION') || value.includes('TOOL PATH')) return 'operation';
  return 'all';
}

function requiresSelection(intent) {
  const value = intent.name_en.toUpperCase();
  if (/^(CREATE|NEW|ADD|OPEN|IMPORT|EXPORT|SHOW|VIEW|MEASURE|GENERATE|UPDATE|LOAD|INITIALIZE|ASSIGN|PLACE)/.test(value)) return false;
  return /\b(EDIT|DELETE|REMOVE|MOVE|COPY|MIRROR|PATTERN|TRIM|EXTEND|REPLACE|SUPPRESS|UNSUPPRESS|RENAME|SCALE|ALIGN|OFFSET|SEW|UNSEW|CONVERT|RECOGNIZE)\b/.test(value);
}

function isDestructive(intent) {
  return /\b(DELETE|REMOVE|CLEAR|SUPPRESS|UNSEW|BREAK|DISCONNECT|CANCEL CHECK OUT|REMOVE PARAMETERS)\b/i.test(intent.name_en);
}

function candidateLetters(name) {
  const tokens = englishTokens(name).filter(token => !STOP_WORDS.has(token));
  const result = [];
  for (const token of [...tokens].reverse()) {
    for (const character of token) if (!result.includes(character)) result.push(character);
  }
  for (const character of 'AEIOURNLDPGBCFHJKMQSTVWXYZ1234567890') if (!result.includes(character)) result.push(character);
  return result;
}

function conflicts(candidate, used) {
  const key = pathKey(candidate);
  if (!key) return true;
  return [...used].some(existing => key.startsWith(existing) || existing.startsWith(key));
}

function reservePath(preferred, name, used, frequency = '') {
  const requested = normalizePath(preferred);
  const targetLength = targetLengthForFrequency(frequency);
  if (requested.length >= 2 && requested.length <= targetLength && !conflicts(requested, used)) { used.add(pathKey(requested)); return requested; }
  const root = requested[0] || 'M';
  const object = requested[1] || 'Z';
  const letters = candidateLetters(name);
  const rootAlternatives = [root, 'C', 'E', 'T', 'X', 'P', 'I', 'V', 'A', 'M', 'F', 'U', 'H']
    .filter(value => value !== 'S' && value !== 'G')
    .filter((value, index, array) => array.indexOf(value) === index);
  const objectAlternatives = [
    object, 'Z', 'F', 'G', 'B', 'C', 'D', 'E', 'H', 'K', 'L', 'M',
    'N', 'O', 'P', 'R', 'S', 'T', 'U', 'V', 'W', 'Y'
  ].filter((value, index, array) => array.indexOf(value) === index);
  if (targetLength <= 2) {
    for (const alternativeRoot of rootAlternatives) {
      for (const leaf of letters) {
        const candidate = [alternativeRoot, leaf];
        if (!conflicts(candidate, used)) { used.add(pathKey(candidate)); return candidate; }
      }
    }
  }
  if (targetLength <= 3) {
    for (const alternativeRoot of rootAlternatives) {
      for (const alternativeObject of objectAlternatives) {
        for (const leaf of letters) {
          const candidate = [alternativeRoot, alternativeObject, leaf];
          if (!conflicts(candidate, used)) { used.add(pathKey(candidate)); return candidate; }
        }
      }
    }
  }
  for (const alternativeRoot of rootAlternatives) {
    for (const alternativeObject of objectAlternatives) {
      for (const first of letters) {
        for (const second of letters) {
          if (first === second) continue;
          const candidate = [alternativeRoot, alternativeObject, first, second];
          if (!conflicts(candidate, used)) { used.add(pathKey(candidate)); return candidate; }
        }
      }
    }
  }
  if (targetLength <= 4) throw new Error(`Unable to allocate a <=${targetLength}-token prefix-free path for ${name}`);
  for (const first of letters) {
    for (const second of letters) {
      for (const third of letters) {
        if (new Set([first, second, third]).size < 2) continue;
        const candidate = [root, object, first, second, third];
        if (!conflicts(candidate, used)) { used.add(pathKey(candidate)); return candidate; }
      }
    }
  }
  for (let index = 1; index <= 999; index += 1) {
    const candidate = [root, 'Z', ...String(index).split('')].slice(0, 5);
    if (!conflicts(candidate, used)) { used.add(pathKey(candidate)); return candidate; }
  }
  throw new Error(`Unable to allocate a prefix-free path for ${name}`);
}

function buildPathLabels(tokens, name) {
  return tokens.map((token, index) => {
    if (index === 0) return ROOT_LABELS[token] ?? token;
    if (index === 1) return OBJECT_LABELS[token] ?? token;
    return index === tokens.length - 1 ? name : token;
  });
}

function addUnique(list, values) {
  const output = Array.isArray(list) ? list : [];
  for (const value of values) {
    if (!value) continue;
    if (!output.some(item => normalizeText(item) === normalizeText(value))) output.push(value);
  }
  return output;
}

function highestFrequency(left, right) {
  const rank = value => ({ K5: 5, K4: 4, K3: 3, K2: 2, K1: 1 }[String(value ?? '').toUpperCase()] ?? 0);
  return rank(right) > rank(left) ? right : left;
}

function allModuleCommands(module) {
  return (module.command_sets ?? []).flatMap(set => (set.commands ?? []).map(command => ({ set, command })));
}

function findExisting(module, intent, resolution) {
  const rows = allModuleCommands(module).filter(row => !isSupportCommand(row.command));
  if (resolution.id) {
    const byId = rows.find(row => String(row.command.command?.id ?? '').toUpperCase() === resolution.id.toUpperCase());
    if (byId) return byId;
  }
  const name = normalizeText(intent.name_en);
  return rows.find(row => normalizeText(row.command.command?.name) === name) ?? null;
}

const intents = loadIntents(intentsDir);
if (intents.length !== 1169) throw new Error(`Expected 1169 source intents, got ${intents.length}`);
const profile = clone(readJson(profilePath));
const catalog = loadNxCatalog(catalogDir, probePath);
addProfileCommandsToCatalog(profile, catalog);
const knownPaths = parseKnownPaths(readText(path.join(repoRoot, 'NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs')));
const modules = (profile.modules ?? []).filter(module => module && module.enabled !== false);
ensureUniversalSupport(modules);
const modulesById = new Map(modules.map(module => [module.id, module]));
const globalTargets = modules.filter(module => module.id !== 'selection_object').map(module => module.id);
const reportRows = [];
const sectionSets = new Map();
let mergedCount = 0;
let addedCount = 0;

for (const intent of intents) {
  const resolution = resolveIntent(intent, catalog);
  const targets = intent.runtime_module === 'global' && duplicateGlobal ? globalTargets : [intent.runtime_module];
  for (const target of targets) {
    const module = modulesById.get(target);
    if (!module) {
      reportRows.push({ intent, target, resolution: { status: 'unresolved', reason: `target module ${target} is absent`, candidates: [] }, result: 'skipped' });
      continue;
    }
    const existing = findExisting(module, intent, resolution);
    if (existing) {
      const command = existing.command;
      command.search_aliases = addUnique(command.search_aliases, [intent.name_en, intent.name_ru, intent.source_module, intent.group]);
      command.catalog_refs = addUnique(command.catalog_refs, [intent.intent_id]);
      command.frequency = highestFrequency(command.frequency, intent.frequency);
      if (!command.path?.length) command.path = knownPaths.get(String(command.command?.id ?? '').toUpperCase()) ?? intent.path_hint;
      mergedCount += 1;
      reportRows.push({ intent, target, resolution: { status: 'existing', id: command.command?.id, score: 1 }, result: 'merged' });
      continue;
    }

    const setKey = `${target}|${intent.source_index}`;
    let set = sectionSets.get(setKey);
    if (!set) {
      set = {
        id: `catalog_${String(intent.source_index).padStart(2, '0')}`,
        label: intent.source_module,
        commands: []
      };
      module.command_sets ??= [];
      module.command_sets.push(set);
      sectionSets.set(setKey, set);
    }
    const resolved = resolution.status === 'resolved';
    const id = resolved ? resolution.id : '';
    const destructive = isDestructive(intent);
    const command = {
      slot: '', submenu_key: '', submenu_label: intent.group, input_key: '',
      path: intent.path_hint,
      path_labels: buildPathLabels(intent.path_hint, intent.name_en),
      aliases: [],
      search_aliases: [intent.name_en, intent.name_ru, intent.source_module, intent.group].filter(Boolean),
      icon_hint: inferIcon(intent),
      display_order: 1000 + intent.source_index * 100 + set.commands.length + 1,
      command: { id, name: resolved && resolution.label ? resolution.label : intent.name_en, aliases: [intent.name_en, intent.name_ru].filter(Boolean) },
      action: id.startsWith('UG_SEL_') ? 'set_selection_filter' : 'execute_command',
      target_module_id: '',
      support_kind: '',
      selection_type: selectionType(intent, id),
      enabled: resolved,
      requires_selection: requiresSelection(intent),
      destructive,
      confirm_before_execute: destructive,
      fallback: `catalog:${intent.intent_id}`,
      notes: `${intent.frequency} | ${intent.source_module} → ${intent.group} | ${resolution.status}: ${resolution.reason ?? id}`,
      catalog_refs: [intent.intent_id],
      frequency: intent.frequency,
      resolution_status: resolution.status,
      resolution_candidates: resolution.candidates
    };
    set.commands.push(command);
    addedCount += 1;
    reportRows.push({ intent, target, resolution, result: 'added' });
  }
}

// Allocate canonical paths for every command, preserving exact known BUTTON ID paths first.
for (const module of modules) {
  const rows = allModuleCommands(module);
  for (const row of rows) {
    const command = row.command;
    command.command ??= { id: '', name: '' };
    const id = String(command.command.id ?? '').toUpperCase();
    const legacy = command.submenu_key ? [command.submenu_key, command.input_key] : [command.input_key];
    command.__preferred = command.path?.length ? command.path : knownPaths.get(id) ?? legacy;
    command.__priority = isSupportCommand(command) ? -1 : (knownPaths.has(id) ? 0 : (String(command.fallback ?? '').startsWith('catalog:') ? 2 : 1));
  }
  rows.sort((left, right) => left.command.__priority - right.command.__priority ||
    (left.command.display_order ?? 999999) - (right.command.display_order ?? 999999) ||
    String(left.command.command?.id ?? left.command.command?.name).localeCompare(String(right.command.command?.id ?? right.command.command?.name)));
  const used = new Set();
  for (const { command } of rows) {
    command.path = reservePath(command.__preferred, command.command?.name ?? command.fallback, used, command.frequency);
    command.path_labels = buildPathLabels(command.path, command.command?.name ?? 'Command');
    delete command.__preferred;
    delete command.__priority;
  }
  // Keep only aliases that do not collide with any canonical path or another accepted alias.
  const accepted = new Set(used);
  for (const { command } of rows) {
    const candidates = [];
    for (const alias of command.aliases ?? []) candidates.push(normalizePath(alias));
    if (command.input_key) candidates.push(normalizePath([command.input_key]));
    if (command.submenu_key && command.input_key) candidates.push(normalizePath([command.submenu_key, command.input_key]));
    const clean = [];
    for (const alias of candidates) {
      const key = pathKey(alias);
      if (!key || key === pathKey(command.path) || conflicts(alias, accepted)) continue;
      accepted.add(key);
      clean.push(alias);
    }
    command.aliases = clean;
  }
}

profile.schema_version = 6;
profile.profile ??= {};
profile.profile.name = `${profile.profile.name ?? 'NXKeys'} — Full 1169 Command Map`;
profile.profile.description = `Generated from the complete NX 2512 hierarchy: 1169 command intents. ${catalog.items.length} NX BUTTON IDs were available for resolution.`;
profile.full_command_catalog = {
  schema_version: 2,
  source_intents: intents.length,
  generated_utc: new Date().toISOString(),
  catalog_items: catalog.items.length,
  global_commands_duplicated: duplicateGlobal,
  source_files: fs.readdirSync(intentsDir).filter(name => name.startsWith('nx2512-full-')).sort(),
  sequence_policy_version: SEQUENCE_POLICY_VERSION,
  ...supportMetadata(modules)
};
profile.leader_key ??= {};
profile.leader_key.adaptive_module_mode = true;

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, `${JSON.stringify(profile, null, 2)}\n`, 'utf8');

const statusCounts = reportRows.reduce((map, row) => {
  const key = row.resolution.status;
  map[key] = (map[key] ?? 0) + 1;
  return map;
}, {});
const unresolvedRows = reportRows.filter(row => ['unresolved', 'ambiguous'].includes(row.resolution.status));
const enabledCommands = modules.reduce((sum, module) => sum + allModuleCommands(module).filter(row => row.command.enabled !== false).length, 0);
const totalCommands = modules.reduce((sum, module) => sum + allModuleCommands(module).length, 0);
const markdown = [
  '# NX 2512 full command-map resolution', '',
  `- Source intents: **${intents.length}**`,
  `- NX catalog entries available: **${catalog.items.length}**`,
  `- Existing commands enriched: **${mergedCount}**`,
  `- Catalog commands added: **${addedCount}**`,
  `- Generated module rows: **${totalCommands}**`,
  `- Enabled executable rows: **${enabledCommands}**`,
  `- Resolved matches: **${statusCounts.resolved ?? 0}**`,
  `- Existing exact commands: **${statusCounts.existing ?? 0}**`,
  `- Ambiguous matches: **${statusCounts.ambiguous ?? 0}**`,
  `- Unresolved matches: **${statusCounts.unresolved ?? 0}**`, '',
  '> Unresolved commands remain in the generated profile with their mnemonic path, but are disabled. This prevents NXKeys from sending fabricated BUTTON IDs. Generate the Siemens NX catalog and rerun the compiler to enable them.', '',
  '## Unresolved and ambiguous commands', '',
  '| Intent | Target module | Status | Best candidates |',
  '|---|---|---|---|',
  ...unresolvedRows.map(row => {
    const candidates = (row.resolution.candidates ?? []).slice(0, 3)
      .map(candidate => `${candidate.id} (${Number(candidate.score ?? 0).toFixed(2)})`).join('<br>');
    return `| ${row.intent.name_en.replaceAll('|', '\\|')} | ${row.target} | ${row.resolution.status} | ${candidates || row.resolution.reason || '—'} |`;
  }), '',
  '## Invocation-language policy', '',
  '- Canonical paths are prefix-free within each active NX module.',
  '- Paths contain 2–5 alphanumeric tokens and use the action → object → command pattern.',
  '- Existing curated BUTTON ID paths have priority over generated paths.',
  '- Short aliases are retained only when they cannot shadow a command or submenu.',
  '- Global command intents can be duplicated into every active module; use `--no-global-duplication` to keep them only in `inspect_view`-style scopes.', ''
].join('\n');
fs.mkdirSync(path.dirname(reportPath), { recursive: true });
fs.writeFileSync(reportPath, markdown, 'utf8');

console.log(`[full-command-map] Source intents: ${intents.length}`);
console.log(`[full-command-map] NX catalog entries: ${catalog.items.length}`);
console.log(`[full-command-map] Output commands: ${totalCommands}; enabled: ${enabledCommands}`);
console.log(`[full-command-map] Existing/merged: ${mergedCount}; added: ${addedCount}`);
console.log(`[full-command-map] Resolved: ${statusCounts.resolved ?? 0}; ambiguous: ${statusCounts.ambiguous ?? 0}; unresolved: ${statusCounts.unresolved ?? 0}`);
console.log(`[full-command-map] Profile: ${outputPath}`);
console.log(`[full-command-map] Report: ${reportPath}`);
