using System.Text.Json;
using System.Text.Json.Serialization;
using NxEskd.Core.Utilities;

namespace NxEskd.Core.Runtime;

public enum DrawingCommand
{
    None,
    Generate,
    Update,
    Validate,
    Preview,
    Inventory
}

[method: JsonConstructor]
public sealed record CommandRequest(
    int ProtocolVersion,
    string RequestId,
    DateTimeOffset CreatedUtc,
    DrawingCommand Command,
    string ProfilePath,
    string? PartIdentifier,
    bool DryRun = false,
    bool OpenReport = true,
    string? ProfileSha256 = null)
{
    public const int CurrentProtocolVersion = 1;

    // Совместимый конструктор для существующих вызовов и старых smoke tests.
    public CommandRequest(DrawingCommand command, string profilePath, bool dryRun = false, bool openReport = true)
        : this(CurrentProtocolVersion, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, command,
            Path.GetFullPath(profilePath), null, dryRun, openReport,
            File.Exists(profilePath) ? Hashing.Sha256File(profilePath) : null)
    {
    }

    public static CommandRequest Create(DrawingCommand command, string profilePath, string? partIdentifier,
        bool dryRun = false, bool openReport = true)
        => new(CurrentProtocolVersion, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, command,
            Path.GetFullPath(profilePath), NormalizePartIdentifier(partIdentifier), dryRun, openReport,
            File.Exists(profilePath) ? Hashing.Sha256File(profilePath) : null);

    public static CommandRequest Load(string path, TimeSpan? maximumAge = null)
    {
        var request = JsonSerializer.Deserialize<CommandRequest>(File.ReadAllText(path), JsonOptions())
                      ?? throw new InvalidDataException("Файл запроса пуст или поврежден.");
        request.Validate(maximumAge ?? TimeSpan.FromHours(8));
        return request;
    }

    public void Validate(TimeSpan maximumAge)
    {
        if (ProtocolVersion != CurrentProtocolVersion)
            throw new InvalidDataException($"Неподдерживаемая версия протокола запроса: {ProtocolVersion}.");
        if (!Guid.TryParseExact(RequestId, "N", out _))
            throw new InvalidDataException("requestId должен быть GUID в формате N.");
        if (Command == DrawingCommand.None)
            throw new InvalidDataException("Команда запроса не задана.");
        if (DateTimeOffset.UtcNow - CreatedUtc > maximumAge || CreatedUtc - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(5))
            throw new InvalidDataException("Файл запроса устарел или имеет недопустимое время создания.");
        if (string.IsNullOrWhiteSpace(ProfilePath) || !File.Exists(ProfilePath))
            throw new FileNotFoundException("Профиль из запроса не найден.", ProfilePath);
        if (!string.IsNullOrWhiteSpace(ProfileSha256) &&
            !string.Equals(ProfileSha256, Hashing.Sha256File(ProfilePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Профиль был изменен после формирования запроса.");
    }

    public void SaveAtomic(string path)
        => AtomicFile.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions()));

    public bool TargetsPart(string? currentPartIdentifier)
        => string.IsNullOrWhiteSpace(PartIdentifier)
           || string.Equals(NormalizePartIdentifier(PartIdentifier), NormalizePartIdentifier(currentPartIdentifier),
               StringComparison.OrdinalIgnoreCase);

    private static string? NormalizePartIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return Path.GetFullPath(value); }
        catch { return value.Trim(); }
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
