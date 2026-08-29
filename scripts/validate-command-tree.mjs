import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";
import {
  CANONICAL_SELECTION_FILTERS,
  MODULE_SWITCH_PATHS,
  SWITCHABLE_MODULE_IDS,
  ensureUniversalSupport,
  isModuleSwitchSupportCommand,
  isSelectionSupportCommand,
  targetLengthForCommand
} from "./sequence-policy.mjs";

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
].map(([shortcut, command]) => [normalize(shortcut), command]));
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
  const profile = fs.existsSync(path.join(root, "config/nx2512-pro-hybrid.json")) ? json("config/nx2512-pro-hybrid.json") : null;
  const v8Profile = json("config/nx2512-v8-profile.json");
  const policy = fs.existsSync(path.join(root, "config/nx2512-state-machines.json")) ? json("config/nx2512-state-machines.json") : null;
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
  const protocolSource = text("NXKeys.Protocol/NxProtocol.cs");
  const bridgeSource = text("NX2512_CommandBridge/Program.cs");
  const executorSource = text("NX2512_CommandBridge/NxMenuCommandExecutor.cs");
  const runtimeSource = text("NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs");
  const html = text("docs/command-tree.html");
  const readme = text("README.md");
  const docsReadme = text("docs/README.md");
  const knownPaths = parseKnownPaths(generatorSource);

  if (v8Profile.schema_version !== 8) fail(`v8Profile schema_version must be 8, got ${v8Profile.schema_version}.`);
  const v8Constraints = (v8Profile.operations ?? []).filter(op => String(op.operation_id).startsWith("sketch.") && op.paths?.leader?.[0] === "K");
  if (v8Constraints.length < 13) fail(`Expected at least 13 K-prefixed constraint operations in v8 profile, got ${v8Constraints.length}.`);

  if (profile) {
    if (![4, 5, 6, 8].includes(profile.schema_version)) fail(`source profile schema_version must be 4, 5, 6 or 8, got ${profile.schema_version}.`);
  }
  if (!/CurrentSchemaVersion\s*=\s*[68]/.test(modelSource)) fail("Schema model must expose schema v6 or v8 runtime migration.");
  for (const required of ["path", "path_labels", "aliases", "search_aliases", "MnemonicPathGenerator.Apply"])
    if (!modelSource.includes(required)) fail(`Schema v5 model missing mnemonic feature: ${required}.`);
  if (!modelSource.includes("catalog_backed_support")) fail("Schema model must preserve catalog-backed support traceability.");
  if (fs.existsSync(path.join(root, "NX2512_HotkeyStudio/Models/ConfigModels.cs")) && !projectSource.includes('Compile Remove="Models\\ConfigModels.cs"'))
    fail("HotkeyStudio project must exclude legacy ConfigModels.cs from compilation.");
  if (!generatorSource.includes("GenerateCandidate") || !generatorSource.includes("FilterAliases") || !generatorSource.includes("ReserveUnique"))
    fail("Mnemonic generator must cover unmapped catalog commands and resolve path conflicts.");
  if (knownPaths.size < 90) fail(`Expected at least 90 exact mnemonic BUTTON ID mappings, got ${knownPaths.size}.`);

  let bindings = [];
  let modules = [];
  let commandCount = 0;
  const generatedSequences = new Set();

  if (profile) {
    bindings = (profile.keyboard ?? []).filter(item => item && item.enabled !== false);
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

    ensureUniversalSupport(profile.modules ?? []);
    modules = (profile.modules ?? []).filter(item => item && item.enabled !== false);
    if (modules.length !== 14) fail(`Expected 14 enabled modules, got ${modules.length}.`);
    const moduleIds = new Set();
    const prefixes = new Set();

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

      for (const { set, item } of rows) {
        if (!item.command?.id || !item.command?.name) fail(`Module ${module.id} has command without exact id/name.`);
        if (!item.icon_hint) fail(`Module ${module.id}, ${item.command?.id}: icon_hint is required.`);
        if (set.id === "primary" && item.input_key) {
          const expectedAlias = normalize(item.input_key);
          const aliases = (item.aliases ?? []).map(alias => normalize((alias ?? []).join("")));
          if (!aliases.includes(expectedAlias))
            fail(`Module ${module.id}, ${item.command?.id}: primary command must keep one-key alias ${expectedAlias}.`);
        }
        if (item.command?.id?.startsWith("UG_SEL_")) {
          if (item.action !== "set_selection_filter")
            fail(`Module ${module.id}, ${item.command.id}: selection filter command must use set_selection_filter action.`);
          if (!item.selection_type)
            fail(`Module ${module.id}, ${item.command.id}: selection filter command must declare selection_type.`);
          const expectedPath = CANONICAL_SELECTION_FILTERS.find(filter => filter.id === item.command.id)?.path ?? [];
          if (expectedPath.length && normalize((item.path ?? []).join("")) !== normalize(expectedPath.join("")))
            fail(`Module ${module.id}, ${item.command.id}: selection filter must follow S* policy.`);
        }
        if (item.action === "switch_module") {
          if (module.id === "sketch") fail("Sketch module must not contain module switch commands.");
          if (!item.target_module_id) fail(`Module ${module.id}, ${item.command?.id}: module switch must declare target_module_id.`);
          if (normalize((item.path ?? []).join("")) !== normalize((MODULE_SWITCH_PATHS[item.target_module_id] ?? []).join("")))
            fail(`Module ${module.id}, ${item.command?.id}: module switch path must follow G* policy.`);
        }
        if (item.requires_selection && !item.selection_type)
          fail(`Module ${module.id}, ${item.command?.id}: requires_selection command must declare selection_type or all.`);
        if (item.frequency && !item.support_kind && (item.path ?? []).length > targetLengthForCommand(module.id, item))
          fail(`Module ${module.id}, ${item.command?.id}: ${item.frequency} path exceeds target length.`);
        const known = knownPaths.get(item.command?.id);
        if (!known) continue;
        const key = [prefix, ...known].join("");
        generatedSequences.add(`${module.id}|${key}`);
      }
    }

    if (commandCount < 112) fail(`Expected at least 112 module commands, got ${commandCount}.`);
    const availableModules = new Set(modules.map(module => module.id));
    const expectedSelection = CANONICAL_SELECTION_FILTERS.map(filter => filter.id);
    for (const module of modules) {
      const rows = (module.command_sets ?? []).flatMap(set => set.commands ?? []);
      const selectionIds = new Set(rows.filter(isSelectionSupportCommand).map(command => String(command.command?.id ?? "").toUpperCase()));
      for (const id of expectedSelection) if (!selectionIds.has(id)) fail(`Module ${module.id} is missing universal selection filter ${id}.`);
      const switches = rows.filter(isModuleSwitchSupportCommand);
      if (module.id === "sketch") {
        if (switches.length) fail("Sketch module must not expose module switches.");
        continue;
      }
      if (module.id === "selection_object") continue;
      const switchTargets = new Set(switches.map(command => command.target_module_id).filter(Boolean));
      for (const target of SWITCHABLE_MODULE_IDS.filter(id => id !== module.id && availableModules.has(id))) {
        if (!switchTargets.has(target)) fail(`Module ${module.id} is missing switch target ${target}.`);
      }
    }
  }

  if (!protocolSource.includes("selection_filter")) fail("Protocol request must carry selection_filter.");
  const hasSelectionDispatch = bridgeSource.includes("NxProtocolActions.SetSelectionFilter") &&
    bridgeSource.includes("executor.ApplySelectionCommand");
  if (!hasSelectionDispatch || !executorSource.includes("SetEnabledGlobalFilterMembers"))
    fail("CommandBridge must dispatch selection-filter actions to the NXOpen executor boundary.");
  if (!runtimeSource.includes("SelectionIntent") || !runtimeSource.includes("AddAlias(command, command.InputKey)"))
    fail("Runtime config must preserve short aliases and infer selection intent.");

  if (policy) {
    if (policy.schema_version !== 1) fail(`state-machine policy schema_version must be 1, got ${policy.schema_version}.`);
    if (!(Number(policy.timeouts?.root_ms) > 0) || !(Number(policy.timeouts?.prefix_ms) > 0))
      fail("State-machine policy must declare positive root/prefix timeouts.");
    if (profile) {
      const policyKeys = Object.keys(policy.commands ?? {}).map(normalize);
      const allGeneratedKeys = new Set([...generatedSequences].map(value => value.split("|")[1]));
      for (const key of policyKeys) if (!allGeneratedKeys.has(key)) fail(`Policy references unknown mnemonic sequence: ${key}.`);
    }
    if (policy.adaptive_module?.enabled !== true || policy.adaptive_module?.scope !== "active_module")
      fail("Policy must enable active_module scope.");
  } else {
    fail("Declarative state-machine policy config/nx2512-state-machines.json is required.");
  }

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
    'data-panel="overview"', 'data-panel="leader"', 'data-panel="direct"', 'data-panel="workspaces"',
    'id="leaderGrid"', 'id="directGrid"', 'id="allOpsTable"',
    'nx2512-v8-profile.json',
    'getOperations'
  ];
  for (const marker of htmlMarkers) if (!html.includes(marker)) fail(`HTML marker missing: ${marker}.`);
  if (/<script[^>]+\bsrc\s*=/i.test(html)) fail("HTML must not depend on external scripts.");
  if (/<link[^>]+rel=["']stylesheet["']/i.test(html)) fail("HTML must not depend on external stylesheets.");
  const scripts = [...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/gi)].map(match => match[1]);
  if (scripts.length !== 1) fail(`Expected one inline application script, got ${scripts.length}.`);
  for (const script of scripts) try { new Function(script); } catch (error) { fail(`Inline JavaScript syntax error: ${error.message}.`); }

  if (!readme.includes("CapsLock → действие → объект") || !readme.includes("14 контекстных модулей"))
    fail("Root README lacks current adaptive input documentation.");
  if (!docsReadme.includes("command-tree.html")) fail("docs/README.md must link to the command map.");

  if (!failed) console.log(`[mnemonic-profile] OK: v8 schema, state policy, ${knownPaths.size} exact mnemonic mappings and current NXOpen executor boundary.`);
} catch (error) {
  fail(error?.stack || error?.message || String(error));
}

if (failed) process.exitCode = 1;
