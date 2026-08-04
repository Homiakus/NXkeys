from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, content: str) -> None:
    (ROOT / path).write_text(content, encoding="utf-8", newline="\n")


def replace_once(path: str, old: str, new: str) -> None:
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected one match, found {count}: {old[:180]!r}")
    write(path, text.replace(old, new, 1))


def append_once(path: str, marker: str, content: str) -> None:
    text = read(path)
    if marker in text:
        return
    write(path, text.rstrip() + "\n\n" + content.rstrip() + "\n")


# ---------------------------------------------------------------------------
# Shared design system.
# ---------------------------------------------------------------------------
theme = ROOT / "NX2512_HotkeyStudio/UI/NxKeysTheme.cs"
if theme.exists():
    raise RuntimeError("NxKeysTheme.cs already exists")
theme.write_text(r'''using System.Drawing;
using System.Windows.Forms;

namespace NX2512_HotkeyStudio.UI
{
    public static class NxKeysTheme
    {
        public static bool HighContrast => SystemInformation.HighContrast;
        public static Color Background => HighContrast ? SystemColors.Window : Color.FromArgb(13, 17, 23);
        public static Color Sidebar => HighContrast ? SystemColors.Control : Color.FromArgb(10, 13, 18);
        public static Color Surface => HighContrast ? SystemColors.Control : Color.FromArgb(22, 27, 34);
        public static Color Raised => HighContrast ? SystemColors.ControlLight : Color.FromArgb(33, 38, 45);
        public static Color Border => HighContrast ? SystemColors.WindowText : Color.FromArgb(48, 54, 61);
        public static Color Text => HighContrast ? SystemColors.WindowText : Color.FromArgb(240, 246, 252);
        public static Color Muted => HighContrast ? SystemColors.GrayText : Color.FromArgb(154, 166, 179);
        public static Color Accent => HighContrast ? SystemColors.Highlight : Color.FromArgb(56, 189, 248);
        public static Color Success => HighContrast ? SystemColors.Highlight : Color.FromArgb(16, 185, 129);
        public static Color Warning => HighContrast ? SystemColors.Highlight : Color.FromArgb(245, 158, 11);
        public static Color Danger => HighContrast ? SystemColors.Highlight : Color.FromArgb(239, 68, 68);

        public const int SidebarWidth = 248;
        public const int HeaderHeight = 88;
        public const int FooterHeight = 38;
        public const int ContentPadding = 20;

        public static void ApplyButton(Button button, bool primary = false)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = HighContrast ? 2 : 1;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.BackColor = primary ? Accent : Raised;
            button.ForeColor = primary && !HighContrast ? Background : Text;
            button.UseVisualStyleBackColor = false;
            button.AccessibleName = button.Text;
        }

        public static void ApplyInput(Control control)
        {
            control.BackColor = Raised;
            control.ForeColor = Text;
        }
    }
}
''', encoding="utf-8", newline="\n")

# ---------------------------------------------------------------------------
# Safe draft editor with undo/redo and human-readable diff.
# ---------------------------------------------------------------------------
draft = ROOT / "NX2512_HotkeyStudio/Services/ProfileDraftSession.cs"
if draft.exists():
    raise RuntimeError("ProfileDraftSession.cs already exists")
draft.write_text(r'''using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class ProfileDraftSession
    {
        private sealed class Snapshot
        {
            public string Label { get; }
            public string Json { get; }
            public Snapshot(string label, string json) { Label = label ?? string.Empty; Json = json ?? string.Empty; }
        }

        private readonly Stack<Snapshot> undo = new Stack<Snapshot>();
        private readonly Stack<Snapshot> redo = new Stack<Snapshot>();
        private string acceptedJson;

        public Config Draft { get; private set; }
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public bool IsDirty => !string.Equals(Serialize(Draft), acceptedJson, StringComparison.Ordinal);
        public string UndoLabel => CanUndo ? undo.Peek().Label : string.Empty;
        public string RedoLabel => CanRedo ? redo.Peek().Label : string.Empty;

        public ProfileDraftSession(Config source)
        {
            Draft = Clone(source ?? throw new ArgumentNullException(nameof(source)));
            acceptedJson = Serialize(Draft);
        }

        public bool CaptureMutation(string label, Action<Config> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            string before = Serialize(Draft);
            try
            {
                mutation(Draft);
                Draft.ApplyDefaults();
            }
            catch
            {
                Draft = Deserialize(before);
                throw;
            }
            string after = Serialize(Draft);
            if (string.Equals(before, after, StringComparison.Ordinal)) return false;
            undo.Push(new Snapshot(label, before));
            while (undo.Count > 50)
            {
                Snapshot[] retained = undo.Reverse().Skip(1).ToArray();
                undo.Clear();
                foreach (Snapshot snapshot in retained) undo.Push(snapshot);
            }
            redo.Clear();
            return true;
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            Snapshot previous = undo.Pop();
            redo.Push(new Snapshot(previous.Label, Serialize(Draft)));
            Draft = Deserialize(previous.Json);
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            Snapshot next = redo.Pop();
            undo.Push(new Snapshot(next.Label, Serialize(Draft)));
            Draft = Deserialize(next.Json);
            return true;
        }

        public void AcceptSavedState()
        {
            acceptedJson = Serialize(Draft);
            undo.Clear();
            redo.Clear();
        }

        public string BuildDiff(int maximumLines = 120)
        {
            string[] before = acceptedJson.Replace("\r\n", "\n").Split('\n');
            string[] after = Serialize(Draft).Replace("\r\n", "\n").Split('\n');
            var builder = new StringBuilder();
            builder.AppendLine("Профиль: " + (Draft.Profile?.Name ?? string.Empty));
            builder.AppendLine("Baseline SHA-256: " + Digest(acceptedJson));
            builder.AppendLine("Draft SHA-256:    " + Digest(Serialize(Draft)));
            builder.AppendLine("Модулей: " + (Draft.Modules?.Count ?? 0));
            builder.AppendLine("Команд runtime: " + (Draft.LeaderKey?.Sequences?.Count ?? 0));
            builder.AppendLine();

            int emitted = 0;
            int count = Math.Max(before.Length, after.Length);
            for (int index = 0; index < count && emitted < maximumLines; index++)
            {
                string left = index < before.Length ? before[index] : string.Empty;
                string right = index < after.Length ? after[index] : string.Empty;
                if (string.Equals(left, right, StringComparison.Ordinal)) continue;
                if (left.Length > 0) { builder.AppendLine("- " + left); emitted++; }
                if (right.Length > 0 && emitted < maximumLines) { builder.AppendLine("+ " + right); emitted++; }
            }
            if (emitted == 0) builder.AppendLine("Изменений нет.");
            else if (emitted >= maximumLines) builder.AppendLine("… diff сокращён; показано до " + maximumLines + " строк.");
            return builder.ToString();
        }

        private static Config Clone(Config source) => Deserialize(Serialize(source));

        private static Config Deserialize(string json)
        {
            Config result = JsonSerializer.Deserialize<Config>(json, ReadOptions()) ?? new Config();
            result.ApplyDefaults();
            return result;
        }

        private static string Serialize(Config value) => JsonSerializer.Serialize(value, WriteOptions());

        private static JsonSerializerOptions ReadOptions() => new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private static JsonSerializerOptions WriteOptions() => new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private static string Digest(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)))
                .ToLowerInvariant();
    }
}
''', encoding="utf-8", newline="\n")

