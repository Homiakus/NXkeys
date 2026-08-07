using System.Globalization;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxPartsListService(NxServiceContext context)
{
    private const string ManagedId = "AUTO_PARTS_LIST";
    private const string ObjectKind = "parts_list";

    public void CreateOrUpdate(DrawingPlan plan)
    {
        if (!plan.DocumentKind.Equals("assembly_drawing", StringComparison.OrdinalIgnoreCase)) return;
        if (!JsonNavigator.GetBool(context.Profile.Root, "$.partsListAndBalloons.partsList.enabled", true)) return;

        var rows = new NxBomSnapshotBuilder(context).Build();
        if (rows.Count == 0) return;
        var columns = LoadColumns();
        if (columns.Count == 0)
        {
            context.Report.Issues.Add(new("NX_PARTS_LIST_COLUMNS_EMPTY", IssueSeverity.Error,
                "Конфигурация Parts List не содержит колонок."));
            return;
        }

        var targetSheet = new NxSheetTargetService(context).Activate(plan, "main", "assembly");
        if (targetSheet is null) return;
        var targetSheetId = NxObjectTools.GetStringAttribute(targetSheet, "AUTO_DWG_ID")
                            ?? plan.Sheets.FirstOrDefault()?.Id
                            ?? string.Empty;

        var annotations = NxReflection.Get(context.WorkPart, "Annotations");
        var collection = NxReflection.Get(annotations,
            context.ApiMap.Aliases("partsList.collection", "PartsLists", "PartsListCollection"));
        var all = NxReflection.Enumerate(collection).ToArray();
        var existing = all.FirstOrDefault(IsOwned);
        object partsList;
        if (existing is not null)
        {
            NxObjectTools.EnsureOwnershipMetadata(existing, ManagedId, context.Profile.ProfileId,
                context.ConfigHash, ObjectKind, context.ScopeId);
            if (!ResizeAndPopulate(collection, existing, columns, rows))
            {
                context.Report.Issues.Add(new("NX_PARTS_LIST_UPDATE_UNSUPPORTED", IssueSeverity.Error,
                    "Существующая Parts List найдена, но NX API не подтвердил обновление колонок и строк."));
                return;
            }
            partsList = existing;
            context.Report.UpdatedObjects.Add("partsList:" + ManagedId);
        }
        else
        {
            var nameCollision = all.FirstOrDefault(x =>
                string.Equals(NxReflection.GetName(x), ManagedId, StringComparison.OrdinalIgnoreCase)
                && !IsOwned(x));
            if (nameCollision is not null)
            {
                context.Report.Issues.Add(new("NX_PARTS_LIST_UNMANAGED_COLLISION", IssueSeverity.Error,
                    "Найдена ручная или принадлежащая другой области спецификация AUTO_PARTS_LIST. Автоматическое присвоение или изменение запрещено."));
                return;
            }

            var builderOwner = collection ?? annotations;
            var builder = NxReflection.InvokeFactory(builderOwner,
                context.ApiMap.Aliases("partsList.builder", "CreatePartsListBuilder", "PartsListBuilder"),
                (object?)null);
            if (builder is null)
            {
                context.Report.Issues.Add(new("NX_PARTS_LIST_UNSUPPORTED", IssueSeverity.Error,
                    "Не найден PartsListBuilder. Проверьте лицензию Drafting и api-map."));
                return;
            }

            var commitOwnsBuilder = false;
            try
            {
                _ = SetAny(builder, true, "Associative", "IsAssociative");
                _ = SetAny(builder, ManagedId, "Name", "TableName");
                _ = SetAny(builder, columns.Count, "ColumnCount", "NumberOfColumns");
                _ = SetAny(builder, rows.Count + 1, "RowCount", "NumberOfRows");
                commitOwnsBuilder = true;
                partsList = NxReflection.CommitObjectAndDestroy(builder);
            }
            finally
            {
                if (!commitOwnsBuilder) NxReflection.Destroy(builder);
            }

            if (!Populate(partsList, columns, rows))
                throw new InvalidOperationException("NX создал Parts List, но не подтвердил запись BOM-строк. Транзакция должна быть отменена.");
            NxObjectTools.TagManaged(partsList, ManagedId, context.Profile.ProfileId, context.ConfigHash,
                ObjectKind, context.ScopeId);
            context.Report.CreatedObjects.Add("partsList:" + ManagedId);
        }

        NxObjectTools.SetStringAttribute(partsList, "AUTO_DWG_TARGET_SHEET_ID", targetSheetId);
        NxObjectTools.SetStringAttribute(partsList, "AUTO_DWG_BOM_ROW_COUNT",
            rows.Count.ToString(CultureInfo.InvariantCulture));
        NxObjectTools.SetStringAttribute(partsList, "AUTO_DWG_BOM_FINGERPRINT", Fingerprint(rows));
        new NxBalloonService(context).CreateOrUpdate(rows);
    }

    private bool ResizeAndPopulate(
        object? collection,
        object partsList,
        IReadOnlyList<BomColumn> columns,
        IReadOnlyList<NxBomRow> rows)
    {
        var builder = NxReflection.InvokeFactory(collection,
            context.ApiMap.Aliases("partsList.builder", "CreatePartsListBuilder", "PartsListBuilder"),
            partsList);
        if (builder is not null)
        {
            var commitOwnsBuilder = false;
            try
            {
                _ = SetAny(builder, columns.Count, "ColumnCount", "NumberOfColumns");
                _ = SetAny(builder, rows.Count + 1, "RowCount", "NumberOfRows");
                _ = SetAny(builder, true, "Associative", "IsAssociative");
                commitOwnsBuilder = true;
                NxReflection.CommitCommandAndDestroy(builder);
            }
            finally
            {
                if (!commitOwnsBuilder) NxReflection.Destroy(builder);
            }
        }
        else
        {
            _ = SetAny(partsList, columns.Count, "ColumnCount", "NumberOfColumns");
            _ = SetAny(partsList, rows.Count + 1, "RowCount", "NumberOfRows");
        }
        if (!Populate(partsList, columns, rows)) return false;
        try { _ = NxReflection.InvokeCommand(partsList, ["Update", "Refresh", "Regenerate"]); }
        catch (Exception ex)
        {
            context.Log.Warn("Parts List update: " + ex.Message);
            return false;
        }
        return true;
    }

    private bool Populate(
        object partsList,
        IReadOnlyList<BomColumn> columns,
        IReadOnlyList<NxBomRow> rows)
    {
        for (var column = 0; column < columns.Count; column++)
            if (!TrySetCell(partsList, 0, column, columns[column].Title)) return false;

        for (var row = 0; row < rows.Count; row++)
        for (var column = 0; column < columns.Count; column++)
        {
            var value = ResolveValue(rows[row], columns[column].Source);
            if (!TrySetCell(partsList, row + 1, column, value)) return false;
        }
        return true;
    }

    private static string ResolveValue(NxBomRow row, string source)
        => source.ToLowerInvariant() switch
        {
            "position" or "item_number" => row.Position.ToString(CultureInfo.InvariantCulture),
            "part_number" => row.PartNumber,
            "description" or "name" => row.Description,
            "revision" => row.Revision,
            "material" => row.Material,
            "mass" => row.Mass?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty,
            "quantity" => row.Quantity.ToString(CultureInfo.InvariantCulture),
            "identity" => row.Identity,
            _ => string.Empty
        };

    private bool TrySetCell(object table, int row, int column, string value)
    {
        foreach (var indexes in new[] { (Row: row, Column: column), (Row: row + 1, Column: column + 1) })
        {
            try
            {
                if (NxReflection.InvokeCommand(table,
                        ["SetCellText", "SetCellValue", "SetText", "SetCell"],
                        indexes.Row, indexes.Column, value)) return true;
            }
            catch (Exception ex)
            {
                context.Log.Warn($"Parts List cell [{indexes.Row},{indexes.Column}]: {ex.Message}");
                return false;
            }
        }
        return false;
    }

    private IReadOnlyList<BomColumn> LoadColumns()
    {
        var nodes = JsonNavigator.GetArray(context.Profile.Root,
            "$.partsListAndBalloons.partsList.columns") ?? [];
        return nodes.Select((node, index) =>
        {
            var item = node?.AsObject();
            return new BomColumn(
                item?["id"]?.GetValue<string?>() ?? $"column_{index}",
                item?["title"]?.GetValue<string?>() ?? string.Empty,
                item?["source"]?.GetValue<string?>() ?? item?["id"]?.GetValue<string?>() ?? string.Empty);
        }).Where(x => !string.IsNullOrWhiteSpace(x.Title)).ToArray();
    }

    private bool IsOwned(object item)
        => NxObjectTools.IsManaged(item, ManagedId, context.Profile.ProfileId, ObjectKind, context.ScopeId);

    private bool SetAny(object target, object? value, params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (NxReflection.Set(target, value, path)) return true;
            }
            catch (Exception ex)
            {
                context.Log.Warn($"Parts List property {path}: {ex.Message}");
            }
        }
        return false;
    }

    private static string Fingerprint(IEnumerable<NxBomRow> rows)
    {
        var canonical = string.Join("\n", rows.Select(row =>
            $"{row.Position}|{row.Identity}|{row.PartNumber}|{row.Revision}|{row.Quantity}|{row.Material}|{row.Mass:R}"));
        return NxEskd.Core.Utilities.Hashing.Sha256(canonical);
    }

    private sealed record BomColumn(string Id, string Title, string Source);
}
