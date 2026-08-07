using System.Text.Json.Nodes;
using NxEskd.Core.Diagnostics;

namespace NxEskd.Core.Planning;

public sealed class DrawingPackagePlanner
{
    public (DrawingPackagePlan? Plan, ValidationReport Report) Build(string manifestPath)
    {
        var report = new ValidationReport();
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            report.Add(new("PKG_MANIFEST_MISSING", IssueSeverity.Error,
                $"Манифест комплекта не найден: {manifestPath}"));
            return (null, report);
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
                   ?? throw new InvalidDataException("Корневой JSON-объект отсутствует.");
        }
        catch (Exception ex)
        {
            report.Add(new("PKG_MANIFEST_INVALID_JSON", IssueSeverity.Error,
                "Манифест комплекта повреждён: " + ex.Message));
            return (null, report);
        }

        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var packageId = RequiredString(root, "packageId", report, "$.packageId");
        var name = OptionalString(root, "name") ?? packageId ?? "Без наименования";
        var revision = OptionalString(root, "revision") ?? string.Empty;
        var outputDirectory = ResolvePath(baseDirectory,
            OptionalString(root, "outputDirectory") ?? Path.Combine("output", packageId ?? "package"));
        var sourceAssemblyRaw = OptionalString(root, "sourceAssembly");
        var sourceAssembly = string.IsNullOrWhiteSpace(sourceAssemblyRaw)
            ? null
            : ResolvePath(baseDirectory, sourceAssemblyRaw);

        if (sourceAssembly is not null && !File.Exists(sourceAssembly))
            report.Add(new("PKG_SOURCE_ASSEMBLY_MISSING", IssueSeverity.Error,
                $"Исходная сборка комплекта не найдена: {sourceAssembly}", JsonPath: "$.sourceAssembly"));

        var documentsNode = root["documents"] as JsonArray;
        if (documentsNode is null || documentsNode.Count == 0)
        {
            report.Add(new("PKG_DOCUMENTS_EMPTY", IssueSeverity.Error,
                "Комплект не содержит документов.", JsonPath: "$.documents"));
            return (null, report);
        }

