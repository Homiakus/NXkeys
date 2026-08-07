using NxEskd.Core.Planning;

namespace NxEskd.Configurator;

internal static class SettingMetadataResolver
{
    public static IReadOnlyList<string> AllowedValues(string path, string valueType)
    {
        var normalized = path.ToLowerInvariant();
        if (valueType == "boolean") return ["true", "false"];
        if (normalized.EndsWith(".format")) return ["A0", "A1", "A2", "A3", "A4"];
        if (normalized.EndsWith(".orientation")) return ["landscape", "portrait"];
        if (normalized.EndsWith(".documentkind"))
            return ["part_drawing", "assembly_drawing", "sheet_metal_drawing", "installation_drawing", "overall_drawing"];
        if (normalized.EndsWith(".type") && normalized.Contains(".views["))
            return DrawingViewKinds.RuntimeSupported;
        if (normalized.EndsWith(".direction") && normalized.Contains(".views["))
            return ["top", "bottom", "left", "right"];
        if (normalized.EndsWith(".hiddenlines")) return ["removed", "visible"];
        if (normalized.EndsWith(".mode") && normalized.Contains(".placement"))
            return ["auto", "aligned_auto", "fixed"];
        if (normalized.EndsWith(".severity")) return ["INFO", "WARNING", "ERROR", "MANUAL_REVIEW", "AUTOFIX"];
        if (normalized.EndsWith(".units")) return ["millimeter", "inch"];
        if (normalized.EndsWith(".projectionmethod")) return ["first_angle", "third_angle"];
        if (normalized.EndsWith(".savemode")) return ["save_current", "save_as", "save_current_or_save_as"];
        if (normalized.EndsWith(".role") && normalized.Contains(".sheetplan["))
            return ["main", "flat_pattern", "assembly", "notes", "detail"];
        return Array.Empty<string>();
    }
}
