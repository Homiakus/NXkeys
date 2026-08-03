using System;
using System.Collections.Generic;
using System.Linq;
using NX2512_HotkeyStudio.Models;
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
        Console.WriteLine("[OK] Canonical profile editor and nested menu regressions.");
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
