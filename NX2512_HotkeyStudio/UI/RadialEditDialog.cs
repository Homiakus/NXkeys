using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;
using Binding = NX2512_HotkeyStudio.Models.Binding;

namespace NX2512_HotkeyStudio.UI
{
    public sealed class RadialEditDialog : Form
    {
        public RadialMenu ResultRadial { get; private set; }
        private readonly CatalogIndex catalog;

        private readonly Color backColor = Color.FromArgb(13, 17, 23);
        private readonly Color surfaceColor = Color.FromArgb(22, 27, 34);
        private readonly Color elevatedColor = Color.FromArgb(33, 38, 45);
        private readonly Color borderColor = Color.FromArgb(48, 54, 61);
        private readonly Color textColor = Color.FromArgb(240, 246, 252);
        private readonly Color mutedColor = Color.FromArgb(139, 148, 158);
        private readonly Color accentColor = Color.FromArgb(56, 189, 248);

        private TextBox radialNameBox;
        private TextBox triggerBox;
        private CheckBox enabledCheck;
        private ListView itemsListView;
        private Dictionary<string, Button> dirButtons;

        public RadialEditDialog(RadialMenu radialToEdit, CatalogIndex catalog)
        {
            this.catalog = catalog ?? new CatalogIndex();
            ResultRadial = radialToEdit != null ? CloneRadial(radialToEdit) : new RadialMenu { Enabled = true };

            Text = radialToEdit != null ? "Редактирование радиального меню — NXKeys" : "Новое радиальное меню — NXKeys";
            Size = new Size(780, 620);
            MinimumSize = new Size(680, 520);
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

            // Top Card
            Panel topCard = new Panel { Dock = DockStyle.Fill, BackColor = surfaceColor, Padding = new Padding(16) };
            Label title = new Label { Dock = DockStyle.Top, Height = 24, Text = "Параметры радиального меню", ForeColor = accentColor, Font = new Font("Segoe UI Semibold", 10.5f) };

            TableLayoutPanel pnl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            Label l1 = new Label { Text = "Название меню:", ForeColor = mutedColor, Dock = DockStyle.Fill };
            radialNameBox = new TextBox { BackColor = elevatedColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };

            Label l2 = new Label { Text = "Триггер (Горячая клавиша / Жест):", ForeColor = mutedColor, Dock = DockStyle.Fill };
            triggerBox = new TextBox { BackColor = elevatedColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };

            pnl.Controls.Add(l1, 0, 0);
            pnl.Controls.Add(l2, 1, 0);
            pnl.Controls.Add(radialNameBox, 0, 1);
            pnl.Controls.Add(triggerBox, 1, 1);

            enabledCheck = new CheckBox { Text = "Радиальное меню активно", ForeColor = textColor, Checked = true, Dock = DockStyle.Bottom, Height = 24 };

            topCard.Controls.Add(pnl);
            topCard.Controls.Add(enabledCheck);
            topCard.Controls.Add(title);

            // Middle Card: Visual Compass + List Split Container
            Panel middleCard = new Panel { Dock = DockStyle.Fill, BackColor = surfaceColor, Padding = new Padding(16), Margin = new Padding(0, 12, 0, 0) };
            Label midTitle = new Label { Dock = DockStyle.Top, Height = 24, Text = "Секторы меню (8 Направлений: N, NE, E, SE, S, SW, W, NW)", ForeColor = accentColor, Font = new Font("Segoe UI Semibold", 10.5f) };

            TableLayoutPanel middleSplit = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            middleSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            middleSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Visual Compass Wheel
            Panel compassPanel = new Panel { Dock = DockStyle.Fill, BackColor = elevatedColor, Margin = new Padding(0, 0, 12, 0) };
            BuildCompassButtons(compassPanel);

            itemsListView = new ListView
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
            itemsListView.Columns.Add("Направление", 100);
            itemsListView.Columns.Add("Команда NX", 200);
            itemsListView.Columns.Add("BUTTON ID", 200);
            itemsListView.Resize += (s, e) => AutoSizeColumns(itemsListView);

            itemsListView.DrawColumnHeader += (sender, e) =>
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

            itemsListView.DrawItem += (sender, e) => { e.DrawDefault = false; };

            itemsListView.DrawSubItem += (sender, e) =>
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

            itemsListView.DoubleClick += (s, e) => EditSelectedDirection();

            middleSplit.Controls.Add(compassPanel, 0, 0);
            middleSplit.Controls.Add(itemsListView, 1, 0);

            middleCard.Controls.Add(middleSplit);
            middleCard.Controls.Add(midTitle);

            // Bottom Buttons
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
            mainLayout.Controls.Add(middleCard, 0, 1);
            mainLayout.Controls.Add(bottomBar, 0, 2);

            Controls.Add(mainLayout);
        }

