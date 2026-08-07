using System.Text.Json;
using System.Text.Json.Nodes;

namespace NxEskd.Core.Configuration;

public sealed class ProfileDocument
{
    public ProfileDocument(
        string sourcePath,
        JsonObject root,
        string? originalSchemaVersion = null,
        IReadOnlyList<string>? migrationNotes = null)
    {
        SourcePath = Path.GetFullPath(sourcePath);
        Root = root;
        OriginalSchemaVersion = originalSchemaVersion ?? SchemaVersion;
        MigrationNotes = migrationNotes ?? Array.Empty<string>();
    }

    public string SourcePath { get; }
    public string BaseDirectory => string.IsNullOrWhiteSpace(Path.GetDirectoryName(SourcePath)) ? Environment.CurrentDirectory : Path.GetDirectoryName(SourcePath)!;
    public JsonObject Root { get; }

    public string ProfileId => JsonNavigator.GetString(Root, "$.profileId") ?? "UNKNOWN_PROFILE";
    public string SchemaVersion => JsonNavigator.GetString(Root, "$.schemaVersion") ?? "0.0.0";
    public string OriginalSchemaVersion { get; }
    public IReadOnlyList<string> MigrationNotes { get; }
    public bool WasMigrated => !string.Equals(OriginalSchemaVersion, SchemaVersion, StringComparison.OrdinalIgnoreCase);

    public string ToJson(bool indented = true) => Root.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = indented,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

    public ProfileDocument DeepClone(string? newSourcePath = null)
    {
        var clone = JsonNode.Parse(ToJson())?.AsObject()
                    ?? throw new InvalidOperationException("Не удалось клонировать профиль.");
        return new ProfileDocument(newSourcePath ?? SourcePath, clone, OriginalSchemaVersion, MigrationNotes.ToArray());
    }
}
