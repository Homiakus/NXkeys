using System.Globalization;
using System.Text.Json.Nodes;

namespace NxEskd.Configurator;

internal static class JsonObjectExtensions
{
    public static JsonObject EnsureObject(JsonObject owner, string name)
    {
        if (owner[name] is JsonObject existing) return existing;
        var created = new JsonObject();
        owner[name] = created;
        return created;
    }

    public static string ReadString(JsonObject owner, string name, string fallback = "")
        => owner[name]?.GetValue<string?>() ?? fallback;

    public static bool ReadBool(JsonObject owner, string name, bool fallback = false)
        => owner[name]?.GetValue<bool?>() ?? fallback;

    public static int ReadInt(JsonObject owner, string name, int fallback = 0)
        => owner[name]?.GetValue<int?>() ?? fallback;

    public static double ReadDouble(JsonObject owner, string name, double fallback = 0.0)
        => owner[name]?.GetValue<double?>() ?? fallback;

    public static void WriteString(JsonObject owner, string name, string? value)
    {
        owner[name] = value?.Trim() ?? string.Empty;
    }

    public static void WriteBool(JsonObject owner, string name, bool value)
    {
        owner[name] = value;
    }

    public static void WriteInt(JsonObject owner, string name, int value)
    {
        owner[name] = value;
    }

    public static void WriteDouble(JsonObject owner, string name, double value)
    {
        owner[name] = value;
    }
}
