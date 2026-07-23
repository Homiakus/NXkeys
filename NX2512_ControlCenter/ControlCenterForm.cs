using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.Services;

namespace NX2512_ControlCenter
{
    public sealed class ControlCenterForm : Form
    {
        private readonly string configPath;
        private string catalogPath;
        private Config config;
        private NxBridgeContext context;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 1000 };
        private readonly List<ApiRow> apiRows = new List<ApiRow>();

        private readonly Color bg = Color.FromArgb(13, 17, 23);
        private readonly Color panel = Color.FromArgb(22, 27, 34);
        private readonly Color raised = Color.FromArgb(33, 38, 45);
        private readonly Color fg = Color.FromArgb(240, 246, 252);
        private readonly Color muted = Color.FromArgb(139, 148, 158);
        private readonly Color accent = Color.FromArgb(56, 189, 248);

        private Label status;
        private Label coverageValue;
        private Label bridgeValue;
        private ListView leaderList;
        private TextBox apiQuery;
        private ListView apiList;
        private RichTextBox apiDetails;
        private ComboBox trigger;
        private NumericUpDown firstTimeout;
        private NumericUpDown nextTimeout;
        private CheckBox nxOnly;
        private TextBox catalogBox;

        public ControlCenterForm(string configPath, string catalogPath)
        {
            this.configPath = Path.GetFullPath(configPath);
            this.catalogPath = catalogPath;
            config = LoadConfig(this.configPath);

            Text = "NXKeys Adaptive Control Center";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 560);
            Size = new Size(1260, 780);
            BackColor = bg;
            ForeColor = fg;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildUi();
            timer.Tick += (_, _) => RefreshRuntime();
            timer.Start();
            Shown += (_, _) =>
            {
                DiscoverCatalog();
                LoadApiCatalog();
                RefreshAll();
            };
            FormClosed += (_, _) => timer.Dispose();
        }

        private Config LoadConfig(string path)
        {
            try
            {
                Config loaded = Config.Load(path);
                loaded.ApplyDefaults();
                return loaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить профиль. Используется безопасный профиль по умолчанию.\n\n" + ex.Message,
                    "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                var fallback = new Config();
                fallback.ApplyDefaults();
                return fallback;
            }
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = bg };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            Controls.Add(root);

            var header = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(18, 10, 18, 8) };
            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Контекстное Leader-меню · покрытие workflows · поиск NXOpen/UFUN",
                ForeColor = muted,
                TextAlign = ContentAlignment.BottomLeft
            });
            header.Controls.Add(new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = "NXKeys Control Center",
                ForeColor = fg,
                Font = new Font("Segoe UI Semibold", 16f)
            });
            root.Controls.Add(header, 0, 0);

            var tabs = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal };
            tabs.TabPages.Add(BuildOverview());
            tabs.TabPages.Add(BuildLeader());
            tabs.TabPages.Add(BuildApiExplorer());
            tabs.TabPages.Add(BuildSettings());
            root.Controls.Add(tabs, 0, 1);

            status = new Label { Dock = DockStyle.Fill, BackColor = panel, ForeColor = muted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), Text = "Готово" };
            root.Controls.Add(status, 0, 2);
        }

        private TabPage BuildOverview()
        {
            TabPage page = Page("Обзор");
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(14) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            coverageValue = Metric(layout, 0, "ПОКРЫТИЕ LEADER");
            bridgeValue = Metric(layout, 1, "NX BRIDGE");
            var text = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = raised, ForeColor = fg, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 10f) };
            text.Name = "overviewDetails";
            layout.SetColumnSpan(text, 2);
            layout.Controls.Add(text, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildLeader()
        {
            TabPage page = Page("Adaptive Leader");
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            bar.Controls.Add(ActionButton("Запустить Leader", (_, _) => LaunchHotkeyStudio(true), true));
            bar.Controls.Add(ActionButton("Открыть Studio", (_, _) => LaunchHotkeyStudio(false), false));
            bar.Controls.Add(ActionButton("Сохранить профиль", (_, _) => SaveProfile(), false));
            leaderList = List();
            leaderList.Columns.Add("Последовательность", 130);
            leaderList.Columns.Add("Модуль", 160);
            leaderList.Columns.Add("Команда", 250);
            leaderList.Columns.Add("BUTTON ID", 300);
            leaderList.Columns.Add("Контекст", 230);
            layout.Controls.Add(bar, 0, 0);
            layout.Controls.Add(leaderList, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildApiExplorer()
        {
            TabPage page = Page("NX API Explorer");
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2, Padding = new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            var search = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            apiQuery = new TextBox { Dock = DockStyle.Fill, BackColor = raised, ForeColor = fg, PlaceholderText = "Как через NXOpen создать выдавливание?" };
            apiQuery.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) SearchApi(); };
            search.Controls.Add(apiQuery, 0, 0);
            search.Controls.Add(ActionButton("Спросить API", (_, _) => SearchApi(), true), 1, 0);
            layout.SetColumnSpan(search, 2);
            layout.Controls.Add(search, 0, 0);
            apiList = List();
            apiList.Columns.Add("Тип", 150);
            apiList.Columns.Add("Имя", 330);
            apiList.Columns.Add("UI-команда", 220);
            apiList.SelectedIndexChanged += (_, _) => ShowApi();
            apiDetails = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = raised, ForeColor = fg, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9f) };
            layout.Controls.Add(apiList, 0, 1);
            layout.Controls.Add(apiDetails, 1, 1);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildSettings()
        {
            TabPage page = Page("Настройки");
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(24), BackColor = panel };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            trigger = new ComboBox { Dock = DockStyle.Fill, BackColor = raised, ForeColor = fg };
            trigger.Items.AddRange(new object[] { "CapsLock", "F12", "F11", "Pause", "Scroll" });
            firstTimeout = Number(1000, 60000, 1000);
            nextTimeout = Number(1000, 60000, 1000);
            nxOnly = new CheckBox { Text = "Перехватывать Leader только при активном NX", AutoSize = true, ForeColor = fg, BackColor = panel };
            catalogBox = new TextBox { Dock = DockStyle.Fill, BackColor = raised, ForeColor = fg };
            AddRow(table, 0, "Клавиша Leader", trigger, null);
            AddRow(table, 1, "Первая клавиша, мс", firstTimeout, null);
            AddRow(table, 2, "Следующая клавиша, мс", nextTimeout, null);
            AddRow(table, 3, "Контекст", nxOnly, null);
            AddRow(table, 4, "Каталог NX API", catalogBox, ActionButton("Выбрать", (_, _) => BrowseCatalog(), false));
            AddRow(table, 5, string.Empty, ActionButton("Сохранить настройки", (_, _) => SaveSettings(), true), null);
            page.Controls.Add(table);
            return page;
        }

        private void RefreshAll()
        {
            RefreshRuntime();
            RefreshLeaderList();
            trigger.Text = config.LeaderKey?.TriggerKey ?? "CapsLock";
            firstTimeout.Value = Clamp(config.LeaderKey?.FirstKeyTimeoutMs ?? 20000, firstTimeout.Minimum, firstTimeout.Maximum);
            nextTimeout.Value = Clamp(config.LeaderKey?.NextKeyTimeoutMs ?? 20000, nextTimeout.Minimum, nextTimeout.Maximum);
            nxOnly.Checked = config.LeaderKey?.HookOnlyWhenNXActive ?? true;
            catalogBox.Text = catalogPath ?? string.Empty;
        }

        private void RefreshRuntime()
        {
            context = NxCommandBridgeClient.ReadContext();
            int total = config.LeaderKey?.Sequences?.Count(x => x.Enabled) ?? 0;
            int verified = config.LeaderKey?.Sequences?.Count(x => x.Enabled && !string.IsNullOrWhiteSpace(x.Command?.ID)) ?? 0;
            double coverage = total == 0 ? 0 : verified * 100.0 / total;
            if (coverageValue != null) coverageValue.Text = coverage.ToString("F1") + "%";
            if (bridgeValue != null) bridgeValue.Text = context == null ? "OFFLINE" : context.IsFresh ? "ONLINE" : "STALE";
            RichTextBox details = Controls.Find("overviewDetails", true).FirstOrDefault() as RichTextBox;
            if (details != null)
            {
                details.Text = $"Профиль: {configPath}\nВерсия NX: {config.Profile?.NXVersion}\nПоследовательностей: {total}\nТочных BUTTON ID: {verified}\n\n" +
                               $"Модуль: {context?.ModuleLabel ?? "неизвестен"} ({context?.ModuleId ?? "-"})\nВыбрано: {context?.SelectionCount.ToString() ?? "неизвестно"}\n" +
                               $"Рабочая деталь: {context?.WorkPartAvailable.ToString() ?? "неизвестно"}\nРезультат Bridge: {context?.LastResult ?? "-"}\n{context?.LastMessage ?? string.Empty}";
            }
        }

        private void RefreshLeaderList()
        {
            if (leaderList == null) return;
            string module = context?.ModuleId ?? "modeling";
            List<LeaderSequenceItem> ranked = AdaptiveLeaderPolicy.Rank(config.LeaderKey?.Sequences, context, null, module, true);
            leaderList.BeginUpdate();
            leaderList.Items.Clear();
            foreach (LeaderSequenceItem item in ranked)
            {
                LeaderCommandAvailability state = AdaptiveLeaderPolicy.Evaluate(item, context, null, module);
                var row = new ListViewItem(item.Sequence);
                row.SubItems.Add(string.IsNullOrWhiteSpace(item.ModuleID) ? item.Category : item.ModuleID);
                row.SubItems.Add(item.Command?.Name ?? item.Notes);
                row.SubItems.Add(item.Command?.ID ?? string.Empty);
                row.SubItems.Add(string.IsNullOrWhiteSpace(state.Reason) ? "Готово" : state.Reason);
                row.ForeColor = state.CanExecute ? fg : muted;
                leaderList.Items.Add(row);
            }
            leaderList.EndUpdate();
        }

        private void DiscoverCatalog()
        {
            if (!string.IsNullOrWhiteSpace(catalogPath) && Directory.Exists(catalogPath)) return;
            string env = Environment.GetEnvironmentVariable("NXKEYS_CATALOG_DIR");
            if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env)) { catalogPath = env; return; }
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NXKeys", "catalog");
            if (Directory.Exists(root))
            {
                catalogPath = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                    .Where(HasCatalogFiles).OrderByDescending(Directory.GetLastWriteTimeUtc).FirstOrDefault();
            }
        }

        private bool HasCatalogFiles(string path)
        {
            return File.Exists(Path.Combine(path, "04_nxopen_members.csv")) || File.Exists(Path.Combine(path, "06_ui_commands_buttons.csv"));
        }

        private void LoadApiCatalog()
        {
            apiRows.Clear();
            if (string.IsNullOrWhiteSpace(catalogPath) || !Directory.Exists(catalogPath))
            {
                status.Text = "API-каталог не найден. Укажите экспорт NX2512_Catalog_Studio в настройках.";
                return;
            }
            foreach (string file in new[] { "04_nxopen_members.csv", "05_nxopen_entry_points.csv", "06_ui_commands_buttons.csv", "07_ufun_functions.csv", "08_ui_command_api_candidates.csv" })
            {
                string path = Path.Combine(catalogPath, file);
                if (!File.Exists(path)) continue;
                foreach (string line in File.ReadLines(path).Skip(1))
                {
                    string[] cells = ParseCsv(line);
                    if (cells.Length == 0) continue;
                    apiRows.Add(new ApiRow
                    {
                        Kind = Path.GetFileNameWithoutExtension(file),
                        Name = cells.ElementAtOrDefault(0) ?? string.Empty,
                        UiCommand = cells.FirstOrDefault(x => x.StartsWith("UG_", StringComparison.OrdinalIgnoreCase)) ?? string.Empty,
                        Raw = string.Join(" | ", cells)
                    });
                }
            }
            status.Text = $"API-каталог загружен: {apiRows.Count:N0} записей";
        }

        private void SearchApi()
        {
            string query = ExpandQuery(apiQuery.Text);
            string[] tokens = query.Split(new[] { ' ', '.', ':', '/', '\\', '(', ')', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.Length > 1).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var results = apiRows.Select(row => new { Row = row, Score = tokens.Count(token => row.Raw.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) })
                .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Row.Name).Take(200).ToList();
            apiList.BeginUpdate();
            apiList.Items.Clear();
            foreach (var result in results)
            {
                var item = new ListViewItem(result.Row.Kind);
                item.SubItems.Add(result.Row.Name);
                item.SubItems.Add(result.Row.UiCommand);
                item.Tag = result.Row;
                apiList.Items.Add(item);
            }
            apiList.EndUpdate();
            apiDetails.Text = results.Count == 0 ? "Совпадения не найдены." : Explain(results[0].Row);
            if (apiList.Items.Count > 0) apiList.Items[0].Selected = true;
            status.Text = $"Найдено кандидатов: {results.Count}";
        }

        private string ExpandQuery(string value)
        {
            string result = value ?? string.Empty;
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["выдавливание"] = "extrude feature builder",
                ["эскиз"] = "sketch",
                ["отверстие"] = "hole feature builder",
                ["скругление"] = "edge blend fillet",
                ["фаска"] = "chamfer",
                ["сборка"] = "assembly component",
                ["чертеж"] = "drawing drafting",
                ["размер"] = "dimension",
                ["выбор"] = "selection manager",
                ["тело"] = "body",
                ["грань"] = "face"
            };
            foreach (var pair in map) if (result.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0) result += " " + pair.Value;
            return result;
        }

        private void ShowApi()
        {
            if (apiList.SelectedItems.Count == 0) return;
            apiDetails.Text = Explain(apiList.SelectedItems[0].Tag as ApiRow);
        }

        private string Explain(ApiRow row)
        {
            if (row == null) return string.Empty;
            return $"Тип: {row.Kind}\nИмя: {row.Name}\nUI BUTTON ID: {row.UiCommand}\n\nИсходная запись:\n{row.Raw}\n\nВажно: crosswalk UI→API является кандидатом. Перед автоматическим выполнением проверьте сигнатуру и контекст NXOpen.";
        }

        private void SaveSettings()
        {
            config.LeaderKey.TriggerKey = trigger.Text.Trim();
            config.LeaderKey.FirstKeyTimeoutMs = (int)firstTimeout.Value;
            config.LeaderKey.NextKeyTimeoutMs = (int)nextTimeout.Value;
            config.LeaderKey.HookOnlyWhenNXActive = nxOnly.Checked;
            catalogPath = catalogBox.Text.Trim();
            SaveProfile();
            LoadApiCatalog();
        }

        private void SaveProfile()
        {
            try { config.Save(configPath); status.Text = "Профиль сохранён: " + configPath; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void LaunchHotkeyStudio(bool background)
        {
            string exe = Path.Combine(AppContext.BaseDirectory, "NX2512_HotkeyStudio.exe");
            if (!File.Exists(exe))
            {
                string parent = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)) ?? AppContext.BaseDirectory;
                exe = Path.Combine(parent, "NX2512_HotkeyStudio.exe");
            }
            if (!File.Exists(exe))
            {
                MessageBox.Show("NX2512_HotkeyStudio.exe не найден в папке Control Center или в родительском managed root.", "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var start = new ProcessStartInfo(exe)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory
                };
                start.ArgumentList.Add(background ? "--ensure-background" : "--gui");
                start.ArgumentList.Add("--config");
                start.ArgumentList.Add(configPath);
                Process.Start(start);
                status.Text = background ? "Leader запущен или уже работает." : "Открывается NXKeys Studio.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка запуска NXKeys Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BrowseCatalog()
        {
            using var dialog = new FolderBrowserDialog { Description = "Выберите NX2512_Full_Function_API_Catalog_*", ShowNewFolderButton = false };
            if (dialog.ShowDialog(this) == DialogResult.OK) catalogBox.Text = dialog.SelectedPath;
        }

        private TabPage Page(string text) => new TabPage(text) { BackColor = bg, ForeColor = fg };

        private Label Metric(TableLayoutPanel layout, int column, string caption)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = panel, Margin = new Padding(6), Padding = new Padding(16) };
            var value = new Label { Dock = DockStyle.Fill, ForeColor = accent, Font = new Font("Segoe UI Semibold", 24f), TextAlign = ContentAlignment.MiddleLeft };
            card.Controls.Add(value);
            card.Controls.Add(new Label { Dock = DockStyle.Top, Height = 26, Text = caption, ForeColor = muted, Font = new Font("Segoe UI Semibold", 9f) });
            layout.Controls.Add(card, column, 0);
            return value;
        }

        private Button ActionButton(string text, EventHandler handler, bool primary)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = primary ? accent : raised, ForeColor = primary ? bg : fg, Margin = new Padding(0, 3, 8, 3) };
            button.FlatAppearance.BorderSize = 0;
            button.Click += handler;
            return button;
        }

        private ListView List() => new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, BackColor = panel, ForeColor = fg, BorderStyle = BorderStyle.None };

        private NumericUpDown Number(decimal min, decimal max, decimal step) => new NumericUpDown { Minimum = min, Maximum = max, Increment = step, Width = 180, BackColor = raised, ForeColor = fg };

        private void AddRow(TableLayoutPanel table, int row, string label, Control editor, Control action)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            table.Controls.Add(new Label { Dock = DockStyle.Fill, Text = label, ForeColor = muted, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            editor.Dock = editor is CheckBox ? DockStyle.Left : DockStyle.Fill;
            table.Controls.Add(editor, 1, row);
            if (action != null) { action.Dock = DockStyle.Fill; table.Controls.Add(action, 2, row); }
        }

        private static decimal Clamp(decimal value, decimal min, decimal max) => Math.Max(min, Math.Min(max, value));

        private static string[] ParseCsv(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (character == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"') { current.Append('"'); index++; }
                    else quoted = !quoted;
                }
                else if (character == ',' && !quoted) { values.Add(current.ToString()); current.Clear(); }
                else current.Append(character);
            }
            values.Add(current.ToString());
            return values.ToArray();
        }

        private sealed class ApiRow
        {
            public string Kind { get; set; }
            public string Name { get; set; }
            public string UiCommand { get; set; }
            public string Raw { get; set; }
        }
    }
}
