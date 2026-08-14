import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';
import {
  SEQUENCE_POLICY_VERSION,
  ensureUniversalSupport,
  isModuleSwitchSupportCommand,
  isSelectionSupportCommand,
  supportMetadata
} from './sequence-policy.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const argv = process.argv.slice(2);
const valueOf = (name, fallback = '') => {
  const index = argv.indexOf(name);
  return index >= 0 && index + 1 < argv.length ? argv[index + 1] : fallback;
};
const has = name => argv.includes(name);
const absolute = value => path.isAbsolute(value) ? value : path.resolve(root, value);
const defaultProfile = fs.existsSync(path.resolve(root, 'config/nx2512-v8-profile.json')) ? 'config/nx2512-v8-profile.json' : 'config/nx2512-pro-hybrid.json';
const profilePath = absolute(valueOf('--profile', defaultProfile));
const intentsDir = absolute(valueOf('--intents', 'config/full-command-map'));
const catalogDir = valueOf('--catalog-dir', '');
const probe = valueOf('--probe', 'docs/audit/runtime-command-probe-2026-07-28.json');
const output = absolute(valueOf('--out', 'config/nx2512-pro-main.generated.json'));
const report = absolute(valueOf('--report', 'docs/generated/main-profile-resolution.md'));
const allLevels = ['K1', 'K2', 'K3', 'K4', 'K5'];
if (argv.includes('--all-frequencies') || argv.includes('--frequencies')) {
  throw new Error('NXKeys has one installable preset: K3,K4,K5. Do not pass --all-frequencies or --frequencies.');
}
const selected = ['K3', 'K4', 'K5'];

const readText = file => fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '');
const normalizePath = value => (Array.isArray(value) ? value : String(value ?? '').split(/[\s>\-/]+/))
  .map(token => String(token).trim().match(/[A-Za-z0-9]/)?.[0]?.toUpperCase() ?? '').filter(Boolean).slice(0, 5);
const keyOf = value => normalizePath(value).join('');
const conflicts = (a, b) => a.startsWith(b) || b.startsWith(a);
const unescapeField = value => String(value ?? '').replace(/\\t/g, '\t').replace(/\\n/g, '\n').replace(/\\\\/g, '\\');

function loadIntents(directory) {
  const names = fs.readdirSync(directory).sort((a, b) => a.localeCompare(b));
  const tsv = names.filter(name => /^nx2512-full-.*\.tsv$/i.test(name));
  let fields = [];
  let rows = [];
  if (tsv.length) {
    for (const file of tsv) {
      const lines = readText(path.join(directory, file)).split(/\r?\n/).filter(line => line && !line.startsWith('#'));
      const current = lines.shift()?.split('\t') ?? [];
      if (!fields.length) fields = current;
      rows.push(...lines.map(line => line.split('\t').map(unescapeField)));
    }
  } else {
    const single = names.find(name => /^nx2512-full-command-map\.json\.gz\.b64$/i.test(name));
    const parts = names.filter(name => /^nx2512-full-command-map\.json\.gz\.b64\.part\d+$/i.test(name));
    if (!single && !parts.length) throw new Error(`Command-map payload not found in ${directory}`);
    const encoded = single ? readText(path.join(directory, single)).trim() : parts.map(name => readText(path.join(directory, name)).trim()).join('');
    const payload = JSON.parse(gunzipSync(Buffer.from(encoded, 'base64')).toString('utf8'));
    fields = payload.fields ?? [];
    rows = payload.rows ?? [];
    if (payload.count !== rows.length) throw new Error(`Catalog count mismatch: ${payload.count} != ${rows.length}`);
  }
  const index = Object.fromEntries(fields.map((field, position) => [field, position]));
  return rows.map(row => ({
    intent_id: row[index.intent_id],
    frequency: row[index.frequency],
    name_en: row[index.name_en],
    runtime_module: row[index.runtime_module]
  }));
}

function allCommands(profile) {
  return (profile.modules ?? []).flatMap(module => (module.command_sets ?? []).flatMap(set =>
    (set.commands ?? []).map(command => ({ module, set, command }))));
}

function isRuntimeSupport(command) {
  return isSelectionSupportCommand(command) || isModuleSwitchSupportCommand(command);
}

const intents = loadIntents(intentsDir);
if (intents.length !== 1169) throw new Error(`Expected 1169 source intents, got ${intents.length}.`);
const counts = Object.fromEntries(allLevels.map(level => [level, intents.filter(intent => intent.frequency === level).length]));
const selectedSet = new Set(selected);
const selectedIntents = intents.filter(intent => selectedSet.has(intent.frequency));
const selectedIds = new Set(selectedIntents.map(intent => intent.intent_id));
const frequencyById = new Map(selectedIntents.map(intent => [intent.intent_id, intent.frequency]));

