using NxEskd.Core.Configuration;
using NxEskd.Core.Diagnostics;
using NxEskd.Core.Planning;

namespace NxEskd.NxRuntime;

internal sealed class NxExportService(NxServiceContext context)
{
    public void Export(DrawingPlan plan)
    {
        ExportPdf(plan);
        ExportFlatPatternDxf(plan);
    }

    private void ExportPdf(DrawingPlan plan)
    {
        if (!JsonNavigator.GetBool(context.Profile.Root, "$.output.pdf.enabled", true)) return;
        var raw = JsonNavigator.GetString(context.Profile.Root, "$.output.pdf.file");
        if (string.IsNullOrWhiteSpace(raw)) return;
        var finalPath = Expand(raw, plan);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var temporaryPath = BuildTemporaryPath(finalPath);

        var manager = NxReflection.Get(context.WorkPart,
                          context.ApiMap.Aliases("export.plotManager", "PlotManager", "PrintManager"))
                      ?? NxReflection.Get(context.Session, "DexManager");
        var builder = NxReflection.InvokeFactory(manager,
            context.ApiMap.Aliases("export.pdfBuilder",
                "CreatePrintPdfbuilder", "CreatePrintPdfBuilder", "CreatePrintPDFBuilder", "CreatePdfBuilder"));
        if (builder is null)
        {
            context.Report.Issues.Add(new("NX_PDF_EXPORT_UNSUPPORTED", IssueSeverity.Warning,
                "WorkPart.PlotManager.CreatePrintPdfbuilder не найден; PDF не сформирован."));
            return;
        }

        var commitOwnsBuilder = false;
        try
        {
            if (!NxReflection.Set(builder, temporaryPath, "Filename", "FileName", "OutputFile"))
                throw new InvalidOperationException("PrintPDFBuilder не принял имя выходного файла.");

            var allSheets = JsonNavigator.GetBool(context.Profile.Root, "$.output.pdf.allSheets", true);
            _ = NxReflection.Set(builder, allSheets, "AllSheets", "PrintAllSheets");
            _ = NxReflection.Set(builder,
                JsonNavigator.GetBool(context.Profile.Root, "$.output.pdf.monochrome", true),
                "Monochrome", "BlackAndWhite");
            ConfigurePdfSource(builder, plan, allSheets);

            commitOwnsBuilder = true;
            NxReflection.CommitCommandAndDestroy(builder);
            PublishVerifiedOutput(temporaryPath, finalPath, "PDF");
            context.Report.CreatedObjects.Add("file:" + finalPath);
            context.Log.Info("PDF: " + finalPath);
        }
        catch (Exception ex)
        {
            context.Report.Issues.Add(new("NX_PDF_EXPORT_FAILED", IssueSeverity.Warning,
                "Экспорт PDF завершился ошибкой: " + ex.Message));
        }
        finally
        {
            if (!commitOwnsBuilder) NxReflection.Destroy(builder);
            DeleteTemporary(temporaryPath);
        }
    }

    private void ConfigurePdfSource(object builder, DrawingPlan plan, bool allSheets)
    {
        var source = NxReflection.Get(builder, "SourceBuilder", "Source", "SheetSource");
        if (source is null) return;

        _ = NxReflection.Set(source, allSheets, "AllSheets", "PrintAllSheets");
        if (allSheets) return;

        var sheets = NxReflection.Get(context.WorkPart,
            context.ApiMap.Aliases("sheet.collection", "DrawingSheets"));
        var planned = plan.Sheets
            .Select(sheet => NxReflection.FindByName(sheets, sheet.Id))
            .Where(sheet => sheet is not null)
            .Cast<object>()
            .ToArray();
        if (planned.Length == 0) return;

        _ = NxReflection.TryInvokeCommand(source,
                ["SetSheets", "SetDrawingSheets", "SetSelectedSheets"], planned)
            || NxReflection.Set(source, planned, "Sheets", "DrawingSheets", "SelectedSheets");
    }

