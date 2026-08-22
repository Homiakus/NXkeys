using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NXKeys.Protocol;
using NXOpen;
using NXOpen.MenuBar;
using NXOpen.UF;

namespace NX2512_CommandBridge
{
    /// <summary>
    /// Модуль B (in-NX): единственный канонический владелец снапшота контекста системы/состояния NX.
    /// Собирает <see cref="NxContextSnapshot"/> из NX (application module, selection, work/display part,
    /// modal), владеет ревизией/метрикой свежести и публикует его в bridge/context.json.
    /// Не исполняет команды (A) и не выбирает команды (C).
    /// </summary>
    public sealed class NxContextMonitor : INxContextProvider
    {
        private readonly Session session;
        private readonly UFSession ufSession;
        private readonly UI ui;
        private readonly Action<string> log;

        private DateTime lastContextWriteUtc = DateTime.MinValue;
        private long contextRevision;
        private string lastContextFingerprint = string.Empty;

        /// <summary>Ревизия контекста (монотонно растёт при изменении семантического fingerprint).</summary>
        public long CurrentRevision => contextRevision;

        /// <summary>Время последней записи context.json (для политики свежести/периодической публикации).</summary>
        public DateTime LastContextWriteUtc => lastContextWriteUtc;

        /// <summary>Последний обработанный request_id — встраивается в снапшот для диагностики.</summary>
        public string LastRequestId { get; set; } = string.Empty;

        /// <summary>Последний результат исполнения — встраивается в снапшот.</summary>
        public string LastResult { get; set; } = string.Empty;

        /// <summary>Последнее сообщение — встраивается в снапшот.</summary>
        public string LastMessage { get; set; } = string.Empty;

        /// <summary>Статус security-гейта — встраивается в снапшот.</summary>
        public string SecurityStatus { get; set; } = "not_initialized";

        /// <summary>Session id security-гейта — встраивается в снапшот.</summary>
        public string SecuritySessionId { get; set; } = string.Empty;

        /// <summary>Profile digest security-гейта — встраивается в снапшот.</summary>
        public string SecurityProfileDigest { get; set; } = string.Empty;

        public event Action ContextChanged;

        public NxContextMonitor(Session session, UFSession ufSession, UI ui, Action<string> log)
        {
            this.session = session;
            this.ufSession = ufSession;
            this.ui = ui;
            this.log = log ?? (_ => { });
        }

        private string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private string BridgeRoot => Path.Combine(LocalAppData, "NXKeys", "bridge");
        public string ContextPath => Path.Combine(BridgeRoot, "context.json");

        public NxContextSnapshot? GetCurrent()
        {
            string applicationId = AskCurrentApplicationId(out int applicationConfidence);
            string moduleId = ModuleIdFromRuntimeContext(applicationId, out int moduleConfidence);
            int selectionCount = AskSelectionSnapshot(out List<string> selectedTypes, out string selectionFingerprint);
            bool workPart = AskWorkPartAvailable();
            bool displayPart = AskDisplayPartAvailable();
            bool modal = IsModalDialogActive();

            var snapshot = new NxContextSnapshot
            {
                SchemaVersion = NxProtocolConstants.SchemaVersion,
                Status = "running",
                ApplicationId = applicationId,
                ModuleId = moduleId,
                ModuleLabel = ModuleLabelFromModule(moduleId),
                SelectionCount = selectionCount,
                SelectionState = selectionCount < 0 ? "unknown" : selectionCount == 0 ? "none" : selectionCount == 1 ? "single" : "multiple",
                SelectedTypes = selectedTypes,
                SelectionFingerprint = selectionFingerprint,
                WorkPartAvailable = workPart,
                DisplayPartAvailable = displayPart,
                ModalDialogActive = modal,
                ActiveCommandId = string.Empty,
                ContextConfidence = Math.Min(applicationConfidence, moduleConfidence),
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                LastRequestId = LastRequestId,
                LastResult = LastResult,
                LastMessage = LastMessage,
                SecurityStatus = SecurityStatus,
                SecuritySessionId = SecuritySessionId,
                SecurityProfileDigest = SecurityProfileDigest
            };

            string fingerprint = snapshot.SemanticFingerprint();
            if (!string.Equals(fingerprint, lastContextFingerprint, StringComparison.Ordinal))
            {
                contextRevision++;
                lastContextFingerprint = fingerprint;
            }
            snapshot.Revision = Math.Max(1, contextRevision);
            return snapshot;
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
                NxContextSnapshot current = GetCurrent();
                return current != null &&
                       string.Equals(current.Status, "running", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(current.SecurityStatus, "authenticated", StringComparison.OrdinalIgnoreCase);
            }
        }

