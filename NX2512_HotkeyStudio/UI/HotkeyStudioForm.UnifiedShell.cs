using System;
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
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(16) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 360));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));

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

            mainLayout.Controls.Add(root, 0, 0);

            var backupGroup = Card();
            backupGroup.Dock = DockStyle.Fill;
            var backupLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            backupLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            backupLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            backupList.Dock = DockStyle.Fill;
            StyleList(backupList);
            backupList.Columns.Clear();
            backupList.Columns.Add("Время создания", 190);
            backupList.Columns.Add("Профиль", 260);
            backupList.Columns.Add("Файлов", 80);
            backupList.Columns.Add("Каталог", 500);

            var backupButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(6) };
            Button refreshBackups = CreateActionButton("Обновить копии", text);
            refreshBackups.Click += (_, _) => RefreshBackups();
            Button restoreBackup = CreateActionButton("Восстановить выбранную", Color.FromArgb(245, 158, 11));
            restoreBackup.Click += (_, _) => RestoreSelected();
            backupButtons.Controls.Add(refreshBackups);
            backupButtons.Controls.Add(restoreBackup);

            backupLayout.Controls.Add(backupList, 0, 0);
            backupLayout.Controls.Add(backupButtons, 0, 1);
            backupGroup.Controls.Add(backupLayout);

            mainLayout.Controls.Add(backupGroup, 0, 1);

            page.Controls.Add(mainLayout);
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
