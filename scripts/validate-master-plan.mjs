import fs from 'node:fs';
import path from 'node:path';

const REQUIRED_HEADINGS = [
  '# 1. Mission',
  '# 2. Current State',
  '# 3. Architecture Map',
  '# 4. Baseline',
  '# 5. System Invariants',
  '# 6. Findings Registry',
  '# 7. Risk Register',
  '# 8. Pareto Improvements',
  '# 9. Dependency DAG',
  '# 10. Implementation Phases',
  '# 11. Atomic Tasks',
  '# 12. Testing Strategy',
  '# 13. Mutation Testing Strategy',
  '# 14. Performance Baselines',
  '# 15. Security Hardening',
  '# 16. Migration Strategy',
  '# 17. Deferred Work',
  '# 18. Rejected Decisions',
  '# 19. Completed Tasks',
  '# 20. Iteration Log',
  '# 21. Definition of Final Done',
];

const TASK_STATUSES = new Set([
  'TODO',
  'READY',
  'PLANNED',
  'IN_PROGRESS',
  'VERIFYING',
  'BLOCKED',
  'DONE',
  'DEFERRED',
  'REJECTED',
]);
const PRIORITIES = new Set(['P0', 'P1', 'P2', 'P3']);
const TASK_TYPES = new Set(['FIX', 'HARDEN', 'REMOVE', 'IMPROVE', 'FEATURE', 'DOC', 'RESEARCH']);
const LEVERAGE = new Set(['HIGH', 'MEDIUM', 'LOW']);
const SEVERITIES = new Set(['Critical', 'High', 'Medium', 'Low']);
const CONFIDENCE = new Set(['Confirmed', 'Strong', 'Tentative']);

function countExactLine(text, line) {
  return text.split(/\r?\n/).filter((candidate) => candidate.trimEnd() === line).length;
}

function sectionBody(text, heading, nextHeading) {
  const start = text.indexOf(`${heading}\n`);
  if (start < 0) return '';
  const bodyStart = start + heading.length + 1;
  if (!nextHeading) return text.slice(bodyStart);
  const end = text.indexOf(`${nextHeading}\n`, bodyStart);
  return end < 0 ? text.slice(bodyStart) : text.slice(bodyStart, end);
}

function collectIds(text, prefix) {
  const regex = new RegExp(`^## (${prefix}-\\d{3})\\s+—\\s+.+$`, 'gm');
  return [...text.matchAll(regex)].map((match) => match[1]);
}

function duplicateIds(ids) {
  const seen = new Set();
  const duplicates = new Set();
  for (const id of ids) {
    if (seen.has(id)) duplicates.add(id);
    seen.add(id);
  }
  return [...duplicates];
}

