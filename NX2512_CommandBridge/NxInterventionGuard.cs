using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NX2512_CommandBridge
{
    /// <summary>
    /// Единый in-NX гвард «безопасно ли вмешиваться в ввод»: активно ли окно NX,
    /// не зажаты ли системные модификаторы, не в текстовом ли поле фокус.
    /// Используется применением Selection Intent (и, при рефакторинге, admission-гейтами).
    /// Общая политика контекстной безопасности (modal/status/security) — в NxContextSnapshot.
    /// </summary>
    internal static class NxInterventionGuard
    {
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

        /// <summary>Активно ли окно NX-процесса (текущий процесс = NX add-in).</summary>
        public static bool IsCurrentNxForeground()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out uint processId);
            return processId != 0 && processId == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        /// <summary>Зажат ли системный модификатор (Ctrl/Alt/Win) — чтобы не перехватывать комбо.</summary>
        public static bool HasSystemModifier()
        {
            return (GetKeyState(VK_CONTROL) & 0x8000) != 0 ||
                   (GetKeyState(VK_MENU) & 0x8000) != 0 ||
                   (GetKeyState(VK_LWIN) & 0x8000) != 0 ||
                   (GetKeyState(VK_RWIN) & 0x8000) != 0;
        }

        /// <summary>Не в текстовом ли поле фокус (edit/textbox/richedit/scintilla/spin/numeric).</summary>
        public static bool IsFocusedInTextInput()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            uint threadId = GetWindowThreadProcessId(window, out _);
            var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
            if (!GetGUIThreadInfo(threadId, ref info) || info.hwndFocus == IntPtr.Zero) return false;

            var className = new StringBuilder(192);
            GetClassName(info.hwndFocus, className, className.Capacity);
            string value = className.ToString().ToLowerInvariant();
            return value.Contains("edit") || value.Contains("textbox") ||
                   value.Contains("richedit") || value.Contains("scintilla") ||
                   value.Contains("spin") || value.Contains("numeric");
        }
    }
}
