using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.UI;
using Timer = System.Windows.Forms.Timer;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class LeaderKeyEngine : IDisposable
    {
        #region Win32 API Interop
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const uint LLKHF_INJECTED = 0x00000010;

        private const byte VK_CAPITAL = 0x14;
        private const byte VK_F12 = 0x7B;
        private const byte VK_ESCAPE = 0x1B;
        private const byte VK_BACK = 0x08;
        private const byte VK_TAB = 0x09;
        private const byte VK_RETURN = 0x0D;
        private const byte VK_SPACE = 0x20;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int SW_RESTORE = 9;

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
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgti);

        #endregion

        private LeaderKeyConfig config;
        private IntPtr hookId = IntPtr.Zero;
        private HookProc hookDelegate;

        private bool isActive = false;
        private bool isSticky = false;
        private string buffer = string.Empty;
        private string searchFilter = null;
        private DateTime lastLeaderPressTime = DateTime.MinValue;

        private Timer hudDelayTimer;
        private Timer progressTimer;
        private DateTime timeoutStartTime;
        private int currentTimeoutMs = 4000;

        private LeaderHudForm hudForm;
        private uint triggerVk = VK_CAPITAL;
        private bool isRunning = false;
        private IntPtr targetNxWindow = IntPtr.Zero;
        private string activeModuleId = "modeling";
        private string activeModuleLabel = "Modeling";
        private LeaderSequenceItem pendingConfirmationItem;
        private Dictionary<string, List<LeaderSequenceItem>> prefixIndex = new Dictionary<string, List<LeaderSequenceItem>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<LeaderSequenceItem>> searchIndex = new Dictionary<string, List<LeaderSequenceItem>>(StringComparer.OrdinalIgnoreCase);

        public event Action<string> StatusChanged;
        public event Action<string, LeaderSequenceItem> SequenceExecuted;

        public bool IsRunning => isRunning;
        public bool IsActive => isActive;

        public LeaderKeyEngine(LeaderKeyConfig cfg)
        {
            config = cfg ?? new LeaderKeyConfig();
            config.ApplyDefaults();
            RebuildIndexes();
            ParseTriggerKey();

            hudDelayTimer = new Timer();
            hudDelayTimer.Interval = Math.Max(50, config.HudDelayMs);
            hudDelayTimer.Tick += HudDelayTimer_Tick;

            progressTimer = new Timer();
            progressTimer.Interval = 40; // 25 fps smooth progress update
            progressTimer.Tick += ProgressTimer_Tick;
        }

        private void ParseTriggerKey()
        {
            string key = (config.TriggerKey ?? "CapsLock").Trim().ToLowerInvariant();
            switch (key)
            {
                case "f12": triggerVk = VK_F12; break;
                case "capslock": default: triggerVk = VK_CAPITAL; break;
            }
        }

        public void Start()
        {
            if (isRunning) return;

            hudForm = new LeaderHudForm();
            hookDelegate = HookCallback;

            IntPtr hMod = GetModuleHandle(null);
            if (hMod == IntPtr.Zero)
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    hMod = GetModuleHandle(curModule.ModuleName);
                }
            }

            hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookDelegate, hMod, 0);

            if (hookId == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to install low-level keyboard hook. Win32 Error: {err}");
            }

            isRunning = true;
            StatusChanged?.Invoke("Leader Key Engine активен (Ожидание Leader Key)");
        }

        public void Stop()
        {
            if (!isRunning) return;

            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }

            ResetState();
            if (hudForm != null && !hudForm.IsDisposed)
            {
                hudForm.DismissHud();
                hudForm.Dispose();
                hudForm = null;
            }

            isRunning = false;
            StatusChanged?.Invoke("Leader Key Engine остановлен");
        }

        private void ResetState()
        {
            isActive = false;
            isSticky = false;
            buffer = string.Empty;
            searchFilter = null;
            hudDelayTimer.Stop();
            progressTimer.Stop();

            if (hudForm != null && !hudForm.IsDisposed)
            {
                hudForm.DismissHud();
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if ((hookStruct.flags & LLKHF_INJECTED) != 0)
                {
                    return CallNextHookEx(hookId, nCode, wParam, lParam);
                }

                uint vkCode = hookStruct.vkCode;
                IntPtr activeNxWindow = GetActiveNXWindow();

                if (config.HookOnlyWhenNXActive && activeNxWindow == IntPtr.Zero)
                {
                    if (isActive) ResetState();
                    return CallNextHookEx(hookId, nCode, wParam, lParam);
                }
                if (activeNxWindow != IntPtr.Zero)
                {
                    targetNxWindow = activeNxWindow;
                }

                // Check Leader Trigger Key press (CapsLock)
                if (vkCode == triggerVk)
                {
                    DateTime now = DateTime.Now;
                    TimeSpan sinceLast = now - lastLeaderPressTime;
                    lastLeaderPressTime = now;

                    if (!isActive)
                    {
                        isActive = true;
                        isSticky = false;
                        buffer = string.Empty;
                        pendingConfirmationItem = null;
                        RefreshModuleFromBridge();
                        if (activeNxWindow != IntPtr.Zero) targetNxWindow = activeNxWindow;

                        if (config.StickyModeOnDoubleTap && sinceLast.TotalMilliseconds <= 380)
                        {
                            isSticky = true;
                        }

                        hudDelayTimer.Stop();
                        hudDelayTimer.Interval = Math.Max(50, config.HudDelayMs);
                        hudDelayTimer.Start();

                        timeoutStartTime = DateTime.Now;
                        currentTimeoutMs = Math.Max(1000, config.FirstKeyTimeoutMs); // 4000ms
                        progressTimer.Start();

                        StatusChanged?.Invoke(isSticky ? "Leader Key: STICKY MODE" : "Leader Key: Активен");
                        return (IntPtr)1; // Intercept trigger key
                    }
                    else
                    {
                        if (config.StickyModeOnDoubleTap && !isSticky && sinceLast.TotalMilliseconds <= 380)
                        {
                            isSticky = true;
                            if (hudForm != null && !hudForm.IsDisposed)
                            {
                                var matches = GetMatchingSequences(buffer);
                                hudForm.UpdateState(buffer, matches, true, activeModuleLabel, activeModuleId);
                            }
                            return (IntPtr)1;
                        }
                    }
                }

                if (isActive)
                {
                    // Escape key -> cancel
                    if (vkCode == VK_ESCAPE)
                    {
                        ResetState();
                        StatusChanged?.Invoke("Leader Key: Отменено (Esc)");
                        return (IntPtr)1;
                    }

                    if (vkCode == VK_TAB)
                    {
                        CycleActiveModule(IsShiftDown() ? -1 : 1);
                        buffer = string.Empty;
                        pendingConfirmationItem = null;
                        var matches = GetMatchingSequences(buffer);
                        if (hudForm != null && !hudForm.IsDisposed) hudForm.UpdateState(buffer, matches, isSticky, activeModuleLabel, activeModuleId);
                        StatusChanged?.Invoke($"Leader Key: модуль {activeModuleLabel}");
                        return (IntPtr)1;
                    }

                    if (vkCode == VK_RETURN && pendingConfirmationItem != null)
                    {
                        LeaderSequenceItem confirmed = pendingConfirmationItem;
                        pendingConfirmationItem = null;
                        if (!isSticky) ResetState();
                        ExecuteSequence(confirmed, true);
                        return (IntPtr)1;
                    }

                    // Backspace key -> step back or reset
                    if (vkCode == VK_BACK)
                    {
                        if (searchFilter != null)
                        {
                            searchFilter = null;
                            var matches = GetMatchingSequences(buffer);
                            if (hudForm != null && !hudForm.IsDisposed) hudForm.UpdateState(buffer, matches, isSticky, activeModuleLabel, activeModuleId);
                        }
                        else if (!string.IsNullOrEmpty(buffer))
                        {
                            buffer = buffer.Substring(0, buffer.Length - 1).Trim();
                            var matches = GetMatchingSequences(buffer);
                            if (hudForm != null && !hudForm.IsDisposed) hudForm.UpdateState(buffer, matches, isSticky, activeModuleLabel, activeModuleId);
                        }
                        else
                        {
                            ResetState();
                        }
                        return (IntPtr)1;
                    }

                    // Space key -> search mode
                    if (vkCode == VK_SPACE && searchFilter == null)
                    {
                        searchFilter = "";
                        var allMatches = config.Sequences.Where(s => s.Enabled).ToList();
                        if (hudForm != null && !hudForm.IsDisposed) hudForm.SetSearchMode("", VisibleSequences().ToList(), activeModuleLabel, activeModuleId);
                        return (IntPtr)1;
                    }

                    // Translate Virtual Key to character
                    char keyChar = MapVkToChar(vkCode, hookStruct.scanCode);
                    if (keyChar != '\0')
                    {
                        if (searchFilter != null)
                        {
                            searchFilter += char.ToUpper(keyChar);
                            var filtered = SearchSequences(searchFilter);

                            if (hudForm != null && !hudForm.IsDisposed) hudForm.SetSearchMode(searchFilter, filtered, activeModuleLabel, activeModuleId);
                            return (IntPtr)1;
                        }

                        string testBuffer = string.IsNullOrEmpty(buffer) ? keyChar.ToString() : (buffer + " " + keyChar);
                        var matches = GetMatchingSequences(testBuffer);

                        if (matches.Count == 1 && string.Equals(matches[0].Sequence, testBuffer, StringComparison.OrdinalIgnoreCase))
                        {
                            // Exact sequence match!
                            LeaderSequenceItem item = matches[0];

                            if (!isSticky)
                            {
                                ResetState();
                                ExecuteSequence(item, false);
                            }
                            else
                            {
                                if (hudForm != null && !hudForm.IsDisposed) hudForm.DismissHud();
                                ExecuteSequence(item, false);
                                buffer = string.Empty;
                                var resetMatches = GetMatchingSequences("");
                                if (hudForm != null && !hudForm.IsDisposed) hudForm.UpdateState("", resetMatches, true, activeModuleLabel, activeModuleId);
                            }
                            return (IntPtr)1;
                        }
                        else if (matches.Count > 0)
                        {
                            // Partial match: advance buffer and extend timeout
                            buffer = testBuffer;
                            timeoutStartTime = DateTime.Now;
                            currentTimeoutMs = Math.Max(1000, config.NextKeyTimeoutMs); // 3000ms

                            if (hudForm != null && !hudForm.IsDisposed && hudForm.Visible)
                            {
                                hudForm.UpdateState(buffer, matches, isSticky, activeModuleLabel, activeModuleId);
                            }
                            return (IntPtr)1;
                        }
                        else
                        {
                            // Invalid key in current sequence branch
                            System.Media.SystemSounds.Asterisk.Play();
                            return (IntPtr)1;
                        }
                    }
                }
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private void HudDelayTimer_Tick(object sender, EventArgs e)
        {
            hudDelayTimer.Stop();
            if (isActive && hudForm != null && !hudForm.IsDisposed)
            {
                hudForm.DisplayHud(config.TriggerKey, isSticky, VisibleSequences().ToList(), config.HudOpacity, activeModuleLabel, activeModuleId);
                var matches = GetMatchingSequences(buffer);
                hudForm.UpdateState(buffer, matches, isSticky, activeModuleLabel, activeModuleId);
            }
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (isActive)
            {
                double elapsed = (DateTime.Now - timeoutStartTime).TotalMilliseconds;
                float pct = (float)Math.Max(0.0, 1.0 - (elapsed / currentTimeoutMs));

                if (hudForm != null && !hudForm.IsDisposed && hudForm.Visible)
                {
                    hudForm.UpdateTimeoutProgress(pct);
                }

                if (pct <= 0 && !isSticky)
                {
                    ResetState();
                    StatusChanged?.Invoke("Leader Key: Тайм-аут ожидания ввода");
                }
            }
        }

        private List<LeaderSequenceItem> GetMatchingSequences(string inputBuffer)
        {
            if (config?.Sequences == null) return new List<LeaderSequenceItem>();
            string normInput = (inputBuffer ?? "").Replace(" ", "").ToUpperInvariant();
            if (prefixIndex.TryGetValue(normInput, out List<LeaderSequenceItem> indexed))
            {
                return indexed.Where(IsSequenceVisible).ToList();
            }

            return VisibleSequences().Where(s => string.IsNullOrEmpty(normInput) || (s.Sequence ?? "").Replace(" ", "").ToUpperInvariant().StartsWith(normInput)).ToList();
        }

        private void RebuildIndexes()
        {
            prefixIndex.Clear();
            searchIndex.Clear();
            if (config?.Sequences == null) return;

            foreach (LeaderSequenceItem item in config.Sequences)
            {
                if (item == null || !item.Enabled) continue;
                string sequence = (item.Sequence ?? string.Empty).Replace(" ", "").ToUpperInvariant();
                for (int i = 0; i <= sequence.Length; i++)
                {
                    string prefix = sequence.Substring(0, i);
                    if (!prefixIndex.TryGetValue(prefix, out List<LeaderSequenceItem> bucket))
                    {
                        bucket = new List<LeaderSequenceItem>();
                        prefixIndex[prefix] = bucket;
                    }
                    bucket.Add(item);
                }

                foreach (string token in TokenizeSearchText(item))
                {
                    if (!searchIndex.TryGetValue(token, out List<LeaderSequenceItem> bucket))
                    {
                        bucket = new List<LeaderSequenceItem>();
                        searchIndex[token] = bucket;
                    }
                    if (!bucket.Contains(item)) bucket.Add(item);
                }
            }
        }

        private IEnumerable<LeaderSequenceItem> VisibleSequences()
        {
            if (config?.Sequences == null) return Enumerable.Empty<LeaderSequenceItem>();
            return config.Sequences.Where(IsSequenceVisible);
        }

        private bool IsSequenceVisible(LeaderSequenceItem item)
        {
            if (item == null || !item.Enabled) return false;
            if (string.IsNullOrWhiteSpace(item.ModuleID)) return true;
            if (string.Equals(item.ModuleID, activeModuleId, StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(item.ModuleID, "selection_object", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(item.ModuleID, "inspect_view", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(item.ModuleID, "reuse", StringComparison.OrdinalIgnoreCase);
        }

        private List<LeaderSequenceItem> SearchSequences(string query)
        {
            string normalized = (query ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized)) return VisibleSequences().ToList();

            var matches = new Dictionary<string, LeaderSequenceItem>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in Tokenize(normalized))
            {
                if (searchIndex.TryGetValue(token, out List<LeaderSequenceItem> bucket))
                {
                    foreach (LeaderSequenceItem item in bucket)
                    {
                        if (IsSequenceVisible(item)) matches[item.Sequence] = item;
                    }
                }
            }
            if (matches.Count == 0)
            {
                return VisibleSequences().Where(s =>
                    (s.Sequence ?? string.Empty).Replace(" ", "").Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    (s.Notes ?? string.Empty).Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    (s.Command?.Name ?? string.Empty).Contains(normalized, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            return matches.Values.OrderBy(s => s.Sequence).ToList();
        }

        private void RefreshModuleFromBridge()
        {
            NxBridgeContext context = NxCommandBridgeClient.ReadContext();
            if (context == null || string.IsNullOrWhiteSpace(context.ModuleId)) return;
            activeModuleId = context.ModuleId;
            activeModuleLabel = string.IsNullOrWhiteSpace(context.ModuleLabel) ? context.ModuleId : context.ModuleLabel;
        }

        private void CycleActiveModule(int delta)
        {
            if (config?.Sequences == null) return;
            List<string> moduleIds = config.RuntimeModules != null && config.RuntimeModules.Count > 0
                ? config.RuntimeModules.Where(m => m.Enabled).Select(m => m.ID).ToList()
                : config.Sequences
                    .Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.ModuleID))
                    .Select(s => s.ModuleID)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            if (moduleIds.Count == 0) return;
            int index = moduleIds.FindIndex(s => string.Equals(s, activeModuleId, StringComparison.OrdinalIgnoreCase));
            if (index < 0) index = 0;
            int next = (index + delta) % moduleIds.Count;
            if (next < 0) next += moduleIds.Count;
            activeModuleId = moduleIds[next];
            ModuleConfig module = config.RuntimeModules?.FirstOrDefault(m => string.Equals(m.ID, activeModuleId, StringComparison.OrdinalIgnoreCase));
            LeaderSequenceItem first = config.Sequences.FirstOrDefault(s => string.Equals(s.ModuleID, activeModuleId, StringComparison.OrdinalIgnoreCase));
            activeModuleLabel = module?.Label ?? first?.Category ?? activeModuleId;
            if (module != null)
            {
                try
                {
                    NxCommandBridgeClient.EnqueueModuleSwitch(module);
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"Leader Key: не удалось поставить переключение модуля в очередь: {ex.Message}");
                }
            }
        }

        private static bool IsShiftDown()
        {
            return (GetKeyState(VK_SHIFT) & 0x8000) != 0;
        }

        private static IEnumerable<string> TokenizeSearchText(LeaderSequenceItem item)
        {
            string text = string.Join(" ", new[]
            {
                item.Sequence,
                item.Category,
                item.ModuleID,
                item.Notes,
                item.Command?.ID,
                item.Command?.Name
            });
            return Tokenize(text);
        }

        private static IEnumerable<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            string normalized = new string(text.ToUpperInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ').ToArray());
            foreach (string token in normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return token;
            }
        }

        private void ExecuteSequence(LeaderSequenceItem item, bool confirmationAccepted)
        {
            if (item == null) return;
            if ((item.ConfirmBeforeExecute || item.Destructive) && !confirmationAccepted)
            {
                pendingConfirmationItem = item;
                if (hudForm != null && !hudForm.IsDisposed)
                {
                    hudForm.SetConfirmation(item, activeModuleLabel, activeModuleId);
                }
                StatusChanged?.Invoke($"Leader → {item.Sequence}: требуется подтверждение Enter");
                return;
            }
            StatusChanged?.Invoke($"Выполнение: Leader → {item.Sequence} ({item.Command?.Name})");

            try
            {
                NxCommandRequest request = NxCommandBridgeClient.Enqueue(item);
                StatusChanged?.Invoke($"Leader Key: queued direct NX command {request.CommandId} ({request.RequestId})");
                SequenceExecuted?.Invoke(item.Sequence, item);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Leader → {item.Sequence}: direct NX command queue failed: {ex.Message}");
            }
        }

        public static void SendShortcutToActiveWindow(string shortcut)
        {
            if (string.IsNullOrWhiteSpace(shortcut)) return;

            string[] parts = shortcut.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            List<byte> modifiers = new List<byte>();
            byte mainKey = 0;

            foreach (string p in parts)
            {
                string token = p.Trim().ToLowerInvariant();
                switch (token)
                {
                    case "ctrl": case "control": modifiers.Add(VK_CONTROL); break;
                    case "alt": modifiers.Add(VK_MENU); break;
                    case "shift": modifiers.Add(VK_SHIFT); break;
                    default:
                        if (TryParseKeyToken(token, out Keys k))
                        {
                            mainKey = (byte)k;
                        }
                        break;
                }
            }

            // Key Down for Modifiers
            foreach (byte mod in modifiers) keybd_event(mod, 0, 0, UIntPtr.Zero);

            // Key Down & Up for Main Key
            if (mainKey != 0)
            {
                keybd_event(mainKey, 0, 0, UIntPtr.Zero);
                Thread.Sleep(20);
                keybd_event(mainKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }

            // Key Up for Modifiers
            foreach (byte mod in modifiers) keybd_event(mod, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private bool FocusNXWindowForShortcut()
        {
            IntPtr hWnd = targetNxWindow;
            if (hWnd == IntPtr.Zero || !IsNXWindow(hWnd))
            {
                hWnd = FindNXWindow();
            }
            if (hWnd == IntPtr.Zero) return false;

            targetNxWindow = hWnd;
            try
            {
                if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
                Thread.Sleep(90);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseKeyToken(string token, out Keys key)
        {
            key = Keys.None;
            if (string.IsNullOrWhiteSpace(token)) return false;

            token = token.Trim();
            if (token.Length == 1)
            {
                char ch = char.ToUpperInvariant(token[0]);
                if (ch >= 'A' && ch <= 'Z')
                {
                    key = Keys.A + (ch - 'A');
                    return true;
                }
                if (ch >= '0' && ch <= '9')
                {
                    key = Keys.D0 + (ch - '0');
                    return true;
                }
            }

            switch (token.ToLowerInvariant())
            {
                case "esc": key = Keys.Escape; return true;
                case "del": key = Keys.Delete; return true;
                case "pgup": key = Keys.PageUp; return true;
                case "pgdn": key = Keys.PageDown; return true;
                default:
                    return Enum.TryParse(token, true, out key);
            }
        }

        private static char MapVkToChar(uint vkCode, uint scanCode)
        {
            if (vkCode >= 0x41 && vkCode <= 0x5A) // A-Z
            {
                return (char)vkCode;
            }
            if (vkCode >= 0x30 && vkCode <= 0x39) // 0-9
            {
                return (char)vkCode;
            }
            return '\0';
        }

        private static bool IsNXWindowActive()
        {
            return GetActiveNXWindow() != IntPtr.Zero;
        }

        private static IntPtr GetActiveNXWindow()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return IntPtr.Zero;

            return IsNXWindow(hWnd) ? hWnd : IntPtr.Zero;
        }

        private static bool IsNXWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == 0) return false;

            try
            {
                using (Process proc = Process.GetProcessById((int)processId))
                {
                    string name = proc.ProcessName.ToLowerInvariant();
                    return name == "ugraf" || name == "nx" || name == "run_nx" || name.StartsWith("designcenter");
                }
            }
            catch
            {
                return false;
            }
        }

        private static IntPtr FindNXWindow()
        {
            foreach (string processName in new[] { "ugraf", "run_nx", "nx" })
            {
                try
                {
                    foreach (Process proc in Process.GetProcessesByName(processName))
                    {
                        using (proc)
                        {
                            if (proc.MainWindowHandle != IntPtr.Zero)
                            {
                                return proc.MainWindowHandle;
                            }
                        }
                    }
                }
                catch { }
            }
            return IntPtr.Zero;
        }

        private static bool IsFocusedInTextInput()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return false;

            uint threadId = GetWindowThreadProcessId(hWnd, out _);
            GUITHREADINFO guiInfo = new GUITHREADINFO();
            guiInfo.cbSize = Marshal.SizeOf(guiInfo);

            if (GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.hwndFocus != IntPtr.Zero)
            {
                StringBuilder sb = new StringBuilder(128);
                GetClassName(guiInfo.hwndFocus, sb, 128);
                string className = sb.ToString().ToLowerInvariant();

                if (className.Contains("edit") || className.Contains("textbox") || className.Contains("richedit") || className.Contains("scintilla"))
                {
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            Stop();
            if (hudDelayTimer != null) { hudDelayTimer.Dispose(); hudDelayTimer = null; }
            if (progressTimer != null) { progressTimer.Dispose(); progressTimer = null; }
        }
    }
}
