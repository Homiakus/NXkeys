using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.Services;
using Binding = NX2512_HotkeyStudio.Models.Binding;

namespace NX2512_HotkeyStudio.UI
{
    public sealed class BindingEditDialog : Form
    {
        public Binding ResultBinding { get; private set; }
        private readonly CatalogIndex catalog;

        private readonly Color backColor = Color.FromArgb(13, 17, 23);
        private readonly Color surfaceColor = Color.FromArgb(22, 27, 34);
        private readonly Color elevatedColor = Color.FromArgb(33, 38, 45);
        private readonly Color borderColor = Color.FromArgb(48, 54, 61);
        private readonly Color textColor = Color.FromArgb(240, 246, 252);
        private readonly Color mutedColor = Color.FromArgb(139, 148, 158);
        private readonly Color accentColor = Color.FromArgb(56, 189, 248);

        private CheckBox ctrlCheck;
        private CheckBox altCheck;
        private CheckBox shiftCheck;
        private ComboBox keyCombo;
        private Label hotkeyBadgeLabel;

        private TextBox commandNameBox;
        private TextBox commandIdBox;
        private ComboBox scopeCombo;
        private CheckBox enabledCheck;

        private TextBox searchBox;
        private ListView searchResultsView;

        public BindingEditDialog(Binding bindingToEdit, CatalogIndex catalog)
        {
            this.catalog = catalog ?? new CatalogIndex();
            ResultBinding = bindingToEdit != null ? CloneBinding(bindingToEdit) : new Binding { Enabled = true, Scope = "Global" };

            Text = bindingToEdit != null ? "Редактирование привязки клавиш — NXKeys" : "Новая привязка клавиш — NXKeys";
            Size = new Size(840, 700);
            MinimumSize = new Size(720, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            BackColor = backColor;
            ForeColor = textColor;
            Font = new Font("Segoe UI", 9.5f);

            BuildUI();
            PopulateFields();
        }

        private void BuildUI()
        {
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(18),
                AutoScroll = true
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

            // Top Panel: Hotkey & Scope settings
            Panel topCard = new Panel { Dock = DockStyle.Fill, BackColor = surfaceColor, Padding = new Padding(16) };
            Label keyTitle = new Label { Dock = DockStyle.Top, Height = 26, Text = "1. Сочетание клавиш и область применения", ForeColor = accentColor, Font = new Font("Segoe UI Semibold", 10.5f) };

            FlowLayoutPanel keyRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 4, 0, 0) };
            ctrlCheck = new CheckBox { Text = "Ctrl", ForeColor = textColor, AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
            altCheck = new CheckBox { Text = "Alt", ForeColor = textColor, AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
            shiftCheck = new CheckBox { Text = "Shift", ForeColor = textColor, AutoSize = true, Margin = new Padding(0, 6, 12, 0) };

            ctrlCheck.CheckedChanged += (s, e) => UpdateHotkeyBadge();
            altCheck.CheckedChanged += (s, e) => UpdateHotkeyBadge();
            shiftCheck.CheckedChanged += (s, e) => UpdateHotkeyBadge();

            keyCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = elevatedColor, ForeColor = textColor, Width = 120, FlatStyle = FlatStyle.Flat };
            string[] keys = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
                              "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
                              "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
                              "Home", "End", "Tab", "Space", "Insert", "Delete", "Up", "Down", "Left", "Right" };
            keyCombo.Items.AddRange(keys);
            keyCombo.SelectedIndex = 0;
            keyCombo.SelectedIndexChanged += (s, e) => UpdateHotkeyBadge();

            hotkeyBadgeLabel = new Label
            {
                Text = "[ Ctrl + A ]",
                ForeColor = accentColor,
                Font = new Font("Segoe UI Semibold", 10.5f),
                AutoSize = true,
                Margin = new Padding(20, 6, 0, 0)
            };

            keyRow.Controls.Add(ctrlCheck);
            keyRow.Controls.Add(altCheck);
            keyRow.Controls.Add(shiftCheck);
            keyRow.Controls.Add(keyCombo);
            keyRow.Controls.Add(hotkeyBadgeLabel);

            TableLayoutPanel cmdRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 100, ColumnCount = 2, RowCount = 3 };
            cmdRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            cmdRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            Label l1 = new Label { Text = "Название команды (Name):", ForeColor = mutedColor, Dock = DockStyle.Fill };
            commandNameBox = new TextBox { BackColor = elevatedColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };

            Label l2 = new Label { Text = "Идентификатор (BUTTON ID):", ForeColor = mutedColor, Dock = DockStyle.Fill };
            commandIdBox = new TextBox { BackColor = elevatedColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };

