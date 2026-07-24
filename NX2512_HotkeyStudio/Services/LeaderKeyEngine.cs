using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_TRIGGER_ID = 0x4E584B;
        private const uint LLKHF_INJECTED = 0x10;
        private const uint MOD_NOREPEAT = 0x4000;
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
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
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
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll")] private static extern short GetKeyState(int virtualKey);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

        private enum InputKind { Trigger, Key, Cancel }
        private sealed class InputEvent
        {
            public InputKind Kind { get; set; }
            public uint VirtualKey { get; set; }
            public bool Shift { get; set; }
            public DateTime TimestampUtc { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        private readonly LeaderKeyConfig config;
        private readonly LeaderBehaviorProfile behaviorProfile;
        private readonly ConcurrentQueue<InputEvent> queue = new ConcurrentQueue<InputEvent>();
        private readonly Dictionary<string, LeaderSequenceItem> itemsById;
        private readonly LeaderUsageStore usageStore = new LeaderUsageStore();
        private readonly LeaderStateMachine stateMachine;
        private readonly Timer eventPump = new Timer { Interval = 15 };
        private readonly Timer hudDelay;
        private readonly Timer capsLockRestore = new Timer { Interval = 35 };
        private readonly Timer progress = new Timer { Interval = 40 };
        private readonly Timer contextWatch = new Timer { Interval = 250 };

        private IntPtr hookId;
        private HookProc hookDelegate;
        private HotkeyMessageWindow hotkeyWindow;
        private bool registeredTriggerHotkey;
        private LeaderHudForm hud;
        private uint triggerVk = VK_CAPITAL;
        private bool running;
        private int captureFlag;
        private DateTime lastQueuedTriggerUtc = DateTime.MinValue;
        private DateTime lastTriggerUtc = DateTime.MinValue;
        private DateTime timeoutStartUtc;
        private DateTime deadlineUtc = DateTime.MaxValue;
        private int timeoutMs;
        private int capsLockRestoreAttempts;
        private NxBridgeContext currentContext;
        private ModuleConfig activeModule;

        public event Action<string> StatusChanged;
        public event Action<string, LeaderSequenceItem> SequenceExecuted;
        public bool IsRunning => running;
        public bool IsActive => stateMachine.IsCapturing;
        public LeaderState CurrentState => stateMachine.State;
        public string BehaviorProfilePath => behaviorProfile.SourcePath;
        public string ActiveModuleId => activeModule?.ID ?? string.Empty;

        public LeaderKeyEngine(LeaderKeyConfig value)
        {
            config = value ?? new LeaderKeyConfig();
            config.ApplyDefaults();
            config.RebuildFromModules(config.RuntimeModules);
            if (!config.AdaptiveModuleMode) throw new InvalidOperationException("Adaptive module mode is required.");
            if (config.Sequences.Count == 0) throw new InvalidOperationException("The profile contains no module commands.");

            triggerVk = string.Equals(config.TriggerKey, "F12", StringComparison.OrdinalIgnoreCase) ? VK_F12 : VK_CAPITAL;
            behaviorProfile = LeaderBehaviorProfile.LoadDefault();
            List<LeaderSequenceItem> items = config.Sequences.Where(item => item != null && item.Enabled).ToList();
            List<SequenceDefinition> definitions = items.Select(AdaptiveLeaderPolicy.ToDefinition).Where(item => item != null).ToList();
            itemsById = items.ToDictionary(item => AdaptiveLeaderPolicy.NormalizeSequence(item.Sequence), item => item, StringComparer.OrdinalIgnoreCase);
            stateMachine = new LeaderStateMachine(new SequenceAutomaton(definitions), new ContextGuardEvaluator(behaviorProfile));

            hudDelay = new Timer { Interval = Math.Max(50, config.HudDelayMs) };
            eventPump.Tick += EventPumpTick;
            hudDelay.Tick += HudDelayTick;
            capsLockRestore.Tick += CapsLockRestoreTick;
            progress.Tick += ProgressTick;
            contextWatch.Tick += ContextWatchTick;
        }

        public void Start()
        {
            if (running) return;
            hud = new LeaderHudForm();
            hookDelegate = HookCallback;
            IntPtr module = GetModuleHandle(null);
            if (module == IntPtr.Zero)
            {
                using (Process process = Process.GetCurrentProcess())
                using (ProcessModule processModule = process.MainModule)
                    module = GetModuleHandle(processModule?.ModuleName);
            }
            hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookDelegate, module, 0);
            if (hookId == IntPtr.Zero)
                throw new InvalidOperationException("Не удалось установить keyboard hook. Win32=" + Marshal.GetLastWin32Error());
            TryRegisterTriggerHotkey();
            RefreshContext();
            eventPump.Start();
            contextWatch.Start();
            running = true;
            string triggerBackend = registeredTriggerHotkey ? "hook+hotkey" : "hook";
            StatusChanged?.Invoke("Адаптивный Leader активен: CapsLock → команда текущего модуля NX (" + triggerBackend + ")");
        }

        public void Stop()
        {
            if (!running) return;
            eventPump.Stop();
            contextWatch.Stop();
            hudDelay.Stop();
            progress.Stop();
            if (registeredTriggerHotkey && hotkeyWindow != null) UnregisterHotKey(hotkeyWindow.Handle, HOTKEY_TRIGGER_ID);
            registeredTriggerHotkey = false;
            if (hotkeyWindow != null) { hotkeyWindow.DestroyHandle(); hotkeyWindow = null; }
            if (hookId != IntPtr.Zero) { UnhookWindowsHookEx(hookId); hookId = IntPtr.Zero; }
            Apply(stateMachine.Cancel("Движок остановлен."));
            if (hud != null && !hud.IsDisposed) { hud.DismissHud(); hud.Dispose(); }
            hud = null;
            activeModule = null;
            running = false;
            StatusChanged?.Invoke("Адаптивный Leader остановлен");
        }

        private IntPtr HookCallback(int code, IntPtr message, IntPtr dataPointer)
        {
            if (code < 0) return CallNextHookEx(hookId, code, message, dataPointer);
            bool down = message == (IntPtr)WM_KEYDOWN || message == (IntPtr)WM_SYSKEYDOWN;
            bool up = message == (IntPtr)WM_KEYUP || message == (IntPtr)WM_SYSKEYUP;
            if (!down && !up) return CallNextHookEx(hookId, code, message, dataPointer);
            KBDLLHOOKSTRUCT data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(dataPointer);
            if ((data.flags & LLKHF_INJECTED) != 0) return CallNextHookEx(hookId, code, message, dataPointer);

            bool capturing = Volatile.Read(ref captureFlag) == 1;
            if (up)
            {
                if (data.vkCode == triggerVk || capturing) return (IntPtr)1;
                return CallNextHookEx(hookId, code, message, dataPointer);
            }

            if (config.HookOnlyWhenNXActive && GetActiveNxWindow() == IntPtr.Zero)
            {
                if (capturing) queue.Enqueue(new InputEvent { Kind = InputKind.Cancel, Reason = "Фокус покинул Siemens NX.", TimestampUtc = DateTime.UtcNow });
                return CallNextHookEx(hookId, code, message, dataPointer);
            }

            if (data.vkCode == triggerVk)
            {
                if (IsFocusedInTextInput()) return CallNextHookEx(hookId, code, message, dataPointer);
                Interlocked.Exchange(ref captureFlag, 1);
                ScheduleCapsLockRestore();
                QueueTrigger(data.vkCode);
                return (IntPtr)1;
            }

            if (!capturing) return CallNextHookEx(hookId, code, message, dataPointer);
            queue.Enqueue(new InputEvent
            {
                Kind = InputKind.Key,
                VirtualKey = data.vkCode,
                Shift = (GetKeyState(VK_SHIFT) & 0x8000) != 0,
                TimestampUtc = DateTime.UtcNow
            });
            return (IntPtr)1;
        }

        private void TryRegisterTriggerHotkey()
        {
            try
            {
                hotkeyWindow = new HotkeyMessageWindow(this);
                hotkeyWindow.CreateHandle(new CreateParams { Caption = "NXKeysLeaderHotkeySink" });
                registeredTriggerHotkey = RegisterHotKey(hotkeyWindow.Handle, HOTKEY_TRIGGER_ID, MOD_NOREPEAT, triggerVk);
                if (!registeredTriggerHotkey)
                    StatusChanged?.Invoke("CapsLock hotkey fallback недоступен. Win32=" + Marshal.GetLastWin32Error());
            }
            catch (Exception exception)
            {
                registeredTriggerHotkey = false;
                StatusChanged?.Invoke("CapsLock hotkey fallback не запущен: " + exception.Message);
            }
        }

        private void OnTriggerHotkey()
        {
            bool capturing = Volatile.Read(ref captureFlag) == 1;
            if (config.HookOnlyWhenNXActive && GetActiveNxWindow() == IntPtr.Zero)
            {
                if (capturing) queue.Enqueue(new InputEvent { Kind = InputKind.Cancel, Reason = "Фокус покинул Siemens NX.", TimestampUtc = DateTime.UtcNow });
                return;
            }
            if (IsFocusedInTextInput()) return;
            Interlocked.Exchange(ref captureFlag, 1);
            ScheduleCapsLockRestore();
            QueueTrigger(triggerVk);
        }

        private void ScheduleCapsLockRestore()
        {
            if (triggerVk != VK_CAPITAL) return;
            capsLockRestoreAttempts = 0;
            capsLockRestore.Stop();
            capsLockRestore.Start();
        }

        private void CapsLockRestoreTick(object sender, EventArgs eventArgs)
        {
            capsLockRestoreAttempts++;
            EnsureCapsLockOff();
            if (capsLockRestoreAttempts >= 4 || !IsCapsLockOn()) capsLockRestore.Stop();
        }

        private static bool IsCapsLockOn() => (GetKeyState(VK_CAPITAL) & 0x0001) != 0;

        private static void EnsureCapsLockOff()
        {
            if (!IsCapsLockOn()) return;
            keybd_event(VK_CAPITAL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_CAPITAL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void QueueTrigger(uint virtualKey)
        {
            DateTime now = DateTime.UtcNow;
            if ((now - lastQueuedTriggerUtc).TotalMilliseconds < 120) return;
            lastQueuedTriggerUtc = now;
            queue.Enqueue(new InputEvent { Kind = InputKind.Trigger, VirtualKey = virtualKey, TimestampUtc = now });
        }

        private void EventPumpTick(object sender, EventArgs eventArgs)
        {
            int processed = 0;
            while (processed++ < 64 && queue.TryDequeue(out InputEvent input))
            {
                try { ProcessInput(input); }
                catch (Exception exception)
                {
                    Apply(stateMachine.Cancel("Внутренняя ошибка Leader."));
                    StatusChanged?.Invoke(exception.Message);
                }
            }
        }

        private void ProcessInput(InputEvent input)
        {
            if (input.Kind == InputKind.Cancel) { Apply(stateMachine.Cancel(input.Reason)); return; }
            if (input.Kind == InputKind.Trigger) { ProcessTrigger(input.TimestampUtc); return; }
            uint key = input.VirtualKey;
            if (key == VK_ESCAPE) { Apply(stateMachine.Cancel("Отменено клавишей Esc.")); return; }
            if (key == VK_TAB) { CycleModule(input.Shift ? -1 : 1); return; }
            if (key == VK_RETURN)
            {
                if (stateMachine.State == LeaderState.Search) ExecuteFirstSearchResult();
                else Apply(stateMachine.Confirm());
                return;
            }
            if (key == VK_BACK)
            {
                if (stateMachine.State == LeaderState.Prefix) Apply(stateMachine.Cancel("Leader закрыт."));
                else Apply(stateMachine.Backspace());
                return;
            }
            if (key == VK_SPACE) { Apply(stateMachine.EnterSearch()); return; }
            if (key == VK_SHIFT || key == VK_CONTROL || key == VK_MENU) return;
            char character = MapKey(key);
            if (character == '\0') { System.Media.SystemSounds.Asterisk.Play(); return; }
            Apply(stateMachine.InputToken(character.ToString()));
        }

        private void ProcessTrigger(DateTime timestampUtc)
        {
            TimeSpan sinceLast = timestampUtc - lastTriggerUtc;
            lastTriggerUtc = timestampUtc;
            bool doubleTap = config.StickyModeOnDoubleTap && sinceLast.TotalMilliseconds >= 0 && sinceLast.TotalMilliseconds <= 380;
            RefreshContext();
            if (!stateMachine.IsCapturing) ActivateScoped(doubleTap);
            else if (doubleTap && !stateMachine.Sticky) ActivateScoped(true);
            else if (!doubleTap) Apply(stateMachine.Cancel("Leader закрыт повторным нажатием."));
        }

        private void ActivateScoped(bool sticky)
        {
            AdaptiveModuleResolution resolution = AdaptiveModuleResolver.Resolve(config.RuntimeModules, currentContext);
            if (!resolution.IsResolved)
            {
                Apply(stateMachine.Cancel(resolution.Reason));
                System.Media.SystemSounds.Asterisk.Play();
                return;
            }
            activeModule = resolution.Module;
            Apply(stateMachine.Activate(sticky, currentContext));
            Apply(stateMachine.InputToken(activeModule.LeaderPrefix));
            StatusChanged?.Invoke("Leader: " + activeModule.Label + " · 3 колонки");
        }

        private void ExecuteFirstSearchResult()
        {
            LeaderSequenceItem first = RankedCandidates().FirstOrDefault();
            if (first == null) { System.Media.SystemSounds.Asterisk.Play(); return; }
            bool sticky = stateMachine.Sticky;
            stateMachine.Cancel("Search selection");
            Apply(stateMachine.Activate(sticky, currentContext));
            Apply(stateMachine.InputToken(activeModule.LeaderPrefix));
            List<string> tokens = SequenceAutomaton.TokenizeSequence(first.Sequence).ToList();
            int start = tokens.Count > 0 &&
                string.Equals(tokens[0], LeaderKeyConfig.NormalizeInputKey(activeModule?.LeaderPrefix), StringComparison.OrdinalIgnoreCase)
                ? 1 : 0;
            foreach (string token in tokens.Skip(start)) Apply(stateMachine.InputToken(token));
        }

        private void Apply(LeaderTransition transition)
        {
            if (transition == null) return;
            Interlocked.Exchange(ref captureFlag, stateMachine.IsCapturing ? 1 : 0);
            switch (transition.Action)
            {
                case LeaderActionKind.None: return;
                case LeaderActionKind.Activated:
                    StartDeadline(behaviorProfile.Timeouts.RootMs);
                    hudDelay.Stop(); hudDelay.Start(); progress.Start(); UpdateHud();
                    break;
                case LeaderActionKind.Updated:
                    StartDeadline(TimeoutFor(transition.State)); UpdateHud();
                    if (!string.IsNullOrWhiteSpace(transition.Reason)) StatusChanged?.Invoke(transition.Reason);
                    break;
                case LeaderActionKind.Beep:
                    System.Media.SystemSounds.Asterisk.Play();
                    if (!string.IsNullOrWhiteSpace(transition.Reason)) StatusChanged?.Invoke(transition.Reason);
                    break;
                case LeaderActionKind.RequireConfirmation:
                    StartDeadline(behaviorProfile.Timeouts.ConfirmationMs); ShowConfirmation(transition.Command);
                    break;
                case LeaderActionKind.SwitchModule:
                    StartDeadline(behaviorProfile.Timeouts.ModuleSwitchMs); QueueModuleSwitch(transition.TargetModuleId);
                    break;
                case LeaderActionKind.ExecuteCommand: Dispatch(transition.Command, transition.ConfirmationAccepted); break;
                case LeaderActionKind.RequestQueued:
                    StartDeadline(behaviorProfile.Timeouts.ResultMs);
                    StatusChanged?.Invoke("Команда поставлена в очередь: " + transition.RequestId);
                    break;
                case LeaderActionKind.RequestCompleted:
                    if (transition.Command != null && TryGetItem(transition.Command, out LeaderSequenceItem completed)) usageStore.Record(completed);
                    StatusChanged?.Invoke("NX подтвердил выполнение: " + transition.Reason); FinishVisual();
                    break;
                case LeaderActionKind.RequestFailed:
                case LeaderActionKind.Rejected:
                    System.Media.SystemSounds.Exclamation.Play();
                    StatusChanged?.Invoke("Команда отклонена: " + transition.Reason); FinishVisual();
                    break;
                case LeaderActionKind.Cancelled:
                case LeaderActionKind.TimedOut:
                    StatusChanged?.Invoke(transition.Reason); FinishVisual();
                    break;
            }
        }

        private void Dispatch(SequenceDefinition definition, bool confirmationAccepted)
        {
            if (!TryGetItem(definition, out LeaderSequenceItem item))
            {
                Apply(stateMachine.CompleteRequest(false, "Команда отсутствует в профиле."));
                return;
            }
            try
            {
                NxCommandRequest request = NxCommandBridgeClient.Enqueue(item, confirmationAccepted);
                Apply(stateMachine.MarkRequestQueued(request.RequestId));
                SequenceExecuted?.Invoke(item.DisplayPath(config.TriggerKey), item);
            }
            catch (Exception exception) { Apply(stateMachine.CompleteRequest(false, exception.Message)); }
        }

        private void QueueModuleSwitch(string targetModuleId)
        {
            ModuleConfig module = config.RuntimeModules.FirstOrDefault(value => value.Enabled && string.Equals(
                ContextGuardEvaluator.NormalizeModule(value.ID), ContextGuardEvaluator.NormalizeModule(targetModuleId), StringComparison.OrdinalIgnoreCase));
            if (module == null) { Apply(stateMachine.Cancel("Модуль отсутствует в профиле: " + targetModuleId)); return; }
            try
            {
                NxCommandBridgeClient.EnqueueModuleSwitch(module);
                StatusChanged?.Invoke("Ожидание переключения NX → " + module.Label);
                UpdateHud(module.Label + " (переключение)", module.ID);
            }
            catch (Exception exception) { Apply(stateMachine.Cancel("Не удалось переключить модуль: " + exception.Message)); }
        }

        private void CycleModule(int delta)
        {
            List<ModuleConfig> modules = config.RuntimeModules.Where(module => module != null && module.Enabled).ToList();
            if (modules.Count == 0) return;
            int index = modules.FindIndex(module => AdaptiveModuleResolver.Same(module, activeModule));
            if (index < 0) index = 0;
            int next = (index + delta) % modules.Count;
            if (next < 0) next += modules.Count;
            Apply(stateMachine.BeginManualModuleSwitch(modules[next].ID));
        }

        private void ContextWatchTick(object sender, EventArgs eventArgs)
        {
            if (!running) return;
            if (triggerVk == VK_CAPITAL && GetActiveNxWindow() != IntPtr.Zero) EnsureCapsLockOff();
            ModuleConfig previous = activeModule;
            RefreshContext();
            LeaderTransition contextTransition = stateMachine.UpdateContext(currentContext);
            if (contextTransition.Action != LeaderActionKind.None) Apply(contextTransition);

            if (stateMachine.State == LeaderState.AwaitingResult &&
                NxCommandBridgeClient.TryReadResult(stateMachine.PendingRequestId, out NxBridgeResult result))
                Apply(stateMachine.CompleteRequest(result.Success, result.Message));

            if (stateMachine.IsCapturing && stateMachine.State != LeaderState.AwaitingResult &&
                stateMachine.State != LeaderState.Dispatching && stateMachine.State != LeaderState.AwaitingConfirmation &&
                stateMachine.State != LeaderState.SwitchingModule && !AdaptiveModuleResolver.Same(previous, activeModule))
            {
                bool sticky = stateMachine.Sticky;
                stateMachine.Cancel("Контекст изменён");
                ActivateScoped(sticky);
            }
            else if (stateMachine.IsCapturing && stateMachine.State == LeaderState.Root && activeModule != null)
            {
                Apply(stateMachine.InputToken(activeModule.LeaderPrefix));
            }
        }

        private void RefreshContext()
        {
            NxBridgeContext latest = NxCommandBridgeClient.ReadContext();
            if (latest != null && latest.IsFresh) currentContext = latest;
            else if (TryCreateForegroundNxFallbackContext(out NxBridgeContext fallback)) currentContext = fallback;
            AdaptiveModuleResolution resolution = AdaptiveModuleResolver.Resolve(config.RuntimeModules, currentContext);
            if (resolution.IsResolved) activeModule = resolution.Module;
        }

        private static bool TryCreateForegroundNxFallbackContext(out NxBridgeContext context)
        {
            context = null;
            IntPtr window = GetActiveNxWindow();
            if (window == IntPtr.Zero) return false;

            string title = GetWindowTitle(window);
            string moduleId = ModuleIdFromWindowTitle(title);
            string applicationId = ApplicationIdFromModuleId(moduleId);
            context = new NxBridgeContext
            {
                SchemaVersion = NXKeys.Protocol.NxProtocolConstants.SchemaVersion,
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

        private void HudDelayTick(object sender, EventArgs eventArgs)
        {
            hudDelay.Stop();
            if (!stateMachine.IsCapturing || hud == null || hud.IsDisposed) return;
            hud.DisplayHud(config.TriggerKey, stateMachine.Sticky, RankedCandidates(), config.HudOpacity,
                ModuleLabel(), activeModule?.ID ?? currentContext?.ModuleId ?? string.Empty, IsBridgeReady(), currentContext?.SelectionCount ?? -1,
                stateMachine.Prefix);
            UpdateHud();
        }

        private void ProgressTick(object sender, EventArgs eventArgs)
        {
            if (!stateMachine.IsCapturing) { progress.Stop(); return; }
            if (deadlineUtc == DateTime.MaxValue) return;
            double elapsed = (DateTime.UtcNow - timeoutStartUtc).TotalMilliseconds;
            float remaining = timeoutMs <= 0 ? 0 : (float)Math.Max(0, 1 - elapsed / timeoutMs);
            if (hud != null && !hud.IsDisposed && hud.Visible) hud.UpdateTimeoutProgress(remaining);
            if (remaining > 0) return;
            if (stateMachine.State == LeaderState.AwaitingResult)
                Apply(stateMachine.CompleteRequest(false, "Bridge не вернул результат за отведённое время."));
            else Apply(stateMachine.Timeout());
        }

        private void StartDeadline(int milliseconds)
        {
            timeoutMs = Math.Max(250, milliseconds);
            timeoutStartUtc = DateTime.UtcNow;
            deadlineUtc = timeoutStartUtc.AddMilliseconds(timeoutMs);
            progress.Start();
        }

        private int TimeoutFor(LeaderState state)
        {
            switch (state)
            {
                case LeaderState.Prefix: return behaviorProfile.Timeouts.PrefixMs;
                case LeaderState.Search: return behaviorProfile.Timeouts.SearchMs;
                case LeaderState.SwitchingModule: return behaviorProfile.Timeouts.ModuleSwitchMs;
                case LeaderState.AwaitingConfirmation: return behaviorProfile.Timeouts.ConfirmationMs;
                case LeaderState.AwaitingResult: return behaviorProfile.Timeouts.ResultMs;
                default: return behaviorProfile.Timeouts.RootMs;
            }
        }

        private void UpdateHud(string moduleLabel = null, string moduleId = null)
        {
            if (hud == null || hud.IsDisposed || !hud.Visible) return;
            List<LeaderSequenceItem> candidates = RankedCandidates();
            string label = moduleLabel ?? ModuleLabel();
            string id = moduleId ?? activeModule?.ID ?? currentContext?.ModuleId ?? string.Empty;
            if (stateMachine.State == LeaderState.Search)
                hud.SetSearchMode(stateMachine.SearchQuery, candidates, label, id, IsBridgeReady(), currentContext?.SelectionCount ?? -1,
                    stateMachine.Prefix);
            else
                hud.UpdateState(string.Empty, candidates, stateMachine.Sticky, label, id, IsBridgeReady(), currentContext?.SelectionCount ?? -1,
                    stateMachine.Prefix);
        }

        private void ShowConfirmation(SequenceDefinition definition)
        {
            if (TryGetItem(definition, out LeaderSequenceItem item) && hud != null && !hud.IsDisposed)
                hud.SetConfirmation(item, ModuleLabel(), activeModule?.ID ?? currentContext?.ModuleId ?? string.Empty);
        }

        private List<LeaderSequenceItem> RankedCandidates()
        {
            string moduleId = activeModule?.ID ?? currentContext?.ModuleId;
            List<LeaderSequenceItem> items = stateMachine.CurrentCandidates()
                .Select(definition => TryGetItem(definition, out LeaderSequenceItem item) ? item : null)
                .Where(item => item != null && string.Equals(ContextGuardEvaluator.NormalizeModule(item.ModuleID),
                    ContextGuardEvaluator.NormalizeModule(moduleId), StringComparison.OrdinalIgnoreCase)).ToList();
            return AdaptiveLeaderPolicy.Rank(items, currentContext, usageStore, moduleId, true);
        }

        private bool TryGetItem(SequenceDefinition definition, out LeaderSequenceItem item)
        {
            item = null;
            return definition != null && itemsById.TryGetValue(definition.Id, out item);
        }

        private string ModuleLabel() => activeModule?.Label ?? currentContext?.ModuleLabel ?? currentContext?.ModuleId ?? "NX context unknown";

        private bool IsBridgeReady()
        {
            if (currentContext == null || !currentContext.IsFresh) return false;
            if (string.Equals(currentContext.LastResult, "fallback", StringComparison.OrdinalIgnoreCase)) return false;
            return string.Equals(currentContext.Status, "running", StringComparison.OrdinalIgnoreCase);
        }

        private void FinishVisual()
        {
            hudDelay.Stop();
            if (!stateMachine.IsCapturing)
            {
                progress.Stop(); deadlineUtc = DateTime.MaxValue;
                if (hud != null && !hud.IsDisposed) hud.DismissHud();
            }
            else
            {
                if (stateMachine.State == LeaderState.Root && activeModule != null) Apply(stateMachine.InputToken(activeModule.LeaderPrefix));
                StartDeadline(behaviorProfile.Timeouts.PrefixMs);
                UpdateHud();
            }
            Interlocked.Exchange(ref captureFlag, stateMachine.IsCapturing ? 1 : 0);
        }

        private static char MapKey(uint value)
        {
            if (value >= 0x41 && value <= 0x5A) return (char)value;
            if (value >= 0x30 && value <= 0x39) return (char)value;
            return '\0';
        }

        private static IntPtr GetActiveNxWindow()
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
            eventPump.Dispose();
            hudDelay.Dispose();
            capsLockRestore.Dispose();
            progress.Dispose();
            contextWatch.Dispose();
        }

        private sealed class HotkeyMessageWindow : NativeWindow
        {
            private readonly LeaderKeyEngine owner;

            public HotkeyMessageWindow(LeaderKeyEngine value)
            {
                owner = value;
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == WM_HOTKEY && message.WParam.ToInt32() == HOTKEY_TRIGGER_ID)
                {
                    owner.OnTriggerHotkey();
                    return;
                }
                base.WndProc(ref message);
            }
        }
    }
}
