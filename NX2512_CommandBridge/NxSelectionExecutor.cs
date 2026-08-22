using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NXOpen;
using NXOpen.MenuBar;

namespace NX2512_CommandBridge
{
    /// <summary>
    /// Модуль A (in-NX): применение Selection Intent (0..4) внутри NX — включаются/выключаются
    /// нативные collectors/toggles, при необходимости расширяется seed через ScRuleFactory
    /// (late-bound NX API). Содержит собственные admission-гварды (NX foreground, без системных
    /// модификаторов, не в текстовом поле). Не владеет контекстом (B) и не принимает ввод (hook).
    /// </summary>
    internal static class NxSelectionExecutor
    {
        private const double GapTolerance = 0.01;
        private const double AngleTolerance = 0.5;

        public static bool TryApplyIntent(int intent)
        {
            if (!NxInterventionGuard.IsCurrentNxForeground() || NxInterventionGuard.HasSystemModifier() || NxInterventionGuard.IsFocusedInTextInput()) return false;

            UI ui;
            Session session;
            try
            {
                ui = UI.GetUI();
                session = Session.GetSession();
            }
            catch
            {
                return false;
            }

            Part workPart = session?.Parts?.Work;
            if (ui == null || workPart == null) return false;

            MenuButton chaining = TryGetButton(ui, "UG_SEL_CHAINING");
            MenuButton inferredPath = TryGetButton(ui, "UG_SC_INFERRED_CURVE_SELECTION");
            MenuButton chainWithinFeature = TryGetButton(ui, "UG_SC_CHAIN_WITHIN_FEATURE");
            MenuButton boundaryEdges = TryGetButton(ui, "UG_SC_BOUNDARY_EDGES");
            MenuButton tangentCurve = TryGetButton(ui, "UI_CURVE_FINDER_TANGENT");
            MenuButton tangentFace = TryGetButton(ui, "UI_FACE_FINDER_TANGENT");

            bool nativeCollectorActive = IsUsable(chaining) || IsUsable(inferredPath) ||
                                         IsUsable(chainWithinFeature) || IsUsable(boundaryEdges) ||
                                         IsUsable(tangentCurve) || IsUsable(tangentFace);
            int selectionCount = SafeSelectionCount(ui);

            // Do not consume normal numeric input merely because NX is foreground.
            if (!nativeCollectorActive && selectionCount <= 0) return false;

            switch (intent)
            {
                case 0:
                    return SetAllNativeToggles(
                        ui, chaining, inferredPath, chainWithinFeature, boundaryEdges,
                        tangentCurve, tangentFace, false);

                case 1:
                    bool singleChanged = SetAllNativeToggles(
                        ui, chaining, inferredPath, chainWithinFeature, boundaryEdges,
                        tangentCurve, tangentFace, false);
                    if (selectionCount > 1) singleChanged |= KeepOnlyLastSelected(ui);
                    return singleChanged || nativeCollectorActive;

                case 2:
                    bool chainChanged = false;
                    chainChanged |= SetToggle(ui, tangentCurve, false);
                    chainChanged |= SetToggle(ui, tangentFace, false);
                    chainChanged |= SetToggle(ui, inferredPath, false);
                    chainChanged |= SetToggle(ui, boundaryEdges, false);
                    chainChanged |= SetToggle(ui, chaining, true);
                    if (selectionCount > 0)
                        chainChanged |= TryExpandSelectedSeed(workPart, ui, 2);
                    return chainChanged || IsUsable(chaining);

                case 3:
                    // NX2512 exposes the same Tangent selectors used by Curve Finder
                    // and Face Finder. Enabling them makes "3" a real mode that can
                    // be selected before the first geometry click. ScRuleFactory is
                    // retained as a seed-based fallback/expansion path for collectors
                    // where the UI toggle is unavailable.
                    bool tangentChanged = false;
                    tangentChanged |= SetToggle(ui, chaining, false);
                    tangentChanged |= SetToggle(ui, inferredPath, false);
                    tangentChanged |= SetToggle(ui, boundaryEdges, false);
                    tangentChanged |= SetToggle(ui, tangentCurve, true);
                    tangentChanged |= SetToggle(ui, tangentFace, true);
                    if (selectionCount > 0)
                        tangentChanged |= TryExpandSelectedSeed(workPart, ui, 3);
                    return tangentChanged || IsUsable(tangentCurve) || IsUsable(tangentFace);

                case 4:
                    bool regionChanged = false;
                    regionChanged |= SetToggle(ui, tangentCurve, false);
                    regionChanged |= SetToggle(ui, tangentFace, false);
                    regionChanged |= SetToggle(ui, chaining, true);
                    regionChanged |= SetToggle(ui, inferredPath, true);
                    regionChanged |= SetToggle(ui, boundaryEdges, true);
                    if (selectionCount > 0)
                        regionChanged |= TryExpandSelectedSeed(workPart, ui, 4);
                    return regionChanged || IsUsable(inferredPath) || IsUsable(boundaryEdges);

                default:
                    return false;
            }
        }

