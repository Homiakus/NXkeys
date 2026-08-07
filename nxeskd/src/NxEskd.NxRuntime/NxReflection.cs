using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace NxEskd.NxRuntime;

internal static class NxReflection
{
    private static readonly object VoidResult = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> PropertyCache = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name), FieldInfo?> FieldCache = new();
    private static readonly ConcurrentDictionary<(Type Type, string Names), MethodInfo[]> MethodCache = new();

    private readonly record struct InvocationResult(bool Invoked, bool IsVoid, object? ReturnValue);

    public static object? Get(object? target, params string[] names)
    {
        if (target is null) return null;
        var type = target.GetType();
        foreach (var name in names)
        {
            var property = PropertyCache.GetOrAdd((type, name), key =>
                key.Type.GetProperty(key.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase));
            if (property is not null) return property.GetValue(target);

            var field = FieldCache.GetOrAdd((type, name), key =>
                key.Type.GetField(key.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase));
            if (field is not null) return field.GetValue(target);
        }
        return null;
    }

    public static object? GetOrInvoke(object? target, params string[] names)
    {
        var value = Get(target, names);
        if (value is not null) return value;
        return InvokeFactory(target, names);
    }

    public static bool Set(object? target, object? value, params string[] propertyPaths)
    {
        if (target is null) return false;
        foreach (var path in propertyPaths)
        {
            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            object? owner = target;
            for (var i = 0; i < segments.Length - 1 && owner is not null; i++) owner = Get(owner, segments[i]);
            if (owner is null) continue;

            var ownerType = owner.GetType();
            var property = PropertyCache.GetOrAdd((ownerType, segments[^1]), key =>
                key.Type.GetProperty(key.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase));
            if (property is null || !property.CanWrite) continue;
            property.SetValue(owner, ConvertValue(value, property.PropertyType));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Backward-compatible reflection invocation. A successful void call returns an internal
    /// non-null marker so legacy callers can distinguish it from "method not found".
    /// Factory and builder code must use InvokeFactory/CommitObjectAndDestroy instead.
    /// </summary>
    public static object? Invoke(object? target, IEnumerable<string> methodNames, params object?[]? suppliedArgs)
    {
        var invocation = InvokeCore(target, methodNames, suppliedArgs);
        if (!invocation.Invoked) return null;
        return invocation.IsVoid ? VoidResult : invocation.ReturnValue;
    }

    public static object? Invoke(object? target, string methodName, params object?[]? args)
        => Invoke(target, [methodName], args);

    /// <summary>
    /// Invokes a command-like API. Success is based on whether a compatible method completed,
    /// not on its return value. This is the correct path for NX methods returning void.
    /// </summary>
    public static bool InvokeCommand(object? target, IEnumerable<string> methodNames, params object?[]? suppliedArgs)
        => InvokeCore(target, methodNames, suppliedArgs).Invoked;

    public static bool InvokeCommand(object? target, string methodName, params object?[]? suppliedArgs)
        => InvokeCommand(target, [methodName], suppliedArgs);

    /// <summary>
    /// Invokes a factory-like API. Void methods are rejected and never exposed as created objects.
    /// A null return remains null so the caller can fail safely before changing ownership metadata.
    /// </summary>
    public static object? InvokeFactory(object? target, IEnumerable<string> methodNames, params object?[]? suppliedArgs)
    {
        var invocation = InvokeCore(target, methodNames, suppliedArgs);
        return invocation.Invoked && !invocation.IsVoid ? invocation.ReturnValue : null;
    }

    public static object? InvokeFactory(object? target, string methodName, params object?[]? suppliedArgs)
        => InvokeFactory(target, [methodName], suppliedArgs);

    public static bool TryInvoke(object? target, IEnumerable<string> methodNames, out object? result, params object?[]? suppliedArgs)
    {
        try
        {
            var invocation = InvokeCore(target, methodNames, suppliedArgs);
            if (!invocation.Invoked)
            {
                result = null;
                return false;
            }
            result = invocation.IsVoid ? VoidResult : invocation.ReturnValue;
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    public static bool TryInvokeCommand(object? target, IEnumerable<string> methodNames, params object?[]? suppliedArgs)
    {
        try { return InvokeCommand(target, methodNames, suppliedArgs); }
        catch { return false; }
    }

    public static IEnumerable<object> Enumerate(object? collection)
    {
        if (collection is null) yield break;
        if (collection is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                if (item is not null) yield return item;
            yield break;
        }
        var enumerator = InvokeFactory(collection, "GetEnumerator") as IEnumerator;
        if (enumerator is null) yield break;
        while (enumerator.MoveNext())
            if (enumerator.Current is not null) yield return enumerator.Current;
    }

    public static string? GetName(object? target)
        => Get(target, "Name", "JournalIdentifier", "Title")?.ToString();

    /// <summary>
    /// Commits a builder that must return a newly created or edited NX object and destroys the
    /// builder exactly once. A void/null commit is a hard failure and cannot be tagged as managed.
    /// </summary>
    public static object CommitObjectAndDestroy(object builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        try
        {
            return InvokeFactory(builder, ["Commit", "CommitFeature", "CommitObject"])
                   ?? throw new InvalidOperationException(
                       $"NX Builder '{builder.GetType().FullName}' не вернул объект после Commit.");
        }
        finally
        {
            Destroy(builder);
        }
    }

    /// <summary>
    /// Commits a command-style builder whose successful commit may return void, then destroys it
    /// exactly once. Use this for exporters and editors where no NX object is expected.
    /// </summary>
    public static void CommitCommandAndDestroy(object builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        try
        {
            if (!InvokeCommand(builder, ["Commit", "CommitFeature", "CommitObject"]))
                throw new MissingMethodException(builder.GetType().FullName, "Commit/CommitFeature/CommitObject");
        }
        finally
        {
            Destroy(builder);
        }
    }

    public static void Destroy(object? builder)
    {
        if (builder is null) return;
        try { _ = InvokeCommand(builder, ["Destroy", "Dispose"]); }
        catch { /* Destroy is best-effort; the primary NX exception must remain visible. */ }
    }

    public static object? FindByName(object? collection, string name)
    {
        foreach (var method in new[] { "FindObject", "Find", "Get" })
        {
            try
            {
                var result = InvokeFactory(collection, method, name);
                if (result is not null) return result;
            }
            catch
            {
                // Some NX collections throw for a missing name. Enumeration below is the safe fallback.
            }
        }
        return Enumerate(collection).FirstOrDefault(x => string.Equals(GetName(x), name, StringComparison.OrdinalIgnoreCase));
    }

    private static InvocationResult InvokeCore(object? target, IEnumerable<string> methodNames, object?[]? suppliedArgs)
    {
        if (target is null) return default;
        suppliedArgs ??= [];
        var orderedNames = methodNames.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (orderedNames.Length == 0) return default;

        var cacheKey = string.Join("\u001f", orderedNames.Select(x => x.ToUpperInvariant()));
        var methods = MethodCache.GetOrAdd((target.GetType(), cacheKey), key =>
        {
            var rank = orderedNames.Select((name, index) => (name, index))
                .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
            return key.Type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => rank.ContainsKey(m.Name))
                .OrderBy(m => rank[m.Name])
                .ThenBy(m => m.GetParameters().Length)
                .ToArray();
        });

        var candidates = new List<(MethodInfo Method, object?[] Args, int Score)>();
        foreach (var method in methods)
            if (TryBuildArguments(method.GetParameters(), suppliedArgs, out var args, out var score))
                candidates.Add((method, args, score));

        Exception? lastInvocationError = null;
        foreach (var candidate in candidates.OrderBy(x => x.Score))
        {
            try
            {
                var result = candidate.Method.Invoke(target, candidate.Args);
                return new InvocationResult(true, candidate.Method.ReturnType == typeof(void), result);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                lastInvocationError = ex.InnerException;
                break; // Compatible NX API started and failed: never hide it behind another overload.
            }
            catch (ArgumentException)
            {
                // Reflection rejected a candidate despite the binder check; try the next compatible overload.
            }
        }

        if (lastInvocationError is not null) ExceptionDispatchInfo.Capture(lastInvocationError).Throw();
        return default;
    }

    private static bool TryBuildArguments(ParameterInfo[] parameters, object?[] supplied, out object?[] result, out int score)
    {
        result = new object?[parameters.Length];
        score = 0;
        if (supplied.Length > parameters.Length) return false;

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterType = parameter.ParameterType.IsByRef
                ? parameter.ParameterType.GetElementType()!
                : parameter.ParameterType;

            if (i < supplied.Length)
            {
                try
                {
                    result[i] = ConvertValue(supplied[i], parameterType);
                    if (supplied[i] is not null)
                    {
                        if (parameterType == supplied[i]!.GetType()) score += 0;
                        else if (parameterType.IsInstanceOfType(supplied[i])) score += 1;
                        else score += 3;
                    }
                }
                catch
                {
                    return false;
                }
            }
            else if (parameter.IsOut)
            {
                result[i] = parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
                score += 2;
            }
            else if (parameter.HasDefaultValue)
            {
                result[i] = parameter.DefaultValue;
                score += 4;
            }
            else
            {
                return false; // Never fabricate null/default for required NX API parameters.
            }
        }

        score += Math.Abs(parameters.Length - supplied.Length) * 10;
        return true;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        var effectiveTarget = targetType.IsByRef ? targetType.GetElementType()! : targetType;
        if (value is null)
        {
            if (effectiveTarget.IsValueType && Nullable.GetUnderlyingType(effectiveTarget) is null)
                throw new InvalidCastException($"null нельзя передать в {effectiveTarget.FullName}.");
            return null;
        }

        var effective = Nullable.GetUnderlyingType(effectiveTarget) ?? effectiveTarget;
        if (effective.IsInstanceOfType(value)) return value;
        if (effective.IsEnum)
        {
            if (value is string s) return Enum.Parse(effective, s, ignoreCase: true);
            return Enum.ToObject(effective, value);
        }
        if (effective == typeof(string)) return value.ToString();
        if (effective == typeof(double) && value is string ds)
            return double.Parse(ds.Replace(',', '.'), CultureInfo.InvariantCulture);
        return Convert.ChangeType(value, effective, CultureInfo.InvariantCulture);
    }
}