function taskSections(text) {
  const matches = [...text.matchAll(/^## (T-\d{3})\s+—\s+.+$/gm)];
  return matches.map((match, index) => {
    const start = match.index ?? 0;
    const end = index + 1 < matches.length ? matches[index + 1].index : text.length;
    return { id: match[1], body: text.slice(start, end) };
  });
}

function findingSections(text) {
  const findings = sectionBody(text, '# 6. Findings Registry', '# 7. Risk Register');
  const matches = [...findings.matchAll(/^## (F-\d{3})\s+—\s+.+$/gm)];
  return matches.map((match, index) => {
    const start = match.index ?? 0;
    const end = index + 1 < matches.length ? matches[index + 1].index : findings.length;
    return { id: match[1], body: findings.slice(start, end) };
  });
}

function metadataValue(body, label) {
  const escaped = label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const match = body.match(new RegExp(`\\*\\*${escaped}:\\*\\*\\s*([^|\\n]+)`));
  return match?.[1]?.trim() ?? null;
}

export function validateMasterPlan(text) {
  const errors = [];

  for (const heading of REQUIRED_HEADINGS) {
    const count = countExactLine(text, heading);
    if (count !== 1) errors.push(`heading:${heading}:expected-once:actual-${count}`);
  }

  for (let i = 0; i < REQUIRED_HEADINGS.length - 1; i += 1) {
    const current = text.indexOf(`${REQUIRED_HEADINGS[i]}\n`);
    const next = text.indexOf(`${REQUIRED_HEADINGS[i + 1]}\n`);
    if (current >= 0 && next >= 0 && current >= next) {
      errors.push(`heading-order:${REQUIRED_HEADINGS[i]}:${REQUIRED_HEADINGS[i + 1]}`);
    }
  }

  const findingIds = collectIds(sectionBody(text, '# 6. Findings Registry', '# 7. Risk Register'), 'F');
  const taskIds = collectIds(sectionBody(text, '# 11. Atomic Tasks', '# 12. Testing Strategy'), 'T');

  if (findingIds.length === 0) errors.push('findings:none');
  if (taskIds.length === 0) errors.push('tasks:none');
  for (const id of duplicateIds(findingIds)) errors.push(`finding-id:duplicate:${id}`);
  for (const id of duplicateIds(taskIds)) errors.push(`task-id:duplicate:${id}`);

  for (const { id, body } of findingSections(text)) {
    const severity = metadataValue(body, 'Severity');
    const confidence = metadataValue(body, 'Confidence');
    if (!severity) errors.push(`finding:${id}:missing-severity`);
    else if (!SEVERITIES.has(severity)) errors.push(`finding:${id}:invalid-severity:${severity}`);
    if (!confidence) errors.push(`finding:${id}:missing-confidence`);
    else if (!CONFIDENCE.has(confidence)) errors.push(`finding:${id}:invalid-confidence:${confidence}`);
  }

  for (const { id, body } of taskSections(sectionBody(text, '# 11. Atomic Tasks', '# 12. Testing Strategy'))) {
    const status = metadataValue(body, 'Status');
    const priority = metadataValue(body, 'Priority');
    const type = metadataValue(body, 'Type');
    const leverage = metadataValue(body, 'Leverage');

    if (!status) errors.push(`task:${id}:missing-status`);
    else if (!TASK_STATUSES.has(status)) errors.push(`task:${id}:invalid-status:${status}`);
    if (!priority) errors.push(`task:${id}:missing-priority`);
    else if (!PRIORITIES.has(priority)) errors.push(`task:${id}:invalid-priority:${priority}`);
    if (!type) errors.push(`task:${id}:missing-type`);
    else if (!TASK_TYPES.has(type)) errors.push(`task:${id}:invalid-type:${type}`);
    if (!leverage) errors.push(`task:${id}:missing-leverage`);
    else if (!LEVERAGE.has(leverage)) errors.push(`task:${id}:invalid-leverage:${leverage}`);
  }

  const iterationLog = sectionBody(text, '# 20. Iteration Log', '# 21. Definition of Final Done');
  if (!/^## Iteration\s+\d+\b/m.test(iterationLog)) errors.push('iteration-log:no-iteration-entry');

  return errors;
}

function requireFailure(name, candidate, expectedFragment) {
  const errors = validateMasterPlan(candidate);
  if (errors.length === 0) throw new Error(`self-test:${name}:validator-accepted-invalid-plan`);
  if (!errors.some((error) => error.includes(expectedFragment))) {
    throw new Error(`self-test:${name}:expected-${expectedFragment}:actual-${errors.join(',')}`);
  }
}

function runSelfTests(validText) {
  const baselineErrors = validateMasterPlan(validText);
  if (baselineErrors.length > 0) {
    throw new Error(`self-test:baseline-invalid:${baselineErrors.join(',')}`);
  }

  requireFailure(
    'missing-heading',
    validText.replace('# 7. Risk Register', '# 7. Risk Archive'),
    'heading:# 7. Risk Register',
  );

  const firstFinding = validText.match(/^## (F-\d{3})\s+—/m)?.[1];
  const secondFinding = [...validText.matchAll(/^## (F-\d{3})\s+—/gm)][1]?.[1];
  if (!firstFinding || !secondFinding) throw new Error('self-test:need-two-findings');
  requireFailure(
    'duplicate-finding-id',
    validText.replace(`## ${secondFinding} —`, `## ${firstFinding} —`),
    `finding-id:duplicate:${firstFinding}`,
  );

  requireFailure(
    'invalid-task-status',
    validText.replace('**Status:** VERIFYING', '**Status:** UNKNOWN'),
    'invalid-status:UNKNOWN',
  );

  const iterationLog = sectionBody(validText, '# 20. Iteration Log', '# 21. Definition of Final Done');
  requireFailure(
    'missing-iteration-entry',
    validText.replace(iterationLog, '\nNo executable iteration history.\n\n'),
    'iteration-log:no-iteration-entry',
  );

  console.log('[master-plan] self-test OK: malformed plans are rejected deterministically.');
}

function main() {
  const args = process.argv.slice(2);
  const selfTest = args.includes('--self-test');
  const positional = args.filter((arg) => arg !== '--self-test');
  const planPath = path.resolve(positional[0] ?? 'MASTER_PLAN.md');

  if (!fs.existsSync(planPath)) {
    console.error(`[master-plan] missing file: ${planPath}`);
    process.exitCode = 1;
    return;
  }

  const text = fs.readFileSync(planPath, 'utf8');
  const errors = validateMasterPlan(text);
  if (errors.length > 0) {
    console.error('[master-plan] INVALID');
    for (const error of errors) console.error(` - ${error}`);
    process.exitCode = 1;
    return;
  }

  if (selfTest) runSelfTests(text);
  console.log(`[master-plan] OK: ${path.relative(process.cwd(), planPath) || planPath}`);
}

main();
