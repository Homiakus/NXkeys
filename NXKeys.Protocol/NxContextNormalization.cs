using System;
using System.Linq;

namespace NXKeys.Protocol
{
    /// <summary>
    /// Каноническая нормализация и маппинг идентификаторов модулей/приложений NX.
    /// Чистые функции (без Win32/NX) — общий источник правды для модуля B (контекст)
    /// на обеих сторонах процесса: нормализация module id (включая v8-префиксы),
    /// application id → module id, window title → module id, module → label.
    /// </summary>
    public static class NxContextNormalization
    {
        /// <summary>Нормализация module id (регистр/пробелы, v8-префиксы, синонимы).</summary>
        public static string NormalizeModule(string moduleId)
        {
            string value = (moduleId ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');

            // v8-translated modules carry a "v8_" prefix; resolve them to the
            // canonical NX module name so context validation passes.
            string v8Resolved = NormalizeV8Module(value);
            if (v8Resolved != null) return v8Resolved;

            switch (value)
            {
                case "view":
                case "inspect":
                case "inspect_/_view": return "inspect_view";
                case "selection_filters":
                case "selection": return "selection_object";
                case "cam_/_manufacturing":
                case "cam": return "manufacturing";
                case "cae_/_simulation":
                case "cae": return "simulation";
                case "mold_/_tooling": return "mold";
                case "reuse_/_templates": return "reuse";
                default: return value;
            }
        }

        /// <summary>Является ли модуль разделяемым (selection_object / inspect_view / reuse).</summary>
        public static bool IsSharedModule(string moduleId)
        {
            string normalized = NormalizeModule(moduleId);
            return normalized == "selection_object" || normalized == "inspect_view" || normalized == "reuse";
        }

        private static string NormalizeV8Module(string moduleId)
        {
            string id = (moduleId ?? string.Empty).Trim().ToLowerInvariant();
            if (!id.StartsWith("v8_", StringComparison.Ordinal)) return null;
            string suffix = id.Substring(3);
            switch (suffix)
            {
                case "m": return "modeling";
                case "s": return "sketch";
                case "a": return "assembly";
                case "d": return "drafting";
                case "g": return "modeling";
                case "h": return "sheet_metal";
                case "k": return "sketch";
                case "p": return "inspect_view";
                case "v": return "drafting";
                case "i": return "simulation";
                case "n": return "manufacturing";
                case "u": return "surface";
                case "r": return "routing";
                case "l": return "mold";
                case "sm": return "sheet_metal";
                case "sh": return "sheet_metal";
                default: return "modeling";
            }
        }

        /// <summary>Application id (UG_APP_*) → module id.</summary>
        public static string ModuleIdFromApplication(string applicationId)
        {
            string id = (applicationId ?? string.Empty).ToUpperInvariant();
            if (id.Contains("DRAFTING")) return "drafting";
            if (id.Contains("MANUFACTURING")) return "manufacturing";
            if (id.Contains("SFEM") || id.Contains("DESFEM")) return "simulation";
            if (id.Contains("SHEETMETAL")) return "sheet_metal";
            if (id.Contains("ROUTING")) return "routing";
            if (id.Contains("STUDIO")) return "surface";
            if (id.Contains("MOLD")) return "mold";
            if (id.Contains("ASSEMBL")) return "assembly";
            if (id.Contains("MODEL")) return "modeling";
            return "inspect_view";
        }

        /// <summary>Заголовок окна NX (нижний регистр, EN/RU) → module id.</summary>
        public static string ModuleIdFromWindowTitle(string title)
        {
            string value = (title ?? string.Empty).ToLowerInvariant();
            if (value.Contains("sketch") || value.Contains("эскиз")) return "sketch";
            if (value.Contains("assembl") || value.Contains("сбор")) return "assembly";
            if (value.Contains("draft") || value.Contains("черт")) return "drafting";
            if (value.Contains("sheet") || value.Contains("лист")) return "sheet_metal";
            if (value.Contains("manufact") || value.Contains("cam") || value.Contains("обработ")) return "manufacturing";
            if (value.Contains("simulat") || value.Contains("cae") || value.Contains("симуля")) return "simulation";
            if (value.Contains("routing") || value.Contains("трасс")) return "routing";
            if (value.Contains("mold") || value.Contains("пресс")) return "mold";
            if (value.Contains("pmi")) return "pmi";
            if (value.Contains("surface") || value.Contains("поверх")) return "surface";
            if (value.Contains("model") || value.Contains("модел")) return "modeling";
            return "inspect_view";
        }

        /// <summary>Module id → application id (UG_APP_*).</summary>
        public static string ApplicationIdFromModuleId(string moduleId)
        {
            switch ((moduleId ?? string.Empty).ToLowerInvariant())
            {
                case "modeling": return "UG_APP_MODELING";
                case "sketch": return "UG_APP_SKETCH";
                case "assembly": return "UG_APP_ASSEMBLIES";
                case "drafting": return "UG_APP_DRAFTING";
                case "pmi": return "UG_APP_PMI";
                case "surface": return "UG_APP_STUDIO";
                case "sheet_metal": return "UG_APP_SHEETMETAL";
                case "manufacturing": return "UG_APP_MANUFACTURING";
                case "simulation": return "UG_APP_SFEM";
                case "routing": return "UG_APP_ROUTING";
                case "mold": return "UG_APP_MOLDWIZARD";
                default: return "UG_APP_GATEWAY";
            }
        }

        /// <summary>Module id → отображаемая метка/HUD.</summary>
        public static string ModuleLabelFromModule(string moduleId)
        {
            switch ((moduleId ?? string.Empty).ToLowerInvariant())
            {
                case "drafting": return "Drafting";
                case "manufacturing": return "CAM / Manufacturing";
                case "simulation": return "CAE / Simulation";
                case "sheet_metal": return "Sheet Metal";
                case "routing": return "Routing";
                case "surface": return "Surface";
                case "mold": return "Mold / Tooling";
                case "sketch": return "Sketch";
                case "modeling": return "Modeling";
                default: return "Inspect / View";
            }
        }
        /// <summary>Нормализация значения selection-фильтра (регистр/спецсимволы → '_').</summary>
        public static string NormalizeSelectionFilter(string value)
        {
            string normalized = new string((value ?? string.Empty).Trim().ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
            while (normalized.Contains("__", StringComparison.Ordinal)) normalized = normalized.Replace("__", "_");
            return normalized.Trim('_');
        }

        /// <summary>Определить selection-фильтр по command id (deselect/select_all/reset/...).</summary>
        public static string SelectionFilterFromCommandId(string commandId)
        {
            string id = (commandId ?? string.Empty).ToUpperInvariant();
            if (id.Contains("DESELECT")) return "none";
            if (id.Contains("SELECT_ALL")) return "all";
            if (id.Contains("RESET")) return "reset";
            if (id.Contains("EDGE")) return "edge";
            if (id.Contains("FACE")) return "face";
            if (id.Contains("BODY")) return "body";
            if (id.Contains("COMPONENT")) return "component";
            if (id.Contains("CURVE")) return "curve";
            if (id.Contains("DATUM")) return "datum";
            if (id.Contains("FEATURE")) return "feature";
            return string.Empty;
        }
    }
}
