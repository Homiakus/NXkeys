from pathlib import Path
import re

root = Path.cwd()


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8-sig")


def write(path: str, content: str) -> None:
    target = root / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content.replace("\r\n", "\n"), encoding="utf-8", newline="\n")


def replace_exact(path: str, old: str, new: str, expected: int = 1) -> None:
    text = read(path)
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f"{path}: expected {expected} exact matches, found {count}: {old[:120]!r}")
    write(path, text.replace(old, new))


def replace_regex(path: str, pattern: str, replacement: str, expected: int = 1) -> None:
    text = read(path)
    updated, count = re.subn(pattern, lambda _: replacement, text, flags=re.S)
    if count != expected:
        raise RuntimeError(f"{path}: expected {expected} regex matches, found {count}: {pattern[:120]!r}")
    write(path, updated)


write(
    "NX2512_HotkeyStudio/Services/NxRibbonLayout.cs",
    r'''using System.Collections.Generic;
using System.IO;

namespace NX2512_HotkeyStudio.Services
{
    /// <summary>
    /// Defines the single canonical NX ribbon location and the legacy files that must be removed.
    /// Loading the same tab from startup and application/profiles/All creates duplicate NXKeys tabs.
    /// </summary>
    public static class NxRibbonLayout
    {
        public static string CanonicalRelativePath =>
            Path.Combine("custom", "application", "profiles", "All", "rbn_nxkeys.rtb");

        public static IReadOnlyList<string> LegacyRelativePaths { get; } = new[]
        {
            Path.Combine("custom", "startup", "nxkeys_ribbon.rtb")
        };

        public static string BuildTabFile(int toolbarVersion) =>
            "! NXKeys launch ribbon\r\n" +
            "TITLE NXKeys\r\n" +
            "VERSION " + toolbarVersion + "\r\n" +
            "BEGIN_GROUP NXKEYS_RBN_GROUP\r\n" +
            "LABEL NXKeys\r\n" +
            "BITMAP finished_flag\r\n" +
            "    BUTTON UG_NXKEYS_START_BRIDGE\r\n" +
            "    BUTTON UG_NXKEYS_START_DAEMON\r\n" +
            "    BUTTON UG_NXKEYS_OPEN_STUDIO\r\n" +
            "END_GROUP\r\n";
    }
}
''')

replace_exact(
    "NX2512_HotkeyStudio/Services/DeploymentEngine.cs",
    '''            AddOrReplace(files, Path.Combine(startup, "nxkeys_ribbon.rtb"),
                Encoding.UTF8.GetBytes(MenuScriptWriter.Normalize(BuildRibbonTabFile(), ToolbarVersion)), true);
            AddOrReplace(files, Path.Combine(application, "profiles", "All", "rbn_nxkeys.rtb"),
                Encoding.UTF8.GetBytes(MenuScriptWriter.Normalize(BuildRibbonTabFile(), ToolbarVersion)), true);''',
    '''            AddOrReplace(files, Path.Combine(managedRoot, NxRibbonLayout.CanonicalRelativePath),
                Encoding.UTF8.GetBytes(MenuScriptWriter.Normalize(NxRibbonLayout.BuildTabFile(ToolbarVersion), ToolbarVersion)), true);''')

replace_exact(
    "NX2512_HotkeyStudio/Services/DeploymentEngine.cs",
    '''                List<string> staleFiles = FindStaleManagedFiles(previous, managedRoot, nextPaths);''',
    '''                List<string> staleFiles = FindStaleManagedFiles(previous, managedRoot, nextPaths);
                foreach (string relativePath in NxRibbonLayout.LegacyRelativePaths)
                {
                    string legacyPath = Path.GetFullPath(Path.Combine(managedRoot, relativePath));
                    if (!nextPaths.Contains(legacyPath) && File.Exists(legacyPath) &&
                        !staleFiles.Contains(legacyPath, StringComparer.OrdinalIgnoreCase))
                        staleFiles.Add(legacyPath);
                }''')

replace_regex(
    "NX2512_HotkeyStudio/Services/DeploymentEngine.cs",
    r'''\n        private static string BuildRibbonTabFile\(\) =>\n.*?;\n        private static string BuildToolbarFile\(\) =>''',
    '''
        private static string BuildToolbarFile() =>''')