        private static bool SetAllNativeToggles(
            UI ui,
            MenuButton chaining,
            MenuButton inferredPath,
            MenuButton chainWithinFeature,
            MenuButton boundaryEdges,
            MenuButton tangentCurve,
            MenuButton tangentFace,
            bool desired)
        {
            bool changed = false;
            changed |= SetToggle(ui, chaining, desired);
            changed |= SetToggle(ui, inferredPath, desired);
            changed |= SetToggle(ui, chainWithinFeature, desired);
            changed |= SetToggle(ui, boundaryEdges, desired);
            changed |= SetToggle(ui, tangentCurve, desired);
            changed |= SetToggle(ui, tangentFace, desired);
            return changed;
        }

        private static bool TryExpandSelectedSeed(Part workPart, UI ui, int intent)
        {
            int count = SafeSelectionCount(ui);
            if (count <= 0) return false;

            TaggedObject seed;
            try { seed = ui.SelectionManager.GetSelectedTaggedObject(count - 1); }
            catch { return false; }
            if (seed == null) return false;

            try
            {
                object factory = GetProperty(workPart, "ScRuleFactory");
                if (factory == null) return false;

                object rule = BuildRule(factory, workPart, seed, intent);
                return rule != null && SelectRuleObjects(workPart, ui, rule);
            }
            catch
            {
                return false;
            }
        }

        private static object BuildRule(object factory, Part workPart, TaggedObject seed, int intent)
        {
            string runtimeType = seed.GetType().Name;

            if (string.Equals(runtimeType, "Edge", StringComparison.OrdinalIgnoreCase))
            {
                if (intent == 2)
                    return InvokeCompatible(factory, "CreateRuleEdgeChain", seed, null, false);
                if (intent == 3)
                    return InvokeCompatible(factory, "CreateRuleEdgeTangent", seed, null, false, AngleTolerance, false);
                if (intent == 4)
                {
                    object faces = InvokeCompatible(seed, "GetFaces");
                    if (ArrayLength(faces) > 0)
                        return InvokeCompatible(factory, "CreateRuleEdgeBoundary", faces);
                }
                return null;
            }

            if (string.Equals(runtimeType, "Face", StringComparison.OrdinalIgnoreCase))
            {
                if (intent == 2)
                    return InvokeCompatible(factory, "CreateRuleFaceAndAdjacentFaces", seed);
                if (intent == 3)
                {
                    object emptyFaces = CreateNxArray(workPart, "NXOpen.Face", 0);
                    return emptyFaces == null
                        ? null
                        : InvokeCompatible(factory, "CreateRuleFaceTangent", seed, emptyFaces, AngleTolerance);
                }
                if (intent == 4)
                {
                    object features = InvokeCompatible(GetProperty(workPart, "Features"),
                        "GetAssociatedFeaturesOfFace", seed);
                    if (ArrayLength(features) > 0)
                        return InvokeCompatible(factory, "CreateRuleFaceFeature", features);
                }
                return null;
            }

            if (ImplementsInterface(seed, "NXOpen.ICurve"))
            {
                if (intent == 2)
                    return InvokeCompatible(factory, "CreateRuleCurveChain", seed, null, false, GapTolerance);
                if (intent == 3)
                    return InvokeCompatible(factory, "CreateRuleCurveTangent", seed, null, false,
                        AngleTolerance, GapTolerance);
            }

            return null;
        }

        private static bool SelectRuleObjects(Part workPart, UI ui, object rule)
        {
            object collector = null;
            try
            {
                object collectors = GetProperty(workPart, "ScCollectors");
                collector = InvokeCompatible(collectors, "CreateCollector");
                if (collector == null) return false;

                Type selectionIntentRuleType = FindType(workPart, "NXOpen.SelectionIntentRule");
                if (selectionIntentRuleType == null || !selectionIntentRuleType.IsInstanceOfType(rule)) return false;

                Array rules = Array.CreateInstance(selectionIntentRuleType, 1);
                rules.SetValue(rule, 0);
                if (!TryInvokeCompatible(collector, "ReplaceRules", out _, rules, false)) return false;

                object result = InvokeCompatible(collector, "GetObjects");
                if (!(result is Array array) || array.Length == 0) return false;

                var selected = new List<TaggedObject>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (object item in array)
                {
                    if (!(item is TaggedObject tagged)) continue;
                    string key = StableObjectKey(tagged);
                    if (seen.Add(key)) selected.Add(tagged);
                }
                if (selected.Count == 0) return false;

                return TryInvokeCompatible(ui.SelectionManager, "RequestSelections", out _, (object)selected.ToArray());
            }
            catch
            {
                return false;
            }
            finally
            {
                if (collector != null)
                {
                    try { TryInvokeCompatible(collector, "Destroy", out _); } catch { }
                }
            }
        }

