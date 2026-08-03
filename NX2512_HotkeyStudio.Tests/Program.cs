using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.Services;
using NX2512_HotkeyStudio.UI;

internal static class Program
{
    private static void Main()
    {
        var first = Command("UG_TEST_FIRST", "First", new[] { "C", "F", "E" });
        var module = new ModuleConfig
        {
            ID = "modeling",
            Label = "Modeling",
            LeaderPrefix = "M",
            CommandSets = new List<ModuleCommandSet>
            {
                new ModuleCommandSet { ID = "primary", Commands = new List<ModuleCommand> { first } }
            }
        };
        var config = new Config { Modules = new List<ModuleConfig> { module } };
        EditableCommandPathPolicy.Normalize(config);
        Assert(EditableCommandPathPolicy.Validate(config).Count == 0,
            "Canonical path with empty legacy fields must be valid.");

        EditableCommandPathPolicy.ApplyEditedPath(first, "E → F → M", "Edit → Feature → Modify");
        Assert(first.Path.SequenceEqual(new[] { "E", "F", "M" }), "Editor must update canonical Path.");
        Assert(first.PathLocked && first.PathSource == "user", "Edited paths must be user-owned and locked.");
        Assert(first.SubmenuKey == "E" && first.InputKey == "M", "Legacy projection must follow canonical Path.");

        var duplicate = Command("UG_TEST_DUPLICATE", "Duplicate", new[] { "E", "F", "M" });
        module.CommandSets[0].Commands.Add(duplicate);
        List<string> duplicateProblems = EditableCommandPathPolicy.Validate(config);
        Assert(duplicateProblems.Any(value => value.Contains("повторяется", StringComparison.OrdinalIgnoreCase)),
            "Duplicate canonical paths must be rejected.");

        duplicate.Path = new List<string> { "E", "F" };
        List<string> prefixProblems = EditableCommandPathPolicy.Validate(config);
        Assert(prefixProblems.Any(value => value.Contains("команда/подменю", StringComparison.OrdinalIgnoreCase)),
            "Terminal-prefix conflicts must be rejected.");

        var item = new LeaderSequenceItem
        {
            PathLabels = new List<string> { "Create", "Feature", "Extrude" },
            SubmenuLabel = "Legacy root"
        };
        Assert(CommandMenuPolicy.ResolveMenuLabel("F", new[] { item }, 2) == "Feature",
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

        Console.WriteLine("[OK] Canonical profile editor, command menus and single NX ribbon regressions.");
    }

    private static ModuleCommand Command(string id, string name, IEnumerable<string> path) => new ModuleCommand
    {
        Enabled = true,
        Path = path.ToList(),
        Command = new CommandRef { ID = id, Name = name }
    };

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
