import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '..');

const profilePath = path.join(rootDir, 'config', 'nx2512-v8-profile.json');
const lockPath = path.join(rootDir, 'config', 'nx2512-capability-route-lock.json');

function fail(msg) {
  console.error('[capability-route-lock] ERROR:', msg);
  process.exit(1);
}

const profile = JSON.parse(fs.readFileSync(profilePath, 'utf8'));
if (!profile.operations || !Array.isArray(profile.operations)) {
  fail('Invalid v8 profile: missing operations array.');
}

const generate = process.argv.includes('--generate');

const compiledCapabilities = [];
for (const [index, op] of profile.operations.entries()) {
  const opId = op.operation_id || '';
  const kind = op.adapter?.kind || '';
  const adapterValue = op.adapter?.value || '';
  const buttonId = kind === 'button_id' ? adapterValue : '';
  const apps = op.availability?.applications || ['global'];
  const appScope = apps[0] || 'global';
  const action = op.action || (buttonId ? 'execute_command' : 'local_behavior');
  const risk = op.risk || (['assemblies.remove_component', 'manufacturing.delete_operation'].includes(opId) ? 'destructive' : 'safe');
  const confirm = op.confirmation_required || (risk === 'destructive');

  const leaderTokens = op.paths?.leader || [];
  const primaryLeader = leaderTokens.join(' ');
  const aliases = op.paths?.secondary_aliases || [];
  const direct = op.paths?.direct || '';
  const workspaceKey = op.paths?.workspace_key || '';

  compiledCapabilities.push({
    source_index: index,
    operation_id: opId,
    command_name: op.command_name || '',
    application_scope: appScope,
    action,
    adapter_kind: kind,
    adapter_value: adapterValue,
    button_id: buttonId,
    risk,
    confirmation_required: confirm,
    routes: {
      leader_primary: primaryLeader,
      leader_aliases: aliases,
      direct,
      workspace_key: workspaceKey
    },
    status: buttonId ? 'implemented' : (op.adapter?.status || 'target_only')
  });
}

if (compiledCapabilities.some(capability => !capability.operation_id)) {
  fail('Every canonical v8 capability must have a non-empty operation_id.');
}

const groupsByOperationId = new Map();
for (const capability of compiledCapabilities) {
  const key = capability.operation_id.toLowerCase();
  if (!groupsByOperationId.has(key)) groupsByOperationId.set(key, []);
  groupsByOperationId.get(key).push(capability);
}
const duplicateGroups = [...groupsByOperationId.values()].filter(group => group.length > 1);
if (duplicateGroups.length > 0) {
  console.error(`[capability-route-lock] Found ${duplicateGroups.length} duplicate operation_id group(s):`);
  for (const group of duplicateGroups.sort((a, b) => a[0].operation_id.localeCompare(b[0].operation_id))) {
    console.error(`[capability-route-lock] DUPLICATE ${group[0].operation_id} x${group.length}`);
    for (const capability of group) {
      console.error(JSON.stringify({
        source_index: capability.source_index,
        operation_id: capability.operation_id,
        command_name: capability.command_name,
        application_scope: capability.application_scope,
        action: capability.action,
        adapter_kind: capability.adapter_kind,
        adapter_value: capability.adapter_value,
        routes: capability.routes
      }));
    }
  }
  fail('Canonical v8 capability operation_id values must be unique until an explicit composite identity contract is defined end-to-end.');
}

compiledCapabilities.sort((a, b) => a.operation_id.localeCompare(b.operation_id));
const canonicalCapabilities = compiledCapabilities.map(({ source_index, adapter_kind, adapter_value, ...capability }) => capability);

const lockData = {
  schema_version: 1,
  profile_name: profile.profile?.name || 'NX Adaptive Modules 2512.6000 v8',
  total_operations: canonicalCapabilities.length,
  executable_button_count: canonicalCapabilities.filter(c => c.button_id).length,
  capabilities: canonicalCapabilities
};

if (generate) {
  fs.writeFileSync(lockPath, JSON.stringify(lockData, null, 2) + '\n', 'utf8');
  console.log(`[capability-route-lock] Generated optional lock file with ${canonicalCapabilities.length} capabilities (${lockData.executable_button_count} executable).`);
} else if (!fs.existsSync(lockPath)) {
  console.log(`[capability-route-lock] OK: canonical v8 profile contains ${canonicalCapabilities.length} unique operations (${lockData.executable_button_count} executable); optional lock file is absent.`);
} else {
  const existingLock = JSON.parse(fs.readFileSync(lockPath, 'utf8'));
  if (existingLock.total_operations !== lockData.total_operations) {
    fail(`Total operations mismatch: profile has ${lockData.total_operations}, lock has ${existingLock.total_operations}.`);
  }
  if (existingLock.executable_button_count !== lockData.executable_button_count) {
    fail(`Executable button count mismatch: profile has ${lockData.executable_button_count}, lock has ${existingLock.executable_button_count}.`);
  }
  if (!Array.isArray(existingLock.capabilities) || existingLock.capabilities.length !== lockData.capabilities.length) {
    fail('Capability lock must contain the same number of capability records as the canonical profile.');
  }
  const canonical = JSON.stringify(lockData.capabilities);
  const locked = JSON.stringify([...existingLock.capabilities].sort((a, b) => String(a.operation_id).localeCompare(String(b.operation_id))));
  if (canonical !== locked) {
    fail('Capability lock records differ from the canonical v8 profile. Regenerate intentionally with --generate.');
  }
  console.log(`[capability-route-lock] OK: ${existingLock.total_operations} operations (${existingLock.executable_button_count} executable) match the optional lock.`);
}
