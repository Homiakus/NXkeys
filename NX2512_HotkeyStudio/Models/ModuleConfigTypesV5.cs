using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NX2512_HotkeyStudio.Models
{
    public sealed class ModuleConfig
    {
        [JsonPropertyName("id")] public string ID { get; set; } = string.Empty;
        [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("nx_application_ids")] public List<string> NXApplicationIDs { get; set; } = new List<string>();
        [JsonPropertyName("switch_command")] public CommandRef SwitchCommand { get; set; } = new CommandRef();
        [JsonPropertyName("leader_prefix")] public string LeaderPrefix { get; set; } = string.Empty;
        [JsonPropertyName("selection_priorities")] public List<ModuleCommand> SelectionPriorities { get; set; } = new List<ModuleCommand>();
        [JsonPropertyName("command_sets")] public List<ModuleCommandSet> CommandSets { get; set; } = new List<ModuleCommandSet>();
    }

    public sealed class ModuleCommandSet
    {
        [JsonPropertyName("id")] public string ID { get; set; } = string.Empty;
        [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
        [JsonPropertyName("slot_semantics")] public Dictionary<string, string> SlotSemantics { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        [JsonPropertyName("commands")] public List<ModuleCommand> Commands { get; set; } = new List<ModuleCommand>();
    }

    public sealed class ModuleCommand
    {
        [JsonPropertyName("slot")] public string Slot { get; set; } = string.Empty;
        [JsonPropertyName("submenu_key")] public string SubmenuKey { get; set; } = string.Empty;
        [JsonPropertyName("submenu_label")] public string SubmenuLabel { get; set; } = string.Empty;
        [JsonPropertyName("input_key")] public string InputKey { get; set; } = string.Empty;
        [JsonPropertyName("path")] public List<string> Path { get; set; } = new List<string>();
        [JsonPropertyName("path_labels")] public List<string> PathLabels { get; set; } = new List<string>();
        [JsonPropertyName("aliases")] public List<List<string>> Aliases { get; set; } = new List<List<string>>();
        [JsonPropertyName("search_aliases")] public List<string> SearchAliases { get; set; } = new List<string>();
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
        [JsonPropertyName("frequency")] public string Frequency { get; set; } = string.Empty;
        [JsonPropertyName("catalog_backed_support")] public bool CatalogBackedSupport { get; set; }
        [JsonPropertyName("path_locked")] public bool PathLocked { get; set; }
        [JsonPropertyName("path_source")] public string PathSource { get; set; } = string.Empty;
    }

    public sealed class WorkflowControls
    {
        [JsonPropertyName("accept_ok")] public CommandRef AcceptOK { get; set; } = new CommandRef();
        [JsonPropertyName("apply")] public CommandRef Apply { get; set; } = new CommandRef();
        [JsonPropertyName("cancel")] public CommandRef Cancel { get; set; } = new CommandRef();
        [JsonPropertyName("back_previous_step")] public CommandRef BackPreviousStep { get; set; } = new CommandRef();
        [JsonPropertyName("confirm_dangerous")] public bool ConfirmDangerous { get; set; } = true;
        public void ApplyDefaults()
        {
            AcceptOK ??= new CommandRef(); Apply ??= new CommandRef(); Cancel ??= new CommandRef(); BackPreviousStep ??= new CommandRef();
            if (string.IsNullOrWhiteSpace(AcceptOK.Name) && string.IsNullOrWhiteSpace(AcceptOK.ID)) AcceptOK.Name = "OK";
            if (string.IsNullOrWhiteSpace(Apply.Name) && string.IsNullOrWhiteSpace(Apply.ID)) Apply.Name = "Apply";
            if (string.IsNullOrWhiteSpace(Cancel.Name) && string.IsNullOrWhiteSpace(Cancel.ID)) Cancel.Name = "Cancel";
            if (string.IsNullOrWhiteSpace(BackPreviousStep.Name) && string.IsNullOrWhiteSpace(BackPreviousStep.ID)) BackPreviousStep.Name = "Back";
            ConfirmDangerous = true;
        }
    }
}
