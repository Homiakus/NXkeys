import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';
import {
  CANONICAL_SELECTION_FILTERS,
  MODULE_SWITCH_PATHS,
  SEQUENCE_POLICY_VERSION,
  SWITCHABLE_MODULE_IDS,
  commandRows,
  isModuleSwitchSupportCommand,
  isSelectionSupportCommand,
  normalizePath,
  pathKey,
  pathsConflict,
  targetLengthForCommand
} from './sequence-policy.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const outJson = path.join(root, 'docs', 'audit', 'command-sequence-audit.json');
const outMd = path.join(root, 'docs', 'audit', 'command-sequence-audit.md');
let failed = false;
const fail = message => { failed = true; console.error(`[command-sequence-audit] ERROR: ${message}`); };
const readText = relative => fs.readFileSync(path.join(root, relative), 'utf8').replace(/^\uFEFF/, '');
const readJson = relative => JSON.parse(readText(relative));

function loadIntents() {
  const directory = path.join(root, 'config', 'full-command-map');
  const names = fs.readdirSync(directory).sort((a, b) => a.localeCompare(b));
  const single = names.find(name => /^nx2512-full-command-map\.json\.gz\.b64$/i.test(name));
  const parts = names.filter(name => /^nx2512-full-command-map\.json\.gz\.b64\.part\d+$/i.test(name));
  if (!single && !parts.length) throw new Error(`No full-command-map payload found in ${directory}`);
  const encoded = single
    ? fs.readFileSync(path.join(directory, single), 'utf8').trim()
    : parts.map(name => fs.readFileSync(path.join(directory, name), 'utf8').trim()).join('');
  const payload = JSON.parse(gunzipSync(Buffer.from(encoded, 'base64')).toString('utf8'));
  const index = Object.fromEntries((payload.fields ?? []).map((field, position) => [field, position]));
  return (payload.rows ?? []).map(row => ({
    intent_id: row[index.intent_id],
    source_index: Number(row[index.source_index]),
    runtime_module: row[index.runtime_module],
    frequency: row[index.frequency],
    source_module: row[index.source_module],
    group: row[index.group],
    name_en: row[index.name_en],
    name_ru: row[index.name_ru],
    source_path: normalizePath(row[index.path])
  }));
}

function sequenceRows(profile) {
  return (profile.modules ?? []).filter(module => module && module.enabled !== false).flatMap(module =>
    commandRows(module).flatMap(({ set, command }) => {
      const canonical = {
        module_id: module.id,
        module_label: module.label,
        set_id: set.id,
        kind: 'path',
        sequence: pathKey(command.path),
        tokens: normalizePath(command.path),
        command_id: command.command?.id ?? '',
        command_name: command.command?.name ?? '',
        action: command.action || 'execute_command',
        target_module_id: command.target_module_id ?? '',
        support_kind: command.support_kind ?? '',
        frequency: command.frequency ?? '',
        enabled: command.enabled !== false,
        resolution_status: command.resolution_status ?? '',
        catalog_refs: command.catalog_refs ?? [],
        target_length: targetLengthForCommand(module.id, command)
      };
      return [
        canonical,
        ...(command.aliases ?? []).map(alias => ({ ...canonical, kind: 'alias', sequence: pathKey(alias), tokens: normalizePath(alias) }))
      ].filter(row => row.sequence);
    }));
}

function pathProblems(rows) {
  const problems = [];
  const byModule = new Map();
  for (const row of rows) {
    if (row.kind !== 'path' && row.kind !== 'alias') continue;
    const list = byModule.get(row.module_id) ?? [];
    list.push(row);
    byModule.set(row.module_id, list);
  }
  for (const [moduleId, moduleRows] of byModule) {
    const ordered = moduleRows.slice().sort((a, b) => a.sequence.length - b.sequence.length || a.sequence.localeCompare(b.sequence));
    for (let left = 0; left < ordered.length; left += 1) {
      for (let right = left + 1; right < ordered.length; right += 1) {
        if (!pathsConflict(ordered[left].sequence, ordered[right].sequence)) continue;
        problems.push({
          module_id: moduleId,
          left: ordered[left],
          right: ordered[right],
          message: `${moduleId}: ${ordered[left].sequence} (${ordered[left].command_name}) conflicts with ${ordered[right].sequence} (${ordered[right].command_name})`
        });
      }
    }
  }
  return problems;
}

