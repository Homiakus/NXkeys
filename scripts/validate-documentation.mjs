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
  'docs/CHEATSHEET.md',
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
    errors.push(`Missing required documentation file: ${relativePath}`);
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

function normalizeLinkTarget(sourceFile, rawTarget) {
  let target = rawTarget.trim();
  if (!target || target.startsWith('#')) return null;
  if (/^(?:https?:|mailto:|tel:|data:)/i.test(target)) return null;

  if (target.startsWith('<') && target.endsWith('>')) {
    target = target.slice(1, -1);
  }

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

for (const file of canonicalFiles) {
  const content = read(file);
  if (content) validateLinks(file, content);
}

requireText('README.md', 'docs/CHEATSHEET.md', 'link to the detailed cheat sheet');
requireText('README.md', 'Sketch Intent Grammar', 'Sketch workflow badge');
requireText('docs/README.md', 'CHEATSHEET.md', 'cheat sheet entry');
requireText('DEVELOPMENT.md', 'NX2512_HotkeyStudio.Tests', 'HotkeyStudio regression test command');
requireText('CONTRIBUTING.md', 'MnemonicPathGenerator.Sketch.cs', 'Sketch change matrix entry');
requireText('CONTRIBUTING.md', 'пяти токенов', 'Sketch five-token exception');
requireText('docs/SKETCH_INTENT_LANGUAGE.md', 'C → G → V', 'Sketch variant branch');
requireText('docs/CHEATSHEET.md', 'C → G → L', 'Sketch line path');
requireText('docs/CHEATSHEET.md', 'validate → health → запуск NX → bridge-status', 'installation verification sequence');

const policySource = read('scripts/sequence-policy.mjs');
const policyMatch = policySource.match(/SEQUENCE_POLICY_VERSION\s*=\s*(\d+)/);
if (!policyMatch) {
  errors.push('scripts/sequence-policy.mjs: cannot determine SEQUENCE_POLICY_VERSION');
} else {
  const audit = read('docs/audit/command-sequence-audit.md');
  const auditMatch = audit.match(/Sequence policy:\s*\*\*v(\d+)\*\*/);
  if (!auditMatch) {
    errors.push('docs/audit/command-sequence-audit.md: cannot determine generated policy version');
  } else if (auditMatch[1] !== policyMatch[1]) {
    errors.push(
      `Generated sequence audit is stale: source policy v${policyMatch[1]}, audit v${auditMatch[1]}`,
    );
  }
}

const sketchDoc = read('docs/SKETCH_INTENT_LANGUAGE.md');
for (const pathToken of ['C → G → L', 'C → G → R', 'C → G → C', 'C → G → A', 'E → G → T', 'E → G → E', 'T → G → O']) {
  if (!sketchDoc.includes(pathToken)) {
    errors.push(`docs/SKETCH_INTENT_LANGUAGE.md: missing canonical path ${pathToken}`);
  }
}

if (errors.length > 0) {
  console.error('Documentation validation failed:');
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log(`Documentation validation passed for ${canonicalFiles.length} canonical files.`);