        public void LoadPreviousContextRevision()
        {
            try
            {
                if (!File.Exists(ContextPath)) return;
                using (FileStream stream = new FileStream(ContextPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    NxContextSnapshot previous = JsonSerializer.Deserialize<NxContextSnapshot>(stream, NxProtocolJson.ReadOptions);
                    if (previous == null) return;
                    contextRevision = Math.Max(0, previous.Revision);
                    lastContextFingerprint = previous.SemanticFingerprint();
                }
            }
            catch (Exception ex)
            {
                log("LoadPreviousContextRevision warning: " + ex.Message);
            }
        }

        /// <summary>Пересобрать контекст, обновив встроенные результат/сообщение, и записать в bridge/context.json.</summary>
        public void Refresh(string result, string message)
        {
            try
            {
                if (result != null) LastResult = result;
                if (message != null) LastMessage = message;
                NxContextSnapshot snapshot = GetCurrent();
                WriteJsonAtomic(ContextPath, snapshot);
                lastContextWriteUtc = DateTime.UtcNow;
                ContextChanged?.Invoke();
            }
            catch (Exception ex)
            {
                log("WriteContext failed: " + ex.Message);
            }
        }

        private void WriteJsonAtomic<T>(string path, T value)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(value, NxProtocolJson.WriteOptions);
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }
                File.Move(temporary, path, true);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            }
        }

        private string AskCurrentApplicationId(out int confidence)
        {
            confidence = 100;
            try
            {
                if (ufSession == null)
                {
                    confidence = 30;
                    return "UG_APP_GATEWAY";
                }
                int currentModuleId;
                ufSession.UF.AskApplicationModule(out currentModuleId);
                return ApplicationIdFromUfModule(currentModuleId);
            }
            catch (Exception ex)
            {
                confidence = 20;
                log("AskApplicationModule failed: " + ex.Message);
                return "UG_APP_GATEWAY";
            }
        }

        private int AskSelectionSnapshot(out List<string> selectedTypes, out string selectionFingerprint)
        {
            selectedTypes = new List<string>();
            selectionFingerprint = string.Empty;
            try
            {
                int count = ui.SelectionManager.GetNumSelectedObjects();
                int inspected = Math.Min(count, 64);
                var identities = new List<string>(inspected);
                for (int index = 0; index < inspected; index++)
                {
                    object selected = AskSelectedObject(index);
                    AddSelectedType(selectedTypes, selected);
                    identities.Add(SelectedObjectIdentity(selected, index));
                }
                identities.Sort(StringComparer.Ordinal);
                string material = count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                                  string.Join("|", identities);
                using (SHA256 sha256 = SHA256.Create())
                    selectionFingerprint = Convert.ToHexString(
                        sha256.ComputeHash(Encoding.UTF8.GetBytes(material)));
                return count;
            }
            catch (Exception ex)
            {
                log("AskSelectionSnapshot failed: " + ex.Message);
                selectionFingerprint = string.Empty;
                return -1;
            }
        }

        private string SelectedObjectIdentity(object selected, int index)
        {
            if (selected == null) return "null@" + index;
            Type type = selected.GetType();
            foreach (string propertyName in new[] { "Tag", "JournalIdentifier", "Name" })
            {
                try
                {
                    PropertyInfo property = type.GetProperty(propertyName);
                    object value = property?.GetValue(selected);
                    string text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(text))
                        return (type.FullName ?? type.Name) + "#" + propertyName + "=" + text;
                }
                catch
                {
                    // Identity probing is best effort; NX Tag is used when available.
                }
            }
            return (type.FullName ?? type.Name) + "#index=" + index;
        }

        private object AskSelectedObject(int index)
        {
            object manager = ui?.SelectionManager;
            if (manager == null) return null;
            Type type = manager.GetType();
            foreach (string methodName in new[] { "GetSelectedObject", "GetSelectedTaggedObject" })
            {
                try
                {
                    var method = type.GetMethod(methodName, new[] { typeof(int) });
                    if (method == null) continue;
                    return method.Invoke(manager, new object[] { index });
                }
                catch (Exception ex)
                {
                    Exception targetEx = (ex is TargetInvocationException tie) ? (tie.InnerException ?? tie) : ex;
                    log("Selection object probe failed for method " + methodName + "(" + index + "): " + targetEx.Message);
                }
            }
            return null;
        }

        private void AddSelectedType(List<string> selectedTypes, object selected)
        {
            if (selected == null) return;
            for (Type type = selected.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                string typeName = type.FullName;
                if (!string.IsNullOrWhiteSpace(typeName) && !selectedTypes.Contains(typeName, StringComparer.Ordinal))
                    selectedTypes.Add(typeName);
            }
        }

        private bool AskWorkPartAvailable()
        {
            try { return session?.Parts?.Work != null; }
            catch { return false; }
        }

        private bool AskDisplayPartAvailable()
        {
            try { return session?.Parts?.Display != null; }
            catch { return false; }
        }

        private bool IsModalDialogActive()
        {
            try
            {
                IntPtr mainWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (mainWindow == IntPtr.Zero) return false;
                if (!IsWindowEnabled(mainWindow)) return true;
                IntPtr popup = GetLastActivePopup(mainWindow);
                return popup != IntPtr.Zero && popup != mainWindow && IsWindowVisible(popup);
            }
            catch
            {
                return false;
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetLastActivePopup(IntPtr hWnd);

        private string ApplicationIdFromUfModule(int moduleId)
        {
            string constant = TryMatchUfConstant(moduleId);
            switch (constant)
            {
                case "UF_APP_MODELING": return "UG_APP_MODELING";
                case "UF_APP_DRAFTING": return "UG_APP_DRAFTING";
                case "UF_APP_MANUFACTURING": return "UG_APP_MANUFACTURING";
                case "UF_APP_SFEM": return "UG_APP_SFEM";
                case "UF_APP_DESFEM": return "UG_APP_DESFEM";
                case "UF_APP_SHEETMETAL": return "UG_APP_SHEETMETAL";
                case "UF_APP_ROUTING": return "UG_APP_ROUTING";
                case "UF_APP_STUDIO": return "UG_APP_STUDIO";
                default: return "UG_APP_GATEWAY";
            }
        }

        private static string TryMatchUfConstant(int moduleId)
        {
            try
            {
                foreach (var field in typeof(UFConstants).GetFields())
                {
                    if (!field.Name.StartsWith("UF_APP_", StringComparison.OrdinalIgnoreCase)) continue;
                    object value = field.GetValue(null);
                    if (value is int intValue && intValue == moduleId) return field.Name;
                }
            }
            catch { }
            return string.Empty;
        }

        private static string ModuleIdFromApplication(string applicationId)
            => NxContextNormalization.ModuleIdFromApplication(applicationId);

        private static string ModuleIdFromRuntimeContext(string applicationId, out int confidence)
        {
            if (IsButtonReady("UG_SKETCH_FINISH") || IsButtonReady("UG_SKETCH_LINE"))
            {
                confidence = 60;
                return "sketch";
            }
            confidence = 90;
            return ModuleIdFromApplication(applicationId);
        }

        private static bool IsButtonReady(string commandId)
        {
            try
            {
                MenuButton button = UI.GetUI().MenuBarManager.GetButtonFromName(commandId);
                return button != null &&
                       button.ButtonAvailability != MenuButton.AvailabilityStatus.Unavailable &&
                       button.ButtonSensitivity != MenuButton.SensitivityStatus.Insensitive;
            }
            catch
            {
                return false;
            }
        }

        private static string ModuleLabelFromModule(string moduleId)
            => NxContextNormalization.ModuleLabelFromModule(moduleId);

        /// <summary>Является ли модуль разделяемым (selection_object / inspect_view / reuse).</summary>
        public static bool IsSharedModule(string moduleId)
            => NxContextNormalization.IsSharedModule(moduleId);

        /// <summary>Каноническая нормализация module id (используется admission/циклами выбора).</summary>
        public static string NormalizeModule(string moduleId)
            => NxContextNormalization.NormalizeModule(moduleId);
    }
}
