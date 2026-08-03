#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[1]
path = root / "scripts" / "validate-command-tree.mjs"
text = path.read_text(encoding="utf-8-sig")
old_import = "  targetLengthForFrequency\n} from \"./sequence-policy.mjs\";"
new_import = "  targetLengthForCommand\n} from \"./sequence-policy.mjs\";"
if text.count(old_import) != 1:
    raise RuntimeError("validate-command-tree import marker changed")
text = text.replace(old_import, new_import, 1)
old_check = "if (item.frequency && !item.support_kind && (item.path ?? []).length > targetLengthForFrequency(item.frequency))"
new_check = "if (item.frequency && !item.support_kind && (item.path ?? []).length > targetLengthForCommand(module.id, item))"
if text.count(old_check) != 1:
    raise RuntimeError("validate-command-tree length marker changed")
text = text.replace(old_check, new_check, 1)
path.write_text(text, encoding="utf-8", newline="\n")
print("[sketch-intent-fix] Command-tree validator now uses command-aware path limits.")
