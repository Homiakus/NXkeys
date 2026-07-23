using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.Services;
using Binding = NX2512_HotkeyStudio.Models.Binding;

namespace NX2512_HotkeyStudio.UI
{
    public sealed class HotkeyStudioForm : Form
    {
        private Config config;
        private ScanResult scanResult;
        private DeploymentPlan currentPlan;
        private string configFilePath;
        private int currentStepIndex = 0;
        private Task<ScanResult> scanTask;
        private bool hasUnsavedChanges;

        // Modern GitHub/VS Code Dark Design Palette
        private readonly Color backColor = Color.FromArgb(13, 17, 23);       // #0D1117 Main Background
        private readonly Color surfaceColor = Color.FromArgb(22, 27, 34);     // #161B22 Panel/Card Surface
        private readonly Color elevatedColor = Color.FromArgb(33, 38, 45);    // #21262D Input/Row Surface
        private readonly Color borderColor = Color.FromArgb(48, 54, 61);      // #30363D Subtle Borders
        private readonly Color textColor = Color.FromArgb(240, 246, 252);     // #F0F6FC Primary Text
        private readonly Color mutedColor = Color.FromArgb(139, 148, 158);    // #8B949E Secondary Text
        private readonly Color accentColor = Color.FromArgb(56, 189, 248);    // #38BDF8 Sky Cyan Accent
        private readonly Color accentDark = Color.FromArgb(14, 116, 144);     // Accent Dark Border/Bg
        private readonly Color successColor = Color.FromArgb(16, 185, 129);   // #10B981 Emerald Green
        private readonly Color warningColor = Color.FromArgb(245, 158, 11);   // #F59E0B Amber
        private readonly Color dangerColor = Color.FromArgb(239, 68, 68);     // #EF4444 Rose Red

        private Panel contentHost;
        private Panel homePage;
        private Panel bindingsPage;
        private Panel leaderKeyPage;
        private Panel radialsPage;
        private Panel searchPage;
        private Panel pathsPage;
        private Panel planPage;
        private List<Panel> pagesList;
        private List<Button> navButtons;
        private List<Button> topNavButtons;
        private FlowLayoutPanel navigation;
        private Panel sidebarPanel;
        private TableLayoutPanel rootLayout;
        private TableLayoutPanel workspaceLayout;
        private FlowLayoutPanel topNavigation;
        private Label healthSummaryLabel;
        private RichTextBox healthDetailsBox;
        private RichTextBox nxBridgeBox;
        private RichTextBox generatedFilesBox;
        private NxKeysHealthReport currentHealth;

        private LeaderKeyEngine leaderKeyEngine;
        private ListView leaderSequencesListView;
        private Label leaderEngineStatusLabel;
        private Button toggleEngineBtn;

        private Label headerTitle;
        private Label headerSubtitle;
        private Label footerStatus;
        private Label stepperLabel;
        private Panel stepperProgressBar;

        private ListView bindingsListView;
        private ListView radialsListView;
        private ListView searchResultsListView;
        private ListView backupsListView;
        private RichTextBox logBox;
        private CheckBox dryRunCheck;
        private TextBox searchQueryBox;

        public HotkeyStudioForm(string initialConfigPath = null, LeaderKeyEngine existingEngine = null)
        {
            configFilePath = initialConfigPath;
            if (string.IsNullOrWhiteSpace(configFilePath) || !File.Exists(configFilePath))
            {
                configFilePath = FindDefaultConfig();
            }

            try
            {
                config = Config.Load(configFilePath);
            }
            catch
            {
                config = new Config();
                config.ApplyDefaults();
            }

            leaderKeyEngine = existingEngine;

            Text = "NX 2512 Hotkey Studio — Студия настройки горячих клавиш";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 620);
            Size = new Size(1240, 760);
            AutoScaleMode = AutoScaleMode.Dpi;
            TopMost = false;
            BackColor = backColor;
            ForeColor = textColor;
            Font = new Font("Segoe UI", 9.5f);

            BuildInterface();
            SizeChanged += (s, e) => ApplyResponsiveLayout();
            if (config.Performance?.LazyStudioScan == true)
            {
                StartBackgroundScan();
            }
            else
            {
                EnsureScanData();
            }
            RefreshData();
            GoToStep(0);
            FormClosing += HotkeyStudioForm_FormClosing;

            if (leaderKeyEngine != null && leaderKeyEngine.IsRunning)
            {
                leaderKeyEngine.StatusChanged += status =>
                {
                    if (IsDisposed) return;
                    if (InvokeRequired) Invoke(new Action(() => leaderEngineStatusLabel.Text = status));
                    else leaderEngineStatusLabel.Text = status;
                };
                if (toggleEngineBtn != null)
                {
                    toggleEngineBtn.Text = "🔴 Остановить перехват";
                    toggleEngineBtn.BackColor = dangerColor;
                    toggleEngineBtn.ForeColor = Color.White;
                }
                if (leaderEngineStatusLabel != null)
                {
                    leaderEngineStatusLabel.Text = "Leader Key Engine активен в фоновом режиме (NX Hook)";
                }
            }
        }

