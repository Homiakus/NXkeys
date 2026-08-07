using System.Globalization;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;

namespace NxEskd.NxRuntime;

internal sealed record NxBomRow(
    int Position,
    string Identity,
    string PartNumber,
    string Description,
    string Revision,
    string Material,
    double? Mass,
    int Quantity,
    object RepresentativeComponent,
    IReadOnlyList<object> Occurrences);

internal sealed class NxBomSnapshotBuilder(NxServiceContext context)
{
    public IReadOnlyList<NxBomRow> Build()
    {
        var componentAssembly = NxReflection.Get(context.WorkPart, "ComponentAssembly");
        var root = NxReflection.Get(componentAssembly, "RootComponent");
        if (root is null)
        {
            context.Report.Issues.Add(new("NX_BOM_ROOT_COMPONENT_MISSING", IssueSeverity.Error,
                "Сборка не содержит RootComponent; спецификация не может быть построена."));
            return [];
        }

        var includeSuppressed = JsonNavigator.GetBool(context.Profile.Root,
            "$.partsListAndBalloons.partsList.includeSuppressedComponents", false);
        var occurrences = EnumerateComponents(root)
            .Where(component => includeSuppressed || !IsSuppressed(component))
            .Select(ReadOccurrence)
            .Where(x => x is not null)
            .Cast<OccurrenceData>()
            .ToArray();
        if (occurrences.Length == 0)
        {
            context.Report.Issues.Add(new("NX_BOM_EMPTY", IssueSeverity.Error,
                "В сборке не найдено компонентов для спецификации."));
            return [];
        }

        var groups = occurrences.GroupBy(x => x.Identity, StringComparer.OrdinalIgnoreCase).ToArray();
        var usedPositions = groups.SelectMany(x => x.Select(y => ReadPosition(y.Component)))
            .Where(x => x is > 0).Cast<int>().ToHashSet();
        var nextPosition = usedPositions.Count == 0 ? 1 : usedPositions.Max() + 1;
        var rows = new List<NxBomRow>();
        foreach (var group in groups.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var members = group.ToArray();
            var existing = members.Select(x => ReadPosition(x.Component)).Where(x => x is > 0).Cast<int>().Distinct().ToArray();
            if (existing.Length > 1)
            {
                context.Report.Issues.Add(new("NX_BOM_POSITION_CONFLICT", IssueSeverity.Error,
                    $"Компоненты '{group.Key}' имеют несколько позиций: {string.Join(", ", existing)}."));
                continue;
            }
            var position = existing.FirstOrDefault();
            if (position <= 0)
            {
                while (usedPositions.Contains(nextPosition)) nextPosition++;
                position = nextPosition++;
                usedPositions.Add(position);
            }

            foreach (var occurrence in members)
                WritePosition(occurrence.Component, position);
            var representative = members[0];
            rows.Add(new NxBomRow(
                position,
                representative.Identity,
                representative.PartNumber,
                representative.Description,
                representative.Revision,
                representative.Material,
                representative.Mass,
                members.Length,
                representative.Component,
                members.Select(x => x.Component).ToArray()));
        }

        var sorting = JsonNavigator.GetArray(context.Profile.Root,
            "$.partsListAndBalloons.partsList.sorting") ?? [];
        IOrderedEnumerable<NxBomRow>? ordered = null;
        foreach (var ruleNode in sorting)
        {
            var rule = ruleNode?.AsObject();
            var field = rule?["field"]?.GetValue<string?>() ?? "position";
            var descending = string.Equals(rule?["direction"]?.GetValue<string?>(), "desc", StringComparison.OrdinalIgnoreCase);
            Func<NxBomRow, IComparable> key = field.ToLowerInvariant() switch
            {
                "part_number" => row => row.PartNumber,
                "description" => row => row.Description,
                "revision" => row => row.Revision,
                "quantity" => row => row.Quantity,
                _ => row => row.Position
            };
            ordered = ordered is null
                ? descending ? rows.OrderByDescending(key) : rows.OrderBy(key)
                : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }
        var result = (ordered ?? rows.OrderBy(x => x.Position)).ToArray();
        context.Report.Metrics["bom.rowCount"] = result.Length;
        context.Report.Metrics["bom.occurrenceCount"] = occurrences.Length;
        return result;
    }

