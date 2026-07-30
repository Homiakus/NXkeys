from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8-sig")


def write(relative: str, content: str) -> None:
    (ROOT / relative).write_text(content.replace("\r\n", "\n"), encoding="utf-8", newline="\n")


def replace(relative: str, old: str, new: str) -> None:
    content = read(relative)
    if old not in content:
        raise RuntimeError(f"Missing ergonomic v7 phase-3 marker in {relative}: {old!r}")
    write(relative, content.replace(old, new, 1))


replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    '            Add("UG_SKETCH_ARC_FROM_CENTER", "C A");',
    '            Add("UG_SKETCH_ARC_FROM_CENTER", "C M A");',
)

profile_path = ROOT / "config/nx2512-pro-hybrid.json"
profile = json.loads(profile_path.read_text(encoding="utf-8-sig"))
found = False
for module in profile.get("modules", []):
    for command_set in module.get("command_sets", []):
        for command in command_set.get("commands", []):
            if command.get("command", {}).get("id") == "UG_SKETCH_ARC_FROM_CENTER":
                command["path"] = ["C", "M", "A"]
                command["path_labels"] = []
                found = True
if not found:
    raise RuntimeError("UG_SKETCH_ARC_FROM_CENTER is absent from bootstrap profile")
profile_path.write_text(json.dumps(profile, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")

replace(
    "scripts/validate-ergonomic-map.mjs",
    "UG_SKETCH_ARC_BY_THREE_POINTS: 'CTA', UG_SKETCH_ARC_FROM_CENTER: 'CA',",
    "UG_SKETCH_ARC_BY_THREE_POINTS: 'CTA', UG_SKETCH_ARC_FROM_CENTER: 'CMA',",
)

for relative in ["docs/ERGONOMIC_COMMAND_MAP.md", "docs/MNEMONIC_COMMAND_LANGUAGE.md"]:
    path = ROOT / relative
    if not path.exists():
        continue
    content = path.read_text(encoding="utf-8-sig")
    content = content.replace("`CA` arc", "`CMA` arc from center")
    content = content.replace("C G A    Arc", "C M A    Arc from Center")
    path.write_text(content, encoding="utf-8", newline="\n")

(ROOT / "scripts/fix3-ergonomic-v7.py").unlink(missing_ok=True)
print("Ergonomic v7 phase-3 Sketch arc path applied.")