# ---------------------------------------------------------------------------
# Unified diagnostics and settings pages as a partial form.
# ---------------------------------------------------------------------------
unified = ROOT / "NX2512_HotkeyStudio/UI/HotkeyStudioForm.UnifiedShell.cs"
if unified.exists():
    raise RuntimeError("HotkeyStudioForm.UnifiedShell.cs already exists")
unified.write_text(r'''using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.Services;

namespace NX2512_HotkeyStudio.UI
{
    public sealed partial class HotkeyStudioForm
    {
        private readonly Label diagnosticBridge = new Label();
        private readonly Label diagnosticPackage = new Label();
        private readonly Label diagnosticQueue = new Label();
        private readonly RichTextBox diagnosticDetails = new RichTextBox();
        private readonly ComboBox settingsTrigger = new ComboBox();
        private readonly NumericUpDown settingsFirstTimeout = new NumericUpDown();
        private readonly NumericUpDown settingsNextTimeout = new NumericUpDown();
        private readonly NumericUpDown settingsHudDelay = new NumericUpDown();
        private readonly NumericUpDown settingsHudOpacity = new NumericUpDown();
        private readonly CheckBox settingsNxOnly = new CheckBox();
        private readonly CheckBox settingsSticky = new CheckBox();
        private NxKeysHealthReport latestHealth;

        private Control BuildDiagnosticsPage()
        {
            Panel page = CreatePage();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            metrics.Controls.Add(DiagnosticCard("NX BRIDGE", diagnosticBridge), 0, 0);
            metrics.Controls.Add(DiagnosticCard("MANAGED PACKAGE", diagnosticPackage), 1, 0);
            metrics.Controls.Add(DiagnosticCard("QUEUE", diagnosticQueue), 2, 0);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            Button refresh = CreateActionButton("Обновить", accent);
            refresh.Click += async (_, _) => await RefreshDiagnosticsAsync();
            Button copy = CreateActionButton("Копировать отчёт", text);
            copy.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(diagnosticDetails.Text)) Clipboard.SetText(diagnosticDetails.Text); };
            Button logs = CreateActionButton("Открыть журнал", text);
            logs.Click += (_, _) => OpenDiagnosticLog();
            actions.Controls.Add(refresh);
            actions.Controls.Add(copy);
            actions.Controls.Add(logs);

            diagnosticDetails.Dock = DockStyle.Fill;
            diagnosticDetails.ReadOnly = true;
            diagnosticDetails.BackColor = raised;
            diagnosticDetails.ForeColor = text;
            diagnosticDetails.BorderStyle = BorderStyle.FixedSingle;
            diagnosticDetails.Font = new Font("Consolas", 9.3f);
            diagnosticDetails.AccessibleName = "Подробный диагностический отчёт NXKeys";

            layout.Controls.Add(metrics, 0, 0);
            layout.Controls.Add(actions, 0, 1);
            layout.Controls.Add(diagnosticDetails, 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private Panel DiagnosticCard(string caption, Label value)
        {
            Panel card = Card();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(5);
            Label titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = caption,
                ForeColor = muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Padding = new Padding(14, 10, 0, 0)
            };
            value.Dock = DockStyle.Fill;
            value.ForeColor = text;
            value.Font = new Font("Segoe UI Semibold", 15f);
            value.Padding = new Padding(14, 6, 0, 0);
            card.Controls.Add(value);
            card.Controls.Add(titleLabel);
            return card;
        }

        private async Task RefreshDiagnosticsAsync()
        {
            status.Text = "Проверка NXKeys…";
            UseWaitCursor = true;
            try
            {
                NxTransportReadResult<NxBridgeContext> contextRead = NxCommandBridgeClient.ReadContextDetailed();
                NxKeysHealthReport health = await Task.Run(() => NxKeysHealthService.Check(config));
                latestHealth = health;

                diagnosticBridge.Text = contextRead.IsSuccess
                    ? (contextRead.Value.IsFresh ? "ONLINE" : "STALE") + " · " + contextRead.Value.SecurityStatus
                    : contextRead.Status.ToString().ToUpperInvariant();
                diagnosticBridge.ForeColor = contextRead.IsSuccess && contextRead.Value.IsFresh ? success : danger;
                diagnosticPackage.Text = health.ManagedPackageOk ? "VALID" : "ATTENTION";
                diagnosticPackage.ForeColor = health.ManagedPackageOk ? success : danger;
                diagnosticQueue.Text = $"{health.PendingCount} pending · {health.FailedCount} failed";
                diagnosticQueue.ForeColor = health.FailedCount == 0 ? text : NxKeysTheme.Warning;

                var report = new StringBuilder();
                report.AppendLine("NXKEYS DIAGNOSTICS");
                report.AppendLine("Generated: " + DateTimeOffset.Now.ToString("O"));
                report.AppendLine("Profile: " + configPath);
                report.AppendLine("Managed root: " + health.ManagedRoot);
                report.AppendLine();
                report.AppendLine("Bridge read: " + contextRead.Status + (string.IsNullOrWhiteSpace(contextRead.Message) ? string.Empty : " — " + contextRead.Message));
                if (contextRead.IsSuccess)
                {
                    NxBridgeContext context = contextRead.Value;
                    report.AppendLine("Bridge status: " + context.Status);
                    report.AppendLine("Security: " + context.SecurityStatus);
                    report.AppendLine("Module: " + context.ModuleId + " / " + context.ModuleLabel);
                    report.AppendLine("Context revision: " + context.Revision);
                    report.AppendLine("Selection: " + context.SelectionCount + " / " + context.SelectionState);
                    report.AppendLine("Last result: " + context.LastResult + " — " + context.LastMessage);
                }
                report.AppendLine();
                report.AppendLine("NX running: " + health.NxRunning);
                foreach (string process in health.NxProcesses) report.AppendLine("  " + process);
                report.AppendLine("MenuScript versions: " + (health.MenuScriptVersionOk ? "OK" : "ERROR"));
                report.AppendLine("Managed package: " + (health.ManagedPackageOk ? "OK" : "ERROR"));
                report.AppendLine("Queue: pending=" + health.PendingCount + ", completed=" + health.CompletedCount + ", failed=" + health.FailedCount);
                foreach (string warning in health.EnvironmentWarnings) report.AppendLine("Environment: " + warning);
                foreach (string missing in health.MissingManagedFiles) report.AppendLine("Missing: " + missing);
                foreach (string mismatch in health.HashMismatches) report.AppendLine("SHA mismatch: " + mismatch);
                foreach (string failure in health.LastFailures) report.AppendLine("Failure: " + failure);
                report.AppendLine();
                report.AppendLine("Recent Bridge log:");
                foreach (string line in health.LastBridgeLogLines) report.AppendLine("  " + line);
                diagnosticDetails.Text = report.ToString();
                status.Text = "Диагностика обновлена";
            }
            catch (Exception exception)
            {
                diagnosticBridge.Text = "ERROR";
                diagnosticBridge.ForeColor = danger;
                diagnosticDetails.Text = "DIAG-UNEXPECTED\r\n" + exception;
                status.Text = "Ошибка диагностики: " + exception.Message;
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void OpenDiagnosticLog()
        {
            string path = latestHealth?.BridgeLogPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show("Журнал Bridge пока не найден.", "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private Control BuildSettingsPage()
        {
            Panel page = CreatePage();
            var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(20) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));

            settingsTrigger.DropDownStyle = ComboBoxStyle.DropDownList;
            settingsTrigger.Items.AddRange(new object[] { "CapsLock", "F12" });
            ConfigureNumber(settingsFirstTimeout, 1000, 60000, 500);
            ConfigureNumber(settingsNextTimeout, 1000, 60000, 500);
            ConfigureNumber(settingsHudDelay, 50, 3000, 25);
            ConfigureNumber(settingsHudOpacity, 50, 100, 5);
            settingsNxOnly.Text = "Перехватывать Leader только при активном NX";
            settingsSticky.Text = "Double tap включает sticky mode";
            settingsNxOnly.AutoSize = settingsSticky.AutoSize = true;
            settingsNxOnly.ForeColor = settingsSticky.ForeColor = text;

            AddSettingRow(root, 0, "Клавиша Leader", settingsTrigger);
            AddSettingRow(root, 1, "Первый timeout, мс", settingsFirstTimeout);
            AddSettingRow(root, 2, "Следующий timeout, мс", settingsNextTimeout);
            AddSettingRow(root, 3, "Задержка HUD, мс", settingsHudDelay);
            AddSettingRow(root, 4, "Прозрачность HUD, %", settingsHudOpacity);
            AddSettingRow(root, 5, "Контекст", settingsNxOnly);
            AddSettingRow(root, 6, "Sticky mode", settingsSticky);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            Button apply = CreateActionButton("Применить в draft", accent);
            apply.Click += (_, _) => ApplySettingsToDraft();
            Button save = CreateActionButton("Сохранить профиль", success);
            save.Click += (_, _) => SaveConfig();
            buttons.Controls.Add(apply);
            buttons.Controls.Add(save);
            root.Controls.Add(buttons, 1, 7);

            Label note = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                MaximumSize = new Size(660, 0),
                Text = "Изменения попадают в draft и поддерживают Ctrl+Z / Ctrl+Y. Для применения runtime-параметров к уже запущенному Leader перезапустите Leader после сохранения.",
                ForeColor = muted,
                Padding = new Padding(0, 14, 0, 0)
            };
            root.SetColumnSpan(note, 2);
            root.Controls.Add(note, 0, 8);
            page.Controls.Add(root);
            RefreshSettingsControls();
            return page;
        }

        private void ConfigureNumber(NumericUpDown number, decimal minimum, decimal maximum, decimal increment)
        {
            number.Minimum = minimum;
            number.Maximum = maximum;
            number.Increment = increment;
            number.Width = 220;
            NxKeysTheme.ApplyInput(number);
        }

        private void AddSettingRow(TableLayoutPanel table, int row, string caption, Control editor)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            table.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                ForeColor = muted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);
            editor.Dock = editor is CheckBox ? DockStyle.Left : DockStyle.Fill;
            editor.AccessibleName = caption;
            table.Controls.Add(editor, 1, row);
        }

        private void RefreshSettingsControls()
        {
            LeaderKeyConfig leader = config.LeaderKey;
            settingsTrigger.SelectedItem = leader.TriggerKey;
            if (settingsTrigger.SelectedIndex < 0) settingsTrigger.SelectedIndex = 0;
            settingsFirstTimeout.Value = ClampNumber(leader.FirstKeyTimeoutMs, settingsFirstTimeout);
            settingsNextTimeout.Value = ClampNumber(leader.NextKeyTimeoutMs, settingsNextTimeout);
            settingsHudDelay.Value = ClampNumber(leader.HudDelayMs, settingsHudDelay);
            settingsHudOpacity.Value = ClampNumber((decimal)(leader.HudOpacity * 100.0), settingsHudOpacity);
            settingsNxOnly.Checked = leader.HookOnlyWhenNXActive;
            settingsSticky.Checked = leader.StickyModeOnDoubleTap;
        }

        private static decimal ClampNumber(decimal value, NumericUpDown control) =>
            Math.Max(control.Minimum, Math.Min(control.Maximum, value));

        private void ApplySettingsToDraft()
        {
            bool changed = draftSession.CaptureMutation("Настройки Leader", draft =>
            {
                draft.LeaderKey.TriggerKey = Convert.ToString(settingsTrigger.SelectedItem) ?? "CapsLock";
                draft.LeaderKey.FirstKeyTimeoutMs = (int)settingsFirstTimeout.Value;
                draft.LeaderKey.NextKeyTimeoutMs = (int)settingsNextTimeout.Value;
                draft.LeaderKey.HudDelayMs = (int)settingsHudDelay.Value;
                draft.LeaderKey.HudOpacity = (double)settingsHudOpacity.Value / 100.0;
                draft.LeaderKey.HookOnlyWhenNXActive = settingsNxOnly.Checked;
                draft.LeaderKey.StickyModeOnDoubleTap = settingsSticky.Checked;
            });
            config = draftSession.Draft;
            if (changed) MarkDirty();
            UpdateHistoryButtons();
            status.Text = changed ? "Настройки добавлены в draft" : "Настройки не изменились";
        }

        private void ShowDraftDiff()
        {
            using var dialog = new Form
            {
                Text = "NXKeys — изменения профиля",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(920, 680),
                MinimumSize = new Size(700, 480),
                BackColor = background,
                ForeColor = text
            };
            var box = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = raised,
                ForeColor = text,
                Font = new Font("Consolas", 9.2f),
                Text = draftSession.BuildDiff()
            };
            dialog.Controls.Add(box);
            dialog.ShowDialog(this);
        }

        private void UndoDraft()
        {
            if (!draftSession.Undo()) return;
            RestoreDraftAfterHistory("Изменение отменено");
        }

        private void RedoDraft()
        {
            if (!draftSession.Redo()) return;
            RestoreDraftAfterHistory("Изменение возвращено");
        }

        private void RestoreDraftAfterHistory(string message)
        {
            config = draftSession.Draft;
            dirty = draftSession.IsDirty;
            RefreshBasic();
            RefreshModules();
            RefreshSettingsControls();
            RefreshContext();
            UpdateHistoryButtons();
            status.Text = message;
        }

        private void UpdateHistoryButtons()
        {
            if (undoButton == null || redoButton == null) return;
            undoButton.Enabled = draftSession.CanUndo;
            redoButton.Enabled = draftSession.CanRedo;
            undoButton.Text = draftSession.CanUndo ? "Отменить" : "Отменить";
            redoButton.Text = draftSession.CanRedo ? "Повторить" : "Повторить";
        }

        private void UnifiedShellKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (!eventArgs.Control) return;
            if (eventArgs.KeyCode == Keys.Z)
            {
                UndoDraft();
                eventArgs.SuppressKeyPress = true;
            }
            else if (eventArgs.KeyCode == Keys.Y)
            {
                RedoDraft();
                eventArgs.SuppressKeyPress = true;
            }
            else if (eventArgs.KeyCode == Keys.S)
            {
                SaveConfig();
                eventArgs.SuppressKeyPress = true;
            }
        }
    }
}
''', encoding="utf-8", newline="\n")

