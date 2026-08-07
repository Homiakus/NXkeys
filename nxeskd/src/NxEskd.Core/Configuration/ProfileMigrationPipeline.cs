using System.Text.Json.Nodes;

namespace NxEskd.Core.Configuration;

internal static class ProfileMigrationPipeline
{
    private static readonly IReadOnlyDictionary<string, Func<JsonObject, IReadOnlyList<string>>> Migrations =
        new Dictionary<string, Func<JsonObject, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["0.9.0"] = Migrate09To100,
            ["0.9.1"] = Migrate09To100
        };

    public static MigrationResult Apply(JsonObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var root = source.DeepClone().AsObject();
        var originalVersion = root["schemaVersion"]?.GetValue<string?>() ?? "0.0.0";
        if (!Migrations.TryGetValue(originalVersion, out var migration))
            return new MigrationResult(root, originalVersion, originalVersion, []);

        var notes = migration(root);
        root["schemaVersion"] = ProfileValidator.CurrentSchemaVersion;
        return new MigrationResult(root, originalVersion, ProfileValidator.CurrentSchemaVersion, notes);
    }

    private static IReadOnlyList<string> Migrate09To100(JsonObject root)
    {
        var notes = new List<string>();
        root["$schema"] ??= "./nx-eskd-profile.schema.json";
        root["documentType"] ??= "nx-eskd-drawing-generation-profile";

        var execution = root["execution"] as JsonObject ?? new JsonObject();
        root["execution"] = execution;
        execution["rollbackOnError"] ??= true;
        execution["preserveManualObjects"] ??= true;
        execution["singleUndoTransaction"] ??= true;
        execution["allowUnknownJsonProperties"] ??= false;
        notes.Add("Добавлены обязательные безопасные execution-настройки версии 1.0.0.");

        var job = root["job"] as JsonObject;
        if (job is not null && job["jobId"] is null && job["id"] is JsonNode legacyId)
        {
            job["jobId"] = legacyId.DeepClone();
            job.Remove("id");
            notes.Add("Поле job.id переименовано в job.jobId.");
        }

        return notes;
    }
}

internal sealed record MigrationResult(
    JsonObject Root,
    string OriginalVersion,
    string EffectiveVersion,
    IReadOnlyList<string> Notes)
{
    public bool WasMigrated => !string.Equals(OriginalVersion, EffectiveVersion, StringComparison.OrdinalIgnoreCase);
}
