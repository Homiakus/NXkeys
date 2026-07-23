using System;
using System.Collections.Concurrent;
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
using NXKeys.StateMachines;
using Timer = System.Windows.Forms.Timer;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class LeaderKeyEngine : IDisposable
    {
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

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int ResultTimeoutMs = 20000;
        private const int ModuleSwitchTimeoutMs = 8000;
        private const int ConfirmationTimeoutMs = 10000;

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

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

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

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);

        private enum InputKind
        {
            Trigger,
            Key,
            Cancel
        }

        private sealed class InputEvent
        {
            public InputKind Kind { get; set; }
            public uint VirtualKey { get; set; }
            public uint ScanCode { get; set; }
            public bool Shift { get; set; }
            public DateTime TimestampUtc { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        private readonly LeaderKeyConfig config;
        private readonly ConcurrentQueue<InputEvent> inputQueue = new ConcurrentQueue<InputEvent>();
        private readonly Dictionary<string, LeaderSequenceItem> itemByDefinitionId;
        private readonly LeaderUsageStore usageStore = new LeaderUsageStore();
        private readonly LeaderStateMachine stateMachine;
        private readonly Timer eventPumpTimer;
        private readonly Timer hudDelayTimer;
        private readonly Timer progressTimer;
        private readonly Timer contextTimer;

        private IntPtr hookId = IntPtr.Zero;
        private HookProc hookDelegate;
        private LeaderHudForm hudForm;
        private uint triggerVk = VK_CAPITAL;
        private bool isRunning;
        private int captureFlag;
        private DateTime lastLeaderPressUtc = DateTime.MinValue;
        private DateTime deadlineUtc = DateTime.MaxValue;
        private DateTime timeoutStartedUtc = DateTime.MinValue;
        private int currentTimeoutMs;
        private IntPtr targetNxWindow = IntPtr.Zero;
        private NxBridgeContext currentContext;

        public event Action<string> StatusChanged;
        public event Action<string, LeaderSequenceItem> SequenceExecuted;

        public bool IsRunning => isRunning;
        public bool IsActive => stateMachine.IsCapturing;
        public LeaderState CurrentState => stateMachine.State;

        public LeaderKeyEngine(LeaderKeyConfig cfg)
        {
            config = cfg ?? new LeaderKeyConfig();
            config.ApplyDefaults();
            ParseTriggerKey();

            List<LeaderSequenceItem> items = (config.Sequences ?? new List<LeaderSequenceItem>())
                .Where(item => item != null && item.Enabled)
                .ToList();
            List<SequenceDefinition> definitions = items
                .Select(AdaptiveLeaderPolicy.ToDefinition)
                .Where(definition => definition != null)
                .ToList();
            itemByDefinitionId = items.ToDictionary(
                item => AdaptiveLeaderPolicy.NormalizeSequence(item.Sequence),
                item => item,
                StringComparer.OrdinalIgnoreCase);
            stateMachine = new LeaderStateMachine(new SequenceAutomaton(definitions));

            eventPumpTimer = new Timer { Interval = 15 };
            eventPumpTimer.Tick += EventPumpTimerTick;
            hudDelayTimer = new Timer { Interval = Math.Max(50, config.HudDelayMs) };
            hudDelayTimer.Tick += HudDelayTimerTick;
            progressTimer = new Timer { Interval = 40 };
            progressTimer.Tick += ProgressTimerTick;
            contextTimer = new Timer { Interval = 250 };
            contextTimer.Tick += ContextTimerTick;
        }

        public void Start()
        {
            if (isRunning) return;
            hudForm = new LeaderHudForm();
            hookDelegate = HookCallback;

            IntPtr module = GetModuleHandle(null);
            if (module == IntPtr.Zero)
            {
                using (Process process = Process.GetCurrentProcess())
                using (ProcessModule processModule = process.MainModule)
                {
                    module = GetModuleHandle(processModule.ModuleName);
                }
            }

            hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookDelegate, module, 0);
            if (hookId == IntPtr.Zero)
                throw new InvalidOperationException("Не удалось установить low-level keyboard hook. Win32=" + Marshal.GetLastWin32Error());

            currentContext = NxCommandBridgeClient.ReadContext();
            eventPumpTimer.Start();
            contextTimer.Start();
            isRunning = true;
            StatusChanged?.Invoke("Leader HFSM активен; состояние Idle");
        }

        public void Stop()
        {
            if (!isRunning) return;
            eventPumpTimer.Stop();
            contextTimer.Stop();
            hudDelayTimer.Stop();
            progressTimer.Stop();
            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }
            ApplyTransition(stateMachine.Cancel("Движок остановлен."));
            if (hudForm != null && !hudForm.IsDisposed)
            {
                hudForm.DismissHud();
                hudForm.Dispose();
            }
            hudForm = null;
            isRunning = false;
            StatusChanged?.Invoke("Leader HFSM остановлен");
        }

        private void ParseTriggerKey()
        {
            string key = (config.TriggerKey ?? "CapsLock").Trim().ToLowerInvariant();
            triggerVk = key == "f12" ? VK_F12 : VK_CAPITAL;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(hookId, nCode, wParam, lParam);
            bool keyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            bool keyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;
            if (!keyDown && !keyUp) return CallNextHookEx(hookId, nCode, wParam, lParam);

            KBDLLHOOKSTRUCT data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if ((data.flags & LLKHF_INJECTED) != 0)
                return CallNextHookEx(hookId, nCode, wParam, lParam);

            bool capture = Volatile.Read(ref captureFlag) == 1;
            if (keyUp)
            {
                if (data.vkCode == triggerVk || capture) return (IntPtr)1;
                return CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            IntPtr activeNxWindow = GetActiveNXWindow();
            if (config.HookOnlyWhenNXActive && activeNxWindow == IntPtr.Zero)
            {
                if (capture)
                {
                    inputQueue.Enqueue(new InputEvent
                    {
                        Kind = InputKind.Cancel,
                        TimestampUtc = DateTime.UtcNow,
                        Reason = "Фокус покинул Siemens NX."
                    });
                }
                return CallNextHookEx(hookId, nCode, wParam, lParam);
            }
            if (activeNxWindow != IntPtr.Zero) targetNxWindow = activeNxWindow;

            if (data.vkCode == triggerVk)
            {
                if (IsFocusedInTextInput()) return CallNextHookEx(hookId, nCode, wParam, lParam);
                Interlocked.Exchange(ref captureFlag, 1);
                inputQueue.Enqueue(new InputEvent
                {
                    Kind = InputKind.Trigger,
                    VirtualKey = data.vkCode,
                    ScanCode = data.scanCode,
                    TimestampUtc = DateTime.UtcNow
                });
                return (IntPtr)1;
            }

            if (capture)
            {
                inputQueue.Enqueue(new InputEvent
                {
                    Kind = InputKind.Key,
                    VirtualKey = data.vkCode,
                    ScanCode = data.scanCode,
                    Shift = (GetKeyState(VK_SHIFT) & 0x8000) != 0,
                    TimestampUtc = DateTime.UtcNow
                });
                return (IntPtr)1;
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private void EventPumpTimerTick(object sender, EventArgs e)
        {
            int processed = 0;
            while (processed < 64 && inputQueue.TryDequeue(out InputEvent input))
            {
                processed++;
                try { ProcessInput(input); }
                catch (Exception ex)
                {
                    ApplyTransition(stateMachine.Cancel("Внутренняя ошибка обработки ввода."));
                    StatusChanged?.Invoke("Leader HFSM error: " + ex.Message);
                }
            }
        }

        private void ProcessInput(InputEvent input)
        {
            if (input.Kind == InputKind.Cancel)
            {
                ApplyTransition(stateMachine.Cancel(input.Reason));
                return;
            }
            if (input.Kind == InputKind.Trigger)
            {
                TimeSpan sinceLast = input.TimestampUtc - lastLeaderPressUtc;
                lastLeaderPressUtc = input.TimestampUtc;
                bool doubleTap = config.StickyModeOnDoubleTap && sinceLast.TotalMilliseconds >= 0 && sinceLast.TotalMilliseconds <= 380;
                RefreshContext();
                if (!stateMachine.IsCapturing)
                {
                    ApplyTransition(stateMachine.Activate(doubleTap, currentContext));
                }
                else if (doubleTap && !stateMachine.Sticky)
                {
                    ApplyTransition(stateMachine.Activate(true, currentContext));
                }
                else if (!doubleTap)
                {
                    ApplyTransition(stateMachine.Cancel("Leader закрыт повторным нажатием."));
                }
                return;
            }

            uint key = input.VirtualKey;
            if (key == VK_ESCAPE)
            {
                ApplyTransition(stateMachine.Cancel("Отменено клавишей Esc."));
                return;
            }
            if (key == VK_TAB)
            {
                CycleModule(input.Shift ? -1 : 1);
                return;
            }
            if (key == VK_RETURN)
            {
                ApplyTransition(stateMachine.Confirm());
                return;
            }
            if (key == VK_BACK)
            {
                ApplyTransition(stateMachine.Backspace());
                return;
            }
            if (key == VK_SPACE)
            {
                ApplyTransition(stateMachine.EnterSearch());
                return;
            }
            if (key == VK_SHIFT || key == VK_CONTROL || key == VK_MENU) return;

            char character = MapVkToChar(key);
            if (character == '\0')
            {
                System.Media.SystemSounds.Asterisk.Play();
                return;
            }
            ApplyTransition(stateMachine.InputToken(character.ToString()));
        }

        private void ApplyTransition(LeaderTransition transition)
        {
            if (transition == null) return;
            Interlocked.Exchange(ref captureFlag, stateMachine.IsCapturing ? 1 : 0);

            switch (transition.Action)
            {
                case LeaderActionKind.None:
                    return;

                case LeaderActionKind.Activated:
                    StartDeadline(Math.Max(1000, config.FirstKeyTimeoutMs));
                    hudDelayTimer.Stop();
                    hudDelayTimer.Interval = Math.Max(50, config.HudDelayMs);
                    hudDelayTimer.Start();
                    progressTimer.Start();
                    StatusChanged?.Invoke(stateMachine.Sticky ? "Leader: Sticky Root" : "Leader: Root");
                    UpdateHud();
                    break;

                case LeaderActionKind.Updated:
                    StartDeadline(TimeoutForState(transition.State));
                    UpdateHud();
                    if (!string.IsNullOrWhiteSpace(transition.Reason)) StatusChanged?.Invoke(transition.Reason);
                    break;

                case LeaderActionKind.Beep:
                    System.Media.SystemSounds.Asterisk.Play();
                    if (!string.IsNullOrWhiteSpace(transition.Reason)) StatusChanged?.Invoke(transition.Reason);
                    break;

                case LeaderActionKind.RequireConfirmation:
                    StartDeadline(ConfirmationTimeoutMs);
                    ShowConfirmation(transition.Command);
                    StatusChanged?.Invoke("Leader → " + transition.Command?.Sequence + ": требуется Enter");
                    break;

                case LeaderActionKind.SwitchModule:
                    StartDeadline(ModuleSwitchTimeoutMs);
                    QueueModuleSwitch(transition.TargetModuleId);
                    break;

                case LeaderActionKind.ExecuteCommand:
                    DispatchCommand(transition.Command, transition.ConfirmationAccepted);
                    break;

                case LeaderActionKind.RequestQueued:
                    StartDeadline(ResultTimeoutMs);
                    StatusChanged?.Invoke("Команда поставлена в очередь: " + transition.RequestId);
                    break;

                case LeaderActionKind.RequestCompleted:
                    if (transition.Command != null && TryGetItem(transition.Command, out LeaderSequenceItem completed))
                        usageStore.Record(completed);
                    StatusChanged?.Invoke("NX подтвердил выполнение: " + transition.Reason);
                    FinishVisualState();
                    break;

                case LeaderActionKind.RequestFailed:
                case LeaderActionKind.Rejected:
                    System.Media.SystemSounds.Exclamation.Play();
                    StatusChanged?.Invoke("Команда отклонена: " + transition.Reason);
                    FinishVisualState();
                    break;

                case LeaderActionKind.Cancelled:
                case LeaderActionKind.TimedOut:
                    StatusChanged?.Invoke(transition.Reason);
                    FinishVisualState();
                    break;
            }
        }

        private void DispatchCommand(SequenceDefinition definition, bool confirmationAccepted)
        {
            if (!TryGetItem(definition, out LeaderSequenceItem item))
            {
                ApplyTransition(stateMachine.CompleteRequest(false, "Команда отсутствует в профиле."));
                return;
            }

            try
            {
                NxCommandRequest request = NxCommandBridgeClient.Enqueue(item, confirmationAccepted);
                ApplyTransition(stateMachine.MarkRequestQueued(request.RequestId));
                SequenceExecuted?.Invoke(item.Sequence, item);
            }
            catch (Exception ex)
            {
                ApplyTransition(stateMachine.CompleteRequest(false, ex.Message));
            }
        }

        private void QueueModuleSwitch(string targetModuleId)
        {
            ModuleConfig module = config.RuntimeModules?.FirstOrDefault(value =>
                value.Enabled && string.Equals(
                    ContextGuardEvaluator.NormalizeModule(value.ID),
                    ContextGuardEvaluator.NormalizeModule(targetModuleId),
                    StringComparison.OrdinalIgnoreCase));
            if (module == null)
            {
                ApplyTransition(stateMachine.Cancel("В профиле отсутствует описание модуля " + targetModuleId + "."));
                return;
            }
            try
            {
                NxCommandBridgeClient.EnqueueModuleSwitch(module);
                StatusChanged?.Invoke("Ожидание подтверждения переключения NX → " + module.Label);
                UpdateHud(module.Label + " (переключение)", module.ID);
            }
            catch (Exception ex)
            {
                ApplyTransition(stateMachine.Cancel("Не удалось запросить переключение модуля: " + ex.Message));
            }
        }

        private void CycleModule(int delta)
        {
            List<ModuleConfig> modules = config.RuntimeModules?
                .Where(module => module != null && module.Enabled && !string.IsNullOrWhiteSpace(module.ID))
                .ToList() ?? new List<ModuleConfig>();
            if (modules.Count == 0)
            {
                System.Media.SystemSounds.Asterisk.Play();
                return;
            }

            string current = ContextGuardEvaluator.NormalizeModule(currentContext?.ModuleId);
            int index = modules.FindIndex(module => string.Equals(
                ContextGuardEvaluator.NormalizeModule(module.ID), current, StringComparison.OrdinalIgnoreCase));
            if (index < 0) index = 0;
            int next = (index + delta) % modules.Count;
            if (next < 0) next += modules.Count;
            ApplyTransition(stateMachine.BeginManualModuleSwitch(modules[next].ID));
        }

        private void ContextTimerTick(object sender, EventArgs e)
        {
            if (!isRunning) return;
            RefreshContext();
            LeaderTransition contextTransition = stateMachine.UpdateContext(currentContext);
            if (contextTransition.Action != LeaderActionKind.None) ApplyTransition(contextTransition);

            if (stateMachine.State == LeaderState.AwaitingResult &&
                NxCommandBridgeClient.TryReadResult(stateMachine.PendingRequestId, out NxBridgeResult result))
            {
                ApplyTransition(stateMachine.CompleteRequest(result.Success, result.Message));
            }
        }

        private void RefreshContext()
        {
            NxBridgeContext latest = NxCommandBridgeClient.ReadContext();
            if (latest != null) currentContext = latest;
        }

        private void HudDelayTimerTick(object sender, EventArgs e)
        {
            hudDelayTimer.Stop();
            if (!stateMachine.IsCapturing || hudForm == null || hudForm.IsDisposed) return;
            string label = ModuleLabel();
            string id = currentContext?.ModuleId ?? string.Empty;
            hudForm.DisplayHud(config.TriggerKey, stateMachine.Sticky, RankedCandidates(), config.HudOpacity, label, id);
            UpdateHud();
        }

        private void ProgressTimerTick(object sender, EventArgs e)
        {
            if (!stateMachine.IsCapturing)
            {
                progressTimer.Stop();
                return;
            }
            if (deadlineUtc == DateTime.MaxValue) return;

            double elapsed = (DateTime.UtcNow - timeoutStartedUtc).TotalMilliseconds;
            float remaining = currentTimeoutMs <= 0
                ? 0
                : (float)Math.Max(0.0, 1.0 - elapsed / currentTimeoutMs);
            if (hudForm != null && !hudForm.IsDisposed && hudForm.Visible)
                hudForm.UpdateTimeoutProgress(remaining);

            if (remaining > 0) return;
            if (stateMachine.State == LeaderState.AwaitingResult)
                ApplyTransition(stateMachine.CompleteRequest(false, "Bridge не вернул результат за отведённое время."));
            else
                ApplyTransition(stateMachine.Timeout());
        }

        private void StartDeadline(int milliseconds)
        {
            currentTimeoutMs = Math.Max(250, milliseconds);
            timeoutStartedUtc = DateTime.UtcNow;
            deadlineUtc = timeoutStartedUtc.AddMilliseconds(currentTimeoutMs);
            progressTimer.Start();
        }

        private int TimeoutForState(LeaderState state)
        {
            switch (state)
            {
                case LeaderState.Prefix:
                case LeaderState.Search:
                    return Math.Max(1000, config.NextKeyTimeoutMs);
                case LeaderState.SwitchingModule:
                    return ModuleSwitchTimeoutMs;
                case LeaderState.AwaitingConfirmation:
                    return ConfirmationTimeoutMs;
                case LeaderState.AwaitingResult:
                    return ResultTimeoutMs;
                default:
                    return Math.Max(1000, config.FirstKeyTimeoutMs);
            }
        }

        private void UpdateHud(string moduleLabel = null, string moduleId = null)
        {
            if (hudForm == null || hudForm.IsDisposed || !hudForm.Visible) return;
            string label = moduleLabel ?? ModuleLabel();
            string id = moduleId ?? currentContext?.ModuleId ?? string.Empty;
            List<LeaderSequenceItem> candidates = RankedCandidates();
            if (stateMachine.State == LeaderState.Search)
                hudForm.SetSearchMode(stateMachine.SearchQuery, candidates, label, id);
            else
                hudForm.UpdateState(stateMachine.Prefix, candidates, stateMachine.Sticky, label, id);
        }

        private void ShowConfirmation(SequenceDefinition definition)
        {
            if (!TryGetItem(definition, out LeaderSequenceItem item)) return;
            if (hudForm != null && !hudForm.IsDisposed)
                hudForm.SetConfirmation(item, ModuleLabel(), currentContext?.ModuleId ?? string.Empty);
        }

        private List<LeaderSequenceItem> RankedCandidates()
        {
            List<LeaderSequenceItem> items = stateMachine.CurrentCandidates()
                .Select(definition => TryGetItem(definition, out LeaderSequenceItem item) ? item : null)
                .Where(item => item != null)
                .ToList();
            return AdaptiveLeaderPolicy.Rank(
                items,
                currentContext,
                usageStore,
                currentContext?.ModuleId,
                true);
        }

        private bool TryGetItem(SequenceDefinition definition, out LeaderSequenceItem item)
        {
            item = null;
            return definition != null && itemByDefinitionId.TryGetValue(definition.Id, out item);
        }

        private string ModuleLabel()
        {
            if (!string.IsNullOrWhiteSpace(currentContext?.ModuleLabel)) return currentContext.ModuleLabel;
            return string.IsNullOrWhiteSpace(currentContext?.ModuleId) ? "NX context unknown" : currentContext.ModuleId;
        }

        private void FinishVisualState()
        {
            hudDelayTimer.Stop();
            if (!stateMachine.IsCapturing)
            {
                progressTimer.Stop();
                deadlineUtc = DateTime.MaxValue;
                if (hudForm != null && !hudForm.IsDisposed) hudForm.DismissHud();
            }
            else
            {
                StartDeadline(Math.Max(1000, config.FirstKeyTimeoutMs));
                UpdateHud();
            }
            Interlocked.Exchange(ref captureFlag, stateMachine.IsCapturing ? 1 : 0);
        }

        public static void SendShortcutToActiveWindow(string shortcut)
        {
            if (string.IsNullOrWhiteSpace(shortcut)) return;
            string[] parts = shortcut.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            var modifiers = new List<byte>();
            byte mainKey = 0;
            foreach (string part in parts)
            {
                string token = part.Trim().ToLowerInvariant();
                switch (token)
                {
                    case "ctrl":
                    case "control": modifiers.Add(VK_CONTROL); break;
                    case "alt": modifiers.Add(VK_MENU); break;
                    case "shift": modifiers.Add(VK_SHIFT); break;
                    default:
                        if (TryParseKeyToken(token, out Keys key)) mainKey = (byte)key;
                        break;
                }
            }
            foreach (byte modifier in modifiers) keybd_event(modifier, 0, 0, UIntPtr.Zero);
            if (mainKey != 0)
            {
                keybd_event(mainKey, 0, 0, UIntPtr.Zero);
                Thread.Sleep(20);
                keybd_event(mainKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            for (int index = modifiers.Count - 1; index >= 0; index--)
                keybd_event(modifiers[index], 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private static bool TryParseKeyToken(string token, out Keys key)
        {
            key = Keys.None;
            if (string.IsNullOrWhiteSpace(token)) return false;
            if (token.Length == 1)
            {
                char character = char.ToUpperInvariant(token[0]);
                if (character >= 'A' && character <= 'Z')
                {
                    key = Keys.A + (character - 'A');
                    return true;
                }
                if (character >= '0' && character <= '9')
                {
                    key = Keys.D0 + (character - '0');
                    return true;
                }
            }
            switch (token.ToLowerInvariant())
            {
                case "esc": key = Keys.Escape; return true;
                case "del": key = Keys.Delete; return true;
                case "pgup": key = Keys.PageUp; return true;
                case "pgdn": key = Keys.PageDown; return true;
                default: return Enum.TryParse(token, true, out key);
            }
        }

        private static char MapVkToChar(uint virtualKey)
        {
            if (virtualKey >= 0x41 && virtualKey <= 0x5A) return (char)virtualKey;
            if (virtualKey >= 0x30 && virtualKey <= 0x39) return (char)virtualKey;
            return '\0';
        }

        private static IntPtr GetActiveNXWindow()
        {
            IntPtr window = GetForegroundWindow();
            return IsNXWindow(window) ? window : IntPtr.Zero;
        }

        private static bool IsNXWindow(IntPtr window)
        {
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out uint processId);
            if (processId == 0) return false;
            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                {
                    string name = process.ProcessName.ToLowerInvariant();
                    return name == "ugraf" || name == "nx" || name == "run_nx" || name.StartsWith("designcenter");
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsFocusedInTextInput()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            uint threadId = GetWindowThreadProcessId(window, out _);
            var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
            if (!GetGUIThreadInfo(threadId, ref info) || info.hwndFocus == IntPtr.Zero) return false;
            var className = new StringBuilder(128);
            GetClassName(info.hwndFocus, className, className.Capacity);
            string value = className.ToString().ToLowerInvariant();
            return value.Contains("edit") || value.Contains("textbox") || value.Contains("richedit") || value.Contains("scintilla");
        }

        public void Dispose()
        {
            Stop();
            eventPumpTimer.Dispose();
            hudDelayTimer.Dispose();
            progressTimer.Dispose();
            contextTimer.Dispose();
        }
    }
}
