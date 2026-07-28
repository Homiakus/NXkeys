import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const intentsDir = path.join(root, 'config', 'full-command-map');
const MAIN_FREQUENCIES = ['K3', 'K4', 'K5'];
const EXPECTED_COUNTS = { K1: 4, K2: 280, K3: 445, K4: 371, K5: 69 };
const EXPECTED_MAIN_INTENTS = 885;
let failed = false;
const fail = message => { failed = true; console.error(`[main-command-map] ERROR: ${message}`); };
const normalizeKey = value => String(value ?? '').trim().match(/[A-Za-z0-9]/)?.[0]?.toUpperCase() ?? '';
const normalizePath = value => (Array.isArray(value) ? value : String(value ?? '').split(/[\s>\-/]+/))
  .map(normalizeKey).filter(Boolean).slice(0, 5);
const keyOf = value => normalizePath(value).join('');
const isConflict = (left, right) => left.startsWith(right) || right.startsWith(left);
const unescapeField = value => String(value ?? '').replace(/\\t/g, '\t').replace(/\\n/g, '\n').replace(/\\\\/g, '\\');
const readText = relative => fs.readFileSync(path.join(root, relative), 'utf8').replace(/^\uFEFF/, '');

function loadIntents() {
  const names = fs.readdirSync(intentsDir).sort((a, b) => a.localeCompare(b));
  const tsvFiles = names.filter(name => /^nx2512-full-.*\.tsv$/i.test(name));
  let header = [];
  let rows = [];
  if (tsvFiles.length) {
    for (const file of tsvFiles) {
      const lines = fs.readFileSync(path.join(intentsDir, file), 'utf8').replace(/^\uFEFF/, '')
        .split(/\r?\n/).filter(line => line && !line.startsWith('#'));
      const currentHeader = lines.shift()?.split('\t') ?? [];
      if (!header.length) header = currentHeader;
      for (const line of lines) rows.push({ fields: line.split('\t').map(unescapeField), file });
    }
  } else {
    const single = names.find(name => /^nx2512-full-command-map\.json\.gz\.b64$/i.test(name));
    const parts = names.filter(name => /^nx2512-full-command-map\.json\.gz\.b64\.part\d+$/i.test(name));
    if (!single && !parts.length) throw new Error(`No full-command-map data files found in ${intentsDir}`);
    const encoded = single
      ? fs.readFileSync(path.join(intentsDir, single), 'utf8').trim()
      : parts.map(file => fs.readFileSync(path.join(intentsDir, file), 'utf8').trim()).join('');
    const payload = JSON.parse(gunzipSync(Buffer.from(encoded, 'base64')).toString('utf8'));
    header = payload.fields ?? [];
    rows = (payload.rows ?? []).map(fields => ({ fields, file: single ?? parts.join('+') }));
    if (payload.count !== rows.length) throw new Error(`Compressed catalog count mismatch: ${payload.count} != ${rows.length}`);
  }
  const index = Object.fromEntries(header.map((name, position) => [name, position]));
  return rows.map(({ fields, file }) => ({
    intent_id: fields[index.intent_id], source_index: Number(fields[index.source_index]),
    runtime_module: fields[index.runtime_module], frequency: fields[index.frequency],
    source_module: fields[index.source_module], group: fields[index.group],
    name_en: fields[index.name_en], name_ru: fields[index.name_ru], path: normalizePath(fields[index.path]), file
  }));
}

function compile(tempRoot, extraArgs = []) {
  const output = path.join(tempRoot, extraArgs.includes('--all-frequencies') ? 'all-profile.json' : 'main-profile.json');
  const report = path.join(tempRoot, extraArgs.includes('--all-frequencies') ? 'all-report.md' : 'main-report.md');
  const result = spawnSync(process.execPath, [
    path.join(root, 'scripts', 'compile-main-command-map.mjs'),
    '--profile', path.join(root, 'config', 'nx2512-pro-hybrid.json'),
    '--intents', intentsDir,
    '--probe', path.join(root, 'docs', 'audit', 'runtime-command-probe-2026-07-28.json'),
    '--out', output,
    '--report', report,
    ...extraArgs
  ], { cwd: root, encoding: 'utf8' });
  if (result.status !== 0) fail(`Compiler failed:\n${result.stdout}\n${result.stderr}`);
  if (!fs.existsSync(output)) fail(`Compiler did not create ${output}.`);
  if (!fs.existsSync(report)) fail(`Compiler did not create ${report}.`);
  return fs.existsSync(output) ? JSON.parse(fs.readFileSync(output, 'utf8')) : null;
}

