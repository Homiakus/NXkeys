using System;
using System.Collections.Generic;
using System.Linq;

namespace NX2512_HotkeyStudio.Models
{
    /// <summary>
    /// Canonical editor contract for schema-v6 mnemonic paths. Runtime dispatch uses Path;
    /// SubmenuKey/InputKey are maintained only as a backward-compatible projection.
    /// </summary>
    public static class EditableCommandPathPolicy
    {
        public static IReadOnlyList<string> EffectivePath(ModuleCommand command)
        {
            IReadOnlyList<string> canonical = MnemonicPathGenerator.NormalizePath(command?.Path);
            if (canonical.Count > 0) return canonical;
            string submenu = LeaderKeyConfig.NormalizeInputKey(command?.SubmenuKey);
            string input = LeaderKeyConfig.NormalizeInputKey(command?.InputKey);
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();
            return string.IsNullOrWhiteSpace(submenu)
                ? new[] { input }
                : new[] { submenu, input };
        }

        public static string FormatPath(IEnumerable<string> path) =>
            string.Join(" → ", MnemonicPathGenerator.NormalizePath(path));

        public static string FormatLabels(IEnumerable<string> labels) =>
            string.Join(" → ", (labels ?? Enumerable.Empty<string>())
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => value.Length > 0));

        public static List<string> ParsePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<string>();
            string normalized = value.Replace("->", " ").Replace("→", " ").Replace(">", " ")
                .Replace("/", " ").Replace("\\", " ").Replace(",", " ").Replace(";", " ");
            string[] parts = normalized.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<string> raw = parts.Length == 1 && parts[0].Length > 1 && parts[0].All(char.IsLetterOrDigit)
                ? parts[0].Select(character => character.ToString())
                : parts;
            return raw.Select(LeaderKeyConfig.NormalizeInputKey)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Take(5)
                .ToList();
        }

        public static List<string> ParseLabels(string value, IReadOnlyList<string> path, string commandName)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                string normalized = value.Replace("->", "|").Replace("→", "|").Replace(">", "|").Replace(";", "|");
                List<string> labels = normalized.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim()).Where(item => item.Length > 0).ToList();
                if (labels.Count == path.Count) return labels;
            }
            return MnemonicPathGenerator.BuildPathLabels(path, commandName);
        }

        public static void ApplyEditedPath(ModuleCommand command, string pathText, string labelsText)
        {
            if (command == null) return;
            List<string> path = ParsePath(pathText);
            IReadOnlyList<string> previous = EffectivePath(command);
            bool changed = !previous.SequenceEqual(path, StringComparer.OrdinalIgnoreCase);
            command.Path = path;
            command.PathLabels = ParseLabels(labelsText, path, command.Command?.Name);
            if (changed)
            {
                command.PathLocked = true;
                command.PathSource = "user";
            }
            command.Aliases ??= new List<List<string>>();
            command.Aliases = command.Aliases
                .Select(alias => MnemonicPathGenerator.NormalizePath(alias).ToList())
                .Where(alias => alias.Count > 0 && !alias.SequenceEqual(path, StringComparer.OrdinalIgnoreCase))
                .GroupBy(alias => string.Concat(alias), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).ToList();
            SyncLegacyFields(command);
        }

        public static void Normalize(Config config)
        {
            if (config == null) return;
            foreach (ModuleConfig module in config.Modules ?? new List<ModuleConfig>())
            {
                IEnumerable<ModuleCommand> commands = module?.CommandSets?
                    .Where(set => set?.Commands != null).SelectMany(set => set.Commands)
                    .Where(command => command != null) ?? Enumerable.Empty<ModuleCommand>();
                foreach (ModuleCommand command in commands)
                {
                    List<string> path = EffectivePath(command).ToList();
                    command.Path = path;
                    if (command.PathLabels == null || command.PathLabels.Count != path.Count)
                        command.PathLabels = MnemonicPathGenerator.BuildPathLabels(path, command.Command?.Name);
                    SyncLegacyFields(command);
                }
            }
            config.LeaderKey?.RebuildFromModules(config.Modules);
        }

        public static List<string> Validate(Config config)
        {
            var problems = new List<string>();
            if (config == null) { problems.Add("Профиль отсутствует."); return problems; }
            foreach (ModuleConfig module in (config.Modules ?? new List<ModuleConfig>()).Where(value => value != null && value.Enabled))
            {
                string prefix = LeaderKeyConfig.NormalizeInputKey(module.LeaderPrefix);
                if (string.IsNullOrWhiteSpace(prefix)) problems.Add($"{module.Label}: отсутствует leader_prefix");
                var seen = new List<(string Sequence, string Path, string Command)>();
                IEnumerable<ModuleCommand> commands = module.CommandSets?
                    .Where(set => set?.Commands != null).SelectMany(set => set.Commands)
                    .Where(command => command != null && command.Enabled) ?? Enumerable.Empty<ModuleCommand>();
                foreach (ModuleCommand command in commands)
                {
                    IReadOnlyList<string> path = EffectivePath(command);
                    string pathText = FormatPath(path);
                    string commandName = command.Command?.Name ?? command.Command?.ID ?? "без названия";
                    if (path.Count == 0) problems.Add($"{module.Label}: пустой путь для {commandName}");
                    if (string.IsNullOrWhiteSpace(command.Command?.Name)) problems.Add($"{module.Label}: пустое Command Name для пути {pathText}");
                    if (string.IsNullOrWhiteSpace(command.Command?.ID)) problems.Add($"{module.Label}: пустой BUTTON ID для пути {pathText}");
                    if (path.Count == 0 || string.IsNullOrWhiteSpace(prefix)) continue;
                    string sequence = prefix + string.Concat(path);
                    foreach ((string existingSequence, string existingPath, string existingCommand) in seen)
                    {
                        if (string.Equals(sequence, existingSequence, StringComparison.OrdinalIgnoreCase))
                            problems.Add($"{module.Label}: путь {pathText} повторяется у {existingCommand} и {commandName}");
                        else if (sequence.StartsWith(existingSequence, StringComparison.OrdinalIgnoreCase) ||
                                 existingSequence.StartsWith(sequence, StringComparison.OrdinalIgnoreCase))
                            problems.Add($"{module.Label}: пути {existingPath} и {pathText} образуют конфликт команда/подменю");
                    }
                    seen.Add((sequence, pathText, commandName));
                }
            }
            return problems.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static void SyncLegacyFields(ModuleCommand command)
        {
            IReadOnlyList<string> path = MnemonicPathGenerator.NormalizePath(command?.Path);
            if (command == null) return;
            command.SubmenuKey = path.Count > 1 ? path[0] : string.Empty;
            command.InputKey = path.LastOrDefault() ?? string.Empty;
            command.SubmenuLabel = path.Count > 1 && command.PathLabels != null && command.PathLabels.Count > 0
                ? command.PathLabels[0]
                : string.Empty;
        }
    }
}
