using System;
using System.Collections.Generic;

namespace NX2512_HotkeyStudio.Models
{
    public static class CommandIconHints
    {
        public static string FromCommand(string commandId, string commandName = "", string submenuKey = "", string submenuLabel = "")
        {
            string value = string.Join(" ", commandId ?? string.Empty, commandName ?? string.Empty, submenuLabel ?? string.Empty).ToUpperInvariant();
            if (value.Contains("WAVE")) return "wave";
            if (value.Contains("LAYER")) return "layer";
            if (value.Contains("MATERIAL")) return "material";
            if (value.Contains("SHEET") || value.Contains("FLANGE") || value.Contains("BEND") || value.Contains("SBSM")) return "sheet_metal";
            if (value.Contains("ASSEMB") || value.Contains("COMPONENT") || value.Contains("CONSTRAINTS")) return "assembly";
            if (value.Contains("SKETCH") || value.Contains("RECTANGLE") || value.Contains("CIRCLE") || value.Contains("ARC") || value.Contains("LINE")) return "sketch";
            if (value.Contains("SEL_") || value.Contains("SELECT") || value.Contains("DESELECT")) return "selection";
            if (value.Contains("VIEW") || value.Contains("DISPLAY") || value.Contains("SHOW") || value.Contains("HIDE")) return "view";
            if (value.Contains("MEASURE") || value.Contains("INFO") || value.Contains("ANALYSIS")) return "inspect";
            if (value.Contains("MIRROR") || value.Contains("PATTERN")) return "pattern";
            if (value.Contains("EXTRUDE") || value.Contains("REVOLVE") || value.Contains("HOLE") || value.Contains("BLEND") || value.Contains("CHAMFER")) return "feature";
            if (!string.IsNullOrWhiteSpace(submenuKey)) return "menu";
            return "command";
        }

        public static string Glyph(string hint, string commandId = "")
        {
            string id = (commandId ?? string.Empty).ToUpperInvariant();
            if (id.Contains("EXTRUDE")) return "⬡";
            if (id.Contains("REVOLVE")) return "↺";
            if (id.Contains("HOLE")) return "◎";
            if (id.Contains("BLEND") || id.Contains("FILLET")) return "⌒";
            if (id.Contains("CHAMFER")) return "⟁";
            if (id.Contains("RECTANGLE")) return "▭";
            if (id.Contains("CIRCLE")) return "○";
            if (id.Contains("ARC")) return "⌒";
            if (id.Contains("LINE")) return "╱";
            if (id.Contains("CONSTRAINT")) return "⧉";
            if (id.Contains("PATTERN")) return "❖";
            if (id.Contains("MIRROR")) return "⧖";
            if (id.Contains("WAVE")) return "〰";
            if (id.Contains("LAYER")) return "≡";
            if (id.Contains("MATERIAL")) return "◈";
            if (id.Contains("BODY_PRIORITY")) return "⬛";
            if (id.Contains("FACE_PRIORITY")) return "▨";
            if (id.Contains("EDGE_PRIORITY")) return "━";
            if (id.Contains("DESELECT")) return "✕";

            string value = (string.IsNullOrWhiteSpace(hint) ? FromCommand(commandId) : hint).Trim().ToLowerInvariant();
            switch (value)
            {
                case "wave": return "〰";
                case "layer": return "≡";
                case "material": return "◈";
                case "sheet_metal": return "📜";
                case "assembly": return "🧩";
                case "sketch": return "📐";
                case "selection": return "🎯";
                case "view": return "👁";
                case "inspect": return "📏";
                case "pattern": return "❖";
                case "feature": return "⚡";
                case "menu": return "📁";
                default: return "NX";
            }
        }
    }

    public static class ModuleDefaults
    {
        public static readonly IReadOnlyList<string> Slots = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        public static readonly IReadOnlyDictionary<string, string> DefaultSlotKeyMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["N"] = "W", ["NE"] = "E", ["E"] = "D", ["SE"] = "C",
                ["S"] = "X", ["SW"] = "Z", ["W"] = "A", ["NW"] = "Q"
            };
        public static readonly IReadOnlyList<string> DefaultInputKeys = new[] { "W", "E", "D", "C", "X", "Z", "A", "Q" };
        public static readonly IReadOnlyDictionary<string, string> SlotSemantics =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["N"] = "запуск, создание или открытие основного объекта",
                ["NE"] = "следующий основной шаг процесса",
                ["E"] = "добавление объекта, материала или зависимости",
                ["SE"] = "преобразование или замена",
                ["S"] = "завершение, удаление или вторичная обработка",
                ["SW"] = "удаление, уменьшение или ослабление",
                ["W"] = "структура, связь или паттерн",
                ["NW"] = "инспекция, измерение или сервисная команда"
            };
        public static string NormalizeSlot(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
        public static string SemanticForSlot(string slot, string fallback) =>
            SlotSemantics.TryGetValue(NormalizeSlot(slot), out string value) ? value : fallback ?? string.Empty;
        public static string ModuleIdForCategory(string category)
        {
            string value = (category ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Contains("model")) return "modeling";
            if (value.Contains("sketch")) return "sketch";
            if (value.Contains("assembl")) return "assembly";
            if (value.Contains("draft")) return "drafting";
            if (value.Contains("sheet")) return "sheet_metal";
            if (value.Contains("manufact") || value.Contains("cam")) return "manufacturing";
            if (value.Contains("simulat") || value.Contains("cae")) return "simulation";
            if (value.Contains("select")) return "selection_object";
            if (value.Contains("inspect") || value.Contains("view")) return "inspect_view";
            return value.Replace(' ', '_');
        }
    }
}
