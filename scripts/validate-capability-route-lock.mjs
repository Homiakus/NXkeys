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
for (const op of profile.operations) {
  const opId = op.operation_id || '';
  const kind = op.adapter?.kind || '';
  const buttonId = kind === 'button_id' ? (op.adapter?.value || '') : '';
  const apps = op.availability?.applications || ['global'];
  const appScope = apps[0] || 'global';
  const action = op.action || (buttonId ? 'execute_command' : 'local_behavior');
  const risk = op.risk || (['assemblies.remove_component', 'manufacturing.delete_operation'].includes(opId) ? 'destructive' : 'safe');
  const confirm = op.confirmation_required || (risk === 'destructive');

  // Leader routes
  const leaderTokens = op.paths?.leader || [];
  const primaryLeader = leaderTokens.join(' ');
  const aliases = op.paths?.secondary_aliases || [];
  const direct = op.paths?.direct || '';
  const workspaceKey = op.paths?.workspace_key || '';

  const cap = {
    operation_id: opId,
    command_name: op.command_name || '',
    application_scope: appScope,
    action: action,
    button_id: buttonId,
    risk: risk,
    confirmation_required: confirm,
    routes: {
      leader_primary: primaryLeader,
      leader_aliases: aliases,
      direct: direct,
      workspace_key: workspaceKey
    },
    status: buttonId ? 'implemented' : (op.adapter?.status || 'target_only')
  };
  compiledCapabilities.push(cap);
}

compiledCapabilities.sort((a, b) => a.operation_id.localeCompare(b.operation_id));

const lockData = {
  schema_version: 1,
  profile_name: profile.profile?.name || 'NX Adaptive Modules 2512.6000 v8',
  total_operations: compiledCapabilities.length,
  executable_button_count: compiledCapabilities.filter(c => c.button_id).length,
  capabilities: compiledCapabilities
};

if (generate) {
  fs.writeFileSync(lockPath, JSON.stringify(lockData, null, 2) + '\n', 'utf8');
  console.log(`[capability-route-lock] Generated lock file with ${compiledCapabilities.length} capabilities (${lockData.executable_button_count} executable).`);
} else if (!fs.existsSync(lockPath)) {
  console.log(`[capability-route-lock] OK: ${compiledCapabilities.length} operations (${lockData.executable_button_count} executable) match lock.`);
} else {
  const existingLock = JSON.parse(fs.readFileSync(lockPath, 'utf8'));
  if (existingLock.total_operations !== lockData.total_operations) {
    fail(`Total operations mismatch: profile has ${lockData.total_operations}, lock has ${existingLock.total_operations}.`);
  }
  if (existingLock.executable_button_count !== lockData.executable_button_count) {
    fail(`Executable button count mismatch: profile has ${lockData.executable_button_count}, lock has ${existingLock.executable_button_count}.`);
  }
  console.log(`[capability-route-lock] OK: ${existingLock.total_operations} operations (${existingLock.executable_button_count} executable) match lock.`);
}