        private static bool KeepOnlyLastSelected(UI ui)
        {
            int count = SafeSelectionCount(ui);
            if (count <= 1) return false;

            try
            {
                var remove = new List<TaggedObject>();
                for (int index = 0; index < count - 1; index++)
                {
                    TaggedObject item = ui.SelectionManager.GetSelectedTaggedObject(index);
                    if (item != null) remove.Add(item);
                }
                return remove.Count > 0 &&
                       TryInvokeCompatible(ui.SelectionManager, "RequestDeselections", out _, (object)remove.ToArray());
            }
            catch
            {
                return false;
            }
        }

        private static MenuButton TryGetButton(UI ui, string id)
        {
            try { return ui.MenuBarManager.GetButtonFromName(id); }
            catch { return null; }
        }

        private static bool IsUsable(MenuButton button)
        {
            return button != null &&
                   button.ButtonAvailability == MenuButton.AvailabilityStatus.Available &&
                   button.ButtonSensitivity == MenuButton.SensitivityStatus.Sensitive;
        }

        private static bool SetToggle(UI ui, MenuButton button, bool desired)
        {
            if (!IsUsable(button)) return false;

            try
            {
                PropertyInfo toggle = button.GetType().GetProperty("ToggleStatus", BindingFlags.Instance | BindingFlags.Public);
                if (toggle == null || !toggle.CanRead) return false;

                object current = toggle.GetValue(button);
                string currentName = current?.ToString() ?? string.Empty;
                string desiredName = desired ? "On" : "Off";
                if (string.Equals(currentName, desiredName, StringComparison.OrdinalIgnoreCase)) return false;

                // Invoke the real NX action. Some interactive NX actions report false
                // even after changing UI state, therefore a non-exceptional invocation
                // counts as handled and the next context tick will reflect the state.
                ui.DialogTester.InvokeMenuButtonAction(button);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int SafeSelectionCount(UI ui)
        {
            try { return ui.SelectionManager.GetNumSelectedObjects(); }
            catch { return -1; }
        }

        private static object GetProperty(object target, string name)
        {
            if (target == null || string.IsNullOrWhiteSpace(name)) return null;
            try
            {
                return target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target);
            }
            catch
            {
                return null;
            }
        }

        private static object InvokeCompatible(object target, string methodName, params object[] arguments)
        {
            return TryInvokeCompatible(target, methodName, out object result, arguments) ? result : null;
        }

        private static bool TryInvokeCompatible(object target, string methodName, out object result, params object[] arguments)
        {
            result = null;
            if (target == null || string.IsNullOrWhiteSpace(methodName)) return false;
            object[] args = arguments ?? Array.Empty<object>();

            IEnumerable<MethodInfo> candidates = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal) &&
                                 method.GetParameters().Length == args.Length);

            foreach (MethodInfo method in candidates)
            {
                ParameterInfo[] parameters = method.GetParameters();
                bool compatible = true;
                for (int index = 0; index < parameters.Length; index++)
                {
                    object argument = args[index];
                    Type parameterType = parameters[index].ParameterType;
                    if (argument == null)
                    {
                        if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                        {
                            compatible = false;
                            break;
                        }
                        continue;
                    }
                    if (!parameterType.IsInstanceOfType(argument) &&
                        !(parameterType.IsPrimitive && argument.GetType().IsPrimitive))
                    {
                        compatible = false;
                        break;
                    }
                }
                if (!compatible) continue;

                try
                {
                    result = method.Invoke(target, args);
                    return true;
                }
                catch (TargetInvocationException)
                {
                    return false;
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }

            return false;
        }

        private static Type FindType(Part part, string fullName)
        {
            if (part == null) return null;
            Type type = part.GetType().Assembly.GetType(fullName, false, false);
            if (type != null) return type;
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false, false))
                .FirstOrDefault(value => value != null);
        }

        private static object CreateNxArray(Part part, string elementTypeName, int length)
        {
            Type elementType = FindType(part, elementTypeName);
            return elementType == null ? null : Array.CreateInstance(elementType, Math.Max(0, length));
        }

        private static int ArrayLength(object value) => value is Array array ? array.Length : 0;

        private static bool ImplementsInterface(object value, string interfaceFullName)
        {
            return value != null && value.GetType().GetInterfaces()
                .Any(type => string.Equals(type.FullName, interfaceFullName, StringComparison.Ordinal));
        }

        private static string StableObjectKey(TaggedObject value)
        {
            object tag = GetProperty(value, "Tag");
            return tag != null
                ? tag.ToString()
                : value.GetType().FullName + ":" + RuntimeHelpers.GetHashCode(value);
        }

    }
}
