using System;
using System.Collections.Generic;
using System.Linq;
using NX2512_HotkeyStudio.Models;
using NXKeys.StateMachines;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class AdaptiveModuleResolution
    {
        public ModuleConfig Module { get; set; }
        public bool ExactModuleMatch { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool IsResolved => Module != null;
    }

    public static class AdaptiveModuleResolver
    {
        private static readonly Dictionary<string, string> ApplicationFallbacks =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UG_APP_GATEWAY"] = "inspect_view",
                ["UG_APP_MODELING"] = "modeling",
                ["UG_APP_SKETCH"] = "sketch",
                ["UG_APP_ASSEMBLIES"] = "assembly",
                ["UG_APP_DRAFTING"] = "drafting",
                ["UG_APP_PMI"] = "pmi",
                ["UG_APP_STUDIO"] = "surface",
                ["UG_APP_SHEETMETAL"] = "sheet_metal",
                ["UG_APP_MANUFACTURING"] = "manufacturing",
                ["UG_APP_SFEM"] = "simulation",
                ["UG_APP_DESFEM"] = "simulation",
                ["UG_APP_ROUTING"] = "routing",
                ["UG_APP_MOLDWIZARD"] = "mold"
            };

        // Maps v8 module suffixes to normalized NX context module IDs.
        // The important invariant here is that a one-letter HUD label must never
        // win over an exact application/workspace match.  Doing so made e.g.
        // Sheet Metal resolve to unrelated modules such as Annotate.
        private static readonly Dictionary<string, string> V8SuffixToNxModule =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["m"] = "modeling",
                ["q"] = "modeling",
                ["s"] = "sketch",
                ["a"] = "assembly",
                ["d"] = "drafting",
                ["p"] = "pmi",
                ["u"] = "surface",
                ["h"] = "sheet_metal",
                ["sm"] = "sheet_metal",
                ["sh"] = "sheet_metal",
                ["n"] = "manufacturing",
                ["i"] = "inspect_view",
                ["g"] = "inspect_view",
                ["r"] = "routing",
                ["l"] = "mold"
            };

        public static AdaptiveModuleResolution Resolve(IEnumerable<ModuleConfig> source, NxBridgeContext context)
        {
            List<ModuleConfig> modules = (source ?? Enumerable.Empty<ModuleConfig>())
                .Where(module => module != null && module.Enabled && !string.IsNullOrWhiteSpace(module.ID))
                .ToList();
            if (modules.Count == 0) return new AdaptiveModuleResolution { Reason = "В профиле нет активных модулей." };
            if (context == null || !context.IsFresh) return new AdaptiveModuleResolution { Reason = "Контекст NX отсутствует или устарел." };

            string contextModule = ContextGuardEvaluator.NormalizeModule(context.ModuleId);
            string application = (context.ApplicationId ?? string.Empty).Trim();

            // 1. Exact module ID match after normalization.
            ModuleConfig exact = modules.FirstOrDefault(module => string.Equals(
                ContextGuardEvaluator.NormalizeModule(module.ID), contextModule, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return new AdaptiveModuleResolution { Module = exact, ExactModuleMatch = true, Reason = "Модуль определён по module_id." };

            // 2. v8 suffix mapping.  Resolve this before any label heuristics:
            // v8_h must deterministically mean Sheet Metal, v8_s Sketch, etc.
            ModuleConfig byV8Suffix = modules.FirstOrDefault(module =>
            {
                string id = ContextGuardEvaluator.NormalizeModule(module.ID);
                if (!id.StartsWith("v8_", StringComparison.OrdinalIgnoreCase)) return false;
                string suffix = id.Substring(3);
                return V8SuffixToNxModule.TryGetValue(suffix, out string mapped) &&
                       string.Equals(mapped, contextModule, StringComparison.OrdinalIgnoreCase);
            });
            if (byV8Suffix != null)
                return new AdaptiveModuleResolution { Module = byV8Suffix, Reason = "Модуль v8 сопоставлен с контекстом NX по таблице." };

            // 3. Exact NX application match is stronger than labels and fallback names.
            ModuleConfig byApplication = modules.FirstOrDefault(module => module.NXApplicationIDs != null &&
                module.NXApplicationIDs.Any(id => string.Equals(id, application, StringComparison.OrdinalIgnoreCase)));
            if (byApplication != null)
                return new AdaptiveModuleResolution { Module = byApplication, Reason = "Модуль определён по nx_application_ids." };

            // 4. Legacy/non-v8 application fallback mapping.
            if (ApplicationFallbacks.TryGetValue(application, out string preferredId))
            {
                ModuleConfig preferred = modules.FirstOrDefault(module => string.Equals(
                    ContextGuardEvaluator.NormalizeModule(module.ID), preferredId, StringComparison.OrdinalIgnoreCase));
                if (preferred != null)
                    return new AdaptiveModuleResolution { Module = preferred, Reason = "Модуль определён по application_id." };
            }

            // 5. Conservative label fallback.  Never substring-match one-letter
            // module labels: that was non-deterministic for contexts such as
            // "Sheet Metal", "Sketch" and "Modeling".
            string label = NormalizeWords(context.ModuleLabel);
            if (!string.IsNullOrWhiteSpace(label))
            {
                ModuleConfig byLabel = modules.FirstOrDefault(module =>
                {
                    string moduleLabel = NormalizeWords(module.Label);
                    if (moduleLabel.Length < 2) return false;
                    return string.Equals(moduleLabel, label, StringComparison.OrdinalIgnoreCase);
                });
                if (byLabel != null)
                    return new AdaptiveModuleResolution { Module = byLabel, Reason = "Модуль определён по точной подписи контекста." };
            }

            return new AdaptiveModuleResolution
            {
                Reason = "Приложение NX не сопоставлено с модулем профиля: " +
                         (string.IsNullOrWhiteSpace(application) ? "unknown" : application)
            };
        }

        public static bool Same(ModuleConfig left, ModuleConfig right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            return string.Equals(ContextGuardEvaluator.NormalizeModule(left.ID),
                ContextGuardEvaluator.NormalizeModule(right.ID), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeWords(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return new string(value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : ' ').ToArray())
                .Replace("  ", " ").Trim();
        }
    }
}
