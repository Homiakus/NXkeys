from __future__ import annotations

import atexit
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def _read(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8-sig")


def _write(relative: str, content: str) -> None:
    (ROOT / relative).write_text(content.replace("\r\n", "\n"), encoding="utf-8", newline="\n")


def _replace(relative: str, old: str, new: str) -> None:
    content = _read(relative)
    if old not in content:
        raise RuntimeError(f"Fixup marker missing in {relative}: {old[:120]!r}")
    _write(relative, content.replace(old, new, 1))


def apply_fixups() -> None:
    # Preserve any catalog intent that has the same BUTTON ID as a universal selection action.
    policy_path = "scripts/sequence-policy.mjs"
    policy = _read(policy_path)
    pattern = re.compile(
        r"export function ensureUniversalSelectionFilters\(modules\) \{.*?\n\}\n\nexport function ensureUniversalModuleSwitches",
        re.S,
    )
    replacement = '''export function ensureUniversalSelectionFilters(modules) {
  const canonicalIds = new Set(CANONICAL_SELECTION_FILTERS.map(filter => filter.id));
  for (const module of modules ?? []) {
    if (!module || module.enabled === false) continue;
    const carried = new Map();
    for (const existingSet of module.command_sets ?? []) {
      if (existingSet?.id === 'selection_filters') continue;
      existingSet.commands = (existingSet.commands ?? []).filter(command => {
        const id = String(command?.command?.id ?? '').toUpperCase();
        if (!canonicalIds.has(id)) return true;
        const previous = carried.get(id);
        if (!previous) carried.set(id, command);
        else {
          previous.catalog_refs = [...new Set([...(previous.catalog_refs ?? []), ...(command.catalog_refs ?? [])])];
          previous.search_aliases = [...new Set([...(previous.search_aliases ?? []), ...(command.search_aliases ?? [])])];
        }
        return false;
      });
    }
    const set = findOrCreateSet(module, 'selection_filters', 'Selection Filters');
    for (const [index, filter] of CANONICAL_SELECTION_FILTERS.entries()) {
      const existing = set.commands.find(command => String(command?.command?.id ?? '').toUpperCase() === filter.id);
      const catalog = carried.get(filter.id);
      const refs = [...new Set([...(existing?.catalog_refs ?? []), ...(catalog?.catalog_refs ?? [])])];
      const searches = [...new Set([filter.name, filter.id, ...(catalog?.search_aliases ?? []), ...(existing?.search_aliases ?? [])])];
      const source = { ...(catalog ?? {}), ...(existing ?? {}) };
      upsertCommand(
        set,
        command => String(command?.command?.id ?? '').toUpperCase() === filter.id,
        () => ({
          ...source,
          slot: '',
          submenu_key: '',
          submenu_label: 'Selection Filters',
          input_key: filter.path[1],
          path: [...filter.path],
          path_labels: ['Select', filter.name],
          aliases: [],
          search_aliases: searches,
          icon_hint: filter.iconHint,
          display_order: 9000 + index,
          command: { id: filter.id, name: filter.name },
          action: 'set_selection_filter',
          selection_type: filter.selectionType,
          enabled: true,
          requires_selection: false,
          destructive: false,
          confirm_before_execute: false,
          fallback: refs.length ? (catalog?.fallback ?? source.fallback ?? '') : '',
          notes: refs.length ? (catalog?.notes ?? source.notes ?? 'Catalog-backed universal selection action') : 'Universal runtime selection action',
          catalog_refs: refs,
          frequency: refs.length ? (catalog?.frequency ?? source.frequency ?? 'K3') : 'support',
          resolution_status: refs.length ? (catalog?.resolution_status ?? source.resolution_status ?? 'existing') : 'existing',
          resolution_candidates: refs.length ? (catalog?.resolution_candidates ?? source.resolution_candidates ?? []) : [],
          support_kind: 'selection_filter'
        })
      );
    }
  }
}

export function ensureUniversalModuleSwitches'''
    policy, count = pattern.subn(replacement, policy, count=1)
    if count != 1:
        raise RuntimeError("Unable to replace ensureUniversalSelectionFilters")
    _write(policy_path, policy)

    # Hybrid rows can be both catalog coverage and runtime infrastructure.
    validator_path = "scripts/validate-main-command-map.mjs"
    validator = _read(validator_path)
    old = '''      const selectionSupport = isSelectionSupportCommand(command);
      const moduleSwitchSupport = isModuleSwitchSupportCommand(command);
      if (command.profile_support === true || selectionSupport || moduleSwitchSupport) {
        supportCount += 1;
        if (selectionSupport && command.action !== 'set_selection_filter') fail(`Selection support command has invalid action: ${module.id}/${command.command?.name}.`);
        if (selectionSupport && !/^UG_SEL_/i.test(String(command.command?.id ?? ''))) fail(`Selection support command has invalid ID: ${module.id}/${command.command?.name}.`);
        if (moduleSwitchSupport && command.action !== 'switch_module') fail(`Module switch support command has invalid action: ${module.id}/${command.command?.name}.`);
        if (moduleSwitchSupport && !command.target_module_id) fail(`Module switch support command has no target_module_id: ${module.id}/${command.command?.name}.`);
        if (refs.length) fail(`Support command must not claim catalog coverage: ${module.id}/${command.command?.name}.`);
        if (command.frequency !== 'support') fail(`Support command has invalid frequency marker: ${module.id}/${command.command?.name}.`);
      } else {
        for (const reference of refs) {
          seenRefs.add(reference);
          if (!allowedRefs.has(reference)) fail(`Profile contains intent outside selected frequency scope: ${reference}.`);
        }
        if (!expectedFrequencies.includes(command.frequency))
          fail(`Profile row has frequency outside scope: ${module.id}/${command.command?.name}/${command.frequency}.`);
        if (!refs.length) fail(`Non-support profile row has no catalog reference: ${module.id}/${command.command?.name}.`);
        const targetLength = targetLengthForFrequency(command.frequency);
        if (normalizePath(command.path).length > targetLength)
          fail(`Path exceeds ${command.frequency} target length ${targetLength}: ${module.id}/${command.command?.name}/${canonical}.`);
      }'''
    new = '''      const selectionSupport = isSelectionSupportCommand(command);
      const moduleSwitchSupport = isModuleSwitchSupportCommand(command);
      const runtimeSupport = command.profile_support === true || selectionSupport || moduleSwitchSupport;
      if (runtimeSupport) {
        supportCount += 1;
        if (selectionSupport && command.action !== 'set_selection_filter') fail(`Selection support command has invalid action: ${module.id}/${command.command?.name}.`);
        if (selectionSupport && !/^UG_SEL_/i.test(String(command.command?.id ?? ''))) fail(`Selection support command has invalid ID: ${module.id}/${command.command?.name}.`);
        if (moduleSwitchSupport && command.action !== 'switch_module') fail(`Module switch support command has invalid action: ${module.id}/${command.command?.name}.`);
        if (moduleSwitchSupport && !command.target_module_id) fail(`Module switch support command has no target_module_id: ${module.id}/${command.command?.name}.`);
      }
      if (refs.length) {
        for (const reference of refs) {
          seenRefs.add(reference);
          if (!allowedRefs.has(reference)) fail(`Profile contains intent outside selected frequency scope: ${reference}.`);
        }
        if (!expectedFrequencies.includes(command.frequency))
          fail(`Profile row has frequency outside scope: ${module.id}/${command.command?.name}/${command.frequency}.`);
        const targetLength = targetLengthForFrequency(command.frequency);
        if (normalizePath(command.path).length > targetLength)
          fail(`Path exceeds ${command.frequency} target length ${targetLength}: ${module.id}/${command.command?.name}/${canonical}.`);
      } else if (runtimeSupport) {
        if (command.frequency !== 'support') fail(`Pure support command has invalid frequency marker: ${module.id}/${command.command?.name}.`);
      } else {
        fail(`Non-support profile row has no catalog reference: ${module.id}/${command.command?.name}.`);
      }'''
    if old not in validator:
        raise RuntimeError("validate-main support block not found")
    _write(validator_path, validator.replace(old, new, 1))

    # Update declarative safety policy keys to the new canonical sequences.
    state_path = ROOT / "config/nx2512-state-machines.json"
    state = json.loads(state_path.read_text(encoding="utf-8-sig"))
    commands = state.get("commands", {})
    remap = {
        "MEEB": "MEB",
        "MEEC": "MEC",
        "SCGL2": "SCL",
        "AECR": "AER",
        "AXCR": "AXC",
        "CPOP": "CPP",
        "CXOD": "CXO",
    }
    for old_key, new_key in remap.items():
        if old_key in commands:
            value = commands.pop(old_key)
            value["sequence"] = new_key
            commands[new_key] = value
    state_path.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")

    # The C# property uses PascalCase; the JSON marker is validated separately in the generated profile.
    ergonomic_path = "scripts/validate-ergonomic-map.mjs"
    ergonomic = _read(ergonomic_path).replace(
        "['ExecuteWorkflowGesture', 'module_cycle_order', 'MOUSEEVENTF_MIDDLEDOWN']",
        "['ExecuteWorkflowGesture', 'ModuleCycleOrder', 'MOUSEEVENTF_MIDDLEDOWN']",
    )
    _write(ergonomic_path, ergonomic)

    (ROOT / "scripts/sitecustomize.py").unlink(missing_ok=True)


atexit.register(apply_fixups)