const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'nxkeys-main-profile-'));
try {
  const fullOutput = path.join(temp, 'full-profile.json');
  const fullReport = path.join(temp, 'full-report.md');
  const args = [sourceCompiler, '--profile', profilePath, '--intents', intentsDir, '--out', fullOutput, '--report', fullReport];
  if (catalogDir) args.push('--catalog-dir', absolute(catalogDir));
  if (probe) args.push('--probe', absolute(probe));
  if (has('--no-global-duplication')) args.push('--no-global-duplication');
  const result = spawnSync(process.execPath, args, { cwd: root, encoding: 'utf8' });
  if (result.status !== 0) throw new Error(`Base compiler failed:\n${result.stdout}\n${result.stderr}`);

  const profile = JSON.parse(readText(fullOutput));
  const modules = (profile.modules ?? []).filter(module => module && module.enabled !== false);
  ensureUniversalSupport(modules);
  for (const module of profile.modules ?? []) {
    for (const set of module.command_sets ?? []) {
      set.commands = (set.commands ?? []).filter(command => {
        const originalRefs = Array.isArray(command.catalog_refs) ? command.catalog_refs : [];
        const fallback = String(command.fallback ?? '');
        const fallbackRef = fallback.startsWith('catalog:') ? fallback.slice('catalog:'.length) : '';
        const refs = originalRefs.filter(ref => selectedIds.has(ref));
        if (fallbackRef && selectedIds.has(fallbackRef) && !refs.includes(fallbackRef)) refs.push(fallbackRef);
        const runtimeSupport = isRuntimeSupport(command);
        const curatedSketchCore = module.id === 'sketch' &&
          String(command.path_source ?? '').toLowerCase() === 'sketch_curated';

        if (refs.length) {
          command.catalog_refs = refs;
          if (runtimeSupport) {
            command.catalog_backed_support = true;
            command.profile_support = true;
            command.frequency = 'support';
            command.notes = ['Catalog-backed runtime support', command.notes].filter(Boolean).join(' | ');
          } else {
            command.frequency = refs.map(ref => frequencyById.get(ref)).filter(Boolean)
              .sort((a, b) => Number(b.slice(1)) - Number(a.slice(1)))[0] ?? command.frequency;
            delete command.profile_support;
            delete command.catalog_backed_support;
          }
          return true;
        }

        // Verified core Sketch commands are runtime vocabulary, not optional catalog noise.
        // Preserve them even when their source intent is K1/K2 or absent from the selected K3-K5 slice.
        if (curatedSketchCore) {
          command.catalog_refs = [];
          command.profile_support = true;
          command.frequency = 'support';
          command.resolution_status = 'existing';
          command.resolution_candidates = [];
          command.support_kind = 'sketch_core';
          command.notes = ['Verified Sketch core outside frequency filtering', command.notes].filter(Boolean).join(' | ');
          if (fallback.startsWith('catalog:')) delete command.fallback;
          delete command.catalog_backed_support;
          return true;
        }

        // Selection filters and module switches are runtime infrastructure rather than catalog coverage.
        if (runtimeSupport) {
          command.catalog_refs = [];
          command.profile_support = true;
          command.frequency = 'support';
          command.resolution_status = 'existing';
          command.resolution_candidates = [];
          delete command.catalog_backed_support;
          if (fallback.startsWith('catalog:')) delete command.fallback;
          command.notes = ['Runtime support infrastructure', command.notes].filter(Boolean).join(' | ');
          return true;
        }
        return false;
      });
    }
    module.command_sets = (module.command_sets ?? []).filter(set => (set.commands ?? []).length > 0);
  }

  profile.schema_version = 6;
  profile.profile ??= {};
  const isMain = selected.length === 3 && ['K3', 'K4', 'K5'].every(level => selected.includes(level));
  profile.profile.name = isMain ? 'NXKeys NX 2512 — Main K3–K5 Profile' : `NXKeys NX 2512 — ${selected.join(' + ')} Profile`;
  profile.profile.description = `Operational profile for ${selected.join(', ')}: ${selectedIntents.length} of 1169 NX 2512 command intents.`;
  const support = supportMetadata(profile.modules ?? []);
  const supportCommands = support.support_commands;
  profile.full_command_catalog = {
    schema_version: 2,
    source_intents: intents.length,
    selected_intents: selectedIntents.length,
    selected_frequencies: selected,
    frequency_counts: counts,
    support_commands: supportCommands,
    generated_utc: new Date().toISOString(),
    catalog_items: profile.full_command_catalog?.catalog_items ?? 0,
    global_commands_duplicated: profile.full_command_catalog?.global_commands_duplicated !== false,
    source_files: profile.full_command_catalog?.source_files ?? [],
    sequence_policy_version: SEQUENCE_POLICY_VERSION,
    selection_filter_support_commands: support.selection_filter_support_commands,
    module_switch_support_commands: support.module_switch_support_commands
  };

  const rows = allCommands(profile);
  const refs = new Set(rows.flatMap(row => row.command.catalog_refs ?? []));
  for (const intent of selectedIntents) if (!refs.has(intent.intent_id)) throw new Error(`Generated profile lost ${intent.intent_id}.`);
  for (const ref of refs) if (!selectedIds.has(ref)) throw new Error(`Generated profile retained out-of-scope intent ${ref}.`);

  for (const module of (profile.modules ?? []).filter(item => item && item.enabled !== false)) {
    const paths = [];
    const moduleRows = allCommands({ modules: [module] });
    if (!moduleRows.length) throw new Error(`Enabled module has no commands: ${module.id}`);
    for (const { command } of moduleRows) {
      const canonical = keyOf(command.path);
      if (!canonical) throw new Error(`Empty path: ${module.id}/${command.command?.name}`);
      paths.push({ key: canonical, name: command.command?.name, kind: 'path' });
      for (const alias of command.aliases ?? []) {
        const key = keyOf(alias);
        if (key) paths.push({ key, name: command.command?.name, kind: 'alias' });
      }
      if (command.enabled !== false && !command.command?.id) throw new Error(`Enabled command has no BUTTON ID: ${module.id}/${command.command?.name}`);
    }
    paths.sort((a, b) => a.key.length - b.key.length || a.key.localeCompare(b.key));
    for (let left = 0; left < paths.length; left += 1)
      for (let right = left + 1; right < paths.length; right += 1)
        if (conflicts(paths[left].key, paths[right].key))
          throw new Error(`Path conflict in ${module.id}: ${paths[left].key} and ${paths[right].key}.`);
  }

  fs.mkdirSync(path.dirname(output), { recursive: true });
  fs.writeFileSync(output, `${JSON.stringify(profile, null, 2)}\n`, 'utf8');

  const statusCounts = rows.reduce((map, row) => {
    const status = row.command.resolution_status ?? (row.command.enabled === false ? 'unresolved' : 'existing');
    map[status] = (map[status] ?? 0) + 1;
    return map;
  }, {});
  const unresolved = rows.filter(row => row.command.enabled === false);
  const markdown = [
    '# NX 2512 main profile resolution', '',
    `- Source intents: **${intents.length}**`,
    `- Selected frequencies: **${selected.join(', ')}**`,
    `- Selected unique intents: **${selectedIntents.length}**`,
    `- Runtime support commands: **${supportCommands}**`,
    `- Generated module rows: **${rows.length}**`,
    `- Enabled rows: **${rows.filter(row => row.command.enabled !== false).length}**`,
    `- Existing rows: **${statusCounts.existing ?? 0}**`,
    `- Resolved rows: **${statusCounts.resolved ?? 0}**`,
    `- Ambiguous rows: **${statusCounts.ambiguous ?? 0}**`,
    `- Unresolved rows: **${statusCounts.unresolved ?? 0}**`, '',
    '> Disabled ambiguous/unresolved rows keep their mnemonic path but cannot dispatch a fabricated BUTTON ID.', '',
    '## Disabled commands', '',
    '| Command | Module | Frequency | Status | Candidates |',
    '|---|---|---|---|---|',
    ...unresolved.map(({ module, command }) => {
      const candidates = (command.resolution_candidates ?? []).slice(0, 3)
        .map(candidate => `${candidate.id} (${Number(candidate.score ?? 0).toFixed(2)})`).join('<br>');
      return `| ${String(command.command?.name ?? '').replaceAll('|', '\\|')} | ${module.id} | ${command.frequency ?? '—'} | ${command.resolution_status ?? 'unresolved'} | ${candidates || '—'} |`;
    }), ''
  ].join('\n');
  fs.mkdirSync(path.dirname(report), { recursive: true });
  fs.writeFileSync(report, markdown, 'utf8');

  console.log(`[main-command-map] Source: ${intents.length}; selected ${selected.join(',')}: ${selectedIntents.length}.`);
  console.log(`[main-command-map] Rows: ${rows.length}; enabled: ${rows.filter(row => row.command.enabled !== false).length}; support: ${supportCommands}.`);
  console.log(`[main-command-map] Profile: ${output}`);
  console.log(`[main-command-map] Report: ${report}`);
} finally {
  fs.rmSync(temp, { recursive: true, force: true });
}