        var documents = new List<DrawingPackageDocumentPlan>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var designations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < documentsNode.Count; index++)
        {
            if (documentsNode[index] is not JsonObject node)
            {
                report.Add(new("PKG_DOCUMENT_INVALID", IssueSeverity.Error,
                    $"Элемент documents[{index}] должен быть объектом.", JsonPath: $"$.documents[{index}]"));
                continue;
            }

            var path = $"$.documents[{index}]";
            var id = RequiredString(node, "documentId", report, path + ".documentId");
            var sourcePartRaw = RequiredString(node, "sourcePart", report, path + ".sourcePart");
            var profileRaw = RequiredString(node, "profilePath", report, path + ".profilePath");
            var designation = RequiredString(node, "designation", report, path + ".designation");
            var documentName = OptionalString(node, "name") ?? designation ?? id ?? "Документ";
            var documentKind = OptionalString(node, "documentKind") ?? "part_drawing";
            var enabled = node["enabled"]?.GetValue<bool?>() ?? true;
            if (id is null || sourcePartRaw is null || profileRaw is null || designation is null) continue;

            if (!ids.Add(id))
                report.Add(new("PKG_DUPLICATE_DOCUMENT_ID", IssueSeverity.Error,
                    $"Повторяется documentId '{id}'.", JsonPath: path + ".documentId", ObjectId: id));
            if (!designations.Add(designation))
                report.Add(new("PKG_DUPLICATE_DESIGNATION", IssueSeverity.Error,
                    $"В комплекте повторяется обозначение '{designation}'.", JsonPath: path + ".designation", ObjectId: id));
            if (designation.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                report.Add(new("PKG_DESIGNATION_INVALID_FOR_FILE", IssueSeverity.Error,
                    $"Обозначение '{designation}' содержит символы, недопустимые в имени файла.",
                    JsonPath: path + ".designation", ObjectId: id));

            var sourcePart = ResolvePath(baseDirectory, sourcePartRaw);
            var profilePath = ResolvePath(baseDirectory, profileRaw);
            var outputRaw = OptionalString(node, "nativeOutput") ?? Path.Combine(outputDirectory, designation + ".prt");
            var nativeOutput = Path.IsPathRooted(outputRaw)
                ? Path.GetFullPath(outputRaw)
                : ResolvePath(outputDirectory, outputRaw);
            if (!outputs.Add(nativeOutput))
                report.Add(new("PKG_DUPLICATE_OUTPUT", IssueSeverity.Error,
                    $"Несколько документов публикуются в один файл: {nativeOutput}",
                    JsonPath: path + ".nativeOutput", ObjectId: id));
            if (!nativeOutput.EndsWith(".prt", StringComparison.OrdinalIgnoreCase))
                report.Add(new("PKG_NATIVE_EXTENSION_INVALID", IssueSeverity.Error,
                    $"Нативный документ должен иметь расширение .prt: {nativeOutput}",
                    JsonPath: path + ".nativeOutput", ObjectId: id));

            if (enabled && !File.Exists(sourcePart))
                report.Add(new("PKG_SOURCE_PART_MISSING", IssueSeverity.Error,
                    $"Исходная деталь документа '{id}' не найдена: {sourcePart}",
                    JsonPath: path + ".sourcePart", ObjectId: id));
            if (enabled && !File.Exists(profilePath))
                report.Add(new("PKG_PROFILE_MISSING", IssueSeverity.Error,
                    $"Профиль документа '{id}' не найден: {profilePath}",
                    JsonPath: path + ".profilePath", ObjectId: id));

            var dependencies = (node["dependencies"] as JsonArray)?
                .Select(value => value?.GetValue<string?>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
            if (dependencies.Contains(id, StringComparer.OrdinalIgnoreCase))
                report.Add(new("PKG_SELF_DEPENDENCY", IssueSeverity.Error,
                    $"Документ '{id}' зависит сам от себя.", JsonPath: path + ".dependencies", ObjectId: id));

            documents.Add(new DrawingPackageDocumentPlan(
                id,
                sourcePart,
                profilePath,
                designation,
                documentName,
                documentKind,
                nativeOutput,
                dependencies,
                enabled));
        }

        var byId = documents
            .GroupBy(document => document.DocumentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents.Where(document => document.Enabled))
        foreach (var dependency in document.Dependencies)
        {
            if (!byId.TryGetValue(dependency, out var dependencyDocument))
                report.Add(new("PKG_DEPENDENCY_MISSING", IssueSeverity.Error,
                    $"Документ '{document.DocumentId}' зависит от отсутствующего документа '{dependency}'.",
                    ObjectId: document.DocumentId));
            else if (!dependencyDocument.Enabled)
                report.Add(new("PKG_DEPENDENCY_DISABLED", IssueSeverity.Error,
                    $"Документ '{document.DocumentId}' зависит от отключённого документа '{dependency}'.",
                    ObjectId: document.DocumentId));
        }

        var enabledDocuments = documents
            .Where(document => document.Enabled)
            .GroupBy(document => document.DocumentId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var executionOrder = TopologicalOrder(enabledDocuments, report);
        var publicationNode = root["publication"] as JsonObject;
        var journalRaw = OptionalString(publicationNode, "journalPath")
                         ?? Path.Combine(outputDirectory, ".nx-eskd-package-journal.json");
        var journalPath = Path.IsPathRooted(journalRaw)
            ? Path.GetFullPath(journalRaw)
            : ResolvePath(baseDirectory, journalRaw);
        var publication = new PackagePublicationPolicy(
            publicationNode?["dryRun"]?.GetValue<bool?>() ?? true,
            publicationNode?["stopOnFailure"]?.GetValue<bool?>() ?? true,
            publicationNode?["resumeFromJournal"]?.GetValue<bool?>() ?? true,
            publicationNode?["atomicPublish"]?.GetValue<bool?>() ?? true,
            publicationNode?["requireAllDocuments"]?.GetValue<bool?>() ?? true,
            journalPath);

        if (publication.AtomicPublish)
        {
            var rootPath = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var document in documents.Where(document => document.Enabled))
                if (!Path.GetFullPath(document.NativeOutputPath).StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                    report.Add(new("PKG_ATOMIC_OUTPUT_OUTSIDE_ROOT", IssueSeverity.Error,
                        $"Для atomicPublish файл документа '{document.DocumentId}' должен находиться внутри outputDirectory.",
                        ObjectId: document.DocumentId));
        }

        if (report.HasErrors || packageId is null) return (null, report);
        var plan = new DrawingPackagePlan(
            packageId,
            name,
            revision,
            sourceAssembly,
            outputDirectory,
            documents,
            executionOrder,
            publication);
        return (plan, report);
    }

    private static IReadOnlyList<string> TopologicalOrder(
        IReadOnlyList<DrawingPackageDocumentPlan> documents,
        ValidationReport report)
    {
        var byId = documents.ToDictionary(document => document.DocumentId, StringComparer.OrdinalIgnoreCase);
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        void Visit(string id, Stack<string> path)
        {
            if (!byId.TryGetValue(id, out var document)) return;
            if (state.TryGetValue(id, out var current))
            {
                if (current == 2) return;
                if (current == 1)
                {
                    var cycle = string.Join(" -> ", path.Reverse().Append(id));
                    report.Add(new("PKG_DEPENDENCY_CYCLE", IssueSeverity.Error,
                        "Обнаружен цикл документов комплекта: " + cycle, ObjectId: id));
                    return;
                }
            }

            state[id] = 1;
            path.Push(id);
            foreach (var dependency in document.Dependencies.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                Visit(dependency, path);
            path.Pop();
            state[id] = 2;
            result.Add(id);
        }

        foreach (var document in documents.OrderBy(document => document.DocumentId, StringComparer.OrdinalIgnoreCase))
            Visit(document.DocumentId, new Stack<string>());
        return result;
    }

    private static string? RequiredString(JsonObject node, string property, ValidationReport report, string jsonPath)
    {
        var value = OptionalString(node, property);
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        report.Add(new("PKG_REQUIRED_VALUE_MISSING", IssueSeverity.Error,
            $"Обязательное поле '{property}' не заполнено.", JsonPath: jsonPath));
        return null;
    }

    private static string? OptionalString(JsonObject? node, string property)
        => node?[property]?.GetValue<string?>()?.Trim();

    private static string ResolvePath(string baseDirectory, string value)
        => Path.GetFullPath(value, baseDirectory);
}
