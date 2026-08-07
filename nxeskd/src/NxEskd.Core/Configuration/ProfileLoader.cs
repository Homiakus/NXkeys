using System.Text.Json;
using System.Text.Json.Nodes;
using NxEskd.Core.Utilities;

namespace NxEskd.Core.Configuration;

public static class ProfileLoader
{
    public static ProfileDocument Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Путь к профилю не задан.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("Файл профиля не найден.", path);

        var json = File.ReadAllText(path);
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        })?.AsObject() ?? throw new InvalidDataException("Корневой элемент профиля должен быть JSON-объектом.");

        var migration = ProfileMigrationPipeline.Apply(root);
        return new ProfileDocument(path, migration.Root, migration.OriginalVersion, migration.Notes);
    }

    public static void SaveAtomic(ProfileDocument document, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        AtomicFile.WriteAllText(path ?? document.SourcePath, document.ToJson(), createBackup: true);
    }
}