function validateGeneratedProfile(profile, expectedIntents, expectedFrequencies, sourceIntents) {
  if (!profile) return;
  const metadata = profile.full_command_catalog ?? {};
  if (metadata.source_intents !== sourceIntents) fail(`Generated profile reports ${metadata.source_intents} source intents.`);
  if (metadata.selected_intents !== expectedIntents) fail(`Generated profile reports ${metadata.selected_intents} selected intents, expected ${expectedIntents}.`);
  const actualFrequencies = [...(metadata.selected_frequencies ?? [])].sort().join(',');
  if (actualFrequencies !== [...expectedFrequencies].sort().join(','))
    fail(`Generated profile frequency scope is ${actualFrequencies}, expected ${expectedFrequencies.join(',')}.`);

  const allowedRefs = new Set(loadIntents().filter(intent => expectedFrequencies.includes(intent.frequency)).map(intent => intent.intent_id));
  const seenRefs = new Set();
  let commandCount = 0;
  let enabledCount = 0;
  let supportCount = 0;
  const enabledModules = (profile.modules ?? []).filter(item => item && item.enabled !== false);
  for (const module of enabledModules) {
    const rows = (module.command_sets ?? []).flatMap(set => set.commands ?? []);
    if (!rows.length) fail(`Enabled module has no commands: ${module.id}.`);
    commandCount += rows.length;
    const allPaths = [];
    for (const command of rows) {
      if (command.enabled !== false) enabledCount += 1;
      const canonical = keyOf(command.path);
      if (!canonical || normalizePath(command.path).length < 2 || normalizePath(command.path).length > 5)
        fail(`Invalid canonical path in ${module.id}: ${command.command?.name}.`);
      allPaths.push({ key: canonical, kind: 'path', name: command.command?.name });
      for (const alias of command.aliases ?? []) {
        const aliasKey = keyOf(alias);
        if (aliasKey) allPaths.push({ key: aliasKey, kind: 'alias', name: command.command?.name });
      }

      const fallback = String(command.fallback ?? '');
      const refs = [...(command.catalog_refs ?? [])];
      if (fallback.startsWith('catalog:')) refs.push(fallback.slice('catalog:'.length));
      if (command.profile_support === true) {
        supportCount += 1;
        if (module.id !== 'selection_object') fail(`Support command is outside selection_object: ${module.id}/${command.command?.name}.`);
        if (command.action !== 'set_selection_filter') fail(`Support command has invalid action: ${module.id}/${command.command?.name}.`);
        if (!/^UG_SEL_/i.test(String(command.command?.id ?? ''))) fail(`Support command has invalid ID: ${module.id}/${command.command?.name}.`);
        if (refs.length) fail(`Support command must not claim catalog coverage: ${module.id}/${command.command?.name}.`);
        if (command.frequency !== 'support') fail(`Support command has invalid frequency marker: ${module.id}/${command.command?.name}.`);
      } else {
        for (const reference of refs) {
          seenRefs.add(reference);
          if (!allowedRefs.has(reference)) fail(`Profile contains intent outside selected frequency scope: ${reference}.`);
        }
        if (!expectedFrequencies.includes(command.frequency))
          fail(`Profile row has frequency outside scope: ${module.id}/${command.command?.name}/${command.frequency}.`);
        if (!refs.length) fail(`Non-support profile row has no catalog reference: ${module.id}/${command.command?.name}.`);
      }
      if (command.enabled !== false && !command.command?.id)
        fail(`Enabled command has no BUTTON ID: ${module.id}/${command.command?.name}.`);
      if (fallback.startsWith('catalog:') && !command.resolution_status)
        fail(`Catalog command lacks resolution_status: ${module.id}/${command.command?.name}.`);
    }
    allPaths.sort((a, b) => a.key.length - b.key.length || a.key.localeCompare(b.key));
    for (let left = 0; left < allPaths.length; left += 1) {
      for (let right = left + 1; right < allPaths.length; right += 1) {
        if (isConflict(allPaths[left].key, allPaths[right].key))
          fail(`Generated path conflict in ${module.id}: ${allPaths[left].kind} ${allPaths[left].key} (${allPaths[left].name}) and ${allPaths[right].kind} ${allPaths[right].key} (${allPaths[right].name}).`);
      }
    }
  }
  for (const reference of allowedRefs) if (!seenRefs.has(reference)) fail(`Generated profile lost selected intent ${reference}.`);
  if (seenRefs.size !== expectedIntents) fail(`Generated profile covers ${seenRefs.size} unique intents, expected ${expectedIntents}.`);
  if (commandCount < expectedIntents) fail(`Generated profile contains only ${commandCount} rows for ${expectedIntents} intents.`);
  if (enabledCount < 1) fail('Generated profile has no executable command rows.');
  if (supportCount < 1) fail('Generated profile has no runtime selection support commands.');
  if (metadata.support_commands !== supportCount) fail(`Metadata reports ${metadata.support_commands} support commands, actual ${supportCount}.`);
}