    private void ExportFlatPatternDxf(DrawingPlan plan)
    {
        if (!plan.Model.IsSheetMetal) return;
        if (!JsonNavigator.GetBool(context.Profile.Root, "$.sheetMetalFlatPattern.dxfExport.enabled", false)) return;
        var raw = JsonNavigator.GetString(context.Profile.Root, "$.sheetMetalFlatPattern.dxfExport.file");
        if (string.IsNullOrWhiteSpace(raw)) return;
        var finalPath = Expand(raw, plan);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var temporaryPath = BuildTemporaryPath(finalPath);
        var flatPattern = FindManagedFlatPattern();
        if (flatPattern is null)
        {
            context.Report.Issues.Add(new("NX_DXF_FLAT_PATTERN_MISSING", IssueSeverity.Error,
                "DXF не экспортирован: управляемый Flat Pattern текущего задания не найден. " +
                "Экспорт всей детали вместо развертки запрещён."));
            return;
        }

        var dex = NxReflection.Get(context.Session, "DexManager");
        var builder = NxReflection.InvokeFactory(dex,
            context.ApiMap.Aliases("export.dxfBuilder", "CreateDxfdwgCreator", "CreateDxfCreator", "CreateDxfdwgBuilder"));
        if (builder is null)
        {
            context.Report.Issues.Add(new("NX_DXF_EXPORT_UNSUPPORTED", IssueSeverity.Warning,
                "DXF builder не найден; DXF не сформирован."));
            return;
        }

        var commitOwnsBuilder = false;
        try
        {
            NxReflection.Set(builder, temporaryPath, "OutputFile", "Filename", "FileName");
            NxReflection.Set(builder, "DXF", "OutputType", "ExportType");
            NxReflection.Set(builder,
                JsonNavigator.GetString(context.Profile.Root, "$.sheetMetalFlatPattern.dxfExport.units", "millimeter"),
                "Units", "OutputUnits");
            NxReflection.Set(builder,
                JsonNavigator.GetString(context.Profile.Root, "$.sheetMetalFlatPattern.dxfExport.version", "AutoCAD_2013"),
                "Version", "OutputVersion", "AutoCADVersion");

            var sourceConfigured = NxReflection.Set(builder, flatPattern,
                "SourceObject", "ObjectToExport", "FlatPattern", "Selection", "ExportObject");
            if (!sourceConfigured)
                throw new InvalidOperationException(
                    "DXF builder найден, но локальный NX API не подтвердил выбор Flat Pattern как единственного источника. " +
                    "Небезопасный экспорт всей детали отменён.");

            commitOwnsBuilder = true;
            NxReflection.CommitCommandAndDestroy(builder);
            PublishVerifiedOutput(temporaryPath, finalPath, "DXF");
            context.Report.CreatedObjects.Add("file:" + finalPath);
            context.Log.Info("DXF Flat Pattern: " + finalPath);
        }
        finally
        {
            if (!commitOwnsBuilder) NxReflection.Destroy(builder);
            DeleteTemporary(temporaryPath);
        }
    }

    private object? FindManagedFlatPattern()
    {
        var features = NxReflection.Get(context.WorkPart, "Features");
        return NxReflection.Enumerate(features).FirstOrDefault(x =>
            NxObjectTools.IsManaged(x, "FLAT_PATTERN", context.Profile.ProfileId, "feature", context.ScopeId));
    }

    private static string BuildTemporaryPath(string finalPath)
    {
        var directory = Path.GetDirectoryName(finalPath)!;
        var extension = Path.GetExtension(finalPath);
        var name = Path.GetFileNameWithoutExtension(finalPath);
        return Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.tmp{extension}");
    }

    private static void PublishVerifiedOutput(string temporaryPath, string finalPath, string kind)
    {
        var info = new FileInfo(temporaryPath);
        if (!info.Exists || info.Length == 0)
            throw new IOException($"NX сообщил об успешном экспорте {kind}, но временный файл отсутствует или пуст: {temporaryPath}");
        File.Move(temporaryPath, finalPath, overwrite: true);
        var published = new FileInfo(finalPath);
        if (!published.Exists || published.Length == 0)
            throw new IOException($"Не удалось атомарно опубликовать {kind}: {finalPath}");
    }

    private static void DeleteTemporary(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private string Expand(string raw, DrawingPlan plan)
    {
        var vars = VariableExpander.BuildDefault(context.Profile, context.WorkPart.FullPath);
        vars["DOCUMENT_DESIGNATION"] = plan.Designation;
        return Path.GetFullPath(new VariableExpander(vars).Expand(raw, false), context.Profile.BaseDirectory);
    }
}