function selectionCoverage(profile) {
  const expected = CANONICAL_SELECTION_FILTERS.map(filter => filter.id);
  return (profile.modules ?? []).filter(module => module && module.enabled !== false).map(module => {
    const actual = new Set(commandRows(module)
      .filter(({ command }) => isSelectionSupportCommand(command))
      .map(({ command }) => String(command.command?.id ?? '').toUpperCase()));
    return {
      module_id: module.id,
      expected,
      actual: [...actual].sort(),
      missing: expected.filter(id => !actual.has(id))
    };
  });
}

function moduleSwitchCoverage(profile) {
  const modules = (profile.modules ?? []).filter(module => module && module.enabled !== false);
  const available = new Set(modules.map(module => module.id));
  return modules.map(module => {
    const expected = module.id === 'sketch' || module.id === 'selection_object'
      ? []
      : SWITCHABLE_MODULE_IDS.filter(id => id !== module.id && available.has(id));
    const actual = new Set(commandRows(module)
      .filter(({ command }) => isModuleSwitchSupportCommand(command))
      .map(({ command }) => command.target_module_id)
      .filter(Boolean));
    return {
      module_id: module.id,
      expected,
      actual: [...actual].sort(),
      missing: expected.filter(id => !actual.has(id)),
      forbidden: module.id === 'sketch' ? [...actual] : []
    };
  });
}

function lengthStats(rows) {
  const stats = {};
  for (const row of rows.filter(item => item.kind === 'path')) {
    const frequency = row.frequency || 'unknown';
    const bucket = stats[frequency] ?? { count: 0, total_length: 0, over_target: 0, by_length: {} };
    const length = row.tokens.length;
    bucket.count += 1;
    bucket.total_length += length;
    bucket.by_length[length] = (bucket.by_length[length] ?? 0) + 1;
    if (length > row.target_length) bucket.over_target += 1;
    stats[frequency] = bucket;
  }
  for (const bucket of Object.values(stats)) bucket.average_length = bucket.count ? Number((bucket.total_length / bucket.count).toFixed(2)) : 0;
  return stats;
}

function disabledSummary(profile) {
  const rows = commandRows({ command_sets: (profile.modules ?? []).flatMap(module => module.command_sets ?? []) });
  const byStatus = {};
  const disabled = [];
  for (const { command } of rows) {
    if (isSelectionSupportCommand(command) || isModuleSwitchSupportCommand(command)) continue;
    const status = command.resolution_status ?? (command.enabled === false ? 'unresolved' : 'existing');
    byStatus[status] = (byStatus[status] ?? 0) + 1;
    if (command.enabled === false) disabled.push({
      command_id: command.command?.id ?? '',
      command_name: command.command?.name ?? '',
      frequency: command.frequency ?? '',
      status,
      path: pathKey(command.path),
      catalog_refs: command.catalog_refs ?? [],
      candidates: command.resolution_candidates ?? []
    });
  }
  return { by_status: byStatus, disabled };
}