# ---------------------------------------------------------------------------
# Convert the existing form to partial, draft-backed, unified navigation.
# ---------------------------------------------------------------------------
form = "NX2512_HotkeyStudio/UI/HotkeyStudioForm.cs"
replace_once(form, "public sealed class HotkeyStudioForm : Form", "public sealed partial class HotkeyStudioForm : Form")
replace_once(
    form,
    """        private readonly Color background = Color.FromArgb(13, 17, 23);
        private readonly Color surface = Color.FromArgb(22, 27, 34);
        private readonly Color raised = Color.FromArgb(33, 38, 45);
        private readonly Color border = Color.FromArgb(48, 54, 61);
        private readonly Color text = Color.FromArgb(240, 246, 252);
        private readonly Color muted = Color.FromArgb(139, 148, 158);
        private readonly Color accent = Color.FromArgb(56, 189, 248);
        private readonly Color success = Color.FromArgb(16, 185, 129);
        private readonly Color danger = Color.FromArgb(239, 68, 68);
""",
    """        private readonly Color background = NxKeysTheme.Background;
        private readonly Color surface = NxKeysTheme.Surface;
        private readonly Color raised = NxKeysTheme.Raised;
        private readonly Color border = NxKeysTheme.Border;
        private readonly Color text = NxKeysTheme.Text;
        private readonly Color muted = NxKeysTheme.Muted;
        private readonly Color accent = NxKeysTheme.Accent;
        private readonly Color success = NxKeysTheme.Success;
        private readonly Color danger = NxKeysTheme.Danger;
""",
)
replace_once(
    form,
    """        private Config config;
        private readonly string configPath;
""",
    """        private Config config;
        private readonly ProfileDraftSession draftSession;
        private readonly string configPath;
""",
)
replace_once(
    form,
    """        private readonly CheckBox dryRun = new CheckBox();
        private bool refreshingModules;
""",
    """        private readonly CheckBox dryRun = new CheckBox();
        private Button undoButton;
        private Button redoButton;
        private bool refreshingModules;
""",
)
replace_once(
    form,
    """            configPath = ResolveConfig(initialConfigPath);
            config = Config.Load(configPath);
            engine = existingEngine;
""",
    """            configPath = ResolveConfig(initialConfigPath);
            draftSession = new ProfileDraftSession(Config.Load(configPath));
            config = draftSession.Draft;
            engine = existingEngine;
""",
)
replace_once(
    form,
    """            AutoScaleMode = AutoScaleMode.Dpi;

            BuildInterface();
""",
    """            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            KeyDown += UnifiedShellKeyDown;
            AccessibleName = "NXKeys Control Center";
            AccessibleDescription = "Единый центр команд, профиля, установки и диагностики Siemens NX.";

            BuildInterface();
""",
)
replace_once(form, "root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));", "root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, NxKeysTheme.SidebarWidth));")
replace_once(form, "Panel sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 13, 18), Padding = new Padding(14) };", "Panel sidebar = new Panel { Dock = DockStyle.Fill, BackColor = NxKeysTheme.Sidebar, Padding = new Padding(14) };")
replace_once(form, "Height = 360,", "Height = 500,")
replace_once(
    form,
    """            string[] names = { "Обзор", "Базовые сочетания", "Модульные команды", "NX / Bridge", "Развёртывание", "Backups / Profile" };
""",
    """            string[] names = { "Главная", "Базовые сочетания", "Команды", "Живой контекст NX", "Установка", "Backups / Profile", "Диагностика", "Настройки" };
""",
)
replace_once(form, "workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));", "workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, NxKeysTheme.HeaderHeight));")
replace_once(form, "workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));", "workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, NxKeysTheme.FooterHeight));")
replace_once(form, "content.Padding = new Padding(18);", "content.Padding = new Padding(NxKeysTheme.ContentPadding);")
replace_once(form, "status.BackColor = Color.FromArgb(10, 13, 18);", "status.BackColor = NxKeysTheme.Sidebar;")
replace_once(
    form,
    """            pages.Add(BuildBackupPage());
            foreach (Control page in pages) content.Controls.Add(page);
""",
    """            pages.Add(BuildBackupPage());
            pages.Add(BuildDiagnosticsPage());
            pages.Add(BuildSettingsPage());
            foreach (Control page in pages) content.Controls.Add(page);
""",
)