        private string FindDefaultConfig()
        {
            string candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nx2512-pro-hybrid.json");
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "nx2512-pro-hybrid.json");
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);

            return "nx2512-pro-hybrid.json";
        }

        private void EnsureScanData()
        {
            if (scanResult == null)
            {
                if (scanTask != null)
                {
                    scanResult = scanTask.GetAwaiter().GetResult();
                }
                else
                {
                    scanResult = NxScanner.Scan(config);
                }
            }
        }

        private void StartBackgroundScan()
        {
            if (scanTask != null || scanResult != null) return;
            footerStatus.Text = "Фоновое сканирование NX запущено...";
            scanTask = Task.Run(() => NxScanner.Scan(config));
            scanTask.ContinueWith(t =>
            {
                if (IsDisposed) return;
                Action update = () =>
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        scanResult = t.Result;
                        footerStatus.Text = $"Сканирование готово: {scanResult.MenuFiles.Count} menu-файлов, {scanResult.Catalog.Commands.Count} команд";
                    }
                    else
                    {
                        footerStatus.Text = "Фоновое сканирование не удалось; повтор будет при открытии зависимого экрана";
                        scanTask = null;
                    }
                };
                if (InvokeRequired) BeginInvoke(update); else update();
            });
        }

        private void MarkDirty()
        {
            hasUnsavedChanges = true;
            footerStatus.Text = "Есть несохранённые изменения профиля";
        }

        private void HotkeyStudioForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!hasUnsavedChanges) return;
            DialogResult result = MessageBox.Show(
                "Есть несохранённые изменения. Принять и сохранить их в JSON?",
                "NXKeys Studio",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);
            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (result == DialogResult.Yes)
            {
                config.Save(configFilePath);
                hasUnsavedChanges = false;
            }
        }

        private void BuildInterface()
        {
            rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = backColor
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(rootLayout);

            sidebarPanel = BuildSidebar();
            sidebarPanel.Dock = DockStyle.Fill;
            rootLayout.Controls.Add(sidebarPanel, 0, 0);

            workspaceLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = backColor
            };
            workspaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            rootLayout.Controls.Add(workspaceLayout, 1, 0);

            Panel header = BuildHeader();
            header.Dock = DockStyle.Fill;
            workspaceLayout.Controls.Add(header, 0, 0);

            topNavigation = BuildTopNavigation();
            topNavigation.Dock = DockStyle.Fill;
            topNavigation.Visible = false;
            workspaceLayout.Controls.Add(topNavigation, 0, 1);

            contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = backColor,
                Padding = new Padding(16)
            };
            workspaceLayout.Controls.Add(contentHost, 0, 2);

            Panel footer = BuildFooter();
            footer.Dock = DockStyle.Fill;
            workspaceLayout.Controls.Add(footer, 0, 3);

            homePage = BuildOverviewPage();
            bindingsPage = BuildCommandsPage();
            leaderKeyPage = BuildLeaderKeyPage();
            radialsPage = BuildRadialsPage();
            searchPage = BuildNxBridgePage();
            pathsPage = BuildDeployPage();
            planPage = BuildBackupsProfilePage();

            pagesList = new List<Panel> { homePage, bindingsPage, leaderKeyPage, radialsPage, searchPage, pathsPage, planPage };

            foreach (var p in pagesList) contentHost.Controls.Add(p);
            ApplyResponsiveLayout();
        }

        private Panel BuildSidebar()
        {
            Panel sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 230,
                BackColor = Color.FromArgb(10, 13, 18),
                Padding = new Padding(12, 16, 12, 16)
            };

            Label product = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                Text = "NX KEYS",
                ForeColor = textColor,
                Font = new Font("Segoe UI Semibold", 15f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label version = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "HOTKEY STUDIO 2512",
                ForeColor = accentColor,
                Font = new Font("Segoe UI Semibold", 8.5f),
                TextAlign = ContentAlignment.TopLeft
            };

            Panel brandDivider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = borderColor,
                Margin = new Padding(0, 8, 0, 12)
            };

            navigation = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 350,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0),
                BackColor = sidebar.BackColor
            };

            navButtons = new List<Button>();
            string[] navTitles = {
                "Обзор",
                "Команды",
                "Leader Key",
                "Radials",
                "NX / Bridge",
                "Deploy",
                "Backups / Profile"
            };

            for (int i = 0; i < navTitles.Length; i++)
            {
                int stepIdx = i;
                Button btn = CreateNavButton(navTitles[i], () => GoToStep(stepIdx));
                navButtons.Add(btn);
                navigation.Controls.Add(btn);
            }

            Button close = CreateFlatButton("Закрыть", mutedColor);
            close.Dock = DockStyle.Bottom;
            close.Height = 36;
            close.Click += (s, e) => Close();

            sidebar.Controls.Add(close);
            sidebar.Controls.Add(navigation);
            sidebar.Controls.Add(brandDivider);
            sidebar.Controls.Add(version);
            sidebar.Controls.Add(product);

            return sidebar;
        }

        private FlowLayoutPanel BuildTopNavigation()
        {
            FlowLayoutPanel bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12, 4, 12, 4),
                BackColor = Color.FromArgb(10, 13, 18)
            };
            topNavButtons = new List<Button>();
            string[] titles = { "Обзор", "Команды", "Leader", "Radials", "Bridge", "Deploy", "Profile" };
            for (int i = 0; i < titles.Length; i++)
            {
                int stepIdx = i;
                Button btn = CreateFlatButton(titles[i], textColor);
                btn.Width = 104;
                btn.Height = 32;
                btn.Margin = new Padding(2, 2, 6, 2);
                btn.Click += (s, e) => GoToStep(stepIdx);
                topNavButtons.Add(btn);
                bar.Controls.Add(btn);
            }
            return bar;
        }

        private Panel BuildHeader()
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 92,
                BackColor = surfaceColor,
                Padding = new Padding(22, 12, 22, 10)
            };

            stepperLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = accentColor,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Text = "Шаг 1 из 8"
            };

            headerTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = textColor,
                Font = new Font("Segoe UI Semibold", 15f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            headerSubtitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.TopLeft
            };

            stepperProgressBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 4,
                BackColor = elevatedColor
            };
            stepperProgressBar.Paint += (s, e) =>
            {
                float pct = (float)(currentStepIndex + 1) / pagesList.Count;
                int fillWidth = (int)(stepperProgressBar.Width * pct);
                using (SolidBrush brush = new SolidBrush(accentColor))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, fillWidth, stepperProgressBar.Height);
                }
            };

            FlowLayoutPanel navBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 295,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 10, 0, 0)
            };

            Button btnPrev = CreateFlatButton("< Назад", textColor);
            btnPrev.Width = 85;
            btnPrev.Click += (s, e) => GoToStep(currentStepIndex - 1);

            Button btnNext = CreatePrimaryButton("Далее >");
            btnNext.Width = 90;
            btnNext.Click += (s, e) => GoToStep(currentStepIndex + 1);

            Button applyBtn = CreatePrimaryButton("Применить");
            applyBtn.Width = 100;
            applyBtn.BackColor = successColor;
            applyBtn.Click += async (s, e) => await ApplyCurrentPlanAsync();

            navBtns.Controls.Add(btnPrev);
            navBtns.Controls.Add(btnNext);
            navBtns.Controls.Add(applyBtn);

            header.Controls.Add(navBtns);
            header.Controls.Add(headerSubtitle);
            header.Controls.Add(headerTitle);
            header.Controls.Add(stepperLabel);
            header.Controls.Add(stepperProgressBar);

            return header;
        }

        private Panel BuildFooter()
        {
            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                BackColor = Color.FromArgb(10, 13, 18),
                Padding = new Padding(20, 6, 16, 4)
            };

            footerStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Готово к работе",
                ForeColor = mutedColor,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label safety = new Label
            {
                Dock = DockStyle.Right,
                Width = 360,
                Text = "Безопасный оверлей MenuScript | SHA-256 Защита",
                ForeColor = successColor,
                TextAlign = ContentAlignment.MiddleRight
            };

            footer.Controls.Add(safety);
            footer.Controls.Add(footerStatus);
            return footer;
        }

        private Panel BuildOverviewPage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = backColor
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            FlowLayoutPanel metrics = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true
            };

            healthSummaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = textColor,
                BackColor = surfaceColor,
                Padding = new Padding(14),
                Font = new Font("Segoe UI Semibold", 10.5f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            healthDetailsBox = CreateReadOnlyBox();

            Button refreshHealth = CreatePrimaryButton("Обновить диагностику");
            refreshHealth.Width = 190;
            refreshHealth.Click += (s, e) => RefreshHealthView();

            metrics.Controls.Add(CreateMetricPanel("Профиль", config.Profile.Name, accentColor));
            metrics.Controls.Add(CreateMetricPanel("Hotkeys", config.Keyboard.Count.ToString(), successColor));
            metrics.Controls.Add(CreateMetricPanel("Leader", $"{config.LeaderKey?.Sequences?.Count ?? 0}", warningColor));
            metrics.Controls.Add(CreateMetricPanel("Radials", config.Radials.Count.ToString(), accentColor));

            layout.Controls.Add(metrics, 0, 0);
            layout.Controls.Add(healthDetailsBox, 0, 1);
            layout.Controls.Add(refreshHealth, 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildCommandsPage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

            Panel bindingsPanel = BuildBindingsPage();
            bindingsPanel.Dock = DockStyle.Fill;
            Panel searchTop = new Panel { Dock = DockStyle.Fill };
            searchQueryBox = CreateTextBox();
            searchQueryBox.Dock = DockStyle.Fill;
            searchQueryBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) PerformSearch(); };

            Button searchBtn = CreatePrimaryButton("Искать");
            searchBtn.Dock = DockStyle.Right;
            searchBtn.Width = 110;
            searchBtn.Click += (s, e) => PerformSearch();

            searchTop.Controls.Add(searchQueryBox);
            searchTop.Controls.Add(searchBtn);

            searchResultsListView = CreateStyledListView();
            searchResultsListView.Columns.Add("BUTTON ID", 220);
            searchResultsListView.Columns.Add("Label", 260);
            searchResultsListView.Columns.Add("Score", 90);
            searchResultsListView.Columns.Add("NXOpen API", 360);

            layout.Controls.Add(bindingsPanel, 0, 0);
            layout.Controls.Add(searchTop, 0, 1);
            layout.Controls.Add(searchResultsListView, 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildNxBridgePage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            Button scanBtn = CreatePrimaryButton("Сканировать NX");
            scanBtn.Width = 150;
            scanBtn.Click += (s, e) => RefreshNxBridgeView(true);
            Button healthBtn = CreateFlatButton("Обновить bridge", accentColor);
            healthBtn.Width = 150;
            healthBtn.Click += (s, e) => RefreshNxBridgeView(false);
            actions.Controls.Add(scanBtn);
            actions.Controls.Add(healthBtn);

            nxBridgeBox = CreateReadOnlyBox();
            generatedFilesBox = CreateReadOnlyBox();

            layout.Controls.Add(actions, 0, 0);
            layout.Controls.Add(nxBridgeBox, 0, 1);
            layout.Controls.Add(generatedFilesBox, 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildDeployPage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            dryRunCheck = CreateCheckBox("DryRun");
            dryRunCheck.Checked = config.Deployment.DryRun;
            dryRunCheck.CheckedChanged += (s, e) => { config.Deployment.DryRun = dryRunCheck.Checked; MarkDirty(); };
            Button planBtn = CreatePrimaryButton("Сформировать план");
            planBtn.Width = 150;
            planBtn.Click += (s, e) => RefreshPlanView();
            Button applyBtn = CreateFlatButton("Применить", successColor);
            applyBtn.Width = 120;
            applyBtn.Click += async (s, e) => await ApplyCurrentPlanAsync();
            actions.Controls.Add(planBtn);
            actions.Controls.Add(applyBtn);
            actions.Controls.Add(dryRunCheck);

            logBox = CreateReadOnlyBox();
            layout.Controls.Add(actions, 0, 0);
            layout.Controls.Add(logBox, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildBackupsProfilePage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

            backupsListView = CreateStyledListView();
            backupsListView.Columns.Add("Timestamp", 180);
            backupsListView.Columns.Add("Profile", 180);
            backupsListView.Columns.Add("Files", 80);
            backupsListView.Columns.Add("Backup Folder Path", 520);

            FlowLayoutPanel backupActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            Button refreshBackups = CreateFlatButton("Обновить", textColor);
            refreshBackups.Width = 110;
            refreshBackups.Click += (s, e) => RefreshBackupsData();
            Button restoreSelected = CreateFlatButton("Восстановить выбранный", warningColor);
            restoreSelected.Width = 190;
            restoreSelected.Click += (s, e) => RestoreSelectedBackup();
            backupActions.Controls.Add(refreshBackups);
            backupActions.Controls.Add(restoreSelected);

            TableLayoutPanel profileBox = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = surfaceColor,
                Padding = new Padding(12)
            };
            profileBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            profileBox.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            profileBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            profileBox.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label pathLabel = new Label { Dock = DockStyle.Fill, Text = "Current config", ForeColor = mutedColor };
            TextBox pathBox = CreateTextBox();
            pathBox.Dock = DockStyle.Fill;
            pathBox.ReadOnly = true;
            pathBox.Text = configFilePath;
            Button saveCfg = CreatePrimaryButton("Сохранить JSON");
            saveCfg.Dock = DockStyle.Fill;
            saveCfg.Click += (s, e) =>
            {
                config.Save(configFilePath);
                hasUnsavedChanges = false;
                footerStatus.Text = "Конфигурация сохранена";
                MessageBox.Show("Конфигурация успешно сохранена!", "NXKeys Studio");
            };
            profileBox.Controls.Add(pathLabel, 0, 0);
            profileBox.Controls.Add(pathBox, 0, 1);
            profileBox.Controls.Add(saveCfg, 1, 1);

            layout.Controls.Add(backupsListView, 0, 0);
            layout.Controls.Add(backupActions, 0, 1);
            layout.Controls.Add(profileBox, 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildHomePage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = backColor
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 165));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(CreateInfoCard("ПРОФИЛЬ", config.Profile.Name, config.Profile.Description, accentColor), 0, 0);
            layout.Controls.Add(CreateInfoCard("ГОРЯЧИЕ КЛАВИШИ", $"{config.Keyboard.Count} настроено", "Привязки клавиш к командам NX 2512", successColor), 1, 0);
            layout.Controls.Add(CreateInfoCard("РАДИАЛЬНЫЕ МЕНЮ", $"{config.Radials.Count} активных меню", "Управление 8 секторами каждого меню", warningColor), 2, 0);

            Panel infoBox = CreateCard();
            infoBox.Margin = new Padding(6);
            Label title = CreateCardTitle("Мастер пошаговой конфигурации Siemens NX 2512");
            Label body = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 9.5f),
                Text = "Используйте пошаговый мастер или кнопки навигации слева:\r\n\r\n" +
                       "• Шаг 2 (Горячие клавиши): Настройка клавиатурных комбинаций (Ctrl/Alt/Shift + Key).\r\n" +
                       "• Шаг 3 (Радиальные меню): Выбор и назначение команд на 8 направлений (N, NE, E, SE, S, SW, W, NW).\r\n" +
                       "• Шаг 4 (Поиск и API): Поиск BUTTON ID среди 32 124 команд из базы каталога NX 2512.\r\n" +
                       "• Шаг 6 (План изменений): Автоматический анализ невязок и генерация файла оверлея nxkeys_generated.men.",
                Padding = new Padding(0, 10, 0, 0)
            };
            infoBox.Controls.Add(body);
            infoBox.Controls.Add(title);
            layout.SetColumnSpan(infoBox, 3);
            layout.Controls.Add(infoBox, 0, 1);

            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildBindingsPage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            bindingsListView = CreateStyledListView();
            bindingsListView.Columns.Add("Сочетание клавиш", 150);
            bindingsListView.Columns.Add("Команда NX", 210);
            bindingsListView.Columns.Add("BUTTON ID", 250);
            bindingsListView.Columns.Add("Область / Контекст", 130);
            bindingsListView.Columns.Add("Статус", 100);
            bindingsListView.DoubleClick += (s, e) => EditSelectedBinding();

            FlowLayoutPanel toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 6, 0, 0)
            };

            Button addBtn = CreatePrimaryButton("Добавить привязку");
            addBtn.Click += (s, e) => AddNewBinding();

            Button editBtn = CreateFlatButton("Редактировать", textColor);
            editBtn.Click += (s, e) => EditSelectedBinding();

            Button deleteBtn = CreateFlatButton("Удалить", dangerColor);
            deleteBtn.Click += (s, e) => DeleteSelectedBinding();

            Button loadErgoBtn = CreateFlatButton("Загрузить пресет Ergonomic 80", accentColor);
            loadErgoBtn.Click += (s, e) => LoadErgonomic80Preset();

            toolbar.Controls.Add(addBtn);
            toolbar.Controls.Add(editBtn);
            toolbar.Controls.Add(deleteBtn);
            toolbar.Controls.Add(loadErgoBtn);

            layout.Controls.Add(bindingsListView, 0, 0);
            layout.Controls.Add(toolbar, 0, 1);

            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildLeaderKeyPage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            // Top Status & Control Bar
            Panel topBar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = surfaceColor,
                Padding = new Padding(12, 8, 12, 8)
            };

            toggleEngineBtn = CreatePrimaryButton("🟢 Запустить перехват Leader Key");
            toggleEngineBtn.Dock = DockStyle.Left;
            toggleEngineBtn.Width = 240;
            toggleEngineBtn.Click += (s, e) => ToggleLeaderEngine();

            Button testHudBtn = CreateFlatButton("👁 Просмотр HUD (CapsLock)", accentColor);
            testHudBtn.Dock = DockStyle.Left;
            testHudBtn.Width = 200;
            testHudBtn.Margin = new Padding(10, 0, 0, 0);
            testHudBtn.Click += (s, e) => ShowHudPreview();

            Button resetPresetBtn = CreateFlatButton("⚡ Сброс к NX Leader 80", textColor);
            resetPresetBtn.Dock = DockStyle.Left;
            resetPresetBtn.Width = 190;
            resetPresetBtn.Margin = new Padding(10, 0, 0, 0);
            resetPresetBtn.Click += (s, e) => ResetLeaderKeyPreset();

            leaderEngineStatusLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 320,
                Text = "Служба не активна (Нажмите 'Запустить' для работы с NX)",
                ForeColor = mutedColor,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI Semibold", 8.75f)
            };

            topBar.Controls.Add(leaderEngineStatusLabel);
            topBar.Controls.Add(resetPresetBtn);
            topBar.Controls.Add(testHudBtn);
            topBar.Controls.Add(toggleEngineBtn);

            // Sequences ListView
            leaderSequencesListView = CreateStyledListView();
            leaderSequencesListView.Columns.Add("Последовательность", 160);
            leaderSequencesListView.Columns.Add("Модуль", 150);
            leaderSequencesListView.Columns.Add("Категория", 130);
            leaderSequencesListView.Columns.Add("Команда NX", 210);
            leaderSequencesListView.Columns.Add("BUTTON ID", 240);
            leaderSequencesListView.Columns.Add("Запуск", 160);
            leaderSequencesListView.Columns.Add("Статус", 100);

            // Toolbar
            FlowLayoutPanel toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 6, 0, 0)
            };

            Button addSeqBtn = CreatePrimaryButton("Добавить последовательность");
            addSeqBtn.Click += (s, e) => AddNewLeaderSequence();

            Button deleteSeqBtn = CreateFlatButton("Удалить выбранную", dangerColor);
            deleteSeqBtn.Click += (s, e) => DeleteSelectedLeaderSequence();

            toolbar.Controls.Add(addSeqBtn);
            toolbar.Controls.Add(deleteSeqBtn);

            layout.Controls.Add(topBar, 0, 0);
            layout.Controls.Add(leaderSequencesListView, 0, 1);
            layout.Controls.Add(toolbar, 0, 2);

            page.Controls.Add(layout);
            return page;
        }

        private void ToggleLeaderEngine()
        {
            if (leaderKeyEngine == null || !leaderKeyEngine.IsRunning)
            {
                if (config.LeaderKey == null) config.LeaderKey = new LeaderKeyConfig();
                config.LeaderKey.ApplyDefaults();
                config.LeaderKey.RuntimeModules = config.Modules;
                config.LeaderKey.MergeModules(config.Modules);

                leaderKeyEngine = new LeaderKeyEngine(config.LeaderKey);
                leaderKeyEngine.StatusChanged += status =>
                {
                    if (InvokeRequired) Invoke(new Action(() => leaderEngineStatusLabel.Text = status));
                    else leaderEngineStatusLabel.Text = status;
                };

                leaderKeyEngine.Start();
                toggleEngineBtn.Text = "🔴 Остановить перехват";
                toggleEngineBtn.BackColor = dangerColor;
                toggleEngineBtn.ForeColor = Color.White;
            }
            else
            {
                leaderKeyEngine.Stop();
                leaderKeyEngine.Dispose();
                leaderKeyEngine = null;

                toggleEngineBtn.Text = "🟢 Запустить перехват Leader Key";
                toggleEngineBtn.BackColor = accentColor;
                toggleEngineBtn.ForeColor = Color.FromArgb(13, 17, 23);
                leaderEngineStatusLabel.Text = "Служба остановлена";
            }
        }

        private void ShowHudPreview()
        {
            using (var hud = new LeaderHudForm())
            {
                hud.DisplayHud(config.LeaderKey?.TriggerKey ?? "CapsLock", false, config.LeaderKey?.Sequences, 0.95);
                MessageBox.Show("HUD отображен! Нажмите OK для закрытия предпросмотра.", "NX Leader HUD Preview");
                hud.DismissHud();
            }
        }

        private void ResetLeaderKeyPreset()
        {
            config.LeaderKey = new LeaderKeyConfig();
            config.LeaderKey.ApplyDefaults();
            config.LeaderKey.RuntimeModules = config.Modules;
            config.LeaderKey.MergeModules(config.Modules);
            MarkDirty();
            RefreshLeaderKeyData();
            MessageBox.Show("Пресет NX Leader 80 (Sequential Chords) успешно восстановлен!", "NXKeys Studio");
        }

        private void RefreshLeaderKeyData()
        {
            leaderSequencesListView.Items.Clear();
            if (config?.LeaderKey?.Sequences != null)
            {
                foreach (LeaderSequenceItem s in config.LeaderKey.Sequences)
                {
                    ListViewItem item = new ListViewItem(s.Sequence);
                    item.SubItems.Add(string.IsNullOrWhiteSpace(s.ModuleID) ? ModuleDefaults.ModuleIdForCategory(s.Category) : s.ModuleID);
                    item.SubItems.Add(s.Category);
                    item.SubItems.Add(s.Command?.Name ?? s.Notes);
                    item.SubItems.Add(s.Command?.ID ?? string.Empty);
                    item.SubItems.Add("Direct NX command");
                    item.SubItems.Add(s.Enabled ? "Enabled" : "Disabled");
                    leaderSequencesListView.Items.Add(item);
                }
            }
        }

        private void AddNewLeaderSequence()
        {
            EnsureScanData();
            using (BindingEditDialog dlg = new BindingEditDialog(null, scanResult.Catalog))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Binding res = dlg.ResultBinding;
                    string seq = res.Shortcut.Replace("Leader ", "").Trim();
                    LeaderSequenceItem newItem = new LeaderSequenceItem(seq, "Custom", res.Command?.ID, res.Command?.Name, string.Empty);
                    config.LeaderKey.Sequences.Add(newItem);
                    MarkDirty();
                    RefreshLeaderKeyData();
                }
            }
        }

        private void DeleteSelectedLeaderSequence()
        {
            if (leaderSequencesListView.SelectedIndices.Count > 0)
            {
                int idx = leaderSequencesListView.SelectedIndices[0];
                if (idx >= 0 && idx < config.LeaderKey.Sequences.Count)
                {
                    config.LeaderKey.Sequences.RemoveAt(idx);
                    MarkDirty();
                    RefreshLeaderKeyData();
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (hasUnsavedChanges)
                {
                    DialogResult result = MessageBox.Show(
                        "Есть несохранённые изменения. Принять и сохранить их в JSON?",
                        "NXKeys Studio",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning);
                    if (result == DialogResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                    if (result == DialogResult.Yes)
                    {
                        config.Save(configFilePath);
                        hasUnsavedChanges = false;
                    }
                }
                e.Cancel = true;
                Hide();
                return;
            }

            if (leaderKeyEngine != null)
            {
                leaderKeyEngine.Stop();
                leaderKeyEngine.Dispose();
                leaderKeyEngine = null;
            }
            base.OnFormClosing(e);
        }

        private Panel BuildRadialsPage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            radialsListView = CreateStyledListView();
            radialsListView.Columns.Add("Радиальное меню", 220);
            radialsListView.Columns.Add("Модуль", 150);
            radialsListView.Columns.Add("Триггер (Клавиша / Жест)", 170);
            radialsListView.Columns.Add("Секторов настроено", 150);
            radialsListView.Columns.Add("Статус", 110);
            radialsListView.DoubleClick += (s, e) => EditSelectedRadial();

            FlowLayoutPanel toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0) };

            Button editRadialBtn = CreatePrimaryButton("Редактировать секторы меню");
            editRadialBtn.Click += (s, e) => EditSelectedRadial();

            Button addRadialBtn = CreateFlatButton("Добавить радиальное меню", textColor);
            addRadialBtn.Click += (s, e) => AddNewRadial();

            toolbar.Controls.Add(editRadialBtn);
            toolbar.Controls.Add(addRadialBtn);

            layout.Controls.Add(radialsListView, 0, 0);
            layout.Controls.Add(toolbar, 0, 1);

            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildSearchPage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            Panel topBar = new Panel { Dock = DockStyle.Fill };
            searchQueryBox = CreateTextBox();
            searchQueryBox.Dock = DockStyle.Left;
            searchQueryBox.Width = 380;
            searchQueryBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) PerformSearch(); };

            Button searchBtn = CreatePrimaryButton("Искать в каталоге");
            searchBtn.Dock = DockStyle.Left;
            searchBtn.Width = 150;
            searchBtn.Margin = new Padding(8, 0, 0, 0);
            searchBtn.Click += (s, e) => PerformSearch();

            topBar.Controls.Add(searchBtn);
            topBar.Controls.Add(searchQueryBox);

            searchResultsListView = CreateStyledListView();
            searchResultsListView.Columns.Add("BUTTON ID", 240);
            searchResultsListView.Columns.Add("Метка (Label)", 240);
            searchResultsListView.Columns.Add("Match Score", 100);
            searchResultsListView.Columns.Add("NXOpen API Candidate", 420);

            Label info = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Поиск по базе данных 32 124 BUTTON ID и NXOpen API кандидатов Siemens NX 2512.",
                ForeColor = mutedColor
            };

            layout.Controls.Add(topBar, 0, 0);
            layout.Controls.Add(searchResultsListView, 0, 1);
            layout.Controls.Add(info, 0, 2);

            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildPathsPage()
        {
            Panel page = CreatePage();
            RichTextBox rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = elevatedColor,
                ForeColor = textColor,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f)
            };

            Button scanBtn = CreatePrimaryButton("Запустить сканирование NX");
            scanBtn.Dock = DockStyle.Top;
            scanBtn.Height = 38;
            scanBtn.Click += (s, e) =>
            {
                scanResult = NxScanner.Scan(config);
                rtb.Clear();
                rtb.AppendText($"Discovered Environment Roots ({scanResult.DiscoveredRoots.Count}):\n");
                foreach (string r in scanResult.DiscoveredRoots) rtb.AppendText($"  - {r}\n");
                rtb.AppendText($"\nCatalog Index Total Commands: {scanResult.Catalog.Commands.Count}\n");
                rtb.AppendText($"Menu Files Found: {scanResult.MenuFiles.Count}\n");
                rtb.AppendText($"Role Files Found: {scanResult.RoleFiles.Count}\n");
            };

            page.Controls.Add(rtb);
            page.Controls.Add(scanBtn);
            return page;
        }

        private Panel BuildPlanPage()
        {
            Panel page = CreatePage();
            logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = elevatedColor,
                ForeColor = textColor,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f)
            };

            Button buildPlanBtn = CreatePrimaryButton("Сформировать план");
            buildPlanBtn.Dock = DockStyle.Top;
            buildPlanBtn.Height = 38;
            buildPlanBtn.Click += (s, e) => RefreshPlanView();

            page.Controls.Add(logBox);
            page.Controls.Add(buildPlanBtn);
            return page;
        }

        private Panel BuildBackupsPage()
        {
            Panel page = CreatePage();
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            backupsListView = CreateStyledListView();
            backupsListView.Columns.Add("Timestamp", 190);
            backupsListView.Columns.Add("Profile", 190);
            backupsListView.Columns.Add("Backup Folder Path", 450);

            FlowLayoutPanel bar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            Button restoreBtn = CreateFlatButton("Восстановить выбранный бэкап", warningColor);
            restoreBtn.Click += (s, e) =>
            {
                var res = BackupEngine.RestoreLatest(config.Deployment.BackupRoot, false);
                MessageBox.Show(res.Success ? "Восстановление выполнено!" : res.ErrorMessage, "Restore");
            };
            bar.Controls.Add(restoreBtn);

            layout.Controls.Add(backupsListView, 0, 0);
            layout.Controls.Add(bar, 0, 1);

            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildProfilesPage()
        {
            Panel page = CreatePage();
            FlowLayoutPanel pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };

            dryRunCheck = CreateCheckBox("Режим проверки (DryRun: не записывать файлы на диск)");
            dryRunCheck.Checked = config.Deployment.DryRun;
            dryRunCheck.CheckedChanged += (s, e) => { config.Deployment.DryRun = dryRunCheck.Checked; MarkDirty(); };

            Button saveCfg = CreatePrimaryButton("Сохранить конфигурацию в JSON");
            saveCfg.Click += (s, e) =>
            {
                config.Save(configFilePath);
                hasUnsavedChanges = false;
                footerStatus.Text = "Конфигурация сохранена";
                MessageBox.Show("Конфигурация успешно сохранена!", "NXKeys Studio");
            };

            pnl.Controls.Add(dryRunCheck);
            pnl.Controls.Add(saveCfg);

            page.Controls.Add(pnl);
            return page;
        }

        private void RefreshHealthView()
        {
            currentHealth = NxKeysHealthService.Check(config);
            int unresolved = 0;
            try
            {
                EnsureScanData();
                var plan = DeploymentEngine.BuildPlan(config, scanResult.Catalog);
                unresolved = plan.Resolutions.Count(r => r.Status != ResolutionStatus.Resolved);
            }
            catch { }

            if (healthSummaryLabel != null)
            {
                Color stateColor = currentHealth.MenuScriptVersionOk && currentHealth.ManagedPackageOk ? successColor : warningColor;
                healthSummaryLabel.ForeColor = stateColor;
                healthSummaryLabel.Text =
                    $"MenuScript: {(currentHealth.MenuScriptVersionOk ? "OK" : "needs cleanup")}   " +
                    $"Bridge: {(currentHealth.BridgeLoaded ? "loaded" : "not loaded")}   " +
                    $"Failed: {currentHealth.FailedCount}   " +
                    $"Unresolved bindings: {unresolved}";
            }

            if (healthDetailsBox == null) return;
            healthDetailsBox.Clear();
            healthDetailsBox.AppendText($"Managed root: {currentHealth.ManagedRoot}\n");
            healthDetailsBox.AppendText($"NX running: {(currentHealth.NxRunning ? "yes" : "no")}\n");
            foreach (string proc in currentHealth.NxProcesses) healthDetailsBox.AppendText($"  - {proc}\n");
            healthDetailsBox.AppendText($"MenuScript version OK: {currentHealth.MenuScriptVersionOk}\n");
            healthDetailsBox.AppendText($"Managed package OK: {currentHealth.ManagedPackageOk}\n");
            healthDetailsBox.AppendText($"Bridge DLL locked: {currentHealth.BridgeDllLocked}\n");
            healthDetailsBox.AppendText($"Bridge pending/completed/failed: {currentHealth.PendingCount}/{currentHealth.CompletedCount}/{currentHealth.FailedCount}\n");

            if (currentHealth.StaleFiles.Count > 0)
            {
                healthDetailsBox.AppendText("\nInvalid NXKeys VERSION files:\n");
                foreach (var stale in currentHealth.StaleFiles.Take(30))
                {
                    healthDetailsBox.AppendText($"  - VERSION {stale.Version}: {stale.Path}\n");
                }
            }

            healthDetailsBox.AppendText("\nGenerated NXKeys menu versions:\n");
            foreach (var file in currentHealth.MenuFiles.Take(20))
            {
                healthDetailsBox.AppendText($"  - VERSION {file.Version}: {file.Path}\n");
            }

            if (currentHealth.LastFailures.Count > 0)
            {
                healthDetailsBox.AppendText("\nRecent bridge failures:\n");
                foreach (string failure in currentHealth.LastFailures) healthDetailsBox.AppendText($"  - {failure}\n");
            }
        }

        private void RefreshNxBridgeView(bool rescan)
        {
            if (rescan)
            {
                scanResult = NxScanner.Scan(config);
            }
            else
            {
                EnsureScanData();
            }

            currentHealth = NxKeysHealthService.Check(config);

            nxBridgeBox.Clear();
            nxBridgeBox.AppendText($"Bridge loaded: {(currentHealth.BridgeLoaded ? "yes" : "no")}\n");
            nxBridgeBox.AppendText($"Pending: {currentHealth.PendingCount}\n");
            nxBridgeBox.AppendText($"Completed: {currentHealth.CompletedCount}\n");
            nxBridgeBox.AppendText($"Failed: {currentHealth.FailedCount}\n");
            nxBridgeBox.AppendText($"NX running: {(currentHealth.NxRunning ? "yes" : "no")}\n");
            foreach (string proc in currentHealth.NxProcesses) nxBridgeBox.AppendText($"  - {proc}\n");
            nxBridgeBox.AppendText("\nRecent failures:\n");
            foreach (string failure in currentHealth.LastFailures) nxBridgeBox.AppendText($"  - {failure}\n");

            generatedFilesBox.Clear();
            generatedFilesBox.AppendText($"Discovered roots: {scanResult.DiscoveredRoots.Count}\n");
            generatedFilesBox.AppendText($"Menu files found: {scanResult.MenuFiles.Count}\n");
            generatedFilesBox.AppendText($"Role files found: {scanResult.RoleFiles.Count}\n");
            generatedFilesBox.AppendText($"Catalog commands: {scanResult.Catalog.Commands.Count}\n\n");
            foreach (var file in currentHealth.MenuFiles)
            {
                generatedFilesBox.AppendText($"VERSION {file.Version}: {file.Path}\n");
            }
        }

        private void RefreshBackupsData()
        {
            if (backupsListView == null) return;
            backupsListView.Items.Clear();
            foreach (BackupManifest backup in BackupEngine.ListBackups(config.Deployment.BackupRoot))
            {
                ListViewItem item = new ListViewItem(backup.Timestamp);
                item.SubItems.Add(backup.ProfileName);
                item.SubItems.Add(backup.Entries.Count.ToString());
                item.SubItems.Add(backup.BackupDirectory);
                item.Tag = backup;
                backupsListView.Items.Add(item);
            }
            AutoSizeListColumns(backupsListView);
        }

        private void RestoreSelectedBackup()
        {
            if (backupsListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите backup в списке.", "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            BackupManifest backup = backupsListView.SelectedItems[0].Tag as BackupManifest;
            string manifestPath = backup != null ? Path.Combine(backup.BackupDirectory, "manifest.json") : string.Empty;
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                MessageBox.Show("Не удалось определить manifest выбранного backup.", "Restore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                $"Восстановить выбранный backup?\n{manifestPath}",
                "NXKeys Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            RestoreResult res = BackupEngine.RestoreFromManifest(manifestPath, false);
            MessageBox.Show(res.Success ? "Восстановление выполнено!" : res.ErrorMessage, "Restore");
            RefreshBackupsData();
            RefreshHealthView();
        }

        private void ApplyResponsiveLayout()
        {
            if (rootLayout == null || workspaceLayout == null || sidebarPanel == null) return;

            bool small = ClientSize.Width < 800;
            bool medium = ClientSize.Width >= 800 && ClientSize.Width < 1100;

            rootLayout.ColumnStyles[0].Width = small ? 0 : (medium ? 92 : 230);
            sidebarPanel.Visible = !small;
            workspaceLayout.RowStyles[1].Height = small ? 42 : 0;
            if (topNavigation != null) topNavigation.Visible = small;

            if (navButtons != null)
            {
                foreach (Button btn in navButtons)
                {
                    btn.Width = medium ? 68 : 206;
                    btn.TextAlign = medium ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
                    string original = btn.Tag as string ?? btn.Text;
                    btn.Text = medium ? original.Split('/')[0].Trim().Substring(0, Math.Min(3, original.Split('/')[0].Trim().Length)) : original;
                    btn.Padding = medium ? new Padding(0) : new Padding(12, 0, 0, 0);
                }
            }

            AutoSizeAllListColumns();
        }

        private void GoToStep(int stepIndex)
        {
            if (stepIndex < 0) stepIndex = 0;
            if (stepIndex >= pagesList.Count) stepIndex = pagesList.Count - 1;

            currentStepIndex = stepIndex;

            for (int i = 0; i < navButtons.Count; i++)
            {
                bool isActive = i == currentStepIndex;
                navButtons[i].BackColor = isActive ? Color.FromArgb(31, 41, 55) : Color.FromArgb(10, 13, 18);
                navButtons[i].ForeColor = isActive ? accentColor : textColor;
            }
            if (topNavButtons != null)
            {
                for (int i = 0; i < topNavButtons.Count; i++)
                {
                    bool isActive = i == currentStepIndex;
                    topNavButtons[i].BackColor = isActive ? Color.FromArgb(31, 41, 55) : elevatedColor;
                    topNavButtons[i].ForeColor = isActive ? accentColor : textColor;
                }
            }

            foreach (Control c in contentHost.Controls) c.Visible = false;

            pagesList[currentStepIndex].Visible = true;
            stepperLabel.Text = $"Раздел {currentStepIndex + 1} из {pagesList.Count}";
            stepperProgressBar.Invalidate();

            switch (currentStepIndex)
            {
                case 0: headerTitle.Text = "Обзор"; headerSubtitle.Text = "Health, профиль, bridge и нерешенные команды"; RefreshHealthView(); break;
                case 1: headerTitle.Text = "Команды"; headerSubtitle.Text = "Hotkeys, каталог BUTTON ID и конфликты"; RefreshData(); break;
                case 2: headerTitle.Text = "Leader Key"; headerSubtitle.Text = "Последовательности, HUD и фоновый engine"; RefreshLeaderKeyData(); break;
                case 3: headerTitle.Text = "Radials"; headerSubtitle.Text = "Радиальные меню и 8 направлений"; RefreshRadialsData(); break;
                case 4: headerTitle.Text = "NX / Bridge"; headerSubtitle.Text = "Сканер NX, generated files и очередь bridge"; RefreshNxBridgeView(false); break;
                case 5: headerTitle.Text = "Deploy"; headerSubtitle.Text = "Dry-run, план и безопасное применение"; RefreshPlanView(); break;
                case 6: headerTitle.Text = "Backups / Profile"; headerSubtitle.Text = "Выборочный restore и сохранение JSON"; RefreshBackupsData(); break;
            }
            AutoSizeAllListColumns();
        }

        private void PerformSearch()
        {
            string query = searchQueryBox?.Text?.Trim();
            if (string.IsNullOrEmpty(query)) return;

            EnsureScanData();
            searchResultsListView.Items.Clear();
            List<string> qList = new List<string> { query };

            List<CandidateMatch> matches = new List<CandidateMatch>();
            foreach (var kvp in scanResult.Catalog.Commands)
            {
                CommandItem cmd = kvp.Value;
                double score = CommandResolver.ScoreCommand(qList, cmd);
                if (score > 0.35)
                {
                    string topApi = cmd.ApiCandidates.Count > 0 ? cmd.ApiCandidates[0].ApiTarget : string.Empty;
                    matches.Add(new CandidateMatch
                    {
                        ID = cmd.ID,
                        Label = cmd.DisplayLabel,
                        Score = score,
                        ApiMatch = topApi
                    });
                }
            }

            matches.Sort((a, b) => b.Score.CompareTo(a.Score));

            foreach (var m in matches.Take(100))
            {
                ListViewItem item = new ListViewItem(m.ID);
                item.SubItems.Add(m.Label);
                item.SubItems.Add(m.Score.ToString("F2"));
                item.SubItems.Add(m.ApiMatch);
                searchResultsListView.Items.Add(item);
            }
        }

        private void RefreshData()
        {
            bindingsListView.Items.Clear();
            if (config?.Keyboard != null)
            {
                foreach (Binding b in config.Keyboard)
                {
                    ListViewItem item = new ListViewItem(b.Shortcut);
                    item.SubItems.Add(b.Command?.Name ?? string.Empty);
                    item.SubItems.Add(b.Command?.ID ?? string.Empty);
                    item.SubItems.Add(b.Scope);
                    item.SubItems.Add(b.Enabled ? "Enabled" : "Disabled");
                    bindingsListView.Items.Add(item);
                }
            }
        }

        private void RefreshRadialsData()
        {
            radialsListView.Items.Clear();
            if (config?.Radials != null)
            {
                foreach (RadialMenu rm in config.Radials)
                {
                    ListViewItem item = new ListViewItem(rm.Name);
                    item.SubItems.Add(string.IsNullOrWhiteSpace(rm.Module) ? "legacy" : rm.Module);
                    item.SubItems.Add(rm.Trigger);
                    item.SubItems.Add($"{rm.Items?.Count ?? 0} / 8");
                    item.SubItems.Add(rm.Enabled ? "Active" : "Disabled");
                    radialsListView.Items.Add(item);
                }
            }
        }

        private void AddNewBinding()
        {
            EnsureScanData();
            using (BindingEditDialog dlg = new BindingEditDialog(null, scanResult.Catalog))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    config.Keyboard.Add(dlg.ResultBinding);
                    MarkDirty();
                    RefreshData();
                }
            }
        }

        private void EditSelectedBinding()
        {
            if (bindingsListView.SelectedIndices.Count == 0) return;
            int idx = bindingsListView.SelectedIndices[0];
            if (idx < 0 || idx >= config.Keyboard.Count) return;

            EnsureScanData();
            Binding target = config.Keyboard[idx];

            using (BindingEditDialog dlg = new BindingEditDialog(target, scanResult.Catalog))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    config.Keyboard[idx] = dlg.ResultBinding;
                    MarkDirty();
                    RefreshData();
                }
            }
        }

        private void DeleteSelectedBinding()
        {
            if (bindingsListView.SelectedIndices.Count > 0)
            {
                int idx = bindingsListView.SelectedIndices[0];
                if (idx >= 0 && idx < config.Keyboard.Count)
                {
                    config.Keyboard.RemoveAt(idx);
                    MarkDirty();
                    RefreshData();
                }
            }
        }

        private void AddNewRadial()
        {
            EnsureScanData();
            using (RadialEditDialog dlg = new RadialEditDialog(null, scanResult.Catalog))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    config.Radials.Add(dlg.ResultRadial);
                    MarkDirty();
                    RefreshRadialsData();
                }
            }
        }

        private void EditSelectedRadial()
        {
            if (radialsListView.SelectedIndices.Count == 0) return;
            int idx = radialsListView.SelectedIndices[0];
            if (idx < 0 || idx >= config.Radials.Count) return;

            EnsureScanData();
            RadialMenu target = config.Radials[idx];

            using (RadialEditDialog dlg = new RadialEditDialog(target, scanResult.Catalog))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    config.Radials[idx] = dlg.ResultRadial;
                    MarkDirty();
                    RefreshRadialsData();
                }
            }
        }

        private void LoadErgonomic80Preset()
        {
            string ergoJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "nx2512-ergo-80.json");
            if (!File.Exists(ergoJsonPath)) ergoJsonPath = "nx2512-ergo-80.json";

            if (File.Exists(ergoJsonPath))
            {
                try
                {
                    config = Config.Load(ergoJsonPath);
                    configFilePath = ergoJsonPath;
                    MarkDirty();
                    RefreshData();
                    RefreshRadialsData();
                    MessageBox.Show("Пресет NX 2512 Ergonomic 80 успешно загружен!", "NXKeys Studio");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки пресета: {ex.Message}", "NXKeys Studio");
                }
            }
            else
            {
                MessageBox.Show("Файл nx2512-ergo-80.json не найден.", "NXKeys Studio");
            }
        }

        private void RefreshPlanView()
        {
            EnsureScanData();
            currentPlan = DeploymentEngine.BuildPlan(config, scanResult.Catalog);

            logBox.Clear();
            logBox.AppendText("=== DEPLOYMENT PLAN SUMMARY ===\n");
            foreach (string s in currentPlan.ActionSummary) logBox.AppendText($"{s}\n");
            logBox.AppendText("\n=== RESOLUTION REPORT ===\n");
            logBox.AppendText(currentPlan.ResolutionReport);
        }

        private async Task ApplyCurrentPlanAsync()
        {
            RefreshPlanView();
            if (currentPlan == null) return;

            bool ok = DeploymentEngine.ApplyPlan(config, currentPlan, out string backupFolder, out string err);
            if (ok)
            {
                if (config.Deployment.DryRun)
                {
                    MessageBox.Show("DryRun: план построен успешно. Файлы на диск не записывались.", "NXKeys Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"План успешно применен!\nBackup folder: {backupFolder}", "NXKeys Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show($"Ошибка применения плана:\n{err}", "NXKeys Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            await Task.CompletedTask;
        }

        private RichTextBox CreateReadOnlyBox()
        {
            return new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = elevatedColor,
                ForeColor = textColor,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.25f),
                WordWrap = false
            };
        }

        private Panel CreateMetricPanel(string title, string value, Color color)
        {
            Panel panel = new Panel
            {
                Width = 190,
                Height = 96,
                BackColor = surfaceColor,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 10, 10)
            };
            Label t = new Label { Dock = DockStyle.Top, Height = 22, Text = title, ForeColor = mutedColor, Font = new Font("Segoe UI Semibold", 8.5f) };
            Label v = new Label { Dock = DockStyle.Fill, Text = value, ForeColor = color, Font = new Font("Segoe UI Semibold", 16f), TextAlign = ContentAlignment.MiddleLeft };
            panel.Controls.Add(v);
            panel.Controls.Add(t);
            return panel;
        }

        private void AutoSizeAllListColumns()
        {
            AutoSizeListColumns(bindingsListView);
            AutoSizeListColumns(leaderSequencesListView);
            AutoSizeListColumns(radialsListView);
            AutoSizeListColumns(searchResultsListView);
            AutoSizeListColumns(backupsListView);
        }

        private void AutoSizeListColumns(ListView listView)
        {
            if (listView == null || listView.Columns.Count == 0 || listView.Width <= 0) return;

            int available = Math.Max(240, listView.ClientSize.Width - 8);
            int perColumn = Math.Max(80, available / listView.Columns.Count);
            for (int i = 0; i < listView.Columns.Count; i++)
            {
                int min = i == listView.Columns.Count - 1 ? 140 : 80;
                listView.Columns[i].Width = Math.Max(min, perColumn);
            }

            if (listView.Columns.Count > 0)
            {
                int used = 0;
                for (int i = 0; i < listView.Columns.Count - 1; i++) used += listView.Columns[i].Width;
                listView.Columns[listView.Columns.Count - 1].Width = Math.Max(140, available - used);
            }
        }

        // Custom OwnerDraw Dark ListView Helper
        private ListView CreateStyledListView()
        {
            ListView lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = surfaceColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.None,
                OwnerDraw = true
            };
            lv.Resize += (s, e) => AutoSizeListColumns(lv);

            lv.DrawColumnHeader += (sender, e) =>
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(16, 21, 28)))
                using (Pen borderPen = new Pen(borderColor))
                using (Font headerFont = new Font("Segoe UI Semibold", 8.8f))
                {
                    e.Graphics.FillRectangle(bgBrush, e.Bounds);
                    e.Graphics.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

                    TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                    Rectangle textBounds = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, e.Header.Text, headerFont, textBounds, mutedColor, flags);
                }
            };

            lv.DrawItem += (sender, e) => { e.DrawDefault = false; };

            lv.DrawSubItem += (sender, e) =>
            {
                bool isSelected = (e.ItemState & ListViewItemStates.Selected) != 0;
                Color rowBg = isSelected ? Color.FromArgb(31, 41, 55) : (e.ItemIndex % 2 == 0 ? surfaceColor : elevatedColor);
                Color rowText = isSelected ? textColor : Color.FromArgb(201, 209, 217);

                using (SolidBrush bgBrush = new SolidBrush(rowBg))
                {
                    e.Graphics.FillRectangle(bgBrush, e.Bounds);
                }

                string txt = e.SubItem.Text;
                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                Rectangle textBounds = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height);

                if (txt == "Enabled" || txt == "Active" || txt == "OK")
                {
                    DrawBadge(e.Graphics, "Active", successColor, e.Bounds);
                }
                else if (txt == "Disabled")
                {
                    DrawBadge(e.Graphics, "Disabled", mutedColor, e.Bounds);
                }
                else if (txt == "Ambiguous" || txt == "Unresolved")
                {
                    DrawBadge(e.Graphics, txt, warningColor, e.Bounds);
                }
                else
                {
                    TextRenderer.DrawText(e.Graphics, txt, e.Item.Font, textBounds, rowText, flags);
                }
            };

            return lv;
        }

        private void DrawBadge(Graphics g, string text, Color color, Rectangle bounds)
        {
            int badgeW = 75;
            int badgeH = 20;
            int x = bounds.Left + 8;
            int y = bounds.Top + (bounds.Height - badgeH) / 2;
            Rectangle rect = new Rectangle(x, y, badgeW, badgeH);

            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(30, color.R, color.G, color.B)))
            using (Pen pen = new Pen(Color.FromArgb(120, color.R, color.G, color.B)))
            using (Font badgeFont = new Font("Segoe UI Semibold", 8f))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(pen, rect);
                TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
                TextRenderer.DrawText(g, text, badgeFont, rect, color, flags);
            }
        }

        private Button CreateNavButton(string text, Action onClick)
        {
            Button btn = new Button
            {
                Width = 206,
                Height = 40,
                Text = text,
                FlatStyle = FlatStyle.Flat,
                ForeColor = textColor,
                BackColor = Color.FromArgb(10, 13, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Margin = new Padding(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Tag = text
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = elevatedColor;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private Panel CreatePage()
        {
            Panel p = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = backColor, AutoScroll = true };
            return p;
        }

        private Panel CreateCard()
        {
            Panel p = new Panel { BackColor = surfaceColor, Padding = new Padding(18) };
            return p;
        }

        private Label CreateCardTitle(string text)
        {
            return new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = text,
                ForeColor = textColor,
                Font = new Font("Segoe UI Semibold", 11.5f)
            };
        }

        private Panel CreateInfoCard(string title, string mainText, string subText, Color color)
        {
            Panel card = CreateCard();
            card.Margin = new Padding(6);

            Panel accentTop = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = color };
            Label t = new Label { Dock = DockStyle.Top, Height = 24, Text = title, ForeColor = color, Font = new Font("Segoe UI Semibold", 9f) };
            Label m = new Label { Dock = DockStyle.Top, Height = 36, Text = mainText, ForeColor = textColor, Font = new Font("Segoe UI Semibold", 13.5f) };
            Label s = new Label { Dock = DockStyle.Fill, Text = subText, ForeColor = mutedColor };

            card.Controls.Add(s);
            card.Controls.Add(m);
            card.Controls.Add(t);
            card.Controls.Add(accentTop);
            return card;
        }

        private Button CreatePrimaryButton(string text)
        {
            Button btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = accentColor,
                ForeColor = Color.FromArgb(13, 17, 23),
                Font = new Font("Segoe UI Semibold", 9.5f),
                Height = 36,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Button CreateFlatButton(string text, Color color)
        {
            Button btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = elevatedColor,
                ForeColor = color,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Height = 36,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private TextBox CreateTextBox()
        {
            return new TextBox { BackColor = elevatedColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle, Height = 28 };
        }

        private CheckBox CreateCheckBox(string text)
        {
            return new CheckBox { Text = text, ForeColor = textColor, AutoSize = true, Margin = new Padding(0, 6, 0, 6) };
        }
    }
}
