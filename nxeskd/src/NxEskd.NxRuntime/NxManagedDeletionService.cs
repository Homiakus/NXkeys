using System.Reflection;

namespace NxEskd.NxRuntime;

internal static class NxManagedDeletionService
{
    public static bool TrySchedule(NxServiceContext context, object target, out string error)
    {
        error = string.Empty;
        var updateManager = NxReflection.Get(context.Session, "UpdateManager");
        if (updateManager is not null)
        {
            var methods = updateManager.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name is "AddToDeleteList" or "AddObjectsToDeleteList")
                .OrderBy(method => method.GetParameters().Length)
                .ToArray();
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 1) continue;
                var argument = BuildDeleteArgument(parameters[0].ParameterType, target);
                if (argument is null) continue;
                try
                {
                    if (NxReflection.InvokeCommand(updateManager, method.Name, argument)) return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
        }

        var deleteMethod = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method => (method.Name is "Delete" or "Destroy")
                                      && method.GetParameters().Length == 0);
        if (deleteMethod is null)
        {
            error = "не найден AddToDeleteList/Delete";
            return false;
        }

        try
        {
            return NxReflection.InvokeCommand(target, deleteMethod.Name);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static object? BuildDeleteArgument(Type parameterType, object target)
    {
        if (parameterType.IsInstanceOfType(target)) return target;
        if (parameterType.IsArray)
        {
            var element = parameterType.GetElementType()!;
            if (!element.IsInstanceOfType(target)) return null;
            var array = Array.CreateInstance(element, 1);
            array.SetValue(target, 0);
            return array;
        }

        if (parameterType.IsGenericType)
        {
            var generic = parameterType.GetGenericArguments().FirstOrDefault();
            if (generic is null || !generic.IsInstanceOfType(target)) return null;
            var listType = typeof(List<>).MakeGenericType(generic);
            var list = Activator.CreateInstance(listType)!;
            listType.GetMethod("Add")!.Invoke(list, [target]);
            if (parameterType.IsInstanceOfType(list)) return list;
        }
        return null;
    }
}
