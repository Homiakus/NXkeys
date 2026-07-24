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
const defaultInputKeys = ["W", "E", "D", "C", "X", "Z", "A", "Q"];
const slots = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
const removedFeaturePattern = /radial[\s_-]*(menu|plan|editor|item)|радиальн\w*\s+меню/i;

try {
  const profile = json("config/nx2512-pro-hybrid.json");
  const policy = json("config/nx2512-state-machines.json");
  const html = text("docs/command-tree.html");
  const readme = text("README.md");
  const docsReadme = text("docs/README.md");

  if (profile.schema_version !== 4) fail(`schema_version must be 4, got ${profile.schema_version}.`);
  if (profile.leader_key?.adaptive_module_mode !== true) fail("leader_key.adaptive_module_mode must be true.");
  if (profile.leader_key && Object.hasOwn(profile.leader_key, "slot_key_map")) fail("leader_key.slot_key_map must not be saved in schema v4.");

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
    if (rows.length < 8) fail(`Module ${module.id} must contain at least 8 commands, got ${rows.length}.`);
    const levelKeys = new Set();
    const rootBranches = new Map();
    for (const row of rows) {
      const slot = String(row.item.slot ?? "").toUpperCase();
      const submenuKey = String(row.item.submenu_key || "").trim().toUpperCase();
      const key = String(row.item.input_key || "").trim().toUpperCase();
      const order = Number(row.item.display_order);
      if (row.item.slot && !slots.includes(slot)) fail(`Module ${module.id} has invalid legacy slot ${slot}.`);
      if (submenuKey && !/^[A-Z0-9]$/.test(submenuKey)) fail(`Module ${module.id} has invalid submenu_key ${submenuKey}.`);
      if (submenuKey && !row.item.submenu_label) fail(`Module ${module.id}, submenu ${submenuKey}: submenu_label is required.`);
      const rootKey = submenuKey || key;
      const branchKind = submenuKey ? "submenu" : "command";
      if (rootKey && rootBranches.has(rootKey) && rootBranches.get(rootKey) !== branchKind)
        fail(`Module ${module.id} root key ${rootKey} is both command and submenu.`);
      if (rootKey && !rootBranches.has(rootKey)) rootBranches.set(rootKey, branchKind);
      if (!/^[A-Z0-9]$/.test(key)) fail(`Module ${module.id} has invalid input_key ${key || "empty"}.`);
      const level = submenuKey || "$root";
      const levelKey = `${level}|${key}`;
      if (levelKeys.has(levelKey)) fail(`Module ${module.id} repeats input_key ${key} in level ${level}.`);
      levelKeys.add(levelKey);
      if (!Number.isInteger(order) || order < 1 || order > 99) fail(`Module ${module.id}, key ${key}: invalid display_order ${row.item.display_order}.`);
      if (!row.item.command?.id) fail(`Module ${module.id}, key ${key}: exact command.id is required.`);
      if (!row.item.command?.name) fail(`Module ${module.id}, key ${key}: command.name is required.`);
      if (!row.item.icon_hint) fail(`Module ${module.id}, key ${key}: icon_hint is required.`);
      const sequence = prefix + submenuKey + key;
      if (internalSequences.has(sequence)) fail(`Derived DFA sequence is repeated: ${sequence}.`);
      internalSequences.add(sequence);
      commandRows.push({ ...row, slot, submenuKey, key, sequence });
    }
  }
  if (commandRows.length < 112) fail(`Expected at least 112 module commands, got ${commandRows.length}.`);
  if (!commandRows.some(row => row.module.id === "sketch" && row.submenuKey === "Q" && /CONSTRAINT/i.test(row.item.command?.id ?? "")))
    fail("Sketch module must expose a Constraints submenu.");
  if (!commandRows.some(row => row.module.id === "sketch" && row.submenuKey === "E" && /RECTANGLE/i.test(row.item.command?.id ?? "")))
    fail("Sketch module must expose Rectangle subtype submenu.");
  if (!commandRows.some(row => row.submenuKey === "1" && row.item.command?.id === "UG_SEL_BODY_PRIORITY"))
    fail("Selection filter submenu must expose Body priority.");
  for (const moduleId of ["modeling", "assembly"]) {
    const special = commandRows.filter(row => row.module.id === moduleId && row.set.id === "special_workflows");
    if (special.length < 24) fail(`Module ${moduleId} must expose expanded Special workflows.`);
    for (const required of ["UG_APP_SBSM", "UG_ASSY_WAVE_LINKER", "UG_LAYER_SETTINGS", "UG_MATERIAL_ASSIGN"]) {
      if (!special.some(row => row.item.command?.id === required)) fail(`Module ${moduleId} Special workflows missing ${required}.`);
    }
  }
  for (const key of defaultInputKeys) {
    if (!commandRows.some(row => row.key === key)) fail(`Default ergonomic key missing from profile: ${key}.`);
  }

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
  const overlaySource = text("NX2512_HotkeyStudio/Services/OverlayGenerator.cs");
  const deploymentSource = text("NX2512_HotkeyStudio/Services/DeploymentEngine.cs");
  if (!overlaySource.includes("ACTIONS NX2512_CommandBridge.dll") || !deploymentSource.includes("ACTIONS NX2512_CommandBridge.dll"))
    fail("Start NXKeys Bridge must load NX2512_CommandBridge.dll through ACTIONS.");
  if (/APPLICATION_BUTTON\s+NXKEYS_COMMAND_BRIDGE/i.test(overlaySource + deploymentSource))
    fail("NXKEYS_COMMAND_BRIDGE APPLICATION_BUTTON must not be used for Bridge startup.");
  if (/RIBBON_TAB\s+NXKEYS_TAB/i.test(deploymentSource))
    fail("Ribbon generation must use BEGIN_GROUP instead of RIBBON_TAB.");
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

  if (!readme.includes("CapsLock") || !readme.includes("3 колонки")) fail("Root README lacks 3-column adaptive input documentation.");
  if (!readme.includes("scripts\\validate-command-tree.mjs") && !readme.includes("scripts/validate-command-tree.mjs")) fail("Root README lacks validator command.");
  if (!docsReadme.includes("command-tree.html")) fail("docs/README.md must link to the command map.");

  if (!failed) console.log(`[adaptive-profile] OK: ${bindings.length} basic shortcuts, ${modules.length} modules, ${commandRows.length} module commands, schema v4.`);
} catch (error) {
  fail(error?.stack || error?.message || String(error));
}

if (failed) process.exitCode = 1;