# Module edit toolbar with global history and diff.
old_toolbar = """            Button save = CreateActionButton("Сохранить профиль", success);
            save.Dock = DockStyle.Right;
            save.Width = 170;
            save.Click += (_, _) => SaveConfig();
            var hint = new Label { Dock = DockStyle.Fill, Text = "В runtime этот выбор выполняется автоматически по контексту NX", ForeColor = muted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 0, 0, 0) };
            bar.Controls.Add(hint);
            bar.Controls.Add(save);
            bar.Controls.Add(moduleBox);
            bar.Controls.Add(label);
"""
new_toolbar = """            Button save = CreateActionButton("Сохранить", success);
            save.Width = 118;
            save.Click += (_, _) => SaveConfig();
            Button diff = CreateActionButton("Diff", text);
            diff.Width = 86;
            diff.Click += (_, _) => ShowDraftDiff();
            undoButton = CreateActionButton("Отменить", text);
            undoButton.Width = 104;
            undoButton.Click += (_, _) => UndoDraft();
            redoButton = CreateActionButton("Повторить", text);
            redoButton.Width = 104;
            redoButton.Click += (_, _) => RedoDraft();
            var history = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 440,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            history.Controls.Add(save);
            history.Controls.Add(diff);
            history.Controls.Add(redoButton);
            history.Controls.Add(undoButton);
            var hint = new Label { Dock = DockStyle.Fill, Text = "Изменения сохраняются в draft · Ctrl+Z / Ctrl+Y · Ctrl+S", ForeColor = muted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 0, 0, 0) };
            bar.Controls.Add(hint);
            bar.Controls.Add(history);
            bar.Controls.Add(moduleBox);
            bar.Controls.Add(label);
            UpdateHistoryButtons();
"""
replace_once(form, old_toolbar, new_toolbar)

