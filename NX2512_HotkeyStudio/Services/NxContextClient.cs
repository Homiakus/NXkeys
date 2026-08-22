using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NXKeys.Protocol;

namespace NX2512_HotkeyStudio.Services
{
    /// <summary>
    /// Модуль B (out-of-NX): поставщик контекста для HotkeyStudio. Читает опубликованный
    /// bridge/context.json (через транспорт), при отсутствии/устаревании строит fallback-контекст
    /// из активного окна NX и применяет единую политику свежести/достоверности.
    /// </summary>
    public sealed class NxContextClient : INxContextProvider
    {
        private readonly INxQueueTransport transport;
        private NxBridgeContext latestContext;

        public NxContextClient(INxQueueTransport transport = null)
        {
            this.transport = transport ?? new NxFileQueueTransport();
        }

        /// <summary>Текущий (последний) контекст, увиденный клиентом.</summary>
        public NxBridgeContext CurrentContext => latestContext;

        public event Action ContextChanged;

        public NxContextSnapshot? GetCurrent()
        {
            NxBridgeContext latest = transport.ReadContext();
            NxBridgeContext result = null;
            // Свежий опубликованный контекст авторитетнее; иначе — fallback по заголовку окна NX.
            if (latest != null && latest.IsFresh) result = latest;
            else if (TryCreateForegroundNxFallbackContext(out NxBridgeContext fallback)) result = fallback;
            if (result != null) latestContext = result;
            return result;
        }

        public bool TryGetFresh(out NxContextSnapshot ctx)
        {
            ctx = GetCurrent();
            return ctx != null && ctx.IsFresh;
        }

        public bool IsBridgeReady
        {
            get
            {
                NxContextSnapshot current = latestContext ?? GetCurrent();
                return current != null &&
                       string.Equals(current.Status, "running", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(current.SecurityStatus, "authenticated", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>Перечитать контекст и уведомить подписчиков (ContextWatch / HUD).</summary>
        public NxBridgeContext Refresh()
        {
            NxBridgeContext current = (NxBridgeContext)GetCurrent();
            ContextChanged?.Invoke();
            return current;
        }

        public static bool TryCreateForegroundNxFallbackContext(out NxBridgeContext context)
        {
            context = null;
            IntPtr window = GetActiveNxWindow();
            if (window == IntPtr.Zero) return false;

            string title = GetWindowTitle(window);
            string moduleId = ModuleIdFromWindowTitle(title);
            string applicationId = ApplicationIdFromModuleId(moduleId);
            context = new NxBridgeContext
            {
                SchemaVersion = NxProtocolConstants.SchemaVersion,
                Revision = 0,
                Status = "running",
                ApplicationId = applicationId,
                ModuleId = moduleId,
                ModuleLabel = ModuleLabelFromModuleId(moduleId),
                SelectionCount = 0,
                SelectionState = "none",
                WorkPartAvailable = true,
                DisplayPartAvailable = true,
                ModalDialogActive = false,
                ContextConfidence = 60,
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                LastResult = "fallback",
                LastMessage = "Command Bridge context is missing; module inferred from active NX window."
            };
            return true;
        }

        private static string GetWindowTitle(IntPtr window)
        {
            int length = Math.Max(256, GetWindowTextLength(window) + 1);
            var builder = new StringBuilder(length);
            return GetWindowText(window, builder, builder.Capacity) > 0 ? builder.ToString() : string.Empty;
        }

        private static string ModuleIdFromWindowTitle(string title)
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

        private static string ApplicationIdFromModuleId(string moduleId)
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

        private static string ModuleLabelFromModuleId(string moduleId)
        {
            switch ((moduleId ?? string.Empty).ToLowerInvariant())
            {
                case "modeling": return "Modeling";
                case "sketch": return "Sketch";
                case "assembly": return "Assembly";
                case "drafting": return "Drafting";
                case "pmi": return "PMI";
                case "surface": return "Surface";
                case "sheet_metal": return "Sheet Metal";
                case "manufacturing": return "CAM / Manufacturing";
                case "simulation": return "CAE / Simulation";
                case "routing": return "Routing";
                case "mold": return "Mold / Tooling";
                default: return "Inspect / View";
            }
        }

        /// <summary>Активное окно NX (процесс ugraf/nx/run_nx/designcenter) или IntPtr.Zero.</summary>
        public static IntPtr GetActiveNxWindow()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return IntPtr.Zero;
            GetWindowThreadProcessId(window, out uint processId);
            if (processId == 0) return IntPtr.Zero;
            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                {
                    string name = process.ProcessName.ToLowerInvariant();
                    return name == "ugraf" || name == "nx" || name == "run_nx" || name.StartsWith("designcenter") ? window : IntPtr.Zero;
                }
            }
            catch { return IntPtr.Zero; }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr window);
    }
}
