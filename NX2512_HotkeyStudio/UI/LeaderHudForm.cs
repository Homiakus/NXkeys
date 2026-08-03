using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.UI
{
    public sealed class LeaderHudForm : Form
    {
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private readonly Color backColor = Color.FromArgb(13, 17, 23);
        private readonly Color cardColor = Color.FromArgb(22, 31, 40);
        private readonly Color cardHighlightColor = Color.FromArgb(28, 42, 54);
        private readonly Color borderColor = Color.FromArgb(55, 70, 84);
        private readonly Color textColor = Color.FromArgb(240, 246, 252);
        private readonly Color mutedColor = Color.FromArgb(154, 166, 179);
        private readonly Color accentColor = Color.FromArgb(56, 189, 248);
        private readonly Color stickyColor = Color.FromArgb(16, 185, 129);
        private readonly Color warningColor = Color.FromArgb(245, 158, 11);
        private readonly Color dangerColor = Color.FromArgb(239, 68, 68);
        private readonly Color keyColor = Color.FromArgb(10, 17, 25);
        private readonly Color headerStartColor = Color.FromArgb(18, 35, 50);
        private readonly Color headerEndColor = Color.FromArgb(14, 20, 29);

        private string triggerKeyName = "CapsLock";
        private string activeModuleId = "modeling";
        private string activeModuleLabel = "Modeling";
        private bool sticky;
        private bool bridgeReady;
        private int selectionCount = -1;
        private string currentPrefix = string.Empty;
        private string searchFilter;
        private LeaderSequenceItem confirmationItem;
        private float timeoutPct = 1.0f;
        private List<LeaderSequenceItem> commands = new List<LeaderSequenceItem>();
        private readonly Timer fadeTimer;
        private double targetOpacity = 0.95;

        private sealed class DisplayRow
        {
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Details { get; set; } = string.Empty;
            public string IconHint { get; set; } = string.Empty;
            public int DisplayOrder { get; set; }
            public bool IsMenu { get; set; }
            public LeaderSequenceItem Item { get; set; }
        }

        public LeaderHudForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            Size = new Size(900, 480);
            BackColor = backColor;
            ForeColor = textColor;
            Opacity = 0;
            fadeTimer = new Timer { Interval = 15 };
            fadeTimer.Tick += (_, _) =>
            {
                if (Opacity < targetOpacity) Opacity = Math.Min(targetOpacity, Opacity + 0.15);
                else fadeTimer.Stop();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
                return parameters;
            }
        }

        protected override bool ShowWithoutActivation => true;

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WM_NCHITTEST)
            {
                message.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
            base.WndProc(ref message);
        }

        public void DisplayHud(string triggerKey, bool isSticky, List<LeaderSequenceItem> moduleCommands,
            double opacity = 0.95, string moduleLabel = "Modeling", string moduleId = "modeling",
            bool isBridgeReady = false, int currentSelectionCount = -1, string prefix = "")
        {
            triggerKeyName = string.IsNullOrWhiteSpace(triggerKey) ? "Leader" : triggerKey;
            sticky = isSticky;
            activeModuleId = string.IsNullOrWhiteSpace(moduleId) ? "unknown" : moduleId;
            activeModuleLabel = string.IsNullOrWhiteSpace(moduleLabel) ? activeModuleId : moduleLabel;
            commands = OrderedCommands(moduleCommands);
            bridgeReady = isBridgeReady;
            selectionCount = currentSelectionCount;
            currentPrefix = prefix ?? string.Empty;
            searchFilter = null;
            confirmationItem = null;
            timeoutPct = 1.0f;
            targetOpacity = opacity;
            PositionNearCursor();
            if (!Visible) Show();
            Opacity = targetOpacity;
            Invalidate();
        }

        public void UpdateState(string currentBuffer, List<LeaderSequenceItem> matches, bool isSticky,
            string moduleLabel = null, string moduleId = null, bool isBridgeReady = false, int currentSelectionCount = -1,
            string prefix = "")
        {
            sticky = isSticky;
            if (!string.IsNullOrWhiteSpace(moduleId)) activeModuleId = moduleId;
            if (!string.IsNullOrWhiteSpace(moduleLabel)) activeModuleLabel = moduleLabel;
            commands = OrderedCommands(matches);
            bridgeReady = isBridgeReady;
            selectionCount = currentSelectionCount;
            currentPrefix = prefix ?? string.Empty;
            searchFilter = null;
            confirmationItem = null;
            timeoutPct = 1.0f;
            Invalidate();
        }

        public void SetSearchMode(string query, List<LeaderSequenceItem> matches, string moduleLabel = null, string moduleId = null,
            bool isBridgeReady = false, int currentSelectionCount = -1, string prefix = "")
        {
            searchFilter = query ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(moduleId)) activeModuleId = moduleId;
            if (!string.IsNullOrWhiteSpace(moduleLabel)) activeModuleLabel = moduleLabel;
            confirmationItem = null;
            commands = OrderedCommands(matches);
            bridgeReady = isBridgeReady;
            selectionCount = currentSelectionCount;
            currentPrefix = prefix ?? string.Empty;
            Invalidate();
        }

        public void SetConfirmation(LeaderSequenceItem item, string moduleLabel = null, string moduleId = null)
        {
            confirmationItem = item;
            if (!string.IsNullOrWhiteSpace(moduleId)) activeModuleId = moduleId;
            if (!string.IsNullOrWhiteSpace(moduleLabel)) activeModuleLabel = moduleLabel;
            searchFilter = null;
            Invalidate();
        }

        public void UpdateTimeoutProgress(float percentage)
        {
            timeoutPct = Math.Max(0.0f, Math.Min(1.0f, percentage));
            Invalidate();
        }

        public void DismissHud()
        {
            fadeTimer.Stop();
            Opacity = 0;
            Hide();
        }

        private void PositionNearCursor()
        {
            if (!GetCursorPos(out POINT point)) { Location = new Point(100, 100); return; }
            Screen screen = Screen.FromPoint(new Point(point.X, point.Y));
            int x = point.X + 24;
            int y = point.Y + 24;
            if (x + Width > screen.WorkingArea.Right) x = point.X - Width - 12;
            if (y + Height > screen.WorkingArea.Bottom) y = point.Y - Height - 12;
            Location = new Point(Math.Max(screen.WorkingArea.Left + 10, x), Math.Max(screen.WorkingArea.Top + 10, y));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath outer = Rounded(bounds, 14))
            using (Pen border = new Pen(borderColor, 1.5f))
            {
                FillPathGradient(graphics, outer, bounds, Color.FromArgb(15, 23, 32), backColor, 90f);
                graphics.DrawPath(border, outer);
            }
            DrawHeader(graphics);
            if (confirmationItem != null) DrawConfirmation(graphics);
            else if (searchFilter != null) DrawSearch(graphics);
            else DrawCommandList(graphics);
            DrawFooter(graphics);
        }

        private void DrawHeader(Graphics graphics)
        {
            Rectangle header = new Rectangle(1, 1, Width - 2, 68);
            using (LinearGradientBrush brush = new LinearGradientBrush(header, headerStartColor, headerEndColor, 0f))
                graphics.FillRectangle(brush, header);
            using (Pen pen = new Pen(borderColor)) graphics.DrawLine(pen, 1, header.Bottom, Width - 2, header.Bottom);

            Rectangle moduleIcon = new Rectangle(18, 15, 38, 38);
            CadIconPainter.Draw(graphics, moduleIcon, activeModuleId, string.Empty, activeModuleLabel);

            using (Font title = new Font("Segoe UI Semibold", 12f))
            using (SolidBrush accent = new SolidBrush(accentColor)) graphics.DrawString("NXKEYS COMMAND LIST", title, accent, 68, 10);
            using (Font moduleFont = new Font("Segoe UI Semibold", 13f))
            using (SolidBrush text = new SolidBrush(textColor))
                DrawEllipsized(graphics, activeModuleLabel, moduleFont, text, new Rectangle(68, 34, 290, 24));
            using (Font idFont = new Font("Consolas", 8.5f))
            using (SolidBrush muted = new SolidBrush(mutedColor)) DrawEllipsized(graphics, activeModuleId, idFont, muted, new Rectangle(364, 40, 140, 16));

            int chipRight = Width - 18;
            chipRight = DrawRightPill(graphics, chipRight, 20, sticky ? "STICKY" : "LIVE", sticky ? stickyColor : accentColor, Color.Black);
            chipRight = DrawRightPill(graphics, chipRight - 8, 20, bridgeReady ? "BRIDGE OK" : "BRIDGE OFF", bridgeReady ? stickyColor : dangerColor, Color.Black);
            if (selectionCount >= 0)
                DrawRightPill(graphics, chipRight - 8, 20, "SEL " + selectionCount, selectionCount > 0 ? accentColor : borderColor, selectionCount > 0 ? Color.Black : mutedColor);
            if (sticky)
            {
                using (Pen glow = new Pen(Color.FromArgb(160, stickyColor), 2f))
                    graphics.DrawLine(glow, 18, header.Bottom - 1, Width - 18, header.Bottom - 1);
            }
        }

        private void DrawCommandList(Graphics graphics)
        {
            using (Font hint = new Font("Segoe UI", 9.5f))
            using (SolidBrush muted = new SolidBrush(mutedColor))
                graphics.DrawString($"{triggerKeyName} → клавиша на карточке   ·   Tab модуль   ·   Space поиск", hint, muted, 18, 82);

            int columnCount = 3;
            int gutter = 12;
            int left = 18;
            int top = 110;
            int bottom = Height - 48;
            int columnWidth = (Width - left * 2 - gutter * (columnCount - 1)) / columnCount;
            int rowHeight = 76;
            int rowsPerColumn = Math.Max(1, (bottom - top) / (rowHeight + 10));
            List<DisplayRow> visible = BuildDisplayRows(commands, currentPrefix).Take(columnCount * rowsPerColumn).ToList();
            for (int index = 0; index < visible.Count; index++)
            {
                int column = index / rowsPerColumn;
                int row = index % rowsPerColumn;
                Rectangle rectangle = new Rectangle(left + column * (columnWidth + gutter), top + row * (rowHeight + 10), columnWidth, rowHeight);
                DrawCommandRow(graphics, rectangle, visible[index]);
            }
        }

        private void DrawCommandRow(Graphics graphics, Rectangle rectangle, DisplayRow row)
        {
            bool destructive = row?.Item?.Destructive == true;
            Color stateColor = StatusColor(row?.Status ?? string.Empty);
            using (GraphicsPath path = Rounded(rectangle, 11))
            using (Pen pen = new Pen(destructive ? dangerColor : Color.FromArgb(70, 86, 102), 1.2f))
            {
                FillPathGradient(graphics, path, rectangle, cardHighlightColor, cardColor, 90f);
                graphics.DrawPath(pen, path);
            }

            using (SolidBrush stripe = new SolidBrush(destructive ? dangerColor : stateColor))
                graphics.FillRectangle(stripe, rectangle.Left, rectangle.Top + 12, 3, rectangle.Height - 24);

            Rectangle keyBox = new Rectangle(rectangle.Left + 10, rectangle.Top + 12, 34, 34);
            using (GraphicsPath keyPath = Rounded(keyBox, 7))
            using (SolidBrush brush = new SolidBrush(keyColor))
            using (Pen pen = new Pen(Color.FromArgb(72, accentColor), 1f))
            {
                graphics.FillPath(brush, keyPath);
                graphics.DrawPath(pen, keyPath);
            }
            using (Font keyFont = new Font("Consolas", 13f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(accentColor)) DrawCentered(graphics, row?.Key ?? "?", keyFont, text, keyBox);

            Rectangle iconBox = new Rectangle(rectangle.Left + 52, rectangle.Top + 10, 34, 34);
            OperationThumbnailRenderer.Draw(graphics, iconBox, row?.IconHint, row?.Item?.Command?.ID, row?.Name);

            string name = row?.Name ?? "Не назначено";
            using (Font nameFont = new Font("Segoe UI Semibold", 9.5f))
            using (SolidBrush text = new SolidBrush(row == null ? mutedColor : textColor))
                DrawEllipsized(graphics, name, nameFont, text, new Rectangle(rectangle.Left + 96, rectangle.Top + 8, rectangle.Width - 106, 22));

            string status = row?.Status ?? string.Empty;
            using (Font small = new Font("Segoe UI", 8f))
                DrawPill(graphics, status, small, new Rectangle(rectangle.Left + 96, rectangle.Top + 33, Math.Min(112, rectangle.Width - 106), 22),
                    StatusColor(status), status == "Готово" || status == "Открыть" ? Color.Black : Color.White);

            using (Font idFont = new Font("Consolas", 7.8f))
            using (SolidBrush muted = new SolidBrush(mutedColor))
                DrawEllipsized(graphics, row?.Details ?? string.Empty, idFont, muted,
                    new Rectangle(rectangle.Left + 10, rectangle.Bottom - 22, rectangle.Width - 20, 16));
        }

        private void DrawSearch(Graphics graphics)
        {
            using (Font title = new Font("Segoe UI Semibold", 11f))
            using (SolidBrush text = new SolidBrush(textColor))
                DrawEllipsized(graphics, "Поиск в модуле: " + searchFilter, title, text, new Rectangle(18, 84, Width - 36, 28));
            int y = 122;
            foreach (LeaderSequenceItem item in commands.Take(8))
            {
                Rectangle row = new Rectangle(18, y, Width - 36, 42);
                using (GraphicsPath path = Rounded(row, 10))
                using (Pen pen = new Pen(Color.FromArgb(64, 83, 100), 1f))
                {
                    FillPathGradient(graphics, path, row, cardHighlightColor, cardColor, 90f);
                    graphics.DrawPath(pen, path);
                }
                Rectangle iconBox = new Rectangle(row.Left + 44, row.Top + 7, 28, 28);
                CadIconPainter.Draw(graphics, iconBox, item.IconHint, item.Command?.ID, item.Command?.Name ?? item.Notes);
                using (Font key = new Font("Consolas", 10f, FontStyle.Bold))
                using (SolidBrush accent = new SolidBrush(accentColor)) DrawCentered(graphics, item.InputKey, key, accent, new Rectangle(row.Left + 10, row.Top + 7, 28, 28));
                using (Font name = new Font("Segoe UI", 9.5f))
                using (SolidBrush text = new SolidBrush(textColor))
                    DrawEllipsized(graphics, item.Command?.Name ?? item.Notes, name, text, new Rectangle(row.Left + 82, row.Top + 10, row.Width - 96, 22));
                y += 48;
            }
            using (Font hint = new Font("Segoe UI", 8.5f))
            using (SolidBrush muted = new SolidBrush(mutedColor))
                graphics.DrawString("Enter — первый результат · Backspace — удалить символ · Esc — закрыть", hint, muted, 18, Height - 52);
        }

        private List<DisplayRow> BuildDisplayRows(IEnumerable<LeaderSequenceItem> items, string prefix)
        {
            List<string> prefixTokens = Tokenize(prefix);
            var groups = new Dictionary<string, List<LeaderSequenceItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (LeaderSequenceItem item in OrderedCommands(items))
            {
                List<string> tokens = Tokenize(item.Sequence);
                if (tokens.Count <= prefixTokens.Count || !StartsWith(tokens, prefixTokens)) continue;
                string key = tokens[prefixTokens.Count];
                if (!groups.TryGetValue(key, out List<LeaderSequenceItem> group))
                {
                    group = new List<LeaderSequenceItem>();
                    groups[key] = group;
                }
                group.Add(item);
            }

            return groups.Select(pair => BuildDisplayRow(pair.Key, pair.Value, prefixTokens.Count))
                .OrderBy(row => row.DisplayOrder <= 0 ? int.MaxValue : row.DisplayOrder)
                .ThenBy(row => row.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private DisplayRow BuildDisplayRow(string key, List<LeaderSequenceItem> group, int prefixDepth)
        {
            LeaderSequenceItem first = group.OrderBy(item => item.DisplayOrder <= 0 ? int.MaxValue : item.DisplayOrder)
                .ThenBy(item => item.InputKey, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            bool terminal = group.Count == 1 && Tokenize(first?.Sequence).Count == prefixDepth + 1;
            if (terminal)
            {
                return new DisplayRow
                {
                    Key = key,
                    Name = first?.Command?.Name ?? "Не назначено",
                    Status = StatusFor(first),
                    Details = first?.Command?.ID ?? string.Empty,
                    IconHint = first?.IconHint ?? string.Empty,
                    DisplayOrder = first?.DisplayOrder ?? 0,
                    Item = first
                };
            }

            return new DisplayRow
            {
                Key = key,
                Name = MenuLabelFor(key, group),
                Status = "Открыть",
                Details = group.Count + " команд",
                IconHint = first?.IconHint ?? "menu",
                DisplayOrder = first?.DisplayOrder ?? 0,
                IsMenu = true,
                Item = first
            };
        }

        private static string MenuLabelFor(string key, List<LeaderSequenceItem> group)
        {
            string label = group.Select(item => item.SubmenuLabel)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(label)) return label + " >";
            return "Подменю " + key + " >";
        }

        private string StatusFor(LeaderSequenceItem item)
        {
            if (item == null) return string.Empty;
            if (!bridgeReady) return "Bridge не загружен";
            if (item.RequiresSelection && selectionCount <= 0) return "Нужен выбор";
            if (item.Destructive || item.ConfirmBeforeExecute) return "Enter";
            return "Готово";
        }

        private Color StatusColor(string status)
        {
            if (string.Equals(status, "Готово", StringComparison.OrdinalIgnoreCase)) return stickyColor;
            if (string.Equals(status, "Bridge не загружен", StringComparison.OrdinalIgnoreCase)) return dangerColor;
            return warningColor;
        }

        private static List<string> Tokenize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<string>();
            return value.Where(char.IsLetterOrDigit)
                .Select(character => char.ToUpperInvariant(character).ToString())
                .ToList();
        }

        private static bool StartsWith(IReadOnlyList<string> value, IReadOnlyList<string> prefix)
        {
            if (prefix.Count > value.Count) return false;
            for (int index = 0; index < prefix.Count; index++)
                if (!string.Equals(value[index], prefix[index], StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static List<LeaderSequenceItem> OrderedCommands(IEnumerable<LeaderSequenceItem> values) =>
            (values ?? Enumerable.Empty<LeaderSequenceItem>())
                .Where(item => item != null && item.Enabled)
                .OrderBy(item => item.DisplayOrder <= 0 ? int.MaxValue : item.DisplayOrder)
                .ThenBy(item => item.InputKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private void DrawConfirmation(Graphics graphics)
        {
            Rectangle box = new Rectangle(28, 118, Width - 56, 210);
            using (GraphicsPath path = Rounded(box, 14))
            using (Pen pen = new Pen(dangerColor, 1.7f))
            {
                FillPathGradient(graphics, path, box, Color.FromArgb(54, 30, 39), Color.FromArgb(25, 22, 31), 90f);
                graphics.DrawPath(pen, path);
            }

            Rectangle iconBox = new Rectangle(box.Left + 22, box.Top + 22, 50, 50);
            CadIconPainter.Draw(graphics, iconBox, confirmationItem?.IconHint, confirmationItem?.Command?.ID, confirmationItem?.Command?.Name);

            using (Font title = new Font("Segoe UI Semibold", 15f))
            using (SolidBrush text = new SolidBrush(textColor)) graphics.DrawString("Требуется подтверждение", title, text, box.Left + 86, box.Top + 24);
            using (Font command = new Font("Segoe UI Semibold", 13f))
            using (SolidBrush text = new SolidBrush(textColor))
                DrawEllipsized(graphics, confirmationItem?.Command?.Name ?? "Command", command, text,
                    new Rectangle(box.Left + 22, box.Top + 84, box.Width - 44, 28));
            using (Font info = new Font("Consolas", 9f))
            using (SolidBrush muted = new SolidBrush(mutedColor))
            {
                DrawEllipsized(graphics, confirmationItem?.Command?.ID ?? string.Empty, info, muted,
                    new Rectangle(box.Left + 22, box.Top + 120, box.Width - 44, 18));
                graphics.DrawString("Enter — выполнить   ·   Esc — отменить", info, muted, box.Left + 22, box.Top + 164);
            }
        }

        private void DrawFooter(Graphics graphics)
        {
            int progressWidth = (int)((Width - 4) * timeoutPct);
            if (progressWidth > 0)
            {
                using (SolidBrush brush = new SolidBrush(sticky ? stickyColor : accentColor))
                    graphics.FillRectangle(brush, 2, Height - 7, progressWidth, 5);
            }
            using (Pen line = new Pen(Color.FromArgb(44, borderColor), 1f))
                graphics.DrawLine(line, 18, Height - 40, Width - 18, Height - 40);
            if (confirmationItem != null || searchFilter != null) return;
            using (Font font = new Font("Segoe UI", 8.3f))
            using (SolidBrush muted = new SolidBrush(mutedColor))
                graphics.DrawString("Tab: другой модуль   Space: поиск   Backspace: закрыть   Esc: отмена", font, muted, 18, Height - 29);
        }

        private static GraphicsPath Rounded(Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawCentered(Graphics graphics, string value, Font font, Brush brush, Rectangle rectangle)
        {
            SizeF size = graphics.MeasureString(value, font);
            graphics.DrawString(value, font, brush, rectangle.Left + (rectangle.Width - size.Width) / 2,
                rectangle.Top + (rectangle.Height - size.Height) / 2);
        }

        private static void FillPathGradient(Graphics graphics, GraphicsPath path, Rectangle bounds, Color start, Color end, float angle)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(bounds, start, end, angle))
                graphics.FillPath(brush, path);
        }

        private static void DrawEllipsized(Graphics graphics, string value, Font font, Brush brush, Rectangle rectangle)
        {
            using (StringFormat format = new StringFormat())
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                format.LineAlignment = StringAlignment.Center;
                graphics.DrawString(value ?? string.Empty, font, brush, rectangle, format);
            }
        }

        private static void DrawPill(Graphics graphics, string value, Font font, Rectangle rectangle, Color fillColor, Color textColor)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            SizeF size = graphics.MeasureString(value, font);
            Rectangle pill = new Rectangle(rectangle.Left, rectangle.Top, Math.Min(rectangle.Width, (int)Math.Ceiling(size.Width) + 18), rectangle.Height);
            using (GraphicsPath path = Rounded(pill, pill.Height / 2))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(220, fillColor)))
            using (SolidBrush text = new SolidBrush(textColor))
            {
                graphics.FillPath(fill, path);
                DrawCentered(graphics, value, font, text, pill);
            }
        }

        private static int DrawRightPill(Graphics graphics, int right, int top, string value, Color fillColor, Color textColor)
        {
            using (Font font = new Font("Segoe UI Semibold", 8.3f))
            {
                int width = Math.Max(54, (int)Math.Ceiling(graphics.MeasureString(value, font).Width) + 20);
                Rectangle rectangle = new Rectangle(right - width, top, width, 28);
                DrawPill(graphics, value, font, rectangle, fillColor, textColor);
                return rectangle.Left;
            }
        }

        private static string Truncate(string value, int maximum)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maximum) return value ?? string.Empty;
            return value.Substring(0, maximum - 1) + "…";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) fadeTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