# Draft-backed grid mutation.
old_persist = """            int order = 1;
            foreach (DataGridViewRow row in moduleGrid.Rows)
            {
                if (row.IsNewRow || row.Tag is not ModuleCommand command) continue;                command.Enabled = ReadBool(row, "Enabled");
                    EditableCommandPathPolicy.ApplyEditedPath(command, ReadText(row, "Path"), ReadText(row, "PathLabels"));
                    command.IconHint = ReadText(row, "Icon").Trim();
                command.DisplayOrder = order++;
                command.Command ??= new CommandRef();
                command.Command.Name = ReadText(row, "CommandName").Trim();
                command.Command.ID = ReadText(row, "ButtonId").Trim();
                command.RequiresSelection = ReadBool(row, "RequiresSelection");
                command.ConfirmBeforeExecute = ReadBool(row, "Confirm");
                command.Notes = ReadText(row, "Notes").Trim();
            }
            config.LeaderKey.RebuildFromModules(config.Modules);
            RefreshModulePreview();
            MarkDirty();
"""
new_persist = """            bool changed = draftSession.CaptureMutation("Редактирование команд " + module.ID, draft =>
            {
                int order = 1;
                foreach (DataGridViewRow row in moduleGrid.Rows)
                {
                    if (row.IsNewRow || row.Tag is not ModuleCommand command) continue;
                    command.Enabled = ReadBool(row, "Enabled");
                    EditableCommandPathPolicy.ApplyEditedPath(command, ReadText(row, "Path"), ReadText(row, "PathLabels"));
                    command.IconHint = ReadText(row, "Icon").Trim();
                    command.DisplayOrder = order++;
                    command.Command ??= new CommandRef();
                    command.Command.Name = ReadText(row, "CommandName").Trim();
                    command.Command.ID = ReadText(row, "ButtonId").Trim();
                    command.RequiresSelection = ReadBool(row, "RequiresSelection");
                    command.ConfirmBeforeExecute = ReadBool(row, "Confirm");
                    command.Notes = ReadText(row, "Notes").Trim();
                }
                draft.LeaderKey.RebuildFromModules(draft.Modules);
            });
            config = draftSession.Draft;
            RefreshModulePreview();
            if (changed) MarkDirty();
            UpdateHistoryButtons();
"""
replace_once(form, old_persist, new_persist)

