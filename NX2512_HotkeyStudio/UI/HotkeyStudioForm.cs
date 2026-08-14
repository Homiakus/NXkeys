using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.Services;

namespace NX2512_HotkeyStudio.UI
{
    public sealed partial class HotkeyStudioForm : Form
    {
        private readonly Color background = NxKeysTheme.Background;
        private readonly Color surface = NxKeysTheme.Surface;
        private readonly Color raised = NxKeysTheme.Raised;
        private readonly Color border = NxKeysTheme.Border;
        private readonly Color text = NxKeysTheme.Text;
        private readonly Color muted = NxKeysTheme.Muted;
        private readonly Color accent = NxKeysTheme.Accent;
        private readonly Color success = NxKeysTheme.Success;
        private readonly Color danger = NxKeysTheme.Danger;

        private Config config;
        private readonly ProfileDraftSession draftSession;
        private readonly string configPath;
        private LeaderKeyEngine engine;
        private readonly bool externalEngine;
        private ScanResult scanResult;
        private DeploymentPlan deploymentPlan;
        private bool dirty;

        private readonly Panel content = new Panel();
        private readonly Label title = new Label();
        private readonly Label subtitle = new Label();
        private readonly Label status = new Label();
        private readonly List<Button> navigationButtons = new List<Button>();
        private readonly List<Control> pages = new List<Control>();
        private readonly ListView basicList = new ListView();
        private readonly ComboBox moduleBox = new ComboBox();
        private readonly TextBox commandSearchBox = new TextBox();
        private readonly DataGridView moduleGrid = new DataGridView();
        private readonly CommandListPreviewPanel modulePreview = new CommandListPreviewPanel();
        private readonly RichTextBox contextBox = new RichTextBox();
        private readonly RichTextBox deploymentBox = new RichTextBox();
        private readonly ListView backupList = new ListView();
        private readonly Label engineState = new Label();
        private readonly Button engineButton = new Button();
        private readonly CheckBox dryRun = new CheckBox();
        private Button undoButton;
        private Button redoButton;
        private bool refreshingModules;

        public HotkeyStudioForm(string initialConfigPath = null, LeaderKeyEngine existingEngine = null)
        {
            configPath = ResolveConfig(initialConfigPath);
            draftSession = new ProfileDraftSession(Config.Load(configPath));
            config = draftSession.Draft;
            engine = existingEngine;
            externalEngine = existingEngine != null;

            Text = "NXKeys — адаптивные модульные команды NX 2512";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 640);
            Size = new Size(1280, 780);
            BackColor = background;
            ForeColor = text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            KeyDown += UnifiedShellKeyDown;
            AccessibleName = "NXKeys Control Center";
            AccessibleDescription = "Единый центр команд, профиля, установки и диагностики Siemens NX.";

            BuildInterface();
            RefreshAll();
            AttachEngine();
            FormClosing += OnFormClosing;
        }

