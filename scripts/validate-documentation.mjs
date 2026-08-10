#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const root = process.cwd();

const canonicalFiles = [
  'README.md',
  'DEVELOPMENT.md',
  'CONTRIBUTING.md',
  'CHANGELOG.md',
  'SECURITY.md',
  'docs/README.md',
  'docs/RUNTIME_V8.md',
  'docs/CHEATSHEET.md',
  'docs/SELECTION_INTENT.md',
  'docs/DOCUMENTATION_AUDIT.md',
  'docs/ARCHITECTURE.md',
  'docs/INSTALLATION.md',
  'docs/CONFIGURATION.md',
  'docs/CLI.md',
  'docs/api.md',
  'docs/MNEMONIC_COMMAND_LANGUAGE.md',
  'docs/SKETCH_INTENT_LANGUAGE.md',
  'docs/STATE_MACHINE_ARCHITECTURE.md',
  'docs/SAFETY_MODEL.md',
  'docs/OPERATIONS.md',
  'docs/TROUBLESHOOTING.md',
  'docs/NX_PRO_HYBRID_SOURCE_SPEC.md',
  'docs/adr/README.md',
  'NX2512_HotkeyStudio/README.md',
  'NX2512_CommandBridge/README.md',
  'NX2512_ControlCenter/README.md',
  'NX2512_Catalog_Studio/README.md',
  'roles/README.md',
];

const errors = [];

function read(relativePath) {
  const absolutePath = path.join(root, relativePath);
  if (!fs.existsSync(absolutePath)) {
    errors.push(`Missing required documentation/source file: ${relativePath}`);
    return '';
  }
  return fs.readFileSync(absolutePath, 'utf8');
}

function requireText(relativePath, text, description = text) {
  const content = read(relativePath);
  if (content && !content.includes(text)) {
    errors.push(`${relativePath}: missing ${description}`);
  }
}

function requireAnyText(relativePath, alternatives, description) {
  const content = read(relativePath);
  if (content && !alternatives.some((text) => content.includes(text))) {
    errors.push(`${relativePath}: missing ${description}`);
  }
}

function requireNoText(relativePath, text, description = text) {
  const content = read(relativePath);
  if (content && content.includes(text)) {
    errors.push(`${relativePath}: contains stale ${description}`);
  }
}

function normalizeLinkTarget(sourceFile, rawTarget) {
  let target = rawTarget.trim();
  if (!target || target.startsWith('#')) return null;
  if (/^(?:https?:|mailto:|tel:|data:)/i.test(target)) return null;

  if (target.startsWith('<') && target.endsWith('>')) target = target.slice(1, -1);

  target = target.split('#', 1)[0].split('?', 1)[0];
  if (!target) return null;

  try {
    target = decodeURIComponent(target);
  } catch {
    // Keep the original path; existence check will report a useful error.
  }

  const base = path.dirname(path.join(root, sourceFile));
  return path.resolve(base, target);
}

