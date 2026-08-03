#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[1]

# Preserve curated Sketch core when compiling the full profile. Source intents may enrich these
# rows by exact BUTTON ID, but must not be able to delete the verified runtime vocabulary.
full_path = root / "scripts" / "compile-full-command-map.mjs"
full_text = full_path.read_text(encoding="utf-8-sig")
old = """    set.commands = (set.commands ?? []).filter(command => isSupportCommand(command));"""
new = """    set.commands = (set.commands ?? []).filter(command =>
      isSupportCommand(command) || String(command?.path_source ?? '').toLowerCase() === 'sketch_curated');"""
if full_text.count(old) != 1:
    raise RuntimeError("compile-full-command-map Sketch pruning marker changed")
full_text = full_text.replace(old, new, 1)
full_path.write_text(full_text, encoding="utf-8", newline="\n")

# Preserve curated Sketch core when reducing the full profile to the operational K3-K5 profile.
# These commands form the interaction language itself and therefore are runtime infrastructure,
# even when a source hierarchy classified one of them as K1/K2 or omitted its catalog intent.
main_path = root / "scripts" / "compile-main-command-map.mjs"
main_text = main_path.read_text(encoding="utf-8-sig")
old = """        const runtimeSupport = isRuntimeSupport(command);\n\n        if (refs.length) {"""
new = """        const runtimeSupport = isRuntimeSupport(command);\n        const curatedSketchCore = module.id === 'sketch' &&\n          String(command.path_source ?? '').toLowerCase() === 'sketch_curated';\n\n        if (refs.length) {"""
if main_text.count(old) != 1:
    raise RuntimeError("compile-main-command-map support marker changed")
main_text = main_text.replace(old, new, 1)

old = """        // Selection filters and module switches are runtime infrastructure rather than catalog coverage.\n        if (runtimeSupport) {"""
new = """        // Verified core Sketch commands are runtime vocabulary, not optional catalog noise.\n        // Preserve them even when their source intent is K1/K2 or absent from the selected K3-K5 slice.\n        if (curatedSketchCore) {\n          command.catalog_refs = [];\n          command.profile_support = true;\n          command.frequency = 'support';\n          command.resolution_status = 'existing';\n          command.resolution_candidates = [];\n          command.support_kind = 'sketch_core';\n          command.notes = ['Verified Sketch core outside frequency filtering', command.notes].filter(Boolean).join(' | ');\n          if (fallback.startsWith('catalog:')) delete command.fallback;\n          delete command.catalog_backed_support;\n          return true;\n        }\n\n        // Selection filters and module switches are runtime infrastructure rather than catalog coverage.\n        if (runtimeSupport) {"""
if main_text.count(old) != 1:
    raise RuntimeError("compile-main-command-map runtime support marker changed")
main_text = main_text.replace(old, new, 1)
main_path.write_text(main_text, encoding="utf-8", newline="\n")

print("[sketch-intent-fix] Verified Sketch core survives full and K3-K5 compilation.")
