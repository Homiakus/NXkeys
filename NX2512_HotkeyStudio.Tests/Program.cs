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

        VerifySketchIntentGrammar();

        Console.WriteLine("[OK] Canonical profile editor, command menus, Sketch intent grammar and single NX ribbon regressions.");
    }


    private static void VerifySketchIntentGrammar()
    {
        var commands = new List<ModuleCommand>
        {
            SketchCommand("UG_SKETCH_LINE", "Line", new[] { "C", "L" }, "K5", 1),
            SketchCommand("UG_SKETCH_LINE_BY_TWO_POINTS", "Line by Two Points", new[] { "C", "L", "2" }, "K3", 2),
            SketchCommand("UG_SKETCH_RECTANGLE", "Rectangle", new[] { "C", "R" }, "K5", 3),
            SketchCommand("UG_SKETCH_TRIM", "Trim", new[] { "E", "T" }, "K5", 4),
            SketchCommand("UG_MODELING_CHAMFER_FEATURE", "Sketch Chamfer", new[] { "C", "G", "C", "H" }, "K3", 5),
            SketchCommand("UG_SKETCH_STUDIO_SPLINE", "Studio Spline", new[] { "M", "Z", "S" }, "K4", 6),
            SketchCommand("UG_SKETCH_USER_CUSTOM", "User Custom", new[] { "U", "S", "X" }, "K3", 7, true)
        };
        commands[0].Aliases = new List<List<string>> { new List<string> { "Q", "W" } };
        var module = new ModuleConfig
        {
            ID = "sketch",
            Label = "Sketch",
            Enabled = true,
            CommandSets = new List<ModuleCommandSet>
            {
                new ModuleCommandSet { ID = "sketch", Label = "Sketch", Commands = commands }
            }
        };

        MnemonicPathGenerator.Apply(new[] { module });
        Assert(PathOf(commands, "UG_SKETCH_LINE").SequenceEqual(new[] { "C", "G", "L" }),
            "Line must use Create -> Geometry -> Line.");
        Assert(PathOf(commands, "UG_SKETCH_LINE_BY_TWO_POINTS").SequenceEqual(new[] { "C", "G", "V", "L", "2" }),
            "Line variants must live under the explicit variant branch.");
        Assert(PathOf(commands, "UG_SKETCH_RECTANGLE").SequenceEqual(new[] { "C", "G", "R" }),
            "Rectangle must use Create -> Geometry -> Rectangle.");
        Assert(PathOf(commands, "UG_SKETCH_TRIM").SequenceEqual(new[] { "E", "G", "T" }),
            "Trim must use Edit -> Geometry -> Trim.");
        Assert(PathOf(commands, "UG_MODELING_CHAMFER_FEATURE").SequenceEqual(new[] { "E", "G", "H" }),
            "The shared NX chamfer BUTTON ID must keep Sketch semantics in Sketch context.");
        Assert(PathOf(commands, "UG_SKETCH_STUDIO_SPLINE").Take(2).SequenceEqual(new[] { "C", "G" }),
            "Unknown Sketch geometry must remain in the Create -> Geometry family.");
        Assert(PathOf(commands, "UG_SKETCH_USER_CUSTOM").SequenceEqual(new[] { "U", "S", "X" }),
            "User-locked paths must remain untouched.");
        Assert(commands.First(item => item.Command.ID == "UG_SKETCH_LINE").Aliases.Count == 0,
            "Legacy positional aliases must be removed from generated Sketch intents.");

        List<string> paths = commands.Select(item => string.Concat(item.Path)).OrderBy(value => value.Length).ToList();
        for (int left = 0; left < paths.Count; left++)
            for (int right = left + 1; right < paths.Count; right++)
                Assert(!paths[right].StartsWith(paths[left], StringComparison.OrdinalIgnoreCase),
                    "Sketch paths must remain prefix-free: " + paths[left] + " / " + paths[right]);
    }

    private static ModuleCommand SketchCommand(
        string id,
        string name,
        IEnumerable<string> path,
        string frequency,
        int order,
        bool locked = false) => new ModuleCommand
    {
        Enabled = true,
        Path = path.ToList(),
        PathLocked = locked,
        PathSource = locked ? "user" : "generated",
        Frequency = frequency,
        DisplayOrder = order,
        Command = new CommandRef { ID = id, Name = name }
    };

    private static IReadOnlyList<string> PathOf(IEnumerable<ModuleCommand> commands, string id) =>
        commands.First(item => string.Equals(item.Command.ID, id, StringComparison.OrdinalIgnoreCase)).Path;

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