replace_exact(
    "NX2512_HotkeyStudio/Services/OverlayGenerator.cs",
    '''            sb.AppendLine("    BUTTON UG_NXKEYS_START_DAEMON");
            sb.AppendLine("    LABEL Включить Leader Key");
            sb.AppendLine($"    ACTIONS SYSTEM \\\"{daemonCmd}\\\"");''',
    '''            sb.AppendLine("    BUTTON UG_NXKEYS_START_DAEMON");
            sb.AppendLine("    LABEL Enable Leader Key");
            sb.AppendLine("    TOOLBAR_LABEL Leader Key");
            sb.AppendLine("    MESSAGE Starts NXKeys Leader Key.");
            sb.AppendLine($"    ACTIONS SYSTEM \\\"{daemonCmd}\\\"");''',
    expected=2)

replace_exact(
    "NX2512_HotkeyStudio/Services/OverlayGenerator.cs",
    '''            sb.AppendLine("    BUTTON UG_NXKEYS_OPEN_STUDIO");
            sb.AppendLine("    LABEL Настройки NXKeys Studio...");
            sb.AppendLine($"    ACTIONS SYSTEM \\\"{guiCmd}\\\"");''',
    '''            sb.AppendLine("    BUTTON UG_NXKEYS_OPEN_STUDIO");
            sb.AppendLine("    LABEL NXKeys Studio Settings...");
            sb.AppendLine("    TOOLBAR_LABEL NXKeys Studio");
            sb.AppendLine("    MESSAGE Opens NXKeys Studio settings.");
            sb.AppendLine($"    ACTIONS SYSTEM \\\"{guiCmd}\\\"");''',
    expected=2)

replace_exact(
    "NX2512_HotkeyStudio.Tests/Program.cs",
    '''using System.Collections.Generic;
using System.Linq;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.UI;''',
    '''using System.Collections.Generic;
using System.IO;
using System.Linq;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.Services;
using NX2512_HotkeyStudio.UI;''')

replace_exact(
    "NX2512_HotkeyStudio.Tests/Program.cs",
    '''        Assert(CommandMenuPolicy.ResolveMenuLabel("F", new[] { item }, 2) == "Feature",
            "Nested menu label must use the matching PathLabels depth.");
        Console.WriteLine("[OK] Canonical profile editor and nested menu regressions.");''',
    '''        Assert(CommandMenuPolicy.ResolveMenuLabel("F", new[] { item }, 2) == "Feature",
            "Nested menu label must use the matching PathLabels depth.");

        string canonicalRibbon = Path.Combine("custom", "application", "profiles", "All", "rbn_nxkeys.rtb");
        string legacyRibbon = Path.Combine("custom", "startup", "nxkeys_ribbon.rtb");
        Assert(string.Equals(NxRibbonLayout.CanonicalRelativePath, canonicalRibbon, StringComparison.OrdinalIgnoreCase),
            "NXKeys must deploy exactly one canonical ribbon tab under application/profiles/All.");
        Assert(NxRibbonLayout.LegacyRelativePaths.Contains(legacyRibbon, StringComparer.OrdinalIgnoreCase),
            "The former startup ribbon copy must be registered for cleanup.");
        string ribbon = NxRibbonLayout.BuildTabFile(170);
        Assert(ribbon.Split(new[] { "TITLE NXKeys" }, StringSplitOptions.None).Length - 1 == 1,
            "The generated ribbon must contain one NXKeys tab title.");

        string overlay = OverlayGenerator.GenerateOverlay(170, "UG_GATEWAY_MAIN_MENUBAR",
            new List<ResolutionResult>(), new Dictionary<string, List<ConflictItem>>(), false);
        Assert(overlay.Contains("TOOLBAR_LABEL Leader Key", StringComparison.Ordinal),
            "Leader Key requires a compact ribbon label.");
        Assert(overlay.Contains("TOOLBAR_LABEL NXKeys Studio", StringComparison.Ordinal),
            "NXKeys Studio requires a compact ribbon label.");
        Assert(!overlay.Contains("Ð", StringComparison.Ordinal) && !overlay.Contains("Ñ", StringComparison.Ordinal),
            "Generated menu labels must not contain UTF-8 mojibake.");

        Console.WriteLine("[OK] Canonical profile editor, command menus and single NX ribbon regressions.");''')

print("NX ribbon UI patch applied.")