        private static string ResolveConfig(string requested)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(requested)) candidates.Add(requested);
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nx2512-pro-hybrid.json"));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "nx2512-pro-hybrid.json"));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "config", "nx2512-pro-hybrid.json"));
            foreach (string candidate in candidates)
            {
                string path = Config.ExpandPath(candidate);
                if (File.Exists(path)) return Path.GetFullPath(path);
            }
            throw new FileNotFoundException("Канонический профиль NXKeys не найден.");
        }

        private void BuildInterface()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = background };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, NxKeysTheme.SidebarWidth));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            Panel sidebar = new Panel { Dock = DockStyle.Fill, BackColor = NxKeysTheme.Sidebar, Padding = new Padding(14) };
            root.Controls.Add(sidebar, 0, 0);
            var brand = new Label
            {
                Dock = DockStyle.Top,
                Height = 72,
                Text = "NXKEYS\nADAPTIVE MODULES",
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = accent
            };
            sidebar.Controls.Add(brand);
            var nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 500,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0)
            };
            sidebar.Controls.Add(nav);

            string[] names = { "Обзор и статус", "Команды и маршруты", "Настройки и безопасность" };
            for (int index = 0; index < names.Length; index++)
            {
                int target = index;
                Button button = CreateNavigationButton(names[index]);
                button.Click += (_, _) => ShowPage(target);
                navigationButtons.Add(button);
                nav.Controls.Add(button);
            }

            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = background };
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, NxKeysTheme.HeaderHeight));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, NxKeysTheme.FooterHeight));
            root.Controls.Add(workspace, 1, 0);

            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = surface, Padding = new Padding(24, 14, 24, 10) };
            title.Dock = DockStyle.Top;
            title.Height = 32;
            title.Font = new Font("Segoe UI Semibold", 16f);
            title.ForeColor = text;
            subtitle.Dock = DockStyle.Top;
            subtitle.Height = 28;
            subtitle.ForeColor = muted;
            header.Controls.Add(subtitle);
            header.Controls.Add(title);
            workspace.Controls.Add(header, 0, 0);

            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(NxKeysTheme.ContentPadding);
            content.BackColor = background;
            workspace.Controls.Add(content, 0, 1);

            status.Dock = DockStyle.Fill;
            status.BackColor = NxKeysTheme.Sidebar;
            status.ForeColor = muted;
            status.Padding = new Padding(18, 7, 0, 0);
            workspace.Controls.Add(status, 0, 2);

            pages.Add(BuildOverviewPage());
            pages.Add(BuildModulesPage());
            pages.Add(BuildSettingsPage());
            foreach (Control page in pages) content.Controls.Add(page);
            ShowPage(0);
        }

        private Control BuildOverviewPage()
        {
            var page = CreatePage();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metrics.Controls.Add(Metric("Среда NX", "NX 2512", accent), 0, 0);
            metrics.Controls.Add(Metric("Command Bridge", "ОК (v4)", success), 1, 0);
            metrics.Controls.Add(Metric("Перехват Leader", config.LeaderKey.TriggerKey, accent), 2, 0);
            metrics.Controls.Add(Metric("Команды v8", config.LeaderKey.Sequences.Count.ToString(), Color.FromArgb(245, 158, 11)), 3, 0);

            Panel actionsPanel = Card();
            actionsPanel.Dock = DockStyle.Fill;
            var actionsLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12, 14, 12, 12) };
            
            Button applyToNx = CreateActionButton("Применить в NX", accent);
            applyToNx.Width = 200;
            applyToNx.Height = 42;
            applyToNx.Click += async (_, _) => await ApplyPlanAsync();

            Button repairProfile = CreateActionButton("Восстановить шаблон", text);
            repairProfile.Width = 210;
            repairProfile.Height = 42;
            repairProfile.Click += (_, _) => RestoreFromEmbedded();

            engineButton.Width = 200;
            engineButton.Height = 42;
            StyleButton(engineButton, accent);
            engineButton.Click += (_, _) => ToggleEngine();

            actionsLayout.Controls.Add(applyToNx);
            actionsLayout.Controls.Add(repairProfile);
            actionsLayout.Controls.Add(engineButton);
            actionsPanel.Controls.Add(actionsLayout);

            Panel contextCard = Card();
            contextCard.Dock = DockStyle.Fill;
            contextBox.Dock = DockStyle.Fill;
            contextBox.BackColor = surface;
            contextBox.ForeColor = text;
            contextBox.BorderStyle = BorderStyle.None;
            contextBox.Font = new Font("Consolas", 9.5f);
            contextBox.ReadOnly = true;
            contextCard.Controls.Add(contextBox);

            layout.Controls.Add(metrics, 0, 0);
            layout.Controls.Add(actionsPanel, 0, 1);
            layout.Controls.Add(contextCard, 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private void RestoreFromEmbedded()
        {
            DialogResult confirm = MessageBox.Show(
                "Восстановить канонический профиль NX 2512 v8 из встроенного шаблона?",
                "NXKeys — восстановление", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            config = Config.LoadEmbedded();
            config.Save(configPath);
            draftSession.AcceptSavedState();
            RefreshAll();
            MessageBox.Show("Канонический профиль успешно восстановлен.", "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private Control BuildModulesPage()
        {
            var page = CreatePage();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var bar = new Panel { Dock = DockStyle.Fill };
            var label = new Label { Dock = DockStyle.Left, Width = 140, Text = "Модуль NX:", ForeColor = muted, TextAlign = ContentAlignment.MiddleLeft };
            moduleBox.Dock = DockStyle.Left;
            moduleBox.Width = 240;
            moduleBox.DropDownStyle = ComboBoxStyle.DropDownList;
            moduleBox.BackColor = raised;
            moduleBox.ForeColor = text;
            moduleBox.SelectedIndexChanged += (_, _) => RefreshModuleCommands();

            commandSearchBox.Dock = DockStyle.Left;
            commandSearchBox.Width = 220;
            commandSearchBox.BackColor = raised;
            commandSearchBox.ForeColor = text;
            commandSearchBox.Font = new Font("Segoe UI", 10f);
            commandSearchBox.PlaceholderText = "Поиск команд…";
            commandSearchBox.TextChanged += (_, _) => RefreshModuleCommands();

            Button save = CreateActionButton("Сохранить", success);
            save.Width = 110;
            save.Click += (_, _) => SaveConfig();
            Button diff = CreateActionButton("Diff", text);
            diff.Width = 76;
            diff.Click += (_, _) => ShowDraftDiff();
            undoButton = CreateActionButton("Отменить", text);
            undoButton.Width = 96;
            undoButton.Click += (_, _) => UndoDraft();
            redoButton = CreateActionButton("Повторить", text);
            redoButton.Width = 96;
            redoButton.Click += (_, _) => RedoDraft();
            var history = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 400,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            history.Controls.Add(save);
            history.Controls.Add(diff);
            history.Controls.Add(redoButton);
            history.Controls.Add(undoButton);
            
            bar.Controls.Add(history);
            bar.Controls.Add(commandSearchBox);
            bar.Controls.Add(moduleBox);
            bar.Controls.Add(label);
            UpdateHistoryButtons();

            var split = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2 };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            ConfigureModuleGrid();
            modulePreview.Dock = DockStyle.Fill;
            modulePreview.BorderStyle = BorderStyle.FixedSingle;
            split.Controls.Add(moduleGrid, 0, 0);
            split.Controls.Add(modulePreview, 1, 0);
            layout.Controls.Add(bar, 0, 0);
            layout.Controls.Add(split, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private void ConfigureModuleGrid()
        {
            moduleGrid.Dock = DockStyle.Fill;
            moduleGrid.AllowUserToAddRows = false;
            moduleGrid.AllowUserToDeleteRows = false;
            moduleGrid.MultiSelect = false;
            moduleGrid.RowHeadersVisible = false;
            moduleGrid.AutoGenerateColumns = false;
            moduleGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            moduleGrid.BackgroundColor = surface;
            moduleGrid.BorderStyle = BorderStyle.FixedSingle;
            moduleGrid.GridColor = border;
            moduleGrid.ForeColor = text;
            moduleGrid.DefaultCellStyle.BackColor = surface;
            moduleGrid.DefaultCellStyle.ForeColor = text;
            moduleGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(38, 73, 92);
            moduleGrid.DefaultCellStyle.SelectionForeColor = text;
            moduleGrid.ColumnHeadersDefaultCellStyle.BackColor = raised;
            moduleGrid.ColumnHeadersDefaultCellStyle.ForeColor = text;
            moduleGrid.EnableHeadersVisualStyles = false;
            moduleGrid.Columns.Clear();
            moduleGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "Вкл", Width = 55 });
            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "Мнемоника", Width = 130 });
            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CommandName", HeaderText = "Команда", Width = 200, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ButtonId", HeaderText = "BUTTON ID", Width = 220 });
            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Статус", Width = 180, ReadOnly = true });
            moduleGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Confirm", HeaderText = "Подтверждение", Width = 110 });
            moduleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Примечание", Width = 160 });
            moduleGrid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (moduleGrid.IsCurrentCellDirty) moduleGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            moduleGrid.CellValueChanged += (_, _) => PersistModuleGrid();
            moduleGrid.CellEndEdit += (_, _) => PersistModuleGrid();
        }

        private Control BuildContextPage()
        {
            var page = CreatePage();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            Button refresh = CreateActionButton("Обновить контекст", accent);
            refresh.Click += (_, _) => RefreshContext();
            Button scan = CreateActionButton("Сканировать NX", text);
            scan.Click += async (_, _) => await ScanNxAsync();
            bar.Controls.Add(refresh);
            bar.Controls.Add(scan);
            contextBox.Dock = DockStyle.Fill;
            contextBox.BackColor = raised;
            contextBox.ForeColor = text;
            contextBox.BorderStyle = BorderStyle.None;
            contextBox.Font = new Font("Consolas", 9.5f);
            contextBox.ReadOnly = true;
            layout.Controls.Add(bar, 0, 0);
            layout.Controls.Add(contextBox, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private Control BuildDeploymentPage()
        {
            var page = CreatePage();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            Button plan = CreateActionButton("Сформировать план", accent);
            plan.Click += async (_, _) => await BuildPlanAsync();
            Button apply = CreateActionButton("Применить", success);
            apply.Click += async (_, _) => await ApplyPlanAsync();
            dryRun.Text = "Dry-run";
            dryRun.ForeColor = text;
            dryRun.AutoSize = true;
            dryRun.Padding = new Padding(8, 8, 0, 0);
            dryRun.Checked = config.Deployment.DryRun;
            dryRun.CheckedChanged += (_, _) => { config.Deployment.DryRun = dryRun.Checked; MarkDirty(); };
            bar.Controls.Add(plan);
            bar.Controls.Add(apply);
            bar.Controls.Add(dryRun);
            deploymentBox.Dock = DockStyle.Fill;
            deploymentBox.BackColor = raised;
            deploymentBox.ForeColor = text;
            deploymentBox.BorderStyle = BorderStyle.None;
            deploymentBox.Font = new Font("Consolas", 9.3f);
            deploymentBox.ReadOnly = true;
            layout.Controls.Add(bar, 0, 0);
            layout.Controls.Add(deploymentBox, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private Control BuildBackupPage()
        {
            var page = CreatePage();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            backupList.Dock = DockStyle.Fill;
            StyleList(backupList);
            backupList.Columns.Add("Timestamp", 190);
            backupList.Columns.Add("Profile", 260);
            backupList.Columns.Add("Files", 90);
            backupList.Columns.Add("Folder", 550);
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            Button refresh = CreateActionButton("Обновить", text);
            refresh.Click += (_, _) => RefreshBackups();
            Button restore = CreateActionButton("Восстановить выбранный", Color.FromArgb(245, 158, 11));
            restore.Click += (_, _) => RestoreSelected();
            bar.Controls.Add(refresh);
            bar.Controls.Add(restore);
            Panel profile = Card();
            profile.Dock = DockStyle.Fill;
            var path = new Label { Dock = DockStyle.Fill, Text = configPath, ForeColor = muted, Padding = new Padding(12, 13, 8, 8) };
            Button save = CreateActionButton("Сохранить JSON", success);
            save.Dock = DockStyle.Right;
            save.Width = 170;
            save.Click += (_, _) => SaveConfig();
            profile.Controls.Add(path);
            profile.Controls.Add(save);
            layout.Controls.Add(backupList, 0, 0);
            layout.Controls.Add(bar, 0, 1);
            layout.Controls.Add(profile, 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private void AttachEngine()
        {
            if (engine != null) engine.StatusChanged += EngineStatusChanged;
            RefreshEngineState();
        }

        private void EngineStatusChanged(string value)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(new Action(() => { status.Text = value; RefreshEngineState(); }));
            else { status.Text = value; RefreshEngineState(); }
        }

        private void ToggleEngine()
        {
            try
            {
                if (engine == null)
                {
                    config.ApplyDefaults();
                    engine = new LeaderKeyEngine(config.LeaderKey);
                    engine.StatusChanged += EngineStatusChanged;
                }
                if (engine.IsRunning) engine.Stop(); else engine.Start();
                RefreshEngineState();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshEngineState()
        {
            bool running = engine?.IsRunning == true;
            engineState.Text = running
                ? "Leader Engine работает. Команды автоматически следуют за активным модулем NX."
                : "Leader Engine остановлен.";
            engineButton.Text = running ? "Остановить Leader" : "Запустить Leader";
            StyleButton(engineButton, running ? danger : accent);
        }

        private void RefreshAll()
        {
            config.ApplyDefaults();
            RefreshBasic();
            RefreshModules();
            RefreshContext();
            RefreshBackups();
            status.Text = "Профиль загружен: " + config.Profile.Name;
        }

        private void RefreshBasic()
        {
            basicList.Items.Clear();
            foreach (Binding binding in config.Keyboard.Where(binding => binding.Enabled))
            {
                var item = new ListViewItem(binding.Shortcut);
                item.SubItems.Add(binding.Command?.Name ?? string.Empty);
                item.SubItems.Add(binding.Command?.ID ?? string.Empty);
                item.SubItems.Add(binding.Scope ?? string.Empty);
                item.SubItems.Add(binding.Notes ?? string.Empty);
                basicList.Items.Add(item);
            }
        }

        private void RefreshModules()
        {
            string selected = (moduleBox.SelectedItem as ModuleChoice)?.Id;
            moduleBox.Items.Clear();
            foreach (ModuleConfig module in config.Modules.Where(module => module.Enabled))
                moduleBox.Items.Add(new ModuleChoice(module.ID, module.Label));
            int index = Math.Max(0, moduleBox.Items.Cast<ModuleChoice>().ToList().FindIndex(item => item.Id == selected));
            if (moduleBox.Items.Count > 0) moduleBox.SelectedIndex = index;
            RefreshModuleCommands();
        }

        private void RefreshModuleCommands()
        {
            refreshingModules = true;
            try
            {
                moduleGrid.Rows.Clear();
                ModuleConfig module = ActiveModule();
                if (module == null)
                {
                    modulePreview.SetCommands(string.Empty, string.Empty, Enumerable.Empty<LeaderSequenceItem>());
                    return;
                }

                string filter = commandSearchBox.Text?.Trim() ?? string.Empty;

                foreach (ModuleCommand command in ModuleCommands(module))
                {
                    string pathStr = EditableCommandPathPolicy.FormatPath(EditableCommandPathPolicy.EffectivePath(command));
                    string nameStr = command.Command?.Name ?? string.Empty;
                    string idStr = command.Command?.ID ?? string.Empty;
                    string notesStr = command.Notes ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(filter))
                    {
                        bool match = pathStr.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     nameStr.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     idStr.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     notesStr.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!match) continue;
                    }

                    string statusStr = command.Destructive
                        ? "Опасная: удаление"
                        : (command.RequiresSelection ? "Требуется выбор" : "Готово к выполнению");

                    int rowIndex = moduleGrid.Rows.Add(
                        command.Enabled,
                        pathStr,
                        nameStr,
                        idStr,
                        statusStr,
                        command.ConfirmBeforeExecute || command.Destructive,
                        notesStr);
                    moduleGrid.Rows[rowIndex].Tag = command;
                }
            }
            finally
            {
                refreshingModules = false;
            }
            RefreshModulePreview();
        }

        private void PersistModuleGrid()
        {
            if (refreshingModules) return;
            ModuleConfig module = ActiveModule();
            if (module == null) return;

            bool changed = draftSession.CaptureMutation("Редактирование команд " + module.ID, draft =>
            {
                int order = 1;
                foreach (DataGridViewRow row in moduleGrid.Rows)
                {
                    if (row.IsNewRow || row.Tag is not ModuleCommand command) continue;
                    command.Enabled = ReadBool(row, "Enabled");
                    EditableCommandPathPolicy.ApplyEditedPath(command, ReadText(row, "Path"), ReadText(row, "CommandName"));
                    command.DisplayOrder = order++;
                    command.Command ??= new CommandRef();
                    command.Command.Name = ReadText(row, "CommandName").Trim();
                    command.Command.ID = ReadText(row, "ButtonId").Trim();
                    command.ConfirmBeforeExecute = ReadBool(row, "Confirm");
                    command.Notes = ReadText(row, "Notes").Trim();
                }
                draft.LeaderKey.RebuildFromModules(draft.Modules);
            });
            config = draftSession.Draft;
            RefreshModulePreview();
            if (changed) MarkDirty();
            UpdateHistoryButtons();
        }

        private void RefreshModulePreview()
        {
            ModuleConfig module = ActiveModule();
            if (module == null)
            {
                modulePreview.SetCommands(string.Empty, string.Empty, Enumerable.Empty<LeaderSequenceItem>());
                return;
            }
            config.LeaderKey.RebuildFromModules(config.Modules);
            NxBridgeContext bridgeContext = NxCommandBridgeClient.ReadContext();
            bool bridgeReady = bridgeContext != null &&
                               bridgeContext.IsFresh &&
                               string.Equals(bridgeContext.Status, "running", StringComparison.OrdinalIgnoreCase);
            modulePreview.SetCommands(module.Label, module.ID,
                config.LeaderKey.Sequences.Where(item => string.Equals(item.ModuleID, module.ID, StringComparison.OrdinalIgnoreCase)),
                bridgeReady, bridgeContext?.SelectionCount ?? 0, module.LeaderPrefix);
        }

        private ModuleConfig ActiveModule()
        {
            string moduleId = (moduleBox.SelectedItem as ModuleChoice)?.Id;
            return config.Modules.FirstOrDefault(value => value.Enabled && value.ID == moduleId);
        }

        private static IEnumerable<ModuleCommand> ModuleCommands(ModuleConfig module) =>
            (module?.CommandSets ?? Enumerable.Empty<ModuleCommandSet>())
                .Where(set => set?.Commands != null)
                .SelectMany(set => set.Commands)
                .Where(command => command != null)
                .OrderBy(command => command.DisplayOrder <= 0 ? int.MaxValue : command.DisplayOrder);

        private static string ReadText(DataGridViewRow row, string column) => Convert.ToString(row.Cells[column].Value) ?? string.Empty;

        private static bool ReadBool(DataGridViewRow row, string column)
        {
            object value = row.Cells[column].Value;
            return value is bool boolean ? boolean : bool.TryParse(Convert.ToString(value), out bool parsed) && parsed;
        }

        private void RefreshContext()
        {
            NxBridgeContext bridgeContext = NxCommandBridgeClient.ReadContext();
            ModuleConfig module = AdaptiveModuleResolver.Resolve(config.LeaderKey.RuntimeModules, bridgeContext).Module;
            var snapshot = new
            {
                resolved_module = module == null ? null : new { module.ID, module.Label, module.LeaderPrefix },
                context = bridgeContext,
                bridge_root = NxCommandBridgeClient.BridgeRoot,
                adaptive_keys = config.LeaderKey.Sequences
                    .GroupBy(item => item.ModuleID)
                    .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder)
                        .Select(item => new { item.Path, item.PathLabels, item.IconHint, item.Sequence, command_id = item.Command?.ID, command_name = item.Command?.Name }).ToList())
            };
            contextBox.Text = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task ScanNxAsync()
        {
            await RunBusyAsync("Сканирование NX…", () =>
            {
                scanResult = NxScanner.Scan(config);
                return $"Найдено menu-файлов: {scanResult.MenuFiles.Count}\nКоманд каталога: {scanResult.Catalog.Commands.Count}\nAPI-каталог: {scanResult.DocumentationCatalogDirectory}";
            }, result => contextBox.Text = result);
        }

        private async Task BuildPlanAsync()
        {
            await RunBusyAsync("Формирование плана…", () =>
            {
                scanResult ??= NxScanner.Scan(config);
                deploymentPlan = DeploymentEngine.BuildPlan(config, scanResult.Catalog);
                var builder = new StringBuilder();
                foreach (string line in deploymentPlan.ActionSummary) builder.AppendLine(line);
                builder.AppendLine().AppendLine(deploymentPlan.ResolutionReport);
                return builder.ToString();
            }, result => deploymentBox.Text = result);
        }

        private async Task ApplyPlanAsync()
        {
            if (deploymentPlan == null) await BuildPlanAsync();
            if (deploymentPlan == null) return;
            if (!config.Deployment.DryRun)
            {
                DialogResult confirm = MessageBox.Show(
                    "Применить транзакционный план NXKeys? Siemens NX должен быть закрыт.",
                    "NXKeys deployment", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;
            }
            await RunBusyAsync("Применение плана…", () =>
            {
                bool ok = DeploymentEngine.ApplyPlan(config, deploymentPlan, out string backup, out string error);
                if (!ok) throw new InvalidOperationException(error);
                return config.Deployment.DryRun ? "Dry-run успешно завершён." : "Пакет установлен. Backup: " + backup;
            }, result => deploymentBox.AppendText(Environment.NewLine + result + Environment.NewLine));
        }

        private async Task RunBusyAsync(string message, Func<string> work, Action<string> completed)
        {
            UseWaitCursor = true;
            Enabled = false;
            status.Text = message;
            try
            {
                string result = await Task.Run(work);
                completed(result);
                status.Text = "Готово";
            }
            catch (Exception exception)
            {
                status.Text = "Ошибка: " + exception.Message;
                MessageBox.Show(exception.Message, "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Enabled = true;
                UseWaitCursor = false;
            }
        }

        private void RefreshBackups()
        {
            backupList.Items.Clear();
            foreach (BackupManifest backup in BackupEngine.ListBackups(config.Deployment.BackupRoot))
            {
                var item = new ListViewItem(backup.Timestamp ?? string.Empty) { Tag = backup };
                item.SubItems.Add(backup.ProfileName ?? string.Empty);
                item.SubItems.Add((backup.Entries?.Count ?? 0).ToString());
                item.SubItems.Add(backup.BackupDirectory ?? string.Empty);
                backupList.Items.Add(item);
            }
        }

        private void RestoreSelected()
        {
            if (backupList.SelectedItems.Count == 0) return;
            BackupManifest backup = backupList.SelectedItems[0].Tag as BackupManifest;
            if (backup == null) return;
            RestoreResult result = BackupEngine.RestoreFromManifest(Path.Combine(backup.BackupDirectory, "manifest.json"), false);
            MessageBox.Show(result.Success ? "Восстановление завершено." : result.ErrorMessage, "NXKeys restore");
        }

        private bool SaveConfig()
        {
            try
            {
                moduleGrid.EndEdit();
                PersistModuleGrid();
                EditableCommandPathPolicy.Normalize(config);
                ValidateEditableCommands();
                config.Save(configPath);
                NxCommandBridgeClient.ConfigureSecurity(configPath);
                draftSession.AcceptSavedState();
                dirty = false;
                UpdateHistoryButtons();
                status.Text = "Профиль сохранён атомарно";
                RefreshAll();
                return true;
            }
            catch (Exception exception)
            {
                dirty = draftSession.IsDirty;
                status.Text = "Профиль не сохранён: " + exception.Message;
                MessageBox.Show(exception.Message, "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void ValidateEditableCommands()
        {
            List<string> problems = EditableCommandPathPolicy.Validate(config);
            if (problems.Count == 0) return;
            const int maximumVisible = 60;
            string omitted = problems.Count > maximumVisible
                ? $"\n- … и ещё {problems.Count - maximumVisible} ошибок"
                : string.Empty;
            throw new InvalidOperationException("Профиль не сохранён:\n- " +
                string.Join("\n- ", problems.Take(maximumVisible)) + omitted);
        }

        private void MarkDirty()
        {
            dirty = draftSession.IsDirty;
            status.Text = dirty ? "Есть несохранённые изменения в draft" : "Изменений нет";
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index >= pages.Count) return;
            for (int position = 0; position < pages.Count; position++)
            {
                pages[position].Visible = position == index;
                navigationButtons[position].BackColor = position == index ? accent : raised;
                navigationButtons[position].ForeColor = position == index ? Color.FromArgb(13, 17, 23) : text;
            }
            string[] headings =
            {
                "Обзор и статус NXKeys", "Команды и маршруты модулей", "Настройки Leader и безопасность"
            };
            string[] descriptions =
            {
                "Состояние среды Siemens NX, Command Bridge и управление развертыванием.",
                "Просмотр и редактирование мнемоник, фильтрация по модулям и живое превью.",
                "Параметры клавиши Leader, тайм-ауты, визуальные подсказки и резервные копии."
            };
            title.Text = headings[index];
            subtitle.Text = descriptions[index];
            if (index == 0) RefreshContext();
            if (index == 1) RefreshModuleCommands();
            if (index == 2) { RefreshSettingsControls(); RefreshBackups(); }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            if (dirty)
            {
                DialogResult answer = MessageBox.Show("Сохранить изменения профиля?", "NXKeys", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) { eventArgs.Cancel = true; return; }
                if (answer == DialogResult.Yes && !SaveConfig())
                {
                    eventArgs.Cancel = true;
                    return;
                }
            }
            if (engine != null)
            {
                engine.StatusChanged -= EngineStatusChanged;
                if (!externalEngine) engine.Dispose();
                engine = null;
            }
        }

        private Panel CreatePage() => new Panel { Dock = DockStyle.Fill, BackColor = background, Visible = false };
        private Panel Card() => new Panel { BackColor = surface, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(4) };

        private Panel Metric(string label, string value, Color color)
        {
            var panel = Card();
            panel.Width = 230;
            panel.Height = 112;
            panel.Margin = new Padding(0, 0, 12, 12);
            var valueLabel = new Label { Dock = DockStyle.Top, Height = 54, Text = value, ForeColor = color, Font = new Font("Segoe UI Semibold", 19f), Padding = new Padding(12, 12, 0, 0) };
            var caption = new Label { Dock = DockStyle.Fill, Text = label, ForeColor = muted, Padding = new Padding(12, 2, 0, 0) };
            panel.Controls.Add(caption);
            panel.Controls.Add(valueLabel);
            return panel;
        }

        private Button CreateNavigationButton(string caption)
        {
            var button = new Button { Width = 218, Height = 42, Text = caption, TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, BackColor = raised, ForeColor = text, Margin = new Padding(0, 0, 0, 6) };
            button.FlatAppearance.BorderColor = border;
            button.AccessibleName = caption;
            button.TabStop = true;
            return button;
        }

        private Button CreateActionButton(string caption, Color color)
        {
            var button = new Button { Width = 180, Height = 36, Text = caption, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 4, 8, 4) };
            StyleButton(button, color);
            return button;
        }

        private void StyleButton(Button button, Color color)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = color;
            button.AccessibleName = button.Text;
            button.BackColor = color == text ? raised : color;
            button.ForeColor = color == accent || color == success ? Color.FromArgb(13, 17, 23) : text;
        }

        private void StyleList(ListView list)
        {
            list.View = View.Details;
            list.FullRowSelect = true;
            list.GridLines = false;
            list.HideSelection = false;
            list.BackColor = surface;
            list.ForeColor = text;
            list.BorderStyle = BorderStyle.FixedSingle;
        }

        private RichTextBox ReadOnlyBox() => new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = surface,
            ForeColor = text,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            Font = new Font("Segoe UI", 10.5f),
            Padding = new Padding(12)
        };

        private sealed class ModuleChoice
        {
            public string Id { get; }
            private readonly string label;
            public ModuleChoice(string id, string labelValue) { Id = id; label = labelValue; }
            public override string ToString() => label + " (" + Id + ")";
        }
    }
}
