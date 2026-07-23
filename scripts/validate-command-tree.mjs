import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
let failed = false;
const fail = message => { failed = true; console.error(`[adaptive-profile] ERROR: ${message}`); };
const text = relative => fs.readFileSync(path.join(root, relative), "utf8").replace(/^\uFEFF/, "");
const json = relative => JSON.parse(text(relative));
const normalizeShortcut = value => String(value ?? "").replace(/\s+/g, "").toUpperCase();

const requiredShortcuts = new Map([
  ["CTRL+N", "UG_FILE_NEW"], ["CTRL+O", "UG_FILE_OPEN"],
  ["CTRL+S", "UG_FILE_SAVE_PART"], ["CTRL+SHIFT+S", "UG_FILE_SAVE_AS"],
  ["CTRL+Z", "UG_EDIT_UNDO"], ["CTRL+Y", "UG_EDIT_REDO"],
  ["CTRL+X", "UG_EDIT_CUT"], ["CTRL+C", "UG_EDIT_COPY"],
  ["CTRL+V", "UG_EDIT_PASTE"], ["DELETE", "UG_EDIT_DELETE"],
  ["CTRL+F", "UG_VIEW_FIT"], ["F5", "UG_VIEW_REFRESH"]
]);
const expectedKeyMap = { N: "W", NE: "E", E: "D", SE: "C", S: "X", SW: "Z", W: "A", NW: "Q" };
const slots = Object.keys(expectedKeyMap);
const removedFeaturePattern = /radial[\s_-]*(menu|plan|editor|item)|радиальн\w*\s+меню/i;