try {
  const intents = loadIntents();
  const bootstrap = readJson('config/nx2512-pro-hybrid.json');
  const runtime = readJson('config/nx2512-pro-main.generated.json');
  const probePath = path.join(root, 'docs', 'audit', 'runtime-command-probe-2026-07-28.json');
  const probe = fs.existsSync(probePath) ? JSON.parse(fs.readFileSync(probePath, 'utf8')) : null;
  const rows = sequenceRows(runtime);
  const conflicts = pathProblems(rows);
  const selections = selectionCoverage(runtime);
  const switches = moduleSwitchCoverage(runtime);
  const disabled = disabledSummary(runtime);
  const stats = lengthStats(rows);
  const missingSelection = selections.filter(row => row.missing.length);
  const missingSwitches = switches.filter(row => row.missing.length || row.forbidden.length);
  const overTarget = Object.entries(stats).filter(([, value]) => value.over_target > 0);

  for (const item of conflicts) fail(item.message);
  for (const item of missingSelection) fail(`${item.module_id} missing selection filters: ${item.missing.join(', ')}`);
  for (const item of missingSwitches) {
    if (item.missing.length) fail(`${item.module_id} missing module switches: ${item.missing.join(', ')}`);
    if (item.forbidden.length) fail(`${item.module_id} has forbidden module switches: ${item.forbidden.join(', ')}`);
  }
  for (const [frequency, value] of overTarget) fail(`${frequency} has ${value.over_target} paths over its command-specific target length`);

  const audit = {
    schema_version: 1,
    sequence_policy_version: SEQUENCE_POLICY_VERSION,
    generated_utc: new Date().toISOString(),
    source_intents: intents.length,
    selected_runtime_intents: runtime.full_command_catalog?.selected_intents ?? 0,
    bootstrap_modules: (bootstrap.modules ?? []).filter(module => module && module.enabled !== false).length,
    runtime_modules: (runtime.modules ?? []).filter(module => module && module.enabled !== false).length,
    probe_results: probe?.results?.length ?? 0,
    sequence_rows: rows,
    path_conflicts: conflicts,
    selection_coverage: selections,
    module_switch_coverage: switches,
    length_stats: stats,
    disabled_summary: disabled,
    passed: !failed
  };
  fs.mkdirSync(path.dirname(outJson), { recursive: true });
  fs.writeFileSync(outJson, `${JSON.stringify(audit, null, 2)}\n`, 'utf8');

  const markdown = [
    '# Command Sequence Audit', '',
    `- Sequence policy: **v${SEQUENCE_POLICY_VERSION}**`,
    `- Full source intents: **${intents.length}**`,
    `- Runtime selected intents: **${audit.selected_runtime_intents}**`,
    `- Runtime modules: **${audit.runtime_modules}**`,
    `- Sequence rows including aliases: **${rows.length}**`,
    `- Path conflicts: **${conflicts.length}**`,
    `- Modules missing selection filters: **${missingSelection.length}**`,
    `- Modules missing/violating switches: **${missingSwitches.length}**`,
    `- Disabled unresolved/ambiguous runtime commands: **${disabled.disabled.length}**`,
    `- Result: **${failed ? 'FAIL' : 'PASS'}**`, '',
    '## Length Stats', '',
    '| Frequency | Count | Average length | Over target | By length |',
    '|---|---:|---:|---:|---|',
    ...Object.entries(stats).sort(([a], [b]) => a.localeCompare(b)).map(([frequency, value]) =>
      `| ${frequency} | ${value.count} | ${value.average_length} | ${value.over_target} | ${Object.entries(value.by_length).map(([length, count]) => `${length}:${count}`).join(', ')} |`),
    '',
    '## Selection Coverage', '',
    '| Module | Missing |',
    '|---|---|',
    ...selections.map(row => `| ${row.module_id} | ${row.missing.join(', ') || '-'} |`),
    '',
    '## Module Switch Coverage', '',
    '| Module | Missing | Forbidden |',
    '|---|---|---|',
    ...switches.map(row => `| ${row.module_id} | ${row.missing.join(', ') || '-'} | ${row.forbidden.join(', ') || '-'} |`),
    ''
  ].join('\n');
  fs.writeFileSync(outMd, markdown, 'utf8');
  console.log(`[command-sequence-audit] JSON: ${outJson}`);
  console.log(`[command-sequence-audit] Markdown: ${outMd}`);
} catch (error) {
  fail(error?.stack || error?.message || String(error));
}

if (failed) process.exitCode = 1;