            Label l3 = new Label { Text = "Область (Scope / Context):", ForeColor = mutedColor, Dock = DockStyle.Fill };
            scopeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = elevatedColor, ForeColor = textColor, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat };
            scopeCombo.Items.AddRange(new[] { "Global", "Modeling", "Sketch", "Drafting", "Assembly", "Selection", "Graphics", "Context" });
            scopeCombo.SelectedIndex = 0;

            cmdRow.Controls.Add(l1, 0, 0);
            cmdRow.Controls.Add(l2, 1, 0);
            cmdRow.Controls.Add(commandNameBox, 0, 1);
            cmdRow.Controls.Add(commandIdBox, 1, 1);
            cmdRow.Controls.Add(l3, 0, 2);
            cmdRow.Controls.Add(scopeCombo, 1, 2);

            enabledCheck = new CheckBox { Text = "Привязка активна", ForeColor = textColor, Checked = true, Dock = DockStyle.Bottom, Height = 24 };

            topCard.Controls.Add(enabledCheck);
            topCard.Controls.Add(cmdRow);
            topCard.Controls.Add(keyRow);
            topCard.Controls.Add(keyTitle);

            // Middle Panel: Catalog Command Search & Auto-complete
            Panel searchCard = new Panel { Dock = DockStyle.Fill, BackColor = surfaceColor, Padding = new Padding(16), Margin = new Padding(0, 12, 0, 0) };
            Label searchTitle = new Label { Dock = DockStyle.Top, Height = 26, Text = "2. Интерактивный поиск по каталогу 32 000+ команд NX 2512", ForeColor = accentColor, Font = new Font("Segoe UI Semibold", 10.5f) };

            Panel searchBar = new Panel { Dock = DockStyle.Top, Height = 38 };
            searchBox = new TextBox { Dock = DockStyle.Left, Width = 380, BackColor = elevatedColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            searchBox.TextChanged += (s, e) => LiveSearch();

            Button btnSearch = new Button
            {
                Text = "Поиск",
                Dock = DockStyle.Left,
                Width = 110,
                FlatStyle = FlatStyle.Flat,
                BackColor = accentColor,
                ForeColor = Color.FromArgb(13, 17, 23),
                Font = new Font("Segoe UI Semibold", 9.5f),
                Margin = new Padding(8, 0, 0, 0)
            };
            btnSearch.FlatAppearance.BorderSize = 0;
            btnSearch.Click += (s, e) => LiveSearch();

            searchBar.Controls.Add(btnSearch);
            searchBar.Controls.Add(searchBox);

            searchResultsView = new ListView
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
            searchResultsView.Columns.Add("BUTTON ID", 230);
            searchResultsView.Columns.Add("Метка (Label)", 220);
            searchResultsView.Columns.Add("Score", 80);
            searchResultsView.Columns.Add("NXOpen API Candidate", 260);
            searchResultsView.Resize += (s, e) => AutoSizeColumns(searchResultsView);

            searchResultsView.DrawColumnHeader += (sender, e) =>
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

            searchResultsView.DrawItem += (sender, e) => { e.DrawDefault = false; };

            searchResultsView.DrawSubItem += (sender, e) =>
            {
                bool isSelected = (e.ItemState & ListViewItemStates.Selected) != 0;
                Color rowBg = isSelected ? Color.FromArgb(31, 41, 55) : (e.ItemIndex % 2 == 0 ? surfaceColor : elevatedColor);
                Color rowText = isSelected ? textColor : Color.FromArgb(201, 209, 217);

                using (SolidBrush bgBrush = new SolidBrush(rowBg))
                {
                    e.Graphics.FillRectangle(bgBrush, e.Bounds);
                }

                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                Rectangle textBounds = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font, textBounds, rowText, flags);
            };

            searchResultsView.DoubleClick += (s, e) => SelectSearchCandidate();

            searchCard.Controls.Add(searchResultsView);
            searchCard.Controls.Add(searchBar);
            searchCard.Controls.Add(searchTitle);

            // Bottom Panel: Action Buttons
            FlowLayoutPanel bottomBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };

            Button saveBtn = new Button
            {
                Text = "Сохранить",
                Width = 130,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = accentColor,
                ForeColor = Color.FromArgb(13, 17, 23),
                Font = new Font("Segoe UI Semibold", 9.5f),
                Cursor = Cursors.Hand
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += (s, e) => SaveAndClose();

            Button cancelBtn = new Button
            {
                Text = "Отмена",
                Width = 110,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = elevatedColor,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Cursor = Cursors.Hand
            };
            cancelBtn.FlatAppearance.BorderSize = 0;
            cancelBtn.Click += (s, e) => DialogResult = DialogResult.Cancel;

            bottomBar.Controls.Add(saveBtn);
            bottomBar.Controls.Add(cancelBtn);

            mainLayout.Controls.Add(topCard, 0, 0);
            mainLayout.Controls.Add(searchCard, 0, 1);
            mainLayout.Controls.Add(bottomBar, 0, 2);

            Controls.Add(mainLayout);
        }

        private void UpdateHotkeyBadge()
        {
            List<string> mods = new List<string>();
            if (ctrlCheck.Checked) mods.Add("Ctrl");
            if (altCheck.Checked) mods.Add("Alt");
            if (shiftCheck.Checked) mods.Add("Shift");
            mods.Add(keyCombo.SelectedItem?.ToString() ?? "A");

            if (hotkeyBadgeLabel != null)
            {
                hotkeyBadgeLabel.Text = "[ " + string.Join(" + ", mods) + " ]";
            }
        }

        private void PopulateFields()
        {
            if (ResultBinding == null) return;

            string sc = ResultBinding.Shortcut ?? string.Empty;
            ctrlCheck.Checked = sc.IndexOf("Ctrl", StringComparison.OrdinalIgnoreCase) >= 0;
            altCheck.Checked = sc.IndexOf("Alt", StringComparison.OrdinalIgnoreCase) >= 0;
            shiftCheck.Checked = sc.IndexOf("Shift", StringComparison.OrdinalIgnoreCase) >= 0;

            string keyToken = sc.Split('+').LastOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(keyToken))
            {
                int idx = keyCombo.FindStringExact(keyToken);
                if (idx >= 0) keyCombo.SelectedIndex = idx;
            }

            commandNameBox.Text = ResultBinding.Command?.Name ?? string.Empty;
            commandIdBox.Text = ResultBinding.Command?.ID ?? string.Empty;

            if (!string.IsNullOrEmpty(ResultBinding.Scope))
            {
                int idx = scopeCombo.FindStringExact(ResultBinding.Scope);
                if (idx >= 0) scopeCombo.SelectedIndex = idx;
            }

            enabledCheck.Checked = ResultBinding.Enabled;
            UpdateHotkeyBadge();

            if (!string.IsNullOrEmpty(commandNameBox.Text))
            {
                searchBox.Text = commandNameBox.Text;
                LiveSearch();
            }
        }

        private void LiveSearch()
        {
            string query = searchBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            searchResultsView.Items.Clear();
            List<string> qList = new List<string> { query };
            List<CandidateMatch> matches = new List<CandidateMatch>();

            foreach (var kvp in catalog.Commands)
            {
                CommandItem item = kvp.Value;
                double score = CommandResolver.ScoreCommand(qList, item);
                if (score > 0.35)
                {
                    string topApi = item.ApiCandidates.Count > 0 ? item.ApiCandidates[0].ApiTarget : string.Empty;
                    matches.Add(new CandidateMatch
                    {
                        ID = item.ID,
                        Label = item.DisplayLabel,
                        Score = score,
                        ApiMatch = topApi
                    });
                }
            }

            matches.Sort((a, b) => b.Score.CompareTo(a.Score));

            foreach (var m in matches.Take(50))
            {
                ListViewItem item = new ListViewItem(m.ID);
                item.SubItems.Add(m.Label);
                item.SubItems.Add(m.Score.ToString("F2"));
                item.SubItems.Add(m.ApiMatch);
                searchResultsView.Items.Add(item);
            }
            AutoSizeColumns(searchResultsView);
        }

        private static void AutoSizeColumns(ListView listView)
        {
            if (listView == null || listView.Columns.Count == 0 || listView.ClientSize.Width <= 0) return;
            int available = Math.Max(320, listView.ClientSize.Width - 8);
            int baseWidth = Math.Max(80, available / listView.Columns.Count);
            for (int i = 0; i < listView.Columns.Count; i++) listView.Columns[i].Width = baseWidth;
            int used = 0;
            for (int i = 0; i < listView.Columns.Count - 1; i++) used += listView.Columns[i].Width;
            listView.Columns[listView.Columns.Count - 1].Width = Math.Max(160, available - used);
        }

        private void SelectSearchCandidate()
        {
            if (searchResultsView.SelectedItems.Count > 0)
            {
                var item = searchResultsView.SelectedItems[0];
                commandIdBox.Text = item.Text;
                if (string.IsNullOrWhiteSpace(commandNameBox.Text) || commandNameBox.Text == "New Command")
                {
                    commandNameBox.Text = item.SubItems[1].Text;
                }
            }
        }

        private void SaveAndClose()
        {
            List<string> mods = new List<string>();
            if (ctrlCheck.Checked) mods.Add("Ctrl");
            if (altCheck.Checked) mods.Add("Alt");
            if (shiftCheck.Checked) mods.Add("Shift");
            mods.Add(keyCombo.SelectedItem?.ToString() ?? "A");

            ResultBinding.Shortcut = string.Join("+", mods);
            if (ResultBinding.Command == null) ResultBinding.Command = new CommandRef();

            ResultBinding.Command.Name = commandNameBox.Text.Trim();
            ResultBinding.Command.ID = commandIdBox.Text.Trim();
            ResultBinding.Scope = scopeCombo.SelectedItem?.ToString() ?? "Global";
            ResultBinding.Enabled = enabledCheck.Checked;

            DialogResult = DialogResult.OK;
        }

        private static Binding CloneBinding(Binding b)
        {
            return new Binding
            {
                Shortcut = b.Shortcut,
                Command = new CommandRef
                {
                    ID = b.Command?.ID ?? string.Empty,
                    Name = b.Command?.Name ?? string.Empty,
                    Aliases = b.Command?.Aliases != null ? new List<string>(b.Command.Aliases) : new List<string>()
                },
                Scope = b.Scope,
                Enabled = b.Enabled,
                Notes = b.Notes
            };
        }
    }
}