function validateDocumentation() {
  const required = {
    'README.md': ['K3–K5', '885', 'install-main-profile.ps1', 'nx2512-pro-main.generated.json', 'главный профиль'],
    'FULL_COMMAND_MAP.md': ['K3–K5', '885', 'K1–K2', '--all-frequencies', '06_ui_commands_buttons.csv'],
    'docs/README.md': ['K3–K5', '885', 'главный профиль'],
    'docs/CONFIGURATION.md': ['selected_frequencies', 'selected_intents', 'K3', 'K4', 'K5'],
    'docs/INSTALLATION.md': ['install-main-profile.ps1', 'nx2512-pro-main.generated.json', 'main-profile-resolution.md'],
    'docs/ARCHITECTURE.md': ['K3–K5', '885'],
    'docs/SAFETY_MODEL.md': ['K3–K5', 'unresolved'],
    'docs/TROUBLESHOOTING.md': ['main-profile-resolution.md', 'K3–K5'],
    'NX2512_ControlCenter/README.md': ['nx2512-pro-main.generated.json', 'K3–K5', '885']
  };
  for (const [relative, markers] of Object.entries(required)) {
    const content = readText(relative);
    for (const marker of markers) if (!content.includes(marker)) fail(`${relative} is missing current documentation marker: ${marker}`);
  }
  const operationalDocs = Object.keys(required).map(readText).join('\n');
  for (const obsolete of ['главный профиль из 1169', 'nx2512-pro-full.generated.json как главный', 'Установка базового профиля'])
    if (operationalDocs.includes(obsolete)) fail(`Operational documentation contains obsolete statement: ${obsolete}`);
}

try {
  const intents = loadIntents();
  if (intents.length !== 1169) fail(`Expected 1169 intents, got ${intents.length}.`);
  const counts = Object.fromEntries(Object.keys(EXPECTED_COUNTS).map(level => [level, intents.filter(intent => intent.frequency === level).length]));
  for (const [level, expected] of Object.entries(EXPECTED_COUNTS))
    if (counts[level] !== expected) fail(`Expected ${expected} ${level} intents, got ${counts[level]}.`);
  const mainIntents = intents.filter(intent => MAIN_FREQUENCIES.includes(intent.frequency));
  if (mainIntents.length !== EXPECTED_MAIN_INTENTS) fail(`Expected ${EXPECTED_MAIN_INTENTS} K3-K5 intents, got ${mainIntents.length}.`);

  const ids = new Set();
  const sectionCounts = new Map();
  const groupedPaths = new Map();
  for (const intent of intents) {
    if (!intent.intent_id || ids.has(intent.intent_id)) fail(`Missing or duplicate intent_id: ${intent.intent_id}.`);
    ids.add(intent.intent_id);
    if (!Number.isInteger(intent.source_index) || intent.source_index < 0 || intent.source_index > 31)
      fail(`Invalid source_index for ${intent.intent_id}: ${intent.source_index}.`);
    sectionCounts.set(intent.source_index, (sectionCounts.get(intent.source_index) ?? 0) + 1);
    if (!(intent.frequency in EXPECTED_COUNTS)) fail(`Invalid frequency for ${intent.intent_id}: ${intent.frequency}.`);
    if (!intent.runtime_module || !intent.source_module || !intent.group || !intent.name_en)
      fail(`Required metadata missing for ${intent.intent_id}.`);
    if (intent.path.length < 2 || intent.path.length > 5) fail(`Path length out of range for ${intent.intent_id}: ${intent.path.join(' ')}.`);
    const modulePaths = groupedPaths.get(intent.runtime_module) ?? [];
    modulePaths.push({ key: keyOf(intent.path), intent });
    groupedPaths.set(intent.runtime_module, modulePaths);
  }
  for (let section = 0; section <= 31; section += 1)
    if (!sectionCounts.has(section)) fail(`Source section ${section} has no command intents.`);
  for (const requiredName of ['Zoom In/Out', 'Select Similar Faces/Edges', 'Export DXF/DWG', 'Import DXF/DWG', 'Arc/Circle'])
    if (!intents.some(intent => intent.name_en === requiredName)) fail(`Bilingual parser damaged command name: ${requiredName}.`);
  for (const [moduleId, rows] of groupedPaths) {
    rows.sort((a, b) => a.key.length - b.key.length || a.key.localeCompare(b.key));
    for (let left = 0; left < rows.length; left += 1)
      for (let right = left + 1; right < rows.length; right += 1)
        if (isConflict(rows[left].key, rows[right].key))
          fail(`Intent path conflict in ${moduleId}: ${rows[left].intent.intent_id}/${rows[left].key} and ${rows[right].intent.intent_id}/${rows[right].key}.`);
  }

  validateDocumentation();
  const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'nxkeys-command-map-'));
  try {
    validateGeneratedProfile(compile(tempRoot), EXPECTED_MAIN_INTENTS, MAIN_FREQUENCIES, intents.length);
    validateGeneratedProfile(compile(tempRoot, ['--all-frequencies']), intents.length, Object.keys(EXPECTED_COUNTS), intents.length);
  } finally {
    fs.rmSync(tempRoot, { recursive: true, force: true });
  }

  if (!failed) console.log(`[main-command-map] OK: 1169 source intents; main K3-K5 profile covers ${EXPECTED_MAIN_INTENTS} intents; selection support and optional all-frequency profile validated.`);
} catch (error) {
  fail(error?.stack || error?.message || String(error));
}

if (failed) process.exitCode = 1;
