using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NX2512_CommandBridge
{
    /// <summary>
    /// In-process Selection Intent hotkeys for active NX collectors.
    ///
    /// 1 = single
    /// 2 = connected / chain
    /// 3 = tangent
    /// 4 = inferred path / region boundary
    /// 0 = reset to normal NX selection
    ///
    /// The stable UI surface is called directly. Advanced ScRuleFactory APIs are
    /// late-bound because NXKeys also compiles the bridge against a deliberately
    /// minimal NXOpen contract in CI. On the real NX2512 runtime the reflected
    /// method names/signatures are verified by the repository's NX2512 API catalog.
    /// </summary>
    internal static class SelectionIntentHotkeys
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const uint LLKHF_INJECTED = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        private static readonly object Sync = new object();
        private static readonly bool[] PhysicalDown = new bool[5];
        private static readonly bool[] HandledDown = new bool[5];
        private static readonly HookProc HookDelegate = HookCallback;
        private static IntPtr hookId;

        [ModuleInitializer]
        internal static void Initialize()
        {
            try
            {
                IntPtr module = GetModuleHandle(null);
                hookId = SetWindowsHookEx(WH_KEYBOARD_LL, HookDelegate, module, 0);
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
            }
            catch
            {
                hookId = IntPtr.Zero;
            }
        }

        private static void Shutdown()
        {
            try
            {
                if (hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookId);
                    hookId = IntPtr.Zero;
                }
            }
            catch { }
        }

        private static IntPtr HookCallback(int code, IntPtr message, IntPtr dataPointer)
        {
            if (code < 0 || hookId == IntPtr.Zero)
                return CallNextHookEx(hookId, code, message, dataPointer);

            bool down = message == (IntPtr)WM_KEYDOWN || message == (IntPtr)WM_SYSKEYDOWN;
            bool up = message == (IntPtr)WM_KEYUP || message == (IntPtr)WM_SYSKEYUP;
            if (!down && !up)
                return CallNextHookEx(hookId, code, message, dataPointer);

            KBDLLHOOKSTRUCT data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(dataPointer);
            if ((data.flags & LLKHF_INJECTED) != 0)
                return CallNextHookEx(hookId, code, message, dataPointer);

            int intent = MapVkToIntent(data.vkCode);
            if (intent < 0 || intent > 4)
                return CallNextHookEx(hookId, code, message, dataPointer);

            lock (Sync)
            {
                if (up)
                {
                    bool swallow = HandledDown[intent];
                    PhysicalDown[intent] = false;
                    HandledDown[intent] = false;
                    return swallow ? (IntPtr)1 : CallNextHookEx(hookId, code, message, dataPointer);
                }

                if (PhysicalDown[intent])
                    return HandledDown[intent] ? (IntPtr)1 : CallNextHookEx(hookId, code, message, dataPointer);

                PhysicalDown[intent] = true;
            }

            bool handled;
            try { handled = NxSelectionExecutor.TryApplyIntent(intent); }
            catch { handled = false; }

            lock (Sync) HandledDown[intent] = handled;
            return handled ? (IntPtr)1 : CallNextHookEx(hookId, code, message, dataPointer);
        }

        private static int MapVkToIntent(uint vkCode)
        {
            switch (vkCode)
            {
                case 0x30: // '0'
                case 0xC0: // '~' / '`' (VK_OEM_3)
                    return 0; // Reset
                case 0x31: // '1'
                case 0x51: // 'Q'
                    return 1; // Single
                case 0x32: // '2'
                case 0x57: // 'W'
                    return 2; // Connected / Chain
                case 0x33: // '3'
                case 0x45: // 'E'
                    return 3; // Tangent
                case 0x34: // '4'
                case 0x52: // 'R'
                    return 4; // Inferred Path / Region Boundary
                default:
                    return -1;
            }
        }




    }
}
