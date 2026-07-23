import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
let failed = false;
const fail = message => { failed = true; console.error(`[command-tree] ERROR: ${message}`); };
const text = relative => {
  const full = path.join(root, relative);
  if (!fs.existsSync(full)) throw new Error(`Missing file: ${relative}`);
  return fs.readFileSync(full, "utf8").replace(/^\uFEFF/, "");
};
const json = relative => JSON.parse(text(relative));

try {
  const profile = json("config/nx2512-pro-hybrid.json");
  const policy = json("config/nx2512-state-machines.json");
  const html = text("docs/command-tree.html");
  const readme = text("README.md");
  const docsReadme = text("docs/README.md");

  const modules = (profile.modules ?? []).filter(x => x && x.enabled !== false);
  const prefixes = modules.map(x => String(x.leader_prefix ?? "").toUpperCase());
  const commandRows = modules.flatMap(module =>
    (module.command_sets ?? []).flatMap(set =>
      (set.commands ?? []).map(item => ({ module, set, item }))));
  const keyboard = (profile.keyboard ?? []).filter(x => x && x.enabled !== false);
  const radials = [
    ...(profile.radials ?? []).filter(x => x && x.enabled !== false),
    ...modules.flatMap(module => (module.radials ?? []).filter(x => x && x.enabled !== false))
  ];
  const radialPositions = radials.reduce((sum, radial) => sum + (radial.items ?? []).length, 0);

  if (modules.length !== 14) fail(`Expected 14 enabled modules, got ${modules.length}.`);
  if (commandRows.length !== 112) fail(`Expected 112 module commands, got ${commandRows.length}.`);
  if (keyboard.length !== 47) fail(`Expected 47 keyboard bindings, got ${keyboard.length}.`);
  if (radialPositions !== 168) fail(`Expected 168 radial positions, got ${radialPositions}.`);
  if (new Set(prefixes).size !== prefixes.length) fail("Enabled module prefixes must be unique.");

  for (const module of modules) {
    const moduleRows = commandRows.filter(x => x.module === module);
    if (moduleRows.length !== 8) fail(`Module ${module.id} must contain 8 primary command slots.`);
    const slots = moduleRows.map(x => String(x.item.slot ?? "").toUpperCase());
    if (new Set(slots).size !== slots.length) fail(`Module ${module.id} repeats a slot.`);
    for (const { item } of moduleRows) {
      if (!item.command?.id && !item.command?.name) fail(`Module ${module.id}, slot ${item.slot}: command id/name missing.`);
    }
  }

  const markers = [
    'id="treeView"', 'id="matrixView"', 'id="keyboardView"', 'id="radialView"',
    'id="fsmView"', 'id="policySummary"', 'data-tab="tree"', 'data-tab="matrix"',
    'data-tab="keyboard"', 'data-tab="radials"', 'data-tab="fsm"',
    '../config/nx2512-pro-hybrid.json', '../config/nx2512-state-machines.json',
    'function renderTree()', 'function renderMatrix()', 'function renderKeyboard()',
    'function renderRadials()', 'function renderPolicy()', 'dataTransfer.files'
  ];
  for (const marker of markers) if (!html.includes(marker)) fail(`HTML marker missing: ${marker}`);

  if (/<script[^>]+\bsrc\s*=/i.test(html)) fail("Command tree must not depend on external scripts.");
  if (/<link[^>]+rel=["']stylesheet["']/i.test(html)) fail("Command tree must not depend on external stylesheets.");
  if (!/<html\s+lang=["']ru["']/i.test(html)) fail("Command tree must declare lang=ru.");
  if (!/<meta\s+name=["']viewport["']/i.test(html)) fail("Responsive viewport is missing.");
  if (!policy.timeouts || !policy.commands) fail("State-machine policy lacks timeouts or command guards.");

  if (!readme.includes("docs/command-tree.html")) fail("Root README must link to the command tree.");
  if (!docsReadme.includes("command-tree.html")) fail("docs/README.md must link to the command tree.");
  if (!readme.includes("scripts\\validate-command-tree.mjs") && !readme.includes("scripts/validate-command-tree.mjs")) {
    fail("Root README must document the validator.");
  }

  if (!failed) {
    console.log(`[command-tree] OK: ${modules.length} modules, ${commandRows.length} module commands, ${keyboard.length} keyboard bindings, ${radialPositions} radial positions.`);
  }
} catch (error) {
  fail(error?.stack || error?.message || String(error));
}

if (failed) process.exitCode = 1;
