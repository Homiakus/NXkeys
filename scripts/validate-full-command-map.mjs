import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const intentsDir = path.join(root, 'config', 'full-command-map');
let failed = false;
const fail = message => { failed = true; console.error(`[full-command-map] ERROR: ${message}`); };
const normalizeKey = value => String(value ?? '').trim().match(/[A-Za-z0-9]/)?.[0]?.toUpperCase() ?? '';
const normalizePath = value => (Array.isArray(value) ? value : String(value ?? '').split(/[\s>\-/]+/))
  .map(normalizeKey).filter(Boolean).slice(0, 5);
const keyOf = value => normalizePath(value).join('');
const isConflict = (left, right) => left.startsWith(right) || right.startsWith(left);
const unescapeField = value => String(value ?? '').replace(/\\t/g, '\t').replace(/\\n/g, '\n').replace(/\\\\/g, '\\');

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

try {
  const intents = loadIntents();
  if (intents.length !== 1169) fail(`Expected 1169 intents, got ${intents.length}.`);
  const ids = new Set();
  const sectionCounts = new Map();
  const groupedPaths = new Map();
  const allowedFrequencies = new Set(['K1', 'K2', 'K3', 'K4', 'K5']);
  for (const intent of intents) {
    if (!intent.intent_id || ids.has(intent.intent_id)) fail(`Missing or duplicate intent_id: ${intent.intent_id}.`);
    ids.add(intent.intent_id);
    if (!Number.isInteger(intent.source_index) || intent.source_index < 0 || intent.source_index > 31)
      fail(`Invalid source_index for ${intent.intent_id}: ${intent.source_index}.`);
    sectionCounts.set(intent.source_index, (sectionCounts.get(intent.source_index) ?? 0) + 1);
    if (!allowedFrequencies.has(intent.frequency)) fail(`Invalid frequency for ${intent.intent_id}: ${intent.frequency}.`);
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
    for (let left = 0; left < rows.length; left += 1) {
      for (let right = left + 1; right < rows.length; right += 1) {
        if (isConflict(rows[left].key, rows[right].key))
          fail(`Intent path conflict in ${moduleId}: ${rows[left].intent.intent_id}/${rows[left].key} and ${rows[right].intent.intent_id}/${rows[right].key}.`);
      }
    }
  }

  const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'nxkeys-full-map-'));
  const output = path.join(tempRoot, 'profile.json');
  const report = path.join(tempRoot, 'report.md');
  const result = spawnSync(process.execPath, [
    path.join(root, 'scripts', 'compile-full-command-map.mjs'),
    '--profile', path.join(root, 'config', 'nx2512-pro-hybrid.json'),
    '--intents', intentsDir,
    '--probe', path.join(root, 'docs', 'audit', 'runtime-command-probe-2026-07-28.json'),
    '--out', output,
    '--report', report
  ], { cwd: root, encoding: 'utf8' });
  if (result.status !== 0) fail(`Compiler failed:\n${result.stdout}\n${result.stderr}`);
  if (!fs.existsSync(output)) fail('Compiler did not create the generated profile.');
  if (!fs.existsSync(report)) fail('Compiler did not create the resolution report.');

  if (fs.existsSync(output)) {
    const profile = JSON.parse(fs.readFileSync(output, 'utf8'));
    if (profile.full_command_catalog?.source_intents !== 1169)
      fail(`Generated profile reports ${profile.full_command_catalog?.source_intents} source intents.`);
    const seenRefs = new Set();
    let commandCount = 0;
    for (const module of (profile.modules ?? []).filter(item => item && item.enabled !== false)) {
      const rows = (module.command_sets ?? []).flatMap(set => set.commands ?? []);
      commandCount += rows.length;
      const allPaths = [];
      for (const command of rows) {
        const canonical = keyOf(command.path);
        if (!canonical || normalizePath(command.path).length > 5) fail(`Invalid canonical path in ${module.id}: ${command.command?.name}.`);
        allPaths.push({ key: canonical, kind: 'path', name: command.command?.name });
        for (const alias of command.aliases ?? []) {
          const aliasKey = keyOf(alias);
          if (aliasKey) allPaths.push({ key: aliasKey, kind: 'alias', name: command.command?.name });
        }
        for (const reference of command.catalog_refs ?? []) seenRefs.add(reference);
        if (String(command.fallback ?? '').startsWith('catalog:')) {
          seenRefs.add(String(command.fallback).slice('catalog:'.length));
          if (command.enabled !== false && !command.command?.id)
            fail(`Enabled catalog command has no BUTTON ID: ${module.id}/${command.command?.name}.`);
          if (!command.resolution_status) fail(`Catalog command lacks resolution_status: ${module.id}/${command.command?.name}.`);
        }
      }
      allPaths.sort((a, b) => a.key.length - b.key.length || a.key.localeCompare(b.key));
      for (let left = 0; left < allPaths.length; left += 1) {
        for (let right = left + 1; right < allPaths.length; right += 1) {
          if (isConflict(allPaths[left].key, allPaths[right].key))
            fail(`Generated path conflict in ${module.id}: ${allPaths[left].kind} ${allPaths[left].key} (${allPaths[left].name}) and ${allPaths[right].kind} ${allPaths[right].key} (${allPaths[right].name}).`);
        }
      }
    }
    for (const intent of intents) if (!seenRefs.has(intent.intent_id)) fail(`Generated profile lost source intent ${intent.intent_id}.`);
    if (commandCount < 1169) fail(`Generated profile contains only ${commandCount} module rows.`);
  }
  fs.rmSync(tempRoot, { recursive: true, force: true });

  if (!failed) console.log(`[full-command-map] OK: ${intents.length} intents, 32 sections, prefix-free paths, generated profile validated.`);
} catch (error) {
  fail(error?.stack || error?.message || String(error));
}

if (failed) process.exitCode = 1;
