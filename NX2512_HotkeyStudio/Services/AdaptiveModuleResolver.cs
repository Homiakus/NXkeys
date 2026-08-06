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

        // Maps v8 single-letter module suffixes to NX context module IDs.
        // v8 modules have IDs like "v8_m", "v8_s", "v8_a" whose suffix is the
        // leader-key mnemonic; this table resolves them to the NX workspace name
        // that Bridge context reports.
        private static readonly Dictionary<string, string> V8SuffixToNxModule =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["m"] = "modeling",
                ["s"] = "sketch",
                ["a"] = "assembly",
                ["d"] = "drafting",
                ["c"] = "modeling",
                ["f"] = "modeling",
                ["t"] = "modeling",
                ["e"] = "modeling",
                ["x"] = "modeling",
                ["g"] = "modeling",
                ["h"] = "modeling",
                ["p"] = "modeling",
                ["r"] = "modeling",
                ["l"] = "modeling",
                ["w"] = "modeling",
                ["sm"] = "sheet_metal",
                ["sh"] = "sheet_metal",
                ["n"] = "manufacturing",
                ["u"] = "surface",
                ["i"] = "inspect_view",
                ["q"] = "modeling",
            };

        public static AdaptiveModuleResolution Resolve(IEnumerable<ModuleConfig> source, NxBridgeContext context)
        {
            List<ModuleConfig> modules = (source ?? Enumerable.Empty<ModuleConfig>())
                .Where(module => module != null && module.Enabled && !string.IsNullOrWhiteSpace(module.ID))
                .ToList();
            if (modules.Count == 0) return new AdaptiveModuleResolution { Reason = "В профиле нет активных модулей." };
            if (context == null || !context.IsFresh) return new AdaptiveModuleResolution { Reason = "Контекст NX отсутствует или устарел." };

            string contextModule = ContextGuardEvaluator.NormalizeModule(context.ModuleId);

            // 1. Exact module ID match after normalization.
            ModuleConfig exact = modules.FirstOrDefault(module => string.Equals(
                ContextGuardEvaluator.NormalizeModule(module.ID), contextModule, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return new AdaptiveModuleResolution { Module = exact, ExactModuleMatch = true, Reason = "Модуль определён по module_id." };

            string label = NormalizeWords(context.ModuleLabel);

            // 2. First-letter match: context "Modeling" → first letter "M" → module with LeaderPrefix "M".
            if (!string.IsNullOrWhiteSpace(label))
            {
                string firstLetter = label.Substring(0, 1);
                ModuleConfig byFirstLetter = modules.FirstOrDefault(module =>
                    string.Equals(NormalizeWords(module.Label), firstLetter, StringComparison.OrdinalIgnoreCase));
                if (byFirstLetter != null)
                    return new AdaptiveModuleResolution { Module = byFirstLetter, Reason = "Модуль определён по первой букве контекста." };
            }

            // 3. Substring label match (fallback for multi-letter labels).
            if (!string.IsNullOrWhiteSpace(label))
            {
                ModuleConfig byLabel = modules.FirstOrDefault(module =>
                    NormalizeWords(module.Label).Contains(label, StringComparison.OrdinalIgnoreCase) ||
                    label.Contains(NormalizeWords(module.Label), StringComparison.OrdinalIgnoreCase));
                if (byLabel != null)
                    return new AdaptiveModuleResolution { Module = byLabel, Reason = "Модуль определён по подписи контекста." };
            }

            // 4. v8 suffix mapping: extract the suffix from module IDs like "v8_m" → "m"
            //    and match against the known NX context module.
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

            // 5. Application fallback mapping.
            string application = (context.ApplicationId ?? string.Empty).Trim();
            if (ApplicationFallbacks.TryGetValue(application, out string preferredId))
            {
                ModuleConfig preferred = modules.FirstOrDefault(module => string.Equals(
                    ContextGuardEvaluator.NormalizeModule(module.ID), preferredId, StringComparison.OrdinalIgnoreCase));
                if (preferred != null)
                    return new AdaptiveModuleResolution { Module = preferred, Reason = "Модуль определён по application_id." };
            }

            // 6. Direct nx_application_ids match.
            ModuleConfig byApplication = modules.FirstOrDefault(module => module.NXApplicationIDs != null &&
                module.NXApplicationIDs.Any(id => string.Equals(id, application, StringComparison.OrdinalIgnoreCase)));
            if (byApplication != null)
                return new AdaptiveModuleResolution { Module = byApplication, Reason = "Модуль определён по nx_application_ids." };

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