replace_once(
    form,
    """                dirty = false;
                status.Text = "Профиль сохранён";
""",
    """                draftSession.AcceptSavedState();
                dirty = false;
                UpdateHistoryButtons();
                status.Text = "Профиль сохранён атомарно";
""",
)
replace_once(
    form,
    """            dirty = true;
            status.Text = "Есть несохранённые изменения";
""",
    """            dirty = draftSession.IsDirty;
            status.Text = dirty ? "Есть несохранённые изменения в draft" : "Изменений нет";
""",
)
replace_once(
    form,
    """                "Адаптивный контур NXKeys", "Только базовые глобальные сочетания", "Команды активного модуля NX",
                "Контекст Command Bridge", "Транзакционное развёртывание", "Резервные копии и профиль"
""",
    """                "NXKeys Control Center", "Только базовые глобальные сочетания", "Команды активного модуля NX",
                "Живой контекст Command Bridge", "Транзакционная установка", "Резервные копии и профиль",
                "Диагностика системы", "Настройки Leader и HUD"
""",
)
replace_once(
    form,
    """                "План, SHA-256, backup, atomic commit и rollback.",
                "Сохранение схемы v4 и безопасное восстановление."
""",
    """                "План, SHA-256, backup, atomic commit и rollback.",
                "Сохранение schema 6 и безопасное восстановление.",
                "Typed transport state, package hashes, queue и журнал Bridge.",
                "Параметры редактируются через undoable draft и сохраняются атомарно."
""",
)
replace_once(
    form,
    """            title.Text = headings[index];
            subtitle.Text = descriptions[index];
""",
    """            title.Text = headings[index];
            subtitle.Text = descriptions[index];
            if (index == 6) _ = RefreshDiagnosticsAsync();
            if (index == 7) RefreshSettingsControls();
""",
)
replace_once(
    form,
    """            button.FlatAppearance.BorderColor = border;
            return button;
""",
    """            button.FlatAppearance.BorderColor = border;
            button.AccessibleName = caption;
            button.TabStop = true;
            return button;
""",
)
replace_once(
    form,
    """            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = color;
""",
    """            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = color;
            button.AccessibleName = button.Text;
""",
)

