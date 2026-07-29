import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const argv = process.argv.slice(2);
const valueOf = (name, fallback = '') => {
  const index = argv.indexOf(name);
  return index >= 0 && index + 1 < argv.length ? argv[index + 1] : fallback;
};
const absolute = value => path.isAbsolute(value) ? value : path.resolve(repoRoot, value);

const mapArg = valueOf('--map');
const sourceRootArg = valueOf('--source-root');
if (!mapArg || !sourceRootArg) {
  throw new Error('Pass --map and --source-root explicitly. The repo keeps only assets/nx-operation-icons.');
}

const mapPath = absolute(mapArg);
const sourceRoot = absolute(sourceRootArg);
const outputRoot = absolute(valueOf('--out', 'assets/nx-operation-icons'));
const clean = argv.includes('--clean');

function safeOperationFileName(name) {
  return String(name ?? '')
    .replace(/[<>:"/\\|?*\u0000-\u001F]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/[. ]+$/g, '') || 'Unnamed Operation';
}

function scoreEntry(entry) {
  let score = Number(entry.score ?? 0);
  if (entry.darkstyle) score += 10000;
  score += Math.max(Number(entry.width ?? 0), Number(entry.height ?? 0)) * 10;
  if (entry.source === 'bma-extracted') score += 500;
  return score;
}

function copyFileEnsuringDirectory(source, destination) {
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  fs.copyFileSync(source, destination);
}

if (!fs.existsSync(mapPath)) throw new Error(`Command icon map not found: ${mapPath}`);
if (!fs.existsSync(sourceRoot)) throw new Error(`Source icon root not found: ${sourceRoot}`);

const outputFull = path.resolve(outputRoot);
const assetsRoot = path.resolve(repoRoot, 'assets');
if (!`${outputFull}${path.sep}`.startsWith(`${assetsRoot}${path.sep}`)) {
  throw new Error(`Output must stay under repo assets: ${outputFull}`);
}

if (clean && fs.existsSync(outputFull)) fs.rmSync(outputFull, { recursive: true, force: true });
fs.mkdirSync(outputFull, { recursive: true });

const sourceMap = JSON.parse(fs.readFileSync(mapPath, 'utf8').replace(/^\uFEFF/, ''));
const entries = Object.values(sourceMap.entries ?? {}).filter(entry => entry && entry.icon);
const operationGroups = new Map();
for (const entry of entries) {
  const operationName = String(entry.command_name || entry.command_id || '').trim();
  const safeName = safeOperationFileName(operationName);
  if (!operationGroups.has(safeName)) operationGroups.set(safeName, []);
  operationGroups.get(safeName).push(entry);
}

const operationFiles = new Map();
for (const [safeName, group] of operationGroups) {
  group.sort((left, right) => scoreEntry(right) - scoreEntry(left));
  const selected = group[0];
  const source = path.resolve(sourceRoot, selected.icon.replaceAll('/', path.sep));
  if (!fs.existsSync(source)) throw new Error(`Selected source icon is missing: ${source}`);
  const extension = path.extname(source) || '.bmp';
  const fileName = `${safeName}${extension.toLowerCase()}`;
  const destination = path.join(outputFull, fileName);
  copyFileEnsuringDirectory(source, destination);
  operationFiles.set(safeName, {
    operation_name: selected.command_name || selected.command_id || safeName,
    file: fileName,
    source_icon: selected.icon,
    width: selected.width,
    height: selected.height,
    darkstyle: !!selected.darkstyle,
    command_ids: [...new Set(group.map(item => item.command_id).filter(Boolean))],
    command_names: [...new Set(group.map(item => item.command_name).filter(Boolean))]
  });
}

const outputEntries = {};
for (const [key, entry] of Object.entries(sourceMap.entries ?? {})) {
  const operationName = String(entry.command_name || entry.command_id || key).trim();
  const safeName = safeOperationFileName(operationName);
  const operationFile = operationFiles.get(safeName);
  if (!operationFile) continue;
  outputEntries[key] = {
    ...entry,
    operation_name: operationName,
    icon: operationFile.file,
    source_icon: entry.icon
  };
}

const manifest = {
  schema_version: 1,
  generated_utc: new Date().toISOString(),
  source_map: path.relative(repoRoot, mapPath).replaceAll(path.sep, '/'),
  source_root: path.relative(repoRoot, sourceRoot).replaceAll(path.sep, '/'),
  output_root: path.relative(repoRoot, outputFull).replaceAll(path.sep, '/'),
  operation_count: operationFiles.size,
  command_entry_count: Object.keys(outputEntries).length,
  darkstyle_count: [...operationFiles.values()].filter(item => item.darkstyle).length,
  high_resolution_count: [...operationFiles.values()].filter(item => Math.max(item.width ?? 0, item.height ?? 0) >= 48).length,
  files: Object.fromEntries([...operationFiles.entries()].sort((left, right) => left[0].localeCompare(right[0])))
};

const runtimeMap = {
  schema_version: 1,
  generated_utc: manifest.generated_utc,
  source_map: manifest.source_map,
  operation_icon_root: manifest.output_root,
  commands_seen: sourceMap.commands_seen,
  selected_count: Object.keys(outputEntries).length,
  selected_darkstyle_count: manifest.darkstyle_count,
  selected_high_resolution_count: manifest.high_resolution_count,
  entries: outputEntries
};

fs.writeFileSync(path.join(outputFull, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
fs.writeFileSync(path.join(outputFull, 'command-icon-map.json'), `${JSON.stringify(runtimeMap, null, 2)}\n`, 'utf8');

console.log(`[operation-icons] files=${manifest.operation_count}; entries=${manifest.command_entry_count}; darkstyle=${manifest.darkstyle_count}; high-res>=48=${manifest.high_resolution_count}`);
console.log(`[operation-icons] output: ${outputFull}`);
