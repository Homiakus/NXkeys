import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
let failed = false;
const fail = message => { failed = true; console.error(`[mnemonic-profile] ERROR: ${message}`); };
const text = relative => fs.readFileSync(path.join(root, relative), "utf8").replace(/^\uFEFF/, "");
const json = relative => JSON.parse(text(relative));
const normalize = value => String(value ?? "").replace(/[^a-z0-9]/gi, "").toUpperCase();
const tokens = value => String(value ?? "").match(/[A-Z0-9]/gi)?.map(value => value.toUpperCase()) ?? [];

const requiredShortcuts = new Map([
  ["CTRL+N", "UG_FILE_NEW"], ["CTRL+O", "UG_FILE_OPEN"],
  ["CTRL+S", "UG_FILE_SAVE_PART"], ["CTRL+SHIFT+S", "UG_FILE_SAVE_AS"],
  ["CTRL+Z", "UG_EDIT_UNDO"], ["CTRL+Y", "UG_EDIT_REDO"],
  ["CTRL+X", "UG_EDIT_CUT"], ["CTRL+C", "UG_EDIT_COPY"],
  ["CTRL+V", "UG_EDIT_PASTE"], ["DELETE", "UG_EDIT_DELETE"],
  ["CTRL+F", "UG_VIEW_FIT"], ["F5", "UG_VIEW_REFRESH"]
]);
const removedFeaturePattern = /radial[\s_-]*(menu|plan|editor|item)|радиальн\w*\s+меню/i;

