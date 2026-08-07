using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxTitleBlockService(NxServiceContext context, NxAttributeService attributes)
{
    public void Fill(object sheet, SheetPlan plan, DrawingPlan drawingPlan, int sheetNumber)
    {
        foreach (var cell in plan.TitleBlockValues)
        {
            var value = attributes.ResolveTitleValue(cell, drawingPlan, sheetNumber, drawingPlan.Sheets.Count);
            if (!string.IsNullOrWhiteSpace(value)) NxObjectTools.SetStringAttribute(context.WorkPart, cell.Label, value);
            else if (cell.Required)
                context.Report.Issues.Add(new("NX_TITLE_VALUE_MISSING", IssueSeverity.Error,
                    $"Не разрешено обязательное поле штампа {cell.Label}.", ObjectId: cell.Label, SheetId: plan.Id));
        }

        if (context.Report.Issues.Any(issue =>
                issue.Severity == IssueSeverity.Error && issue.SheetId == plan.Id))
            return;

        var annotations = NxReflection.Get(context.WorkPart, "Annotations");
        var titleBlocks = NxReflection.Get(annotations,
            context.ApiMap.Aliases("titleBlock.collection", "TitleBlocks", "TitleBlockCollection"));
        var activeDefinition = JsonNavigator.GetString(context.Profile.Root, "$.titleBlocks.activeDefinition");
        var definitions = JsonNavigator.GetArray(context.Profile.Root, "$.titleBlocks.definitions") ?? [];
        var selectedDefinition = definitions.Select(x => x?.AsObject()).FirstOrDefault(x =>
            string.Equals(x?["id"]?.GetValue<string?>(), activeDefinition, StringComparison.OrdinalIgnoreCase));
        var definitionName = selectedDefinition?["objectSelector"]?["objectName"]?.GetValue<string?>();

        if (string.IsNullOrWhiteSpace(definitionName))
        {
            context.Report.Issues.Add(new("NX_TITLE_BLOCK_SELECTOR_MISSING", IssueSeverity.Error,
                $"Активное определение основной надписи '{activeDefinition}' не задаёт objectSelector.objectName.",
                SheetId: plan.Id,
                JsonPath: "$.titleBlocks.definitions"));
            return;
        }

        var titleBlock = NxReflection.FindByName(titleBlocks, definitionName);
        if (titleBlock is null)
        {
            context.Report.Issues.Add(new("NX_TITLE_BLOCK_OBJECT_MISSING", IssueSeverity.Error,
                $"На листе {plan.Id} не найден обязательный объект основной надписи '{definitionName}'. " +
                "Заполнение только атрибутов детали не считается подтверждённым оформлением чертежа.",
                ObjectId: definitionName,
                SheetId: plan.Id));
            return;
        }

        NxObjectTools.SetStringAttribute(titleBlock, "AUTO_DWG_TARGET_SHEET_ID", plan.Id);
        var builder = NxReflection.InvokeFactory(titleBlocks,
            context.ApiMap.Aliases("titleBlock.editBuilder", "CreateEditTitleBlockBuilder", "CreateTitleBlockBuilder"),
            titleBlock);
        if (builder is null)
        {
            context.Report.Issues.Add(new("NX_TITLE_BLOCK_EDIT_UNAVAILABLE", IssueSeverity.Error,
                $"NX API не предоставил редактор объекта основной надписи '{definitionName}' на листе {plan.Id}. " +
                "Невозможно подтвердить запись обязательных ячеек.",
                ObjectId: definitionName,
                SheetId: plan.Id));
            return;
        }

        var commitOwnsBuilder = false;
        try
        {
            foreach (var cell in plan.TitleBlockValues)
            {
                var value = attributes.ResolveTitleValue(cell, drawingPlan, sheetNumber, drawingPlan.Sheets.Count);
                if (value is null) continue;
                if (TrySetCell(builder, titleBlock, cell.Label, value)) continue;

                context.Report.Issues.Add(new(
                    cell.Required ? "NX_TITLE_BLOCK_REQUIRED_CELL_WRITE_FAILED" : "NX_TITLE_BLOCK_CELL_WRITE_UNCONFIRMED",
                    cell.Required ? IssueSeverity.Error : IssueSeverity.Warning,
                    $"Ячейка основной надписи '{cell.Label}' не была подтверждённо записана на листе {plan.Id}.",
                    ObjectId: cell.Label,
                    SheetId: plan.Id));
            }

            if (context.Report.Issues.Any(issue =>
                    issue.Severity == IssueSeverity.Error && issue.SheetId == plan.Id))
                return;

            commitOwnsBuilder = true;
            NxReflection.CommitCommandAndDestroy(builder);
        }
        finally
        {
            if (!commitOwnsBuilder) NxReflection.Destroy(builder);
        }
    }

    private bool TrySetCell(object builder, object titleBlock, string label, string value)
    {
        foreach (var target in new[] { builder, titleBlock })
        {
            foreach (var args in new object?[][] { [label, value], [label, 0, value] })
            {
                try
                {
                    if (NxReflection.InvokeCommand(target,
                            context.ApiMap.Aliases("titleBlock.setCell", "SetCellValue", "SetValueForLabel", "SetCellText", "SetValue"), args))
                        return true;
                }
                catch
                {
                    break;
                }
            }
        }

        var cells = NxReflection.Get(builder, "Cells", "CellList") ?? NxReflection.Get(titleBlock, "Cells", "CellList");
        foreach (var cell in NxReflection.Enumerate(cells))
        {
            var cellLabel = NxReflection.Get(cell, "Label", "Name")?.ToString();
            if (!string.Equals(cellLabel, label, StringComparison.OrdinalIgnoreCase)) continue;
            return NxReflection.Set(cell, value, "Value", "Text", "CellText");
        }
        return false;
    }
}
