using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NXOpen;
using NXOpen.Features;
using NXOpen.MenuBar;

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
    /// This runs inside ugraf together with CommandBridge, so the intent can be
    /// changed through the real NX selection UI and NXOpen ScRuleFactory instead
    /// of trying to emulate collector behavior from the external hotkey process.
    /// </summary>
    internal static class SelectionIntentHotkeys
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const uint LLKHF_INJECTED = 0x10;

        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private const double GapTolerance = 0.01;
        private const double AngleTolerance = 0.5;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

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

            if (data.vkCode < 0x30 || data.vkCode > 0x34)
                return CallNextHookEx(hookId, code, message, dataPointer);

            int intent = (int)data.vkCode - 0x30;
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

            bool handled = false;
            try { handled = TryApplyIntent(intent); }
            catch { handled = false; }

            lock (Sync) HandledDown[intent] = handled;
            return handled ? (IntPtr)1 : CallNextHookEx(hookId, code, message, dataPointer);
        }

        private static bool TryApplyIntent(int intent)
        {
            if (!IsCurrentNxForeground()) return false;
            if (HasSystemModifier()) return false;
            if (IsFocusedInTextInput()) return false;

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

            if (ui == null || session?.Parts?.Work == null) return false;

            // These are real NX2512 controls from definitions_main.btn. Their
            // availability/sensitivity is our strongest indication that the active
            // dialog is currently accepting Selection Intent changes.
            MenuButton chaining = TryGetButton(ui, "UG_SEL_CHAINING");
            MenuButton inferredPath = TryGetButton(ui, "UG_SC_INFERRED_CURVE_SELECTION");
            MenuButton chainWithinFeature = TryGetButton(ui, "UG_SC_CHAIN_WITHIN_FEATURE");
            MenuButton boundaryEdges = TryGetButton(ui, "UG_SC_BOUNDARY_EDGES");

            bool nativeCollectorActive = IsUsable(chaining) || IsUsable(inferredPath) ||
                                         IsUsable(chainWithinFeature) || IsUsable(boundaryEdges);
            int selectionCount = SafeSelectionCount(ui);

            // Never steal a numeric key just because NX has focus. We only own it
            // when a selection-intent control is active or there is an explicit seed
            // selection that can be expanded by NXOpen rules.
            if (!nativeCollectorActive && selectionCount <= 0) return false;

            switch (intent)
            {
                case 0:
                    bool reset = false;
                    reset |= SetToggle(ui, chaining, false);
                    reset |= SetToggle(ui, inferredPath, false);
                    reset |= SetToggle(ui, chainWithinFeature, false);
                    reset |= SetToggle(ui, boundaryEdges, false);
                    return reset;

                case 1:
                    bool singleChanged = false;
                    singleChanged |= SetToggle(ui, chaining, false);
                    singleChanged |= SetToggle(ui, inferredPath, false);
                    singleChanged |= SetToggle(ui, chainWithinFeature, false);
                    singleChanged |= SetToggle(ui, boundaryEdges, false);
                    if (selectionCount > 1)
                        singleChanged |= KeepOnlyLastSelected(ui);
                    return singleChanged || nativeCollectorActive;

                case 2:
                    bool chainChanged = false;
                    chainChanged |= SetToggle(ui, inferredPath, false);
                    chainChanged |= SetToggle(ui, boundaryEdges, false);
                    chainChanged |= SetToggle(ui, chaining, true);
                    if (selectionCount > 0)
                        chainChanged |= TryExpandSelectedSeed(session.Parts.Work, ui, 2);
                    return chainChanged || IsUsable(chaining);

                case 3:
                    // Tangent propagation has no stable global UG_SEL_* toggle in the
                    // NX2512 inventory. Build the native ScRule directly from the last
                    // selected curve/edge/face and request the resulting entities.
                    return selectionCount > 0 && TryExpandSelectedSeed(session.Parts.Work, ui, 3);

                case 4:
                    bool regionChanged = false;
                    regionChanged |= SetToggle(ui, chaining, true);
                    regionChanged |= SetToggle(ui, inferredPath, true);
                    regionChanged |= SetToggle(ui, boundaryEdges, true);
                    if (selectionCount > 0)
                        regionChanged |= TryExpandSelectedSeed(session.Parts.Work, ui, 4);
                    return regionChanged || IsUsable(inferredPath) || IsUsable(boundaryEdges);

                default:
                    return false;
            }
        }

        private static bool TryExpandSelectedSeed(Part workPart, UI ui, int intent)
        {
            int count = SafeSelectionCount(ui);
            if (count <= 0 || workPart == null) return false;

            TaggedObject seed;
            try { seed = ui.SelectionManager.GetSelectedTaggedObject(count - 1); }
            catch { return false; }
            if (seed == null) return false;

            SelectionIntentRule rule = null;
            try
            {
                ScRuleFactory factory = workPart.ScRuleFactory;
                if (factory == null) return false;

                if (seed is Edge edge)
                {
                    if (intent == 2)
                        rule = factory.CreateRuleEdgeChain(edge, null, false);
                    else if (intent == 3)
                        rule = factory.CreateRuleEdgeTangent(edge, null, false, AngleTolerance, false);
                    else if (intent == 4)
                    {
                        Face[] faces = edge.GetFaces();
                        if (faces != null && faces.Length > 0)
                            rule = factory.CreateRuleEdgeBoundary(faces);
                    }
                }
                else if (seed is Face face)
                {
                    if (intent == 2)
                        rule = factory.CreateRuleFaceAndAdjacentFaces(face);
                    else if (intent == 3)
                        rule = factory.CreateRuleFaceTangent(face, Array.Empty<Face>(), AngleTolerance);
                    else if (intent == 4)
                    {
                        Feature[] features = workPart.Features.GetAssociatedFeaturesOfFace(face);
                        if (features != null && features.Length > 0)
                            rule = factory.CreateRuleFaceFeature(features);
                    }
                }
                else if (seed is ICurve curve)
                {
                    if (intent == 2)
                        rule = factory.CreateRuleCurveChain(curve, null, false, GapTolerance);
                    else if (intent == 3)
                        rule = factory.CreateRuleCurveTangent(curve, null, false, AngleTolerance, GapTolerance);
                    // Region/closed-boundary selection is handled by the native
                    // UG_SC_INFERRED_CURVE_SELECTION toggle above. Creating a
                    // RegionBoundaryRule without a cursor seed point would be unsafe.
                }

                if (rule == null) return false;
                return SelectRuleObjects(workPart, ui, rule);
            }
            catch
            {
                return false;
            }
        }

        private static bool SelectRuleObjects(Part workPart, UI ui, SelectionIntentRule rule)
        {
            ScCollector collector = null;
            try
            {
                collector = workPart.ScCollectors.CreateCollector();
                collector.ReplaceRules(new[] { rule }, false);
                TaggedObject[] objects = collector.GetObjects();
                if (objects == null || objects.Length == 0) return false;

                TaggedObject[] unique = objects.Where(value => value != null)
                    .GroupBy(value => value.Tag)
                    .Select(group => group.First())
                    .ToArray();
                if (unique.Length == 0) return false;

                ui.SelectionManager.RequestSelections(unique);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { collector?.Destroy(); } catch { }
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
                if (remove.Count == 0) return false;
                ui.SelectionManager.RequestDeselections(remove.ToArray());
                return true;
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
            if (!IsUsable(button) || button.ButtonType != MenuButton.Type.ToggleButton) return false;
            MenuButton.Toggle target = desired ? MenuButton.Toggle.On : MenuButton.Toggle.Off;
            if (button.ToggleStatus == target) return false;

            try
            {
                // Invoke the actual NX action rather than writing ToggleStatus directly;
                // this keeps the active collector and its UI state synchronized.
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

        private static bool IsCurrentNxForeground()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out uint processId);
            return processId != 0 && processId == (uint)Process.GetCurrentProcess().Id;
        }

        private static bool HasSystemModifier()
        {
            return (GetKeyState(VK_CONTROL) & 0x8000) != 0 ||
                   (GetKeyState(VK_MENU) & 0x8000) != 0 ||
                   (GetKeyState(VK_LWIN) & 0x8000) != 0 ||
                   (GetKeyState(VK_RWIN) & 0x8000) != 0;
        }

        private static bool IsFocusedInTextInput()
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
