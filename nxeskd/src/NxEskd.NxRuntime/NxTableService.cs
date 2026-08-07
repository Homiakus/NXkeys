using NXOpen;
using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxTableService(NxServiceContext context)
{
    private const string ManagedId = "BEND_TABLE";
    private const string ObjectKind = "table";

    public void CreateBendTableIfRequired(DrawingPlan plan)
    {
        if (!ModelIsSheetMetal()) return;
        if (!JsonNavigator.GetBool(context.Profile.Root, "$.sheetMetalFlatPattern.bendTable.enabled", false)) return;

        var rows = LoadConfiguredRows();
        if (rows.Count == 0)
        {
            context.Report.Issues.Add(new("NX_BEND_DATA_UNAVAILABLE", IssueSeverity.ManualReview,
                "Таблица гибов включена, но достоверные данные гибов не предоставлены. Пустая таблица больше не создаётся. " +
                "Добавьте строки в $.job.sheetMetalFlatPattern.bends либо подключите подтверждённый NX BendSnapshot adapter."));
            return;
        }

        var targetSheet = new NxSheetTargetService(context).Activate(plan, "flat_pattern", "main");
        if (targetSheet is null) return;
        var targetSheetId = NxObjectTools.GetStringAttribute(targetSheet, "AUTO_DWG_ID")
                            ?? plan.Sheets.FirstOrDefault(sheet => sheet.Role.Equals("flat_pattern", StringComparison.OrdinalIgnoreCase))?.Id
                            ?? plan.Sheets.FirstOrDefault()?.Id
                            ?? string.Empty;

        var annotations = NxReflection.Get(context.WorkPart, "Annotations");
        var tables = NxReflection.Get(annotations, "TableSections", "TabularNotes", "Tables");
        var all = NxReflection.Enumerate(tables).ToArray();
        var existing = all.FirstOrDefault(IsOwned);
        if (existing is not null)
        {
            NxObjectTools.EnsureOwnershipMetadata(existing, ManagedId, context.Profile.ProfileId,
                context.ConfigHash, ObjectKind, context.ScopeId);
            if (!PopulateTable(existing, rows))
                context.Report.Issues.Add(new("NX_BEND_TABLE_UPDATE_UNSUPPORTED", IssueSeverity.Error,
                    "Существующая таблица гибов найдена, но NX API не подтвердил обновление её ячеек."));
            else
            {
                NxObjectTools.SetStringAttribute(existing, "AUTO_DWG_TARGET_SHEET_ID", targetSheetId);
                context.Report.UpdatedObjects.Add("table:" + ManagedId);
            }
            return;
        }

        var nameCollision = all.FirstOrDefault(x =>
            string.Equals(NxReflection.GetName(x), ManagedId, StringComparison.OrdinalIgnoreCase)
            && !IsOwned(x));
        if (nameCollision is not null)
        {
            context.Report.Issues.Add(new("NX_BEND_TABLE_UNMANAGED_COLLISION", IssueSeverity.Error,
                "Найдена ручная или принадлежащая другой области таблица BEND_TABLE. Автоматическое присвоение или изменение запрещено."));
            return;
        }

        var builder = NxReflection.InvokeFactory(tables ?? annotations,
            context.ApiMap.Aliases("table.builder", "CreateTableSectionBuilder", "CreateTabularNoteBuilder", "CreateTableBuilder"), (object?)null);
        if (builder is null)
        {
            context.Report.Issues.Add(new("NX_BEND_TABLE_UNSUPPORTED", IssueSeverity.Warning,
                "Builder таблицы гибов не найден. Данные гибов сохранены только в отчёте."));
            return;
        }

        var columns = LoadColumns();
        var commitOwnsBuilder = false;
        try
        {
            NxReflection.Set(builder, JsonNavigator.GetString(context.Profile.Root, "$.sheetMetalFlatPattern.bendTable.title", "Таблица гибов"), "Title", "Name");
            NxReflection.Set(builder, new Point3d(260, 35, 0), "Origin", "Placement", "Position");
            NxReflection.Set(builder, columns.Count, "ColumnCount", "NumberOfColumns");
            NxReflection.Set(builder, rows.Count + 1, "RowCount", "NumberOfRows");
            commitOwnsBuilder = true;
            var table = NxReflection.CommitObjectAndDestroy(builder);
            if (!PopulateTable(table, rows))
                throw new InvalidOperationException("NX создал таблицу гибов, но не подтвердил запись заголовков и строк.");
            NxObjectTools.TagManaged(table, ManagedId, context.Profile.ProfileId, context.ConfigHash, ObjectKind, context.ScopeId);
            NxObjectTools.SetStringAttribute(table, "AUTO_DWG_TARGET_SHEET_ID", targetSheetId);
            context.Report.CreatedObjects.Add("table:" + ManagedId);
        }
        finally
        {
            if (!commitOwnsBuilder) NxReflection.Destroy(builder);
        }
    }

    private bool ModelIsSheetMetal()
    {
        if (!context.Report.Metrics.TryGetValue("model.isSheetMetal", out var raw) || raw is null) return false;
        return raw is bool value ? value : bool.TryParse(raw.ToString(), out value) && value;
    }

    private bool IsOwned(object item)
        => NxObjectTools.IsManaged(item, ManagedId, context.Profile.ProfileId, ObjectKind, context.ScopeId);

    private IReadOnlyList<BendColumn> LoadColumns()
    {
        var configured = JsonNavigator.GetArray(context.Profile.Root, "$.sheetMetalFlatPattern.bendTable.columns") ?? [];
        var columns = configured.Select(node =>
        {
            var item = node?.AsObject();
            return new BendColumn(
                item?["id"]?.GetValue<string?>() ?? string.Empty,
                item?["title"]?.GetValue<string?>() ?? string.Empty,
                item?["source"]?.GetValue<string?>() ?? string.Empty);
        }).Where(x => !string.IsNullOrWhiteSpace(x.Id)).ToArray();
        return columns.Length > 0
            ? columns
            : [new("number", "№", "bend_sequence"), new("angle", "Угол", "bend_angle"), new("radius", "R", "inside_radius")];
    }

    private IReadOnlyList<IReadOnlyDictionary<string, string>> LoadConfiguredRows()
    {
        var rows = JsonNavigator.GetArray(context.Profile.Root, "$.job.sheetMetalFlatPattern.bends") ?? [];
        return rows.Select((node, index) =>
        {
            var item = node?.AsObject();
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bend_sequence"] = item?["number"]?.ToString() ?? (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["bend_angle"] = item?["angle"]?.ToString() ?? string.Empty,
                ["inside_radius"] = item?["radius"]?.ToString() ?? string.Empty,
                ["bend_direction"] = item?["direction"]?.GetValue<string?>() ?? string.Empty,
                ["bend_note"] = item?["note"]?.GetValue<string?>() ?? string.Empty
            };
            return (IReadOnlyDictionary<string, string>)result;
        }).ToArray();
    }

    private bool PopulateTable(object table, IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        var columns = LoadColumns();
        for (var column = 0; column < columns.Count; column++)
            if (!TrySetCell(table, 0, column, columns[column].Title)) return false;

        for (var row = 0; row < rows.Count; row++)
        for (var column = 0; column < columns.Count; column++)
        {
            rows[row].TryGetValue(columns[column].Source, out var value);
            if (!TrySetCell(table, row + 1, column, value ?? string.Empty)) return false;
        }
        return true;
    }

    private static bool TrySetCell(object table, int row, int column, string value)
    {
        foreach (var indexes in new[] { (Row: row, Column: column), (Row: row + 1, Column: column + 1) })
        {
            try
            {
                if (NxReflection.InvokeCommand(table,
                        ["SetCellText", "SetCellValue", "SetText", "SetCell"], indexes.Row, indexes.Column, value))
                    return true;
            }
            catch
            {
                break;
            }
        }
        return false;
    }

    private sealed record BendColumn(string Id, string Title, string Source);
}