    private OccurrenceData? ReadOccurrence(object component)
    {
        var prototype = NxReflection.Get(component, "Prototype", "PrototypePart", "Part") ?? component;
        var identity = NxReflection.Get(prototype, "FullPath", "JournalIdentifier", "Name")?.ToString()
                       ?? NxReflection.Get(component, "JournalIdentifier", "Name")?.ToString();
        if (string.IsNullOrWhiteSpace(identity))
        {
            context.Report.Issues.Add(new("NX_BOM_COMPONENT_ID_MISSING", IssueSeverity.Error,
                "Компонент сборки не имеет устойчивого prototype identity."));
            return null;
        }

        var partNumber = ResolveMappedAttribute(component, prototype, "itemNumber",
            ["PART_NUMBER", "DB_PART_NO", "ITEM_ID", "PART_NO"])
                         ?? NxReflection.Get(prototype, "Leaf", "Name")?.ToString()
                         ?? NxReflection.Get(component, "Name")?.ToString()
                         ?? identity;
        var description = ResolveMappedAttribute(component, prototype, "name",
            ["DESCRIPTION", "PART_NAME", "DB_PART_NAME"]) ?? partNumber;
        var revision = ResolveMappedAttribute(component, prototype, "revision",
            ["REVISION", "DB_PART_REV", "REV"])
                       ?? NxReflection.Get(prototype, "Revision")?.ToString()
                       ?? string.Empty;
        var material = ResolveMappedAttribute(component, prototype, "material",
            ["MATERIAL", "MATL", "MATERIAL_NAME"]) ?? string.Empty;
        var massText = ResolveMappedAttribute(component, prototype, "mass", ["MASS", "WEIGHT"]);
        var mass = TryParseDouble(massText)
                   ?? ReadDouble(NxReflection.Get(prototype, "Mass", "Weight"));
        return new OccurrenceData(component, prototype, identity, partNumber, description, revision, material, mass);
    }

    private string? ResolveMappedAttribute(
        object component,
        object prototype,
        string mapKey,
        IReadOnlyList<string> defaults)
    {
        var configured = JsonNavigator.GetString(context.Profile.Root,
            "$.job.source.partAttributeMap." + mapKey);
        var names = string.IsNullOrWhiteSpace(configured) ? defaults : [configured, .. defaults];
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var value = NxObjectTools.GetStringAttribute(component, name)
                        ?? NxObjectTools.GetStringAttribute(prototype, name);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return null;
    }

    private int? ReadPosition(object component)
    {
        var value = NxObjectTools.GetStringAttribute(component, "AUTO_DWG_POSITION")
                    ?? NxObjectTools.GetStringAttribute(component, "POSITION")
                    ?? NxObjectTools.GetStringAttribute(component, "FIND_NUMBER");
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var position)
            && position > 0 ? position : null;
    }

    private void WritePosition(object component, int position)
    {
        var value = position.ToString(CultureInfo.InvariantCulture);
        NxObjectTools.SetStringAttribute(component, "AUTO_DWG_POSITION", value);
        var writeBack = JsonNavigator.GetString(context.Profile.Root,
            "$.partsListAndBalloons.partsList.positionNumber.writeBackAttribute");
        if (!string.IsNullOrWhiteSpace(writeBack)) NxObjectTools.SetStringAttribute(component, writeBack, value);
    }

    private static IEnumerable<object> EnumerateComponents(object root)
    {
        var stack = new Stack<object>();
        foreach (var child in NxReflection.Enumerate(NxReflection.GetOrInvoke(root, "Children", "GetChildren")))
            stack.Push(child);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        while (stack.Count > 0)
        {
            var component = stack.Pop();
            if (!visited.Add(component)) continue;
            yield return component;
            foreach (var child in NxReflection.Enumerate(NxReflection.GetOrInvoke(component, "Children", "GetChildren")))
                stack.Push(child);
        }
    }

    private static bool IsSuppressed(object component)
    {
        var value = NxReflection.Get(component, "IsSuppressed", "Suppressed", "SuppressionState");
        return value is bool suppressed ? suppressed
            : value?.ToString()?.Contains("Suppress", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static double? TryParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            && double.IsFinite(result) ? result : null;
    }

    private static double? ReadDouble(object? value)
    {
        if (value is not IConvertible convertible) return null;
        try
        {
            var result = convertible.ToDouble(CultureInfo.InvariantCulture);
            return double.IsFinite(result) ? result : null;
        }
        catch { return null; }
    }

    private sealed record OccurrenceData(
        object Component,
        object Prototype,
        string Identity,
        string PartNumber,
        string Description,
        string Revision,
        string Material,
        double? Mass);
}
