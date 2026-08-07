using NXOpen;

namespace NxEskd.NxRuntime;

internal readonly record struct ManagedObjectKey(
    string ProfileId,
    string ScopeId,
    string ObjectKind,
    string ManagedId)
{
    public override string ToString() => $"{ProfileId}/{ScopeId}/{ObjectKind}/{ManagedId}";
}

internal static class NxObjectTools
{
    private const string ManagedAttribute = "AUTO_DWG_MANAGED";
    private const string IdAttribute = "AUTO_DWG_ID";
    private const string ProfileAttribute = "AUTO_DWG_PROFILE_ID";
    private const string ScopeAttribute = "AUTO_DWG_SCOPE_ID";
    private const string KindAttribute = "AUTO_DWG_OBJECT_KIND";

    public static void SetStringAttribute(object? nxObject, string name, string value)
    {
        if (nxObject is null) throw new ArgumentNullException(nameof(nxObject));
        Exception? lastError = null;
        var updateOptionType = typeof(Session).Assembly.GetType("NXOpen.Update+Option");
        object? option = null;
        if (updateOptionType?.IsEnum == true)
        {
            var candidate = Enum.GetNames(updateOptionType).FirstOrDefault(x => x.Contains("Later", StringComparison.OrdinalIgnoreCase))
                            ?? Enum.GetNames(updateOptionType).First();
            option = Enum.Parse(updateOptionType, candidate);
        }

        var methods = new[] { "SetUserAttribute", "SetAttribute", "SetStringAttribute" };
        foreach (var args in new object?[][]
                 {
                     [name, -1, value, option], [name, 0, value, option], [name, value], [name, -1, value]
                 })
        {
            try
            {
                if (!NxReflection.InvokeCommand(nxObject, methods, args)) continue;
                if (string.Equals(GetStringAttribute(nxObject, name), value, StringComparison.Ordinal)) return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                break; // A compatible setter started and failed; never issue a second side-effecting fallback.
            }
        }

        throw new InvalidOperationException(
            $"NX не подтвердил запись строкового атрибута '{name}' со значением '{value}'.", lastError);
    }

    public static string? GetStringAttribute(object? nxObject, string name)
    {
        if (nxObject is null) return null;
        foreach (var args in new object?[][] { [name], [name, -1], [name, 0] })
        {
            try
            {
                var result = NxReflection.InvokeFactory(nxObject,
                    ["GetStringUserAttribute", "GetUserAttributeAsString", "GetStringAttribute"], args);
                if (result is not null) return result.ToString();
            }
            catch
            {
                break;
            }
        }
        return null;
    }

    public static bool HasAttribute(object? nxObject, string name)
        => GetStringAttribute(nxObject, name) is not null;

    public static bool IsManaged(
        object? nxObject,
        string? managedId = null,
        string? profileId = null,
        string? objectKind = null,
        string? scopeId = null)
    {
        var flag = GetStringAttribute(nxObject, ManagedAttribute);
        if (!string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)) return false;
        if (managedId is not null && !EqualsAttribute(nxObject, IdAttribute, managedId)) return false;
        if (profileId is not null && !EqualsAttribute(nxObject, ProfileAttribute, profileId)) return false;

        // Legacy managed objects did not have kind/scope. They are accepted only when these attributes
        // are absent; the next successful update migrates them with EnsureOwnershipMetadata.
        var actualKind = GetStringAttribute(nxObject, KindAttribute);
        if (objectKind is not null && actualKind is not null
            && !string.Equals(actualKind, objectKind, StringComparison.OrdinalIgnoreCase)) return false;
        var actualScope = GetStringAttribute(nxObject, ScopeAttribute);
        if (scopeId is not null && actualScope is not null
            && !string.Equals(actualScope, scopeId, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public static void TagManaged(
        object? nxObject,
        string managedId,
        string profileId,
        string configHash,
        string objectKind = "object",
        string scopeId = "default")
    {
        if (nxObject is null) throw new ArgumentNullException(nameof(nxObject));
        SetStringAttribute(nxObject, ManagedAttribute, "true");
        SetStringAttribute(nxObject, IdAttribute, managedId);
        SetStringAttribute(nxObject, ProfileAttribute, profileId);
        SetStringAttribute(nxObject, ScopeAttribute, scopeId);
        SetStringAttribute(nxObject, KindAttribute, objectKind);
        SetStringAttribute(nxObject, "AUTO_DWG_CONFIG_HASH", configHash);
        SetStringAttribute(nxObject, "AUTO_DWG_GENERATOR_VERSION", BuildInfo.Version);

        if (!IsManaged(nxObject, managedId, profileId, objectKind, scopeId))
            throw new InvalidOperationException($"NX-объект '{managedId}' создан, но обязательные managed-атрибуты не подтверждены.");
    }

    public static void EnsureOwnershipMetadata(
        object nxObject,
        string managedId,
        string profileId,
        string configHash,
        string objectKind,
        string scopeId)
    {
        if (!IsManaged(nxObject, managedId, profileId, objectKind, scopeId))
            throw new InvalidOperationException(
                $"Объект '{managedId}' не принадлежит ожидаемой области {profileId}/{scopeId}/{objectKind}.");

        TagManaged(nxObject, managedId, profileId, configHash, objectKind, scopeId);
    }

    public static bool TryGetManagedKey(object? nxObject, string fallbackKind, out ManagedObjectKey key)
    {
        key = default;
        if (!IsManaged(nxObject)) return false;
        var profile = GetStringAttribute(nxObject, ProfileAttribute);
        var id = GetStringAttribute(nxObject, IdAttribute);
        if (string.IsNullOrWhiteSpace(profile) || string.IsNullOrWhiteSpace(id)) return false;
        var scope = GetStringAttribute(nxObject, ScopeAttribute) ?? "legacy";
        var kind = GetStringAttribute(nxObject, KindAttribute) ?? fallbackKind;
        key = new ManagedObjectKey(profile, scope, kind, id);
        return true;
    }

    private static bool EqualsAttribute(object? nxObject, string name, string expected)
        => string.Equals(GetStringAttribute(nxObject, name), expected, StringComparison.OrdinalIgnoreCase);
}
