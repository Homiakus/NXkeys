using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NXOpen;
using Timer = System.Windows.Forms.Timer;

namespace NX2512_CommandBridge
{
    /// <summary>
    /// Command-local Selection Intent runtime for built-in NX Block Styler dialogs.
    ///
    /// Keys are intentionally handled inside the NX process instead of being
    /// translated to global SelectionManager filters.  A curve rule belongs to the
    /// currently focused CurveCollector / SectionBuilder, not to global object type
    /// filtering.  The low-level hook only captures 1..4 while a live compatible
    /// Block Styler selection block has been discovered; text editors are never
    /// intercepted.
    /// </summary>
    internal static class SelectionIntentHotkeyRuntime
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const uint LLKHF_INJECTED = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GuiThreadInfo
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public Rect rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
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
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

        private static readonly ConcurrentQueue<int> Pending = new ConcurrentQueue<int>();
        private static readonly object Gate = new object();
        private static UI ui;
        private static Action<string> log;
        private static Timer pump;
        private static HookProc hookDelegate;
        private static IntPtr hookId;
        private static object activeSelectionBlock;
        private static int available;
        private static int pressedMask;
        private static bool started;

        internal static void Start(UI value, Action<string> logger)
        {
            if (started) return;
            lock (Gate)
            {
                if (started) return;
                ui = value ?? throw new ArgumentNullException(nameof(value));
                log = logger ?? (_ => { });
                hookDelegate = HookCallback;
                hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookDelegate, GetModuleHandle(null), 0);
                if (hookId == IntPtr.Zero)
                {
                    log("Selection Intent hotkey hook was not installed. Win32=" + Marshal.GetLastWin32Error());
                    return;
                }

                pump = new Timer { Interval = 80 };
                pump.Tick += PumpTick;
                pump.Start();
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
                started = true;
                log("Selection Intent hotkeys active: 1=single, 2=connected, 3=tangent, 4=region boundary.");
            }
        }

        private static void Stop()
        {
            lock (Gate)
            {
                try { pump?.Stop(); } catch { }
                try { pump?.Dispose(); } catch { }
                pump = null;
                if (hookId != IntPtr.Zero)
                {
                    try { UnhookWindowsHookEx(hookId); } catch { }
                    hookId = IntPtr.Zero;
                }
                activeSelectionBlock = null;
                Interlocked.Exchange(ref available, 0);
                Interlocked.Exchange(ref pressedMask, 0);
                started = false;
            }
        }

        private static IntPtr HookCallback(int code, IntPtr message, IntPtr dataPointer)
        {
            if (code < 0) return CallNextHookEx(hookId, code, message, dataPointer);
            bool down = message == (IntPtr)WM_KEYDOWN || message == (IntPtr)WM_SYSKEYDOWN;
            bool up = message == (IntPtr)WM_KEYUP || message == (IntPtr)WM_SYSKEYUP;
            if (!down && !up) return CallNextHookEx(hookId, code, message, dataPointer);

            KbdLlHookStruct data = Marshal.PtrToStructure<KbdLlHookStruct>(dataPointer);
            if ((data.flags & LLKHF_INJECTED) != 0 || data.vkCode < 0x31 || data.vkCode > 0x34)
                return CallNextHookEx(hookId, code, message, dataPointer);

            int bit = 1 << (int)(data.vkCode - 0x31);
            if (up)
            {
                int previous = Interlocked.Exchange(ref pressedMask, Volatile.Read(ref pressedMask) & ~bit);
                return (previous & bit) != 0 ? (IntPtr)1 : CallNextHookEx(hookId, code, message, dataPointer);
            }

            if (!IsNxForeground() || IsFocusedTextInput() || Volatile.Read(ref available) == 0)
                return CallNextHookEx(hookId, code, message, dataPointer);

            int oldMask;
            int newMask;
            do
            {
                oldMask = Volatile.Read(ref pressedMask);
                if ((oldMask & bit) != 0) return (IntPtr)1; // keyboard auto-repeat
                newMask = oldMask | bit;
            }
            while (Interlocked.CompareExchange(ref pressedMask, newMask, oldMask) != oldMask);

            Pending.Enqueue((int)(data.vkCode - 0x30));
            return (IntPtr)1;
        }

        private static void PumpTick(object sender, EventArgs e)
        {
            try
            {
                object discovered = SelectionIntentController.FindActiveSelectionBlock(ui);
                if (discovered != null)
                {
                    activeSelectionBlock = discovered;
                    Interlocked.Exchange(ref available, 1);
                }
                else
                {
                    activeSelectionBlock = null;
                    Interlocked.Exchange(ref available, 0);
                }

                int processed = 0;
                while (processed++ < 8 && Pending.TryDequeue(out int key))
                {
                    object block = activeSelectionBlock;
                    if (block == null)
                    {
                        log("Selection Intent " + key + " ignored: active curve collector was not found.");
                        continue;
                    }
                    string result = SelectionIntentController.Apply(block, key);
                    log(result);
                }
            }
            catch (Exception ex)
            {
                activeSelectionBlock = null;
                Interlocked.Exchange(ref available, 0);
                log("Selection Intent runtime warning: " + ex.Message);
            }
        }

        private static bool IsNxForeground()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out uint processId);
            return processId == (uint)Process.GetCurrentProcess().Id;
        }

        private static bool IsFocusedTextInput()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            uint threadId = GetWindowThreadProcessId(window, out _);
            var info = new GuiThreadInfo { cbSize = Marshal.SizeOf<GuiThreadInfo>() };
            if (!GetGUIThreadInfo(threadId, ref info) || info.hwndFocus == IntPtr.Zero) return false;
            var className = new StringBuilder(128);
            GetClassName(info.hwndFocus, className, className.Capacity);
            string value = className.ToString().ToLowerInvariant();
            return value.Contains("edit") || value.Contains("textbox") || value.Contains("richedit") ||
                   value.Contains("scintilla") || value.Contains("spin") || value.Contains("combo");
        }
    }

    internal static class SelectionIntentController
    {
        private static readonly string[] SelectionTypeNames =
        {
            "NXOpen.BlockStyler.CurveCollector",
            "NXOpen.BlockStyler.SectionBuilder",
            "NXOpen.BlockStyler.SuperSection"
        };

        internal static object FindActiveSelectionBlock(UI ui)
        {
            if (ui == null) return null;
            var roots = new List<object> { ui };
            try { if (ui.DialogTester != null) roots.Add(ui.DialogTester); } catch { }

            var queue = new Queue<(object value, int depth)>();
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (object root in roots.Where(value => value != null)) queue.Enqueue((root, 0));

            object firstSelectionBlock = null;
            int inspected = 0;
            while (queue.Count > 0 && inspected++ < 192)
            {
                (object current, int depth) = queue.Dequeue();
                if (current == null || !visited.Add(current)) continue;

                if (IsSelectionBlock(current))
                {
                    if (IsBlockFocusedOrEnabled(current)) return current;
                    firstSelectionBlock ??= current;
                }

                object top = TryGetProperty(current, "TopBlock");
                if (top != null)
                {
                    object fromTop = FindSelectionBlockInTree(top);
                    if (fromTop != null) return fromTop;
                }

                if (depth >= 3) continue;
                foreach (object child in InterestingChildren(current))
                    queue.Enqueue((child, depth + 1));
            }
            return firstSelectionBlock;
        }

        internal static string Apply(object block, int key)
        {
            if (!IsSelectionBlock(block))
                throw new InvalidOperationException("Active Block Styler object is not a curve selection block.");
            if (key < 1 || key > 4)
                throw new ArgumentOutOfRangeException(nameof(key));

            MethodInfo membersMethod = block.GetType().GetMethod("GetDefaultCurveRulesMembers", Type.EmptyTypes);
            PropertyInfo ruleProperty = block.GetType().GetProperty("DefaultCurveRulesAsString");
            if (membersMethod == null || ruleProperty == null || !ruleProperty.CanWrite)
                throw new InvalidOperationException(block.GetType().Name + " does not expose DefaultCurveRulesAsString.");

            string[] members = membersMethod.Invoke(block, null) as string[] ?? Array.Empty<string>();
            string selected = SelectMember(members, key);
            if (string.IsNullOrWhiteSpace(selected))
                throw new InvalidOperationException(
                    "NX did not expose a matching curve rule for key " + key + ". Available: " + string.Join(", ", members));

            ruleProperty.SetValue(block, selected);
            string label = TryGetStringProperty(block, "LabelString");
            return "Selection Intent " + key + " -> " + selected +
                   (string.IsNullOrWhiteSpace(label) ? string.Empty : " [" + label + "]");
        }

        private static string SelectMember(IEnumerable<string> source, int key)
        {
            string[] members = (source ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            string[][] needles =
            {
                Array.Empty<string>(),
                new[] { "singlecurve", "single", "individual", "одиноч" },
                new[] { "connectedcurves", "connected", "curvechain", "chain", "связан", "цеп" },
                new[] { "tangentcurves", "tangent", "касател" },
                new[] { "regionboundary", "closedregion", "region", "boundary", "област", "границ", "замкнут" }
            };

            foreach (string needle in needles[key])
            {
                string hit = members.FirstOrDefault(value => Normalize(value).Contains(needle, StringComparison.Ordinal));
                if (!string.IsNullOrWhiteSpace(hit)) return hit;
            }
            return null;
        }

        private static object FindSelectionBlockInTree(object root)
        {
            if (root == null) return null;
            var queue = new Queue<object>();
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            queue.Enqueue(root);
            object fallback = null;
            int inspected = 0;
            while (queue.Count > 0 && inspected++ < 256)
            {
                object current = queue.Dequeue();
                if (current == null || !visited.Add(current)) continue;
                if (IsSelectionBlock(current))
                {
                    if (IsBlockFocusedOrEnabled(current)) return current;
                    fallback ??= current;
                }

                object lastUpdated = TryGetProperty(current, "LastUpdated");
                if (lastUpdated != null)
                {
                    if (IsSelectionBlock(lastUpdated)) return lastUpdated;
                    queue.Enqueue(lastUpdated);
                }

                MethodInfo getBlocks = current.GetType().GetMethod("GetBlocks", Type.EmptyTypes);
                if (getBlocks == null) continue;
                try
                {
                    if (getBlocks.Invoke(current, null) is Array children)
                        foreach (object child in children) if (child != null) queue.Enqueue(child);
                }
                catch { }
            }
            return fallback;
        }

        private static IEnumerable<object> InterestingChildren(object current)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = current.GetType();
            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length != 0 || !LooksInteresting(property.Name, property.PropertyType)) continue;
                object value = null;
                try { value = property.GetValue(current); } catch { }
                if (value != null) yield return value;
            }
            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (!LooksInteresting(field.Name, field.FieldType)) continue;
                object value = null;
                try { value = field.GetValue(current); } catch { }
                if (value != null) yield return value;
            }
        }

        private static bool LooksInteresting(string name, Type type)
        {
            string fullName = type?.FullName ?? string.Empty;
            if (fullName.StartsWith("NXOpen.BlockStyler.", StringComparison.Ordinal)) return true;
            string value = (name ?? string.Empty).ToLowerInvariant();
            return value.Contains("dialog") || value.Contains("block") || value.Contains("active") ||
                   value.Contains("current") || value.Contains("focus") || value.Contains("styler");
        }

        private static bool IsSelectionBlock(object value)
        {
            string fullName = value?.GetType().FullName ?? string.Empty;
            return SelectionTypeNames.Contains(fullName, StringComparer.Ordinal) &&
                   value.GetType().GetProperty("DefaultCurveRulesAsString") != null;
        }

        private static bool IsBlockFocusedOrEnabled(object block)
        {
            // Focus is the strongest signal when NX exposes it.  If the wrapper only
            // exposes Enable, an enabled collector is still preferable to a stale one.
            object focus = TryGetProperty(block, "Focus");
            if (focus is bool focused && focused) return true;
            object enabled = TryGetProperty(block, "Enable");
            return !(enabled is bool isEnabled) || isEnabled;
        }

        private static object TryGetProperty(object value, string name)
        {
            if (value == null) return null;
            try
            {
                PropertyInfo property = value.GetType().GetProperty(
                    name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property?.GetValue(value);
            }
            catch { return null; }
        }

        private static string TryGetStringProperty(object value, string name)
        {
            try { return Convert.ToString(TryGetProperty(value, name)) ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
