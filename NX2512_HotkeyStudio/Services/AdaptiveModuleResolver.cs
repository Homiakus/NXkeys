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
        // Keep this mapping explicit: several NX applications begin with the same
        // letter (Sketch / Sheet Metal / Surface / Simulation), so first-letter
        // heuristics must never decide those contexts.
        private static readonly Dictionary<string, string> V8SuffixToNxModule =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["m"] = "modeling",
                ["s"] = "sketch",
                ["a"] = "assembly",
                ["d"] = "drafting",
                ["p"] = "pmi",
                ["u"] = "surface",
                ["h"] = "sheet_metal",
                ["sm"] = "sheet_metal",
                ["sh"] = "sheet_metal",
                ["n"] = "manufacturing",
                ["i"] = "simulation",
                ["r"] = "routing",
                ["l"] = "mold",
                ["g"] = "inspect_view"
            };

        public static AdaptiveModuleResolution Resolve(IEnumerable<ModuleConfig> source, NxBridgeContext context)
        {
            List<ModuleConfig> modules = (source ?? Enumerable.Empty<ModuleConfig>())
                .Where(module => module != null && module.Enabled && !string.IsNullOrWhiteSpace(module.ID))
                .ToList();
            if (modules.Count == 0)
                return new AdaptiveModuleResolution { Reason = "В профиле нет активных модулей." };
            if (context == null || !context.IsFresh)
                return new AdaptiveModuleResolution { Reason = "Контекст NX отсутствует или устарел." };

            string contextModule = ContextGuardEvaluator.NormalizeModule(context.ModuleId);
            string application = (context.ApplicationId ?? string.Empty).Trim();

            // 1. Exact runtime module id is always authoritative.
            ModuleConfig exact = modules.FirstOrDefault(module => string.Equals(
                ContextGuardEvaluator.NormalizeModule(module.ID), contextModule, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return new AdaptiveModuleResolution
                {
                    Module = exact,
                    ExactModuleMatch = true,
                    Reason = "Модуль определён по точному module_id."
                };

            // 2. Exact NX application id is the next strongest signal. This must be
            // evaluated BEFORE label/first-letter heuristics. Otherwise Sheet Metal,
            // Surface and Simulation can be incorrectly resolved to Sketch because all
            // of them begin with S.
            if (!string.IsNullOrWhiteSpace(application))
            {
                List<ModuleConfig> applicationMatches = modules
                    .Where(module => module.NXApplicationIDs != null &&
                        module.NXApplicationIDs.Any(id => string.Equals(id, application, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (applicationMatches.Count == 1)
                    return new AdaptiveModuleResolution
                    {
                        Module = applicationMatches[0],
                        Reason = "Модуль определён по точному NX application_id."
                    };

                // If several profile modules intentionally share one application,
                // prefer the one whose v8 suffix matches the runtime module.
                ModuleConfig applicationAndModule = applicationMatches.FirstOrDefault(module =>
                    V8ModuleMatchesContext(module, contextModule));
                if (applicationAndModule != null)
                    return new AdaptiveModuleResolution
                    {
                        Module = applicationAndModule,
                        Reason = "Модуль определён по application_id и runtime module_id."
                    };
            }

            // 3. Explicit v8 suffix mapping. This is deterministic and handles the
            // collision-prone S-family applications safely.
            ModuleConfig byV8Suffix = modules.FirstOrDefault(module => V8ModuleMatchesContext(module, contextModule));
            if (byV8Suffix != null)
                return new AdaptiveModuleResolution
                {
                    Module = byV8Suffix,
                    Reason = "Модуль v8 сопоставлен с контекстом NX по таблице."
                };

            // 4. Legacy application fallback mapping.
            if (ApplicationFallbacks.TryGetValue(application, out string preferredId))
            {
                ModuleConfig preferred = modules.FirstOrDefault(module => string.Equals(
                    ContextGuardEvaluator.NormalizeModule(module.ID), preferredId, StringComparison.OrdinalIgnoreCase));
                if (preferred != null)
                    return new AdaptiveModuleResolution
                    {
                        Module = preferred,
                        Reason = "Модуль определён по application fallback."
                    };
            }

            string label = NormalizeWords(context.ModuleLabel);

            // 5. Exact/contained label match is weaker than runtime ids but still useful
            // for fallback contexts inferred from the NX window title.
            if (!string.IsNullOrWhiteSpace(label))
            {
                List<ModuleConfig> labelMatches = modules.Where(module =>
                {
                    string moduleLabel = NormalizeWords(module.Label);
                    return !string.IsNullOrWhiteSpace(moduleLabel) &&
                           (string.Equals(moduleLabel, label, StringComparison.OrdinalIgnoreCase) ||
                            moduleLabel.Contains(label, StringComparison.OrdinalIgnoreCase) ||
                            label.Contains(moduleLabel, StringComparison.OrdinalIgnoreCase));
                }).ToList();

                if (labelMatches.Count == 1)
                    return new AdaptiveModuleResolution
                    {
                        Module = labelMatches[0],
                        Reason = "Модуль определён по однозначной подписи контекста."
                    };
            }

            // 6. First-letter matching is retained only as a final compatibility
            // fallback and only when it is unambiguous. Never pick the first arbitrary
            // S-module from a list.
            if (!string.IsNullOrWhiteSpace(label))
            {
                string firstLetter = label.Substring(0, 1);
                List<ModuleConfig> firstLetterMatches = modules.Where(module =>
                    string.Equals(NormalizeWords(module.Label), firstLetter, StringComparison.OrdinalIgnoreCase)).ToList();
                if (firstLetterMatches.Count == 1)
                    return new AdaptiveModuleResolution
                    {
                        Module = firstLetterMatches[0],
                        Reason = "Модуль определён по однозначной первой букве контекста."
                    };
            }

            return new AdaptiveModuleResolution
            {
                Reason = "Приложение NX не сопоставлено с модулем профиля: " +
                         (string.IsNullOrWhiteSpace(application) ? "unknown" : application) +
                         "; module=" + (string.IsNullOrWhiteSpace(contextModule) ? "unknown" : contextModule)
            };
        }

        public static bool Same(ModuleConfig left, ModuleConfig right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            return string.Equals(ContextGuardEvaluator.NormalizeModule(left.ID),
                ContextGuardEvaluator.NormalizeModule(right.ID), StringComparison.OrdinalIgnoreCase);
        }

        private static bool V8ModuleMatchesContext(ModuleConfig module, string contextModule)
        {
            string id = ContextGuardEvaluator.NormalizeModule(module?.ID);
            if (!id.StartsWith("v8_", StringComparison.OrdinalIgnoreCase)) return false;
            string suffix = id.Substring(3);
            return V8SuffixToNxModule.TryGetValue(suffix, out string mapped) &&
                   string.Equals(ContextGuardEvaluator.NormalizeModule(mapped), contextModule, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeWords(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return new string(value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : ' ').ToArray())
                .Replace("  ", " ").Trim();
        }
    }
}