function validateLinks(relativePath, content) {
  const markdownLink = /\[[^\]]*\]\(([^)]+)\)/g;
  let match;
  while ((match = markdownLink.exec(content)) !== null) {
    const rawTarget = match[1].trim().replace(/^['"]|['"]$/g, '');
    const resolved = normalizeLinkTarget(relativePath, rawTarget);
    if (!resolved) continue;

    if (!resolved.startsWith(root + path.sep) && resolved !== root) {
      errors.push(`${relativePath}: link escapes repository root: ${rawTarget}`);
      continue;
    }

    if (!fs.existsSync(resolved)) {
      errors.push(`${relativePath}: broken relative link: ${rawTarget}`);
    }
  }
}

function extractRequiredVersion(relativePath, regex, label) {
  const content = read(relativePath);
  const match = content.match(regex);
  if (!match) {
    errors.push(`${relativePath}: cannot determine ${label}`);
    return null;
  }
  return Number(match[1]);
}

for (const file of canonicalFiles) {
  const content = read(file);
  if (content) validateLinks(file, content);
}

const profileSchema = extractRequiredVersion(
  'NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs',
  /CurrentSchemaVersion\s*=\s*(\d+)/,
  'CurrentSchemaVersion',
);
const protocolSchema = extractRequiredVersion(
  'NXKeys.Protocol/NxProtocol.cs',
  /public\s+const\s+int\s+SchemaVersion\s*=\s*(\d+)/,
  'NxProtocolConstants.SchemaVersion',
);
const policyVersion = extractRequiredVersion(
  'scripts/sequence-policy.mjs',
  /SEQUENCE_POLICY_VERSION\s*=\s*(\d+)/,
  'SEQUENCE_POLICY_VERSION',
);

requireText('README.md', 'docs/RUNTIME_V8.md', 'link to canonical v8 runtime contract');
requireText('README.md', 'docs/CHEATSHEET.md', 'link to the detailed cheat sheet');
requireText('README.md', 'docs/SELECTION_INTENT.md', 'link to Selection Intent documentation');
requireText('README.md', 'Sketch Intent Grammar', 'Sketch workflow badge');
requireText('README.md', 'config/nx2512-v8-profile.json', 'current v8 profile path');

requireText('docs/README.md', 'RUNTIME_V8.md', 'v8 runtime entry');
requireText('docs/README.md', 'SELECTION_INTENT.md', 'Selection Intent entry');
requireText('docs/README.md', 'CHEATSHEET.md', 'cheat sheet entry');

requireText('DEVELOPMENT.md', 'NX2512_HotkeyStudio.Tests', 'HotkeyStudio regression test command');
requireText('DEVELOPMENT.md', 'SelectionIntentHotkeys.cs', 'Selection Intent development boundary');
requireText('CONTRIBUTING.md', 'SelectionIntentHotkeys.cs', 'Selection Intent change matrix entry');
requireText('CONTRIBUTING.md', 'workspace_key', 'workspace-local root collision rule');

requireText('docs/SKETCH_INTENT_LANGUAGE.md', 'C → V', 'Sketch variant branch');
requireText('docs/SKETCH_INTENT_LANGUAGE.md', 'D → Q', 'Sketch dimension path');
requireText('docs/SKETCH_INTENT_LANGUAGE.md', 'K → C', 'Sketch constraint path');
requireText('docs/SKETCH_INTENT_LANGUAGE.md', 'J → P', 'Sketch projection path');
requireText('docs/SKETCH_INTENT_LANGUAGE.md', 'U → I', 'Sketch utility path');

requireText('docs/CHEATSHEET.md', 'CapsLock → L', 'current one-token Sketch Line path');
requireText('docs/CHEATSHEET.md', 'CapsLock → K → C', 'current Sketch Coincident path');
requireText('docs/CHEATSHEET.md', 'M → L → S', 'Modeling Manage / Layer Settings path');
requireText('docs/CHEATSHEET.md', 'Selection Intent `0…4`', 'Selection Intent summary');
requireText(
  'docs/CHEATSHEET.md',
  'validate → health → запуск NX через managed launcher → bridge-status',
  'managed installation verification sequence',
);

requireText('docs/SELECTION_INTENT.md', '`0` | Reset', 'Selection Intent reset row');
requireText('docs/SELECTION_INTENT.md', '`4` | Inferred Path / Region Boundary', 'Selection Intent region row');
requireText('docs/SELECTION_INTENT.md', 'UG_SEL_CHAINING', 'native chaining control');
requireText('docs/SELECTION_INTENT.md', 'UI_CURVE_FINDER_TANGENT', 'native tangent selector');

requireText('docs/INSTALLATION.md', 'config\\nx2512-v8-profile.json', 'default installer v8 profile');
requireText('docs/CLI.md', 'nx2512-v8-profile.json', 'CLI v8 profile resolution');
requireText('docs/api.md', 'Authenticated', 'authenticated IPC description');
requireText('docs/SAFETY_MODEL.md', 'payload_hmac', 'HMAC security field');
requireText('NX2512_CommandBridge/README.md', 'Selection Intent', 'Bridge Selection Intent responsibility');
requireText('NX2512_ControlCenter/README.md', 'nx2512-v8-profile.json', 'Control Center current profile');

if (profileSchema !== null) {
  for (const file of [
    'README.md',
    'docs/RUNTIME_V8.md',
    'docs/CONFIGURATION.md',
    'docs/ARCHITECTURE.md',
    'docs/INSTALLATION.md',
    'NX2512_HotkeyStudio/README.md',
  ]) {
    requireAnyText(file, [`schema **${profileSchema}**`, `schema — **${profileSchema}**`, `schema | **${profileSchema}**`], `profile schema ${profileSchema}`);
  }
}

if (protocolSchema !== null) {
  for (const file of [
    'README.md',
    'docs/RUNTIME_V8.md',
    'docs/api.md',
    'docs/ARCHITECTURE.md',
    'docs/SAFETY_MODEL.md',
    'NX2512_CommandBridge/README.md',
  ]) {
    requireAnyText(file, [`schema **${protocolSchema}**`, `schema — **${protocolSchema}**`, `schema | **${protocolSchema}**`, `schema ${protocolSchema}`], `IPC schema ${protocolSchema}`);
  }
}

if (policyVersion !== null) {
  const audit = read('docs/audit/command-sequence-audit.md');
  const auditMatch = audit.match(/Sequence policy:\s*\*\*v(\d+)\*\*/);
  if (!auditMatch) {
    errors.push('docs/audit/command-sequence-audit.md: cannot determine generated policy version');
  } else if (Number(auditMatch[1]) !== policyVersion) {
    errors.push(`Generated sequence audit is stale: source policy v${policyVersion}, audit v${auditMatch[1]}`);
  }

  for (const file of ['README.md', 'docs/RUNTIME_V8.md', 'docs/CONFIGURATION.md', 'docs/ARCHITECTURE.md']) {
    requireAnyText(file, [`policy **v${policyVersion}**`, `policy | **v${policyVersion}**`, `policy v${policyVersion}`], `sequence policy v${policyVersion}`);
  }
}

// Current user-facing guides must not teach the pre-v8 Sketch line paths.
for (const file of ['README.md', 'docs/CHEATSHEET.md']) {
  requireNoText(file, 'CapsLock → C → L', 'pre-v8 Sketch line path CapsLock → C → L');
  requireNoText(file, 'CapsLock → C → G → L', 'pre-v8 Sketch line path CapsLock → C → G → L');
}

// Default installer/runtime resolution must continue to know the v8 profile filename.
requireText('install-nxkeys.ps1', 'nx2512-v8-profile.json', 'installer current v8 profile filename');
requireText('NX2512_HotkeyStudio/Program.cs', 'nx2512-v8-profile.json', 'HotkeyStudio v8 profile auto-resolution');

if (errors.length > 0) {
  console.error('Documentation validation failed:');
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log(`Documentation validation passed for ${canonicalFiles.length} canonical files.`);
console.log(`Contracts: profile schema ${profileSchema}, IPC schema ${protocolSchema}, sequence policy v${policyVersion}.`);
