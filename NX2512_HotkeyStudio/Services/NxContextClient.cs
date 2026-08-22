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
            => NxContextNormalization.ModuleIdFromWindowTitle(title);

        private static string ApplicationIdFromModuleId(string moduleId)
            => NxContextNormalization.ApplicationIdFromModuleId(moduleId);

        private static string ModuleLabelFromModuleId(string moduleId)
            => NxContextNormalization.ModuleLabelFromModule(moduleId);

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
