using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace NX2512_HotkeyStudio.Models
{
    public sealed class LeaderKeyConfig
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("trigger_key")] public string TriggerKey { get; set; } = "CapsLock";
        [JsonPropertyName("adaptive_module_mode")] public bool AdaptiveModuleMode { get; set; } = true;
        [JsonPropertyName("hud_delay_ms")] public int HudDelayMs { get; set; } = 120;
        [JsonPropertyName("first_key_timeout_ms")] public int FirstKeyTimeoutMs { get; set; } = 20000;
        [JsonPropertyName("next_key_timeout_ms")] public int NextKeyTimeoutMs { get; set; } = 20000;
        [JsonPropertyName("sticky_mode_on_double_tap")] public bool StickyModeOnDoubleTap { get; set; } = true;
        [JsonPropertyName("hud_opacity")] public double HudOpacity { get; set; } = 0.95;
        [JsonPropertyName("hook_only_when_nx_active")] public bool HookOnlyWhenNXActive { get; set; } = true;
        [JsonPropertyName("slot_key_map")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string> SlotKeyMap { get; set; }
        [JsonIgnore] public List<LeaderSequenceItem> Sequences { get; set; } = new List<LeaderSequenceItem>();
        [JsonIgnore] public List<ModuleConfig> RuntimeModules { get; set; } = new List<ModuleConfig>();

        public void ApplyDefaults()
        {
            if (string.IsNullOrWhiteSpace(TriggerKey)) TriggerKey = "CapsLock";
            AdaptiveModuleMode = true;
            if (HudDelayMs <= 0) HudDelayMs = 120;
            if (FirstKeyTimeoutMs <= 0) FirstKeyTimeoutMs = 20000;
            if (NextKeyTimeoutMs <= 0) NextKeyTimeoutMs = 20000;
            if (HudOpacity <= 0 || HudOpacity > 1.0) HudOpacity = 0.95;
            if (SlotKeyMap != null)
                SlotKeyMap = new Dictionary<string, string>(SlotKeyMap, StringComparer.OrdinalIgnoreCase);
            Sequences ??= new List<LeaderSequenceItem>();
            RuntimeModules ??= new List<ModuleConfig>();
        }

        public void MergeModules(List<ModuleConfig> modules) => RebuildFromModules(modules);

        public void RebuildFromModules(IEnumerable<ModuleConfig> modules)
        {
            ApplyDefaults();
            RuntimeModules = (modules ?? Enumerable.Empty<ModuleConfig>()).Where(value => value != null).ToList();
            var result = new List<LeaderSequenceItem>();
            foreach (ModuleConfig module in RuntimeModules.Where(value => value.Enabled))
            {
                string prefix = NormalizeInputKey(module.LeaderPrefix);
                IEnumerable<ModuleCommand> commands = module.CommandSets?
                    .Where(set => set?.Commands != null).SelectMany(set => set.Commands).Where(value => value != null)
                    ?? Enumerable.Empty<ModuleCommand>();
                int fallbackOrder = 1;
                foreach (ModuleCommand moduleCommand in commands.OrderBy(command => command.DisplayOrder <= 0 ? int.MaxValue : command.DisplayOrder))
                {
                    if (!moduleCommand.Enabled) continue;
                    string slot = ModuleDefaults.NormalizeSlot(moduleCommand.Slot);
                    IReadOnlyList<string> canonicalPath = MnemonicPathGenerator.NormalizePath(moduleCommand.Path);
                    if (canonicalPath.Count == 0)
                    {
                        string submenuKey = NormalizeInputKey(moduleCommand.SubmenuKey);
                        string inputKey = NormalizeInputKey(moduleCommand.InputKey);
                        if (string.IsNullOrWhiteSpace(inputKey)) inputKey = ResolveInputKey(slot, fallbackOrder);
                        canonicalPath = string.IsNullOrWhiteSpace(submenuKey)
                            ? new[] { inputKey }
                            : new[] { submenuKey, inputKey };
                    }
                    if (string.IsNullOrWhiteSpace(prefix) || canonicalPath.Count == 0 || moduleCommand.Command == null) continue;

                    LeaderSequenceItem canonical = CreateSequenceItem(module, moduleCommand, slot, prefix, canonicalPath, false, fallbackOrder);
                    result.Add(canonical);
                    foreach (List<string> rawAlias in moduleCommand.Aliases ?? new List<List<string>>())
                    {
                        IReadOnlyList<string> alias = MnemonicPathGenerator.NormalizePath(rawAlias);
                        if (alias.Count == 0 || alias.SequenceEqual(canonicalPath, StringComparer.OrdinalIgnoreCase)) continue;
                        result.Add(CreateSequenceItem(module, moduleCommand, slot, prefix, alias, true, fallbackOrder));
                    }
                    fallbackOrder++;
                }
            }
            Sequences = result;
        }

        private static LeaderSequenceItem CreateSequenceItem(
            ModuleConfig module,
            ModuleCommand moduleCommand,
            string slot,
            string prefix,
            IReadOnlyList<string> path,
            bool isAlias,
            int fallbackOrder)
        {
            string sequence = prefix + " " + string.Join(" ", path);
            string inputKey = path.LastOrDefault() ?? string.Empty;
            string submenuKey = path.Count > 1 ? path[0] : string.Empty;
            return new LeaderSequenceItem
            {
                Sequence = sequence,
                CanonicalSequence = prefix + " " + string.Join(" ", MnemonicPathGenerator.NormalizePath(moduleCommand.Path)),
                Category = module.Label,
                ModuleID = module.ID,
                Slot = slot,
                SubmenuKey = submenuKey,
                SubmenuLabel = moduleCommand.PathLabels?.FirstOrDefault() ?? moduleCommand.SubmenuLabel ?? string.Empty,
                InputKey = inputKey,
                Path = path.ToList(),
                PathLabels = moduleCommand.PathLabels?.ToList() ?? new List<string>(),
                SearchAliases = moduleCommand.SearchAliases?.ToList() ?? new List<string>(),
                IsAlias = isAlias,
                IconHint = string.IsNullOrWhiteSpace(moduleCommand.IconHint)
                    ? CommandIconHints.FromCommand(moduleCommand.Command?.ID, moduleCommand.Command?.Name, submenuKey, moduleCommand.SubmenuLabel)
                    : moduleCommand.IconHint.Trim(),
                DisplayOrder = moduleCommand.DisplayOrder <= 0 ? fallbackOrder : moduleCommand.DisplayOrder,
                Command = moduleCommand.Command,
                Action = SelectionIntent.ActionFor(moduleCommand),
                TargetModuleID = moduleCommand.TargetModuleID,
                SupportKind = moduleCommand.SupportKind,
                SelectionType = SelectionIntent.SelectionTypeFor(moduleCommand),
                RequiresSelection = moduleCommand.RequiresSelection,
                Destructive = moduleCommand.Destructive,
                ConfirmBeforeExecute = moduleCommand.ConfirmBeforeExecute || moduleCommand.Destructive,
                Fallback = moduleCommand.Fallback,
                Notes = string.IsNullOrWhiteSpace(moduleCommand.Notes)
                    ? ModuleDefaults.SemanticForSlot(slot, moduleCommand.Command.Name)
                    : moduleCommand.Notes,
                Enabled = true
            };
        }

        public string ResolveInputKey(string slot) => ResolveInputKey(slot, 0);

        public string ResolveInputKey(string slot, int fallbackOrder)
        {
            string normalizedSlot = ModuleDefaults.NormalizeSlot(slot);
            if (SlotKeyMap != null && SlotKeyMap.TryGetValue(normalizedSlot, out string value))
            {
                string mapped = NormalizeInputKey(value);
                if (!string.IsNullOrWhiteSpace(mapped)) return mapped;
            }
            if (ModuleDefaults.DefaultSlotKeyMap.TryGetValue(normalizedSlot, out string defaultValue))
                return NormalizeInputKey(defaultValue);
            if (fallbackOrder > 0 && fallbackOrder <= ModuleDefaults.DefaultInputKeys.Count)
                return ModuleDefaults.DefaultInputKeys[fallbackOrder - 1];
            return string.Empty;
        }

        internal void Validate(List<string> problems)
        {
            if (!AdaptiveModuleMode) problems.Add("leader_key.adaptive_module_mode must be true");
            if (Sequences.Count == 0) problems.Add("derived adaptive sequence list is empty");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LeaderSequenceItem item in Sequences)
            {
                string normalized = new string((item.Sequence ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
                if (!seen.Add(normalized)) problems.Add("duplicate derived adaptive sequence: " + item.Sequence);
            }
        }

        public static string NormalizeInputKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            char character = value.Trim().FirstOrDefault(char.IsLetterOrDigit);
            return character == default ? string.Empty : char.ToUpperInvariant(character).ToString();
        }
    }

    public sealed class LeaderSequenceItem
    {
        [JsonPropertyName("sequence")] public string Sequence { get; set; } = string.Empty;
        [JsonPropertyName("canonical_sequence")] public string CanonicalSequence { get; set; } = string.Empty;
        [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
        [JsonPropertyName("module_id")] public string ModuleID { get; set; } = string.Empty;
        [JsonPropertyName("slot")] public string Slot { get; set; } = string.Empty;
        [JsonPropertyName("submenu_key")] public string SubmenuKey { get; set; } = string.Empty;
        [JsonPropertyName("submenu_label")] public string SubmenuLabel { get; set; } = string.Empty;
        [JsonPropertyName("input_key")] public string InputKey { get; set; } = string.Empty;
        [JsonPropertyName("path")] public List<string> Path { get; set; } = new List<string>();
        [JsonPropertyName("path_labels")] public List<string> PathLabels { get; set; } = new List<string>();
        [JsonPropertyName("search_aliases")] public List<string> SearchAliases { get; set; } = new List<string>();
        [JsonPropertyName("is_alias")] public bool IsAlias { get; set; }
        [JsonPropertyName("icon_hint")] public string IconHint { get; set; } = string.Empty;
        [JsonPropertyName("display_order")] public int DisplayOrder { get; set; }
        [JsonPropertyName("command")] public CommandRef Command { get; set; } = new CommandRef();
        [JsonPropertyName("action")] public string Action { get; set; } = string.Empty;
        [JsonPropertyName("target_module_id")] public string TargetModuleID { get; set; } = string.Empty;
        [JsonPropertyName("support_kind")] public string SupportKind { get; set; } = string.Empty;
        [JsonPropertyName("selection_type")] public string SelectionType { get; set; } = string.Empty;
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("requires_selection")] public bool RequiresSelection { get; set; }
        [JsonPropertyName("destructive")] public bool Destructive { get; set; }
        [JsonPropertyName("confirm_before_execute")] public bool ConfirmBeforeExecute { get; set; }
        [JsonPropertyName("fallback")] public string Fallback { get; set; } = string.Empty;
        [JsonPropertyName("notes")] public string Notes { get; set; } = string.Empty;
        public string DisplayPath(string triggerKey)
        {
            string path = Path != null && Path.Count > 0 ? string.Join(" → ", Path) :
                (string.IsNullOrWhiteSpace(SubmenuKey) ? InputKey : SubmenuKey + " → " + InputKey);
            return $"{triggerKey} → {path}";
        }
    }
}