# ---------------------------------------------------------------------------
# Compact and accessible HUD using the shared theme.
# ---------------------------------------------------------------------------
hud = "NX2512_HotkeyStudio/UI/LeaderHudForm.cs"
replace_once(
    hud,
    """        private readonly Color backColor = Color.FromArgb(13, 17, 23);
        private readonly Color cardColor = Color.FromArgb(22, 31, 40);
        private readonly Color cardHighlightColor = Color.FromArgb(28, 42, 54);
        private readonly Color borderColor = Color.FromArgb(55, 70, 84);
        private readonly Color textColor = Color.FromArgb(240, 246, 252);
        private readonly Color mutedColor = Color.FromArgb(154, 166, 179);
        private readonly Color accentColor = Color.FromArgb(56, 189, 248);
        private readonly Color stickyColor = Color.FromArgb(16, 185, 129);
        private readonly Color warningColor = Color.FromArgb(245, 158, 11);
        private readonly Color dangerColor = Color.FromArgb(239, 68, 68);
""",
    """        private const int MaximumRootRows = 10;
        private readonly Color backColor = NxKeysTheme.Background;
        private readonly Color cardColor = NxKeysTheme.Surface;
        private readonly Color cardHighlightColor = NxKeysTheme.Raised;
        private readonly Color borderColor = NxKeysTheme.Border;
        private readonly Color textColor = NxKeysTheme.Text;
        private readonly Color mutedColor = NxKeysTheme.Muted;
        private readonly Color accentColor = NxKeysTheme.Accent;
        private readonly Color stickyColor = NxKeysTheme.Success;
        private readonly Color warningColor = NxKeysTheme.Warning;
        private readonly Color dangerColor = NxKeysTheme.Danger;
""",
)
replace_once(
    hud,
    """        private readonly Timer fadeTimer;
        private double targetOpacity = 0.95;
""",
    """        private readonly Timer fadeTimer;
        private readonly bool reduceMotion;
        private double targetOpacity = 0.95;
        private int hiddenRows;
""",
)
replace_once(
    hud,
    """            Size = new Size(900, 480);
            BackColor = backColor;
""",
    """            Size = new Size(620, 420);
            BackColor = backColor;
""",
)
replace_once(
    hud,
    """            Opacity = 0;
            fadeTimer = new Timer { Interval = 15 };
""",
    """            Opacity = 0;
            AccessibleName = "NXKeys command palette";
            AccessibleDescription = "Контекстное меню команд активного модуля Siemens NX.";
            reduceMotion = NxKeysTheme.HighContrast || !SystemInformation.IsMenuAnimationEnabled;
            fadeTimer = new Timer { Interval = 15 };
""",
)
replace_once(
    hud,
    """            if (!Visible) Show();
            Opacity = targetOpacity;
""",
    """            if (!Visible) Show();
            if (reduceMotion) { fadeTimer.Stop(); Opacity = targetOpacity; }
            else { Opacity = 0; fadeTimer.Start(); }
""",
)
replace_once(
    hud,
    """            int count = Math.Max(1, BuildDisplayRows(commands, currentPrefix).Count);
""",
    """            int allRows = BuildDisplayRows(commands, currentPrefix).Count;
            hiddenRows = Math.Max(0, allRows - MaximumRootRows);
            int count = Math.Max(1, Math.Min(MaximumRootRows, allRows));
""",
)
replace_once(
    hud,
    """            int width = Math.Min(maxWidth, Math.Max(720,
""",
    """            int width = Math.Min(maxWidth, Math.Max(520,
""",
)
replace_once(
    hud,
    """            List<DisplayRow> visible = BuildDisplayRows(commands, currentPrefix);
""",
    """            List<DisplayRow> allRows = BuildDisplayRows(commands, currentPrefix);
            hiddenRows = Math.Max(0, allRows.Count - MaximumRootRows);
            List<DisplayRow> visible = allRows.Take(MaximumRootRows).ToList();
""",
)
replace_once(
    hud,
    """            if (confirmationItem != null || searchFilter != null) return;
""",
    """            if (confirmationItem != null || searchFilter != null) return;
            if (hiddenRows > 0)
            {
                using Font overflowFont = new Font("Segoe UI", 8.3f);
                using SolidBrush overflowBrush = new SolidBrush(warningColor);
                graphics.DrawString("Показано 10 из " + (10 + hiddenRows) + " · Space — поиск",
                    overflowFont, overflowBrush, 18, Height - 32);
                return;
            }
""",
)

# ---------------------------------------------------------------------------
# Control Center becomes a compatibility launcher to the unified shell.
# ---------------------------------------------------------------------------
write("NX2512_ControlCenter/Program.cs", r'''using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NX2512_ControlCenter
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            string baseDirectory = AppContext.BaseDirectory;
            string executable = Path.Combine(baseDirectory, "NX2512_HotkeyStudio.exe");
            if (!File.Exists(executable))
            {
                string parent = Directory.GetParent(baseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName ?? baseDirectory;
                executable = Path.Combine(parent, "NX2512_HotkeyStudio.exe");
            }
            if (!File.Exists(executable))
            {
                MessageBox.Show(
                    "Единый NXKeys Control Center не найден. Переустановите managed package NXKeys.",
                    "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
                return;
            }

            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? baseDirectory
            };
            start.ArgumentList.Add("--gui");
            foreach (string argument in args ?? Array.Empty<string>()) start.ArgumentList.Add(argument);
            Process.Start(start);
        }
    }
}
''')
replace_once(
    "NX2512_ControlCenter/NX2512_ControlCenter.csproj",
    """  <ItemGroup>
    <ProjectReference Include="..\\NX2512_HotkeyStudio\\NX2512_HotkeyStudio.csproj" />
  </ItemGroup>
""",
    """  <ItemGroup>
    <Compile Remove="ControlCenterForm.cs" />
  </ItemGroup>
""",
)

