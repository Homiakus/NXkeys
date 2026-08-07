using System.Text.Json.Nodes;
using NxEskd.Core.Configuration;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxAttributeService(NxServiceContext context)
{
    public void WriteCanonicalAttributes(DrawingPlan plan)
    {
        var part = context.WorkPart;
        Set(part, "DRAWING_NUMBER", plan.Designation);
        Set(part, "DRAWING_NAME", plan.Name);
        Set(part, "AUTO_DWG_PROFILE_ID", plan.ProfileId);
        Set(part, "AUTO_DWG_CONFIG_HASH", context.ConfigHash);
        Set(part, "AUTO_DWG_GENERATOR_VERSION", BuildInfo.Version);

        var document = JsonNavigator.GetObject(context.Profile.Root, "$.job.document");
        if (document is not null)
        {
            Copy(document, "material", "MATERIAL");
            Copy(document, "revision", "REVISION");
            Copy(document, "organization", "ORGANIZATION");
            Copy(document, "status", "DOCUMENT_STATUS");
        }
        var approvals = JsonNavigator.GetObject(context.Profile.Root, "$.job.approvals");
        if (approvals is not null)
        {
            Copy(approvals, "developedBy", "DEVELOPED_BY");
            Copy(approvals, "checkedBy", "CHECKED_BY");
            Copy(approvals, "normControlBy", "NORM_CONTROL_BY");
            Copy(approvals, "approvedBy", "APPROVED_BY");
        }
        context.Log.Info("Канонические атрибуты модели обновлены.");
    }

    public string? ResolveTitleValue(TitleBlockValuePlan cell, DrawingPlan plan, int sheetNumber, int sheetCount)
    {
        if (cell.Label.Equals("DOCUMENT_DESIGNATION", StringComparison.OrdinalIgnoreCase)) return plan.Designation;
        if (cell.Label.Equals("DOCUMENT_NAME", StringComparison.OrdinalIgnoreCase)) return plan.Name;
        if (cell.Label.Equals("SHEET_NUMBER", StringComparison.OrdinalIgnoreCase)) return sheetNumber.ToString();
        if (cell.Label.Equals("TOTAL_SHEETS", StringComparison.OrdinalIgnoreCase)) return sheetCount.ToString();
        if (cell.Label.Equals("SCALE", StringComparison.OrdinalIgnoreCase)) return plan.Sheets[Math.Max(0, sheetNumber - 1)].Scale;

        if (cell.SourceKind is "nx_part_attribute" or "teamcenter_property")
        {
            var value = NxObjectTools.GetStringAttribute(context.WorkPart, cell.SourceName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        if (!string.IsNullOrWhiteSpace(cell.JsonPath))
        {
            var jsonValue = JsonNavigator.Get(context.Profile.Root, cell.JsonPath);
            if (jsonValue is JsonValue value)
            {
                if (value.TryGetValue<string>(out var text)) return text;
                return value.ToJsonString();
            }
        }
        if (cell.SourceKind == "computed_value")
            return Compute(cell.SourceName, plan, sheetNumber, sheetCount);
        return cell.LiteralValue;
    }

    private string? Compute(string? function, DrawingPlan plan, int sheetNumber, int sheetCount)
        => function switch
        {
            "sheet_count" => sheetCount.ToString(),
            "active_sheet_number" => sheetNumber.ToString(),
            "main_view_scale" => plan.Sheets[Math.Max(0, sheetNumber - 1)].Scale,
            "current_date" => DateTime.Now.ToString("dd.MM.yyyy"),
            "config_hash" => context.ConfigHash,
            "body_mass" => TryMass(),
            _ => null
        };

    private string? TryMass()
    {
        try
        {
            var bodies = NxReflection.Get(context.WorkPart, "Bodies");
            var solidBodies = NxReflection.Enumerate(bodies).Where(b => NxReflection.Get(b, "IsSolidBody") as bool? != false).ToArray();
            if (solidBodies.Length == 0) return null;
            var measureManager = NxReflection.Get(context.WorkPart, "MeasureManager");
            var result = NxReflection.Invoke(measureManager, ["NewMassProperties", "NewMassPropertiesUnit"], solidBodies);
            var mass = NxReflection.Get(result, "Mass")?.ToString();
            NxReflection.Destroy(result);
            return mass;
        }
        catch { return null; }
    }

    private void Copy(JsonObject source, string jsonName, string attributeName)
    {
        var value = source[jsonName]?.GetValue<string?>();
        if (!string.IsNullOrWhiteSpace(value)) Set(context.WorkPart, attributeName, value);
    }

    private static void Set(object target, string name, string value) => NxObjectTools.SetStringAttribute(target, name, value);
}
