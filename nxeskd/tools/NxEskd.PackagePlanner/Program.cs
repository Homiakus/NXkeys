using System.Text.Json;
using System.Text.Json.Serialization;
using NxEskd.Core.Planning;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: NxEskd.PackagePlanner <drawing-package.json>");
    return 1;
}

var manifestPath = Path.GetFullPath(args[0]);
var (plan, report) = new DrawingPackagePlanner().Build(manifestPath);
var payload = new
{
    manifestPath,
    success = plan is not null && !report.HasErrors,
    issues = report.Issues,
    package = plan is null ? null : new
    {
        plan.PackageId,
        plan.Name,
        plan.Revision,
        plan.SourceAssembly,
        plan.OutputDirectory,
        plan.ExecutionOrder,
        plan.Publication,
        documents = plan.Documents.Select(document => new
        {
            document.DocumentId,
            document.Designation,
            document.Name,
            document.DocumentKind,
            document.SourcePart,
            document.ProfilePath,
            document.NativeOutputPath,
            document.Dependencies,
            document.Enabled
        })
    }
};

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};
Console.WriteLine(JsonSerializer.Serialize(payload, options));
return payload.success ? 0 : 2;
