from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8-sig")


def write(relative: str, content: str) -> None:
    (ROOT / relative).write_text(content.replace("\r\n", "\n"), encoding="utf-8", newline="\n")


def replace(relative: str, old: str, new: str) -> None:
    content = read(relative)
    if old not in content:
        raise RuntimeError(f"Missing ergonomic v7 phase-2 marker in {relative}: {old[:120]!r}")
    write(relative, content.replace(old, new, 1))


replace(
    "scripts/sequence-policy.mjs",
    "  support: 2\n};",
    "  support: 2,\n  ergonomic: 3\n};",
)

replace(
    "scripts/compile-full-command-map.mjs",
    "    command.__priority = isSupportCommand(command) ? -2 : locked ? -1 : curatedPath?.length ? 0\n      : (String(command.fallback ?? '').startsWith('catalog:') ? 2 : 1);",
    "    const ergonomicCore = command.profile_support === true || command.support_kind === 'ergonomic_core';\n    command.__priority = isSupportCommand(command) ? -3 : ergonomicCore ? -2 : locked ? -1 : curatedPath?.length ? 0\n      : (String(command.fallback ?? '').startsWith('catalog:') ? 2 : 1);",
)

replace(
    "scripts/compile-main-command-map.mjs",
    "      command.frequency = 'support';",
    "      command.frequency = command.support_kind === 'ergonomic_core' ? 'ergonomic' : 'support';",
)

replace(
    "scripts/validate-main-command-map.mjs",
    "        if (command.frequency !== 'support') fail(`Pure support command has invalid frequency marker: ${module.id}/${command.command?.name}.`);",
    "        const expectedSupportFrequency = command.support_kind === 'ergonomic_core' ? 'ergonomic' : 'support';\n        if (command.frequency !== expectedSupportFrequency) fail(`Pure support command has invalid frequency marker: ${module.id}/${command.command?.name}/${command.frequency}.`);",
)

# Ensure the C# runtime reserves ergonomic-core paths before ordinary generated rows too.
replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    "            if (string.Equals(command?.SupportKind, \"module_switch\", StringComparison.OrdinalIgnoreCase)) return 1;\n            if (string.Equals(command?.Action, \"set_selection_filter\", StringComparison.OrdinalIgnoreCase)) return 0;",
    "            if (string.Equals(command?.SupportKind, \"module_switch\", StringComparison.OrdinalIgnoreCase)) return 1;\n            if (string.Equals(command?.SupportKind, \"ergonomic_core\", StringComparison.OrdinalIgnoreCase)) return 2;\n            if (string.Equals(command?.Action, \"set_selection_filter\", StringComparison.OrdinalIgnoreCase)) return 0;",
)
replace(
    "NX2512_HotkeyStudio/Models/MnemonicPathGenerator.cs",
    "            return 2;\n        }\n\n        private static bool IsSupport(ModuleCommand command) => SupportPriority(command) < 2;",
    "            return 3;\n        }\n\n        private static bool IsSupport(ModuleCommand command) => SupportPriority(command) < 2;",
)

(ROOT / "scripts/fix2-ergonomic-v7.py").unlink(missing_ok=True)
print("Ergonomic v7 phase-2 priority fixups applied.")