        private void BuildCompassButtons(Panel pnl)
        {
            dirButtons = new Dictionary<string, Button>();
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

            // 3x3 Grid Layout for compass positions
            int btnW = 58;
            int btnH = 46;
            int margin = 8;
            int startX = 14;
            int startY = 14;

            (int r, int c)[] pos = new (int, int)[]
            {
                (0, 1), // N
                (0, 2), // NE
                (1, 2), // E
                (2, 2), // SE
                (2, 1), // S
                (2, 0), // SW
                (1, 0), // W
                (0, 0)  // NW
            };

            for (int i = 0; i < dirs.Length; i++)
            {
                string d = dirs[i];
                var p = pos[i];

                Button btn = new Button
                {
                    Text = d,
                    Width = btnW,
                    Height = btnH,
                    Left = startX + p.c * (btnW + margin),
                    Top = startY + p.r * (btnH + margin),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = surfaceColor,
                    ForeColor = accentColor,
                    Font = new Font("Segoe UI Semibold", 10f),
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = borderColor;
                btn.Click += (s, e) => EditDirection(d);

                dirButtons[d] = btn;
                pnl.Controls.Add(btn);
            }
            AutoSizeColumns(itemsListView);
        }

        private static void AutoSizeColumns(ListView listView)
        {
            if (listView == null || listView.Columns.Count == 0 || listView.ClientSize.Width <= 0) return;
            int available = Math.Max(260, listView.ClientSize.Width - 8);
            int baseWidth = Math.Max(80, available / listView.Columns.Count);
            for (int i = 0; i < listView.Columns.Count; i++) listView.Columns[i].Width = baseWidth;
            int used = 0;
            for (int i = 0; i < listView.Columns.Count - 1; i++) used += listView.Columns[i].Width;
            listView.Columns[listView.Columns.Count - 1].Width = Math.Max(120, available - used);
        }

        private void PopulateFields()
        {
            if (ResultRadial == null) return;

            radialNameBox.Text = ResultRadial.Name ?? string.Empty;
            triggerBox.Text = ResultRadial.Trigger ?? string.Empty;
            enabledCheck.Checked = ResultRadial.Enabled;

            RefreshItemsList();
        }

        private void RefreshItemsList()
        {
            itemsListView.Items.Clear();
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

            foreach (string d in dirs)
            {
                RadialItem item = ResultRadial.Items?.FirstOrDefault(x => string.Equals(x.Direction, d, StringComparison.OrdinalIgnoreCase));
                ListViewItem lvi = new ListViewItem(d);
                lvi.SubItems.Add(item?.Command?.Name ?? "(Не назначено)");
                lvi.SubItems.Add(item?.Command?.ID ?? string.Empty);
                itemsListView.Items.Add(lvi);

                if (dirButtons.TryGetValue(d, out Button btn))
                {
                    bool isSet = item != null && (!string.IsNullOrEmpty(item.Command?.Name) || !string.IsNullOrEmpty(item.Command?.ID));
                    btn.BackColor = isSet ? Color.FromArgb(31, 41, 55) : surfaceColor;
                    btn.ForeColor = isSet ? accentColor : mutedColor;
                }
            }
        }

        private void EditSelectedDirection()
        {
            if (itemsListView.SelectedItems.Count == 0) return;
            string dir = itemsListView.SelectedItems[0].Text;
            EditDirection(dir);
        }

        private void EditDirection(string dir)
        {
            RadialItem existing = ResultRadial.Items?.FirstOrDefault(x => string.Equals(x.Direction, dir, StringComparison.OrdinalIgnoreCase));
            Binding dummyBinding = new Binding
            {
                Shortcut = dir,
                Command = existing?.Command ?? new CommandRef { Name = "" },
                Scope = "Radial"
            };

            using (BindingEditDialog dlg = new BindingEditDialog(dummyBinding, catalog))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (ResultRadial.Items == null) ResultRadial.Items = new List<RadialItem>();
                    ResultRadial.Items.RemoveAll(x => string.Equals(x.Direction, dir, StringComparison.OrdinalIgnoreCase));

                    ResultRadial.Items.Add(new RadialItem
                    {
                        Direction = dir,
                        Command = dlg.ResultBinding.Command
                    });

                    RefreshItemsList();
                }
            }
        }

        private void SaveAndClose()
        {
            ResultRadial.Name = radialNameBox.Text.Trim();
            ResultRadial.Trigger = triggerBox.Text.Trim();
            ResultRadial.Enabled = enabledCheck.Checked;

            DialogResult = DialogResult.OK;
        }

        private static RadialMenu CloneRadial(RadialMenu r)
        {
            return new RadialMenu
            {
                Name = r.Name,
                Trigger = r.Trigger,
                Enabled = r.Enabled,
                Items = r.Items != null ? r.Items.Select(x => new RadialItem
                {
                    Direction = x.Direction,
                    Command = new CommandRef { ID = x.Command?.ID ?? "", Name = x.Command?.Name ?? "" },
                    Notes = x.Notes
                }).ToList() : new List<RadialItem>()
            };
        }
    }
}