function parseKnownPaths(source) {
  const result = new Map();
  const expression = /Add\(\s*"([^"]+)"\s*,\s*"([^"]+)"/g;
  for (const match of source.matchAll(expression)) {
    const id = match[1].trim();
    const pathTokens = tokens(match[2]);
    if (!id || pathTokens.length < 2) fail(`Invalid known mnemonic definition: ${match[0]}`);
    if (result.has(id)) fail(`Duplicate known mnemonic definition: ${id}`);
    result.set(id, pathTokens);
  }
  return result;
}

try {
  const profile = json("config/nx2512-pro-hybrid.json");
  const policy = json("config/nx2512-state-machines.json");
  const generatorSource = text("NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs");
  const modelFiles = [
    "NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs",
    "NX2512_HotkeyStudio/Models/BaseConfigTypesV5.cs",
    "NX2512_HotkeyStudio/Models/ModuleConfigTypesV5.cs",
    "NX2512_HotkeyStudio/Models/RuntimeSettingsTypesV5.cs",
    "NX2512_HotkeyStudio/Models/LeaderConfigV5.cs",
    "NX2512_HotkeyStudio/Models/CommandMetadataV5.cs"
  ];
  const modelSource = modelFiles.map(text).join("\n");
  const projectSource = text("NX2512_HotkeyStudio/NX2512_HotkeyStudio.csproj");
  const html = text("docs/command-tree.html");
  const readme = text("README.md");
  const docsReadme = text("docs/README.md");
  const knownPaths = parseKnownPaths(generatorSource);

  if (![4, 5].includes(profile.schema_version)) fail(`source profile schema_version must be 4 or 5, got ${profile.schema_version}.`);
  if (!/CurrentSchemaVersion\s*=\s*5/.test(modelSource)) fail("Schema model must expose schema v5 runtime migration.");
  for (const required of ["path", "path_labels", "aliases", "search_aliases", "MnemonicPathGenerator.Apply"])
    if (!modelSource.includes(required)) fail(`Schema v5 model missing mnemonic feature: ${required}.`);
  if (!projectSource.includes('Compile Remove="Models\\ConfigModels.cs"'))
    fail("HotkeyStudio project must exclude legacy ConfigModels.cs from compilation.");
  if (!generatorSource.includes("GenerateCandidate") || !generatorSource.includes("FilterAliases") || !generatorSource.includes("ReserveUnique"))
    fail("Mnemonic generator must cover unmapped catalog commands and resolve path conflicts.");
  if (knownPaths.size < 90) fail(`Expected at least 90 exact mnemonic BUTTON ID mappings, got ${knownPaths.size}.`);

  const bindings = (profile.keyboard ?? []).filter(item => item && item.enabled !== false);
  if (bindings.length !== requiredShortcuts.size) fail(`Expected ${requiredShortcuts.size} basic shortcuts, got ${bindings.length}.`);
  const seenShortcuts = new Set();
  for (const binding of bindings) {
    const shortcut = normalize(binding.shortcut);
    if (seenShortcuts.has(shortcut)) fail(`Duplicate shortcut: ${binding.shortcut}.`);
    seenShortcuts.add(shortcut);
    if (!requiredShortcuts.has(shortcut)) fail(`Non-basic shortcut is forbidden: ${binding.shortcut}.`);
    if (requiredShortcuts.get(shortcut) !== binding.command?.id)
      fail(`${binding.shortcut} must target ${requiredShortcuts.get(shortcut)}, got ${binding.command?.id}.`);
  }

  const modules = (profile.modules ?? []).filter(item => item && item.enabled !== false);
  if (modules.length !== 14) fail(`Expected 14 enabled modules, got ${modules.length}.`);
  const moduleIds = new Set();
  const prefixes = new Set();
  const generatedSequences = new Set();
  let commandCount = 0;

  for (const module of modules) {
    if (!module.id || moduleIds.has(module.id)) fail(`Module id missing or repeated: ${module.id}.`);
    moduleIds.add(module.id);
    const prefix = normalize(module.leader_prefix);
    if (prefix.length !== 1 || prefixes.has(prefix)) fail(`Module prefix missing or repeated: ${module.id}/${prefix}.`);
    prefixes.add(prefix);
    if (!(module.nx_application_ids ?? []).length) fail(`Module ${module.id} has no nx_application_ids.`);

    const rows = (module.command_sets ?? []).flatMap(set => (set.commands ?? []).map(item => ({ set, item })));
    if (rows.length < 8) fail(`Module ${module.id} must contain at least 8 commands, got ${rows.length}.`);
    commandCount += rows.length;

    for (const { item } of rows) {
      if (!item.command?.id || !item.command?.name) fail(`Module ${module.id} has command without exact id/name.`);
      if (!item.icon_hint) fail(`Module ${module.id}, ${item.command?.id}: icon_hint is required.`);
      const known = knownPaths.get(item.command?.id);
      if (!known) continue;
      const key = [prefix, ...known].join("");
      generatedSequences.add(`${module.id}|${key}`);
    }
  }

  if (commandCount < 112) fail(`Expected at least 112 module commands, got ${commandCount}.`);

  const policyKeys = Object.keys(policy.commands ?? {}).map(normalize);
  const allGeneratedKeys = new Set([...generatedSequences].map(value => value.split("|")[1]));
  for (const key of policyKeys) if (!allGeneratedKeys.has(key)) fail(`Policy references unknown mnemonic sequence: ${key}.`);
  if (policy.adaptive_module?.enabled !== true || policy.adaptive_module?.scope !== "active_module")
    fail("Policy must enable active_module scope.");

  const applicationFiles = [
    ...modelFiles,
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    "NX2512_HotkeyStudio/Services/AdaptiveModuleResolver.cs",
    "NX2512_HotkeyStudio/Services/AdaptiveLeaderPolicy.cs",
    "NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs",
    "NX2512_HotkeyStudio/Services/DeploymentEngine.cs",
    "NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs",
    "NX2512_HotkeyStudio/UI/LeaderHudForm.cs"
  ];
  for (const relative of applicationFiles) if (removedFeaturePattern.test(text(relative)))
    fail(`Removed menu subsystem reference found in ${relative}.`);

  const htmlMarkers = [
    'data-panel="current"', 'data-panel="matrix"', 'data-panel="basic"', 'data-panel="fsm"',
    'id="adaptiveGrid"', 'id="moduleSelect"', 'id="matrix"', 'id="basic"',
    '../config/nx2512-pro-hybrid.json', '../config/nx2512-state-machines.json',
    'function renderGrid()', 'function renderMatrix()', 'function renderBasic()', 'function renderPolicy()',
    'dataTransfer.files'
  ];
  for (const marker of htmlMarkers) if (!html.includes(marker)) fail(`HTML marker missing: ${marker}.`);
  if (/<script[^>]+\bsrc\s*=/i.test(html)) fail("HTML must not depend on external scripts.");
  if (/<link[^>]+rel=["']stylesheet["']/i.test(html)) fail("HTML must not depend on external stylesheets.");
  const scripts = [...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/gi)].map(match => match[1]);
  if (scripts.length !== 1) fail(`Expected one inline application script, got ${scripts.length}.`);
  for (const script of scripts) try { new Function(script); } catch (error) { fail(`Inline JavaScript syntax error: ${error.message}.`); }

  if (!readme.includes("CapsLock") || !readme.includes("3 колонки")) fail("Root README lacks adaptive input documentation.");
  if (!docsReadme.includes("command-tree.html")) fail("docs/README.md must link to the command map.");

  if (!failed) console.log(`[mnemonic-profile] OK: ${bindings.length} basic shortcuts, ${modules.length} modules, ${commandCount} commands, ${knownPaths.size} exact mnemonic mappings, schema v5 runtime.`);
} catch (error) {
  fail(error?.stack || error?.message || String(error));
}

if (failed) process.exitCode = 1;