# ---------------------------------------------------------------------------
# Regression tests for draft behavior.
# ---------------------------------------------------------------------------
tests = "NX2512_HotkeyStudio.Tests/Program.cs"
replace_once(
    tests,
    """        VerifyBridgeInbox();

        Console.WriteLine("[OK] Canonical profile editor, Sketch grammar, authenticated IPC and background Bridge inbox regressions.");
""",
    """        VerifyBridgeInbox();
        VerifyProfileDraftSession();

        Console.WriteLine("[OK] Profile draft, Sketch grammar, authenticated IPC and background Bridge inbox regressions.");
""",
)
draft_test = r'''
    private static void VerifyProfileDraftSession()
    {
        string sourceConfig = FindRepositoryFile(Path.Combine("config", "nx2512-pro-hybrid.json"));
        Config source = Config.Load(sourceConfig);
        var session = new ProfileDraftSession(source);
        string initialTrigger = session.Draft.LeaderKey.TriggerKey;
        string changedTrigger = string.Equals(initialTrigger, "F12", StringComparison.OrdinalIgnoreCase) ? "CapsLock" : "F12";

        Assert(!session.IsDirty && !session.CanUndo && !session.CanRedo,
            "A new draft session must start clean.");
        Assert(session.CaptureMutation("Trigger", draft => draft.LeaderKey.TriggerKey = changedTrigger),
            "A real draft mutation must be captured.");
        Assert(session.IsDirty && session.CanUndo && session.Draft.LeaderKey.TriggerKey == changedTrigger,
            "Captured draft mutation must be visible and undoable.");
        Assert(session.Undo() && session.Draft.LeaderKey.TriggerKey == initialTrigger && session.CanRedo,
            "Undo must restore the previous immutable snapshot.");
        Assert(session.Redo() && session.Draft.LeaderKey.TriggerKey == changedTrigger,
            "Redo must restore the changed snapshot.");
        Assert(session.BuildDiff().Contains("Draft SHA-256", StringComparison.Ordinal),
            "Draft diff must contain a reproducible digest.");
        session.AcceptSavedState();
        Assert(!session.IsDirty && !session.CanUndo && !session.CanRedo,
            "Accepting an atomic save must reset draft history.");
        Assert(!session.CaptureMutation("No-op", _ => { }),
            "No-op UI events must not pollute undo history.");
    }

'''
replace_once(tests, "    private static void VerifyBridgeInbox()\n", draft_test + "    private static void VerifyBridgeInbox()\n")

# ---------------------------------------------------------------------------
# Documentation and changelog.
# ---------------------------------------------------------------------------
append_once(
    "docs/ARCHITECTURE.md",
    "## Unified desktop shell",
    """## Unified desktop shell

HotkeyStudio is the canonical desktop shell for runtime control, command editing, live NX context,
deployment, backups, diagnostics and Leader/HUD settings. The former Control Center executable is
kept only as a compatibility launcher that opens the same shell; it no longer contains a second
mutable profile model or duplicate diagnostics UI.

Profile editing uses `ProfileDraftSession`: changes are made against a cloned draft, recorded as
bounded immutable snapshots, support undo/redo and a digest-backed diff, and reach disk only through
the existing atomic `Config.Save` path.

The UI design system is centralized in `NxKeysTheme`, including high-contrast-aware colors,
consistent spacing and accessible button/input styling. HUD root views show at most ten immediate
choices and direct the user to module search for the remainder.
""",
)
append_once(
    "docs/NX_PLUGIN_FRAGILITY_ARCHITECTURE_UI_AUDIT.md",
    "## 20. Статус реализации UI-фазы",
    """## 20. Статус реализации UI-фазы

Реализованы ключевые рекомендации UI-аудита:

- HotkeyStudio стал единым desktop shell; Control Center сохранён как compatibility launcher;
- добавлены страницы typed diagnostics и Leader/HUD settings;
- профиль редактируется через draft session с undo/redo, diff и atomic commit;
- общая тема учитывает Windows high contrast и централизует визуальные tokens;
- HUD ограничен десятью ближайшими действиями, стал компактнее и поддерживает reduced motion;
- добавлены клавиатурные команды Ctrl+Z, Ctrl+Y и Ctrl+S;
- `NXK-FR-020` закрыт: grid больше не считается единственным live source без истории.
""",
)
replace_once(
    "CHANGELOG.md",
    """### Architecture

- добавлены отдельные class libraries `NXKeys.Protocol` и `NXKeys.BridgeCore`;
""",
    """### UI

- HotkeyStudio и Control Center объединены в один canonical desktop shell;
- добавлены typed diagnostics, Leader/HUD settings и compatibility launcher;
- редактор профиля переведён на draft session с undo/redo, diff и atomic save;
- введена общая high-contrast-aware тема и compact HUD с десятью ближайшими действиями;
- добавлены Ctrl+Z, Ctrl+Y и Ctrl+S для работы с профилем.

### Architecture

- добавлены отдельные class libraries `NXKeys.Protocol` и `NXKeys.BridgeCore`;
""",
)

print("Unified desktop UI migration applied successfully.")