try {
  const profile = json("config/nx2512-pro-hybrid.json");
  const policy = json("config/nx2512-state-machines.json");
  const html = text("docs/command-tree.html");
  const readme = text("README.md");
  const docsReadme = text("docs/README.md");

  if (profile.schema_version !== 3) fail(`schema_version must be 3, got ${profile.schema_version}.`);
  if (profile.leader_key?.adaptive_module_mode !== true) fail("leader_key.adaptive_module_mode must be true.");

  const bindings = (profile.keyboard ?? []).filter(item => item && item.enabled !== false);
  if (bindings.length !== requiredShortcuts.size) fail(`Expected ${requiredShortcuts.size} basic shortcuts, got ${bindings.length}.`);
  const seenShortcuts = new Set();
  for (const binding of bindings) {
    const shortcut = normalizeShortcut(binding.shortcut);
    if (seenShortcuts.has(shortcut)) fail(`Duplicate shortcut: ${binding.shortcut}.`);
    seenShortcuts.add(shortcut);
    if (!requiredShortcuts.has(shortcut)) fail(`Non-basic shortcut is forbidden: ${binding.shortcut}.`);
    if (requiredShortcuts.get(shortcut) !== binding.command?.id) fail(`${binding.shortcut} must target ${requiredShortcuts.get(shortcut)}, got ${binding.command?.id}.`);
  }
  for (const shortcut of requiredShortcuts.keys()) if (!seenShortcuts.has(shortcut)) fail(`Missing basic shortcut: ${shortcut}.`);

  const keyMap = profile.leader_key?.slot_key_map ?? {};
  const usedKeys = new Set();
  for (const [slot, key] of Object.entries(expectedKeyMap)) {
    const actual = String(keyMap[slot] ?? "").toUpperCase();
    if (actual !== key) fail(`slot_key_map.${slot} must be ${key}, got ${actual || "empty"}.`);
    if (usedKeys.has(actual)) fail(`Adaptive key is repeated: ${actual}.`);
    usedKeys.add(actual);
  }

  const modules = (profile.modules ?? []).filter(item => item && item.enabled !== false);
  if (modules.length !== 14) fail(`Expected 14 enabled modules, got ${modules.length}.`);
  const moduleIds = new Set();
  const prefixes = new Set();
  const internalSequences = new Set();
  const commandRows = [];
  for (const module of modules) {
    if (!module.id || moduleIds.has(module.id)) fail(`Module id missing or repeated: ${module.id}.`);
    moduleIds.add(module.id);
    const prefix = String(module.leader_prefix ?? "").trim().toUpperCase();
    if (!prefix || prefixes.has(prefix)) fail(`Module prefix missing or repeated: ${module.id}/${prefix}.`);
    prefixes.add(prefix);
    if (!(module.nx_application_ids ?? []).length) fail(`Module ${module.id} has no nx_application_ids.`);
    const rows = (module.command_sets ?? []).flatMap(set => (set.commands ?? []).map(item => ({ module, set, item })));
    if (rows.length !== 8) fail(`Module ${module.id} must contain exactly 8 commands, got ${rows.length}.`);
    const moduleSlots = new Set();
    for (const row of rows) {
      const slot = String(row.item.slot ?? "").toUpperCase();
      if (!slots.includes(slot)) fail(`Module ${module.id} has invalid slot ${slot}.`);
      if (moduleSlots.has(slot)) fail(`Module ${module.id} repeats slot ${slot}.`);
      moduleSlots.add(slot);
      if (!row.item.command?.id) fail(`Module ${module.id}, slot ${slot}: exact command.id is required.`);
      const sequence = prefix + expectedKeyMap[slot];
      if (internalSequences.has(sequence)) fail(`Derived DFA sequence is repeated: ${sequence}.`);
      internalSequences.add(sequence);
      commandRows.push({ ...row, slot, sequence });
    }
  }
  if (commandRows.length !== 112) fail(`Expected 112 module commands, got ${commandRows.length}.`);

  for (const sequence of Object.keys(policy.commands ?? {})) if (!internalSequences.has(sequence.toUpperCase())) fail(`Policy references unknown adaptive sequence: ${sequence}.`);
  if (policy.adaptive_module?.enabled !== true || policy.adaptive_module?.scope !== "active_module") fail("Policy must enable active_module scope.");

  const serializedProfile = JSON.stringify(profile).toLowerCase();
  for (const key of ["radials", "legacy_radials"]) if (serializedProfile.includes(`\"${key}\"`)) fail(`Removed JSON key returned: ${key}.`);

  const applicationFiles = [
    "NX2512_HotkeyStudio/Models/ConfigModels.cs",
    "NX2512_HotkeyStudio/Services/AdaptiveModuleResolver.cs",
    "NX2512_HotkeyStudio/Services/AdaptiveLeaderPolicy.cs",
    "NX2512_HotkeyStudio/Services/LeaderKeyEngine.cs",
    "NX2512_HotkeyStudio/Services/DeploymentEngine.cs",
    "NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs",
    "NX2512_HotkeyStudio/UI/LeaderHudForm.cs"
  ];
  for (const relative of applicationFiles) if (removedFeaturePattern.test(text(relative))) fail(`Removed menu subsystem reference found in ${relative}.`);
  const uiDirectory = path.join(root, "NX2512_HotkeyStudio", "UI");
  for (const name of fs.readdirSync(uiDirectory)) if (/radial/i.test(name)) fail(`Removed UI file still exists: ${name}.`);

  const documentationFiles = [
    "README.md", "docs/README.md", "docs/CONFIGURATION.md", "docs/ARCHITECTURE.md",
    "docs/INSTALLATION.md", "docs/SAFETY_MODEL.md", "docs/NX_PRO_HYBRID_SOURCE_SPEC.md",
    "roles/README.md", "docs/command-tree.html"
  ];
  for (const relative of documentationFiles) if (removedFeaturePattern.test(text(relative))) fail(`Removed feature is still documented in ${relative}.`);

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

  const normalizedReadme = readme.replace(/\s+/g, "");
  if (!readme.includes("CapsLock") || !normalizedReadme.includes("QWE/A·D/ZXC")) fail("Root README lacks adaptive input documentation.");
  if (!readme.includes("scripts\\validate-command-tree.mjs") && !readme.includes("scripts/validate-command-tree.mjs")) fail("Root README lacks validator command.");
  if (!docsReadme.includes("command-tree.html")) fail("docs/README.md must link to the command map.");

  if (!failed) console.log(`[adaptive-profile] OK: ${bindings.length} basic shortcuts, ${modules.length} modules, ${commandRows.length} module commands, ${usedKeys.size} adaptive keys.`);
} catch (error) {
  fail(error?.stack || error?.message || String(error));
}

if (failed) process.exitCode = 1;
