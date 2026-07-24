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
        private readonly Color cardColor = Color.FromArgb(22, 27, 34);
        private readonly Color borderColor = Color.FromArgb(48, 54, 61);
        private readonly Color textColor = Color.FromArgb(240, 246, 252);
        private readonly Color mutedColor = Color.FromArgb(139, 148, 158);
        private readonly Color accentColor = Color.FromArgb(56, 189, 248);
        private readonly Color stickyColor = Color.FromArgb(16, 185, 129);
        private readonly Color warningColor = Color.FromArgb(245, 158, 11);
        private readonly Color dangerColor = Color.FromArgb(239, 68, 68);
        private readonly Color keyColor = Color.FromArgb(33, 38, 45);

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
            using (SolidBrush background = new SolidBrush(backColor))
            using (Pen border = new Pen(borderColor, 1.5f))
            {
                graphics.FillPath(background, outer);
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
            using (SolidBrush brush = new SolidBrush(cardColor)) graphics.FillRectangle(brush, header);
            using (Pen pen = new Pen(borderColor)) graphics.DrawLine(pen, 1, header.Bottom, Width - 2, header.Bottom);
            using (Font title = new Font("Segoe UI Semibold", 12f))
            using (SolidBrush accent = new SolidBrush(accentColor)) graphics.DrawString("NXKEYS COMMAND LIST", title, accent, 18, 12);
            using (Font moduleFont = new Font("Segoe UI Semibold", 13f))
            using (SolidBrush text = new SolidBrush(textColor)) graphics.DrawString(activeModuleLabel, moduleFont, text, 18, 36);
            using (Font idFont = new Font("Consolas", 8.5f))
            using (SolidBrush muted = new SolidBrush(mutedColor)) graphics.DrawString(activeModuleId, idFont, muted, 235, 42);
            if (sticky)
            {
                Rectangle badge = new Rectangle(Width - 126, 20, 104, 28);
                using (SolidBrush brush = new SolidBrush(stickyColor)) graphics.FillRectangle(brush, badge);
                using (Font font = new Font("Segoe UI Semibold", 8.5f))
                using (SolidBrush text = new SolidBrush(Color.Black)) graphics.DrawString("STICKY", font, text, badge.Left + 28, badge.Top + 6);
            }
        }

        private void DrawCommandList(Graphics graphics)
        {
            using (Font hint = new Font("Segoe UI", 9.5f))
            using (SolidBrush muted = new SolidBrush(mutedColor))
                graphics.DrawString($"{triggerKeyName} → показанная клавиша · Tab модуль · Space поиск", hint, muted, 18, 82);

            int columnCount = 3;
            int gutter = 12;
            int left = 18;
            int top = 112;
            int bottom = Height - 48;
            int columnWidth = (Width - left * 2 - gutter * (columnCount - 1)) / columnCount;
            int rowHeight = 78;
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
            using (GraphicsPath path = Rounded(rectangle, 11))
            using (SolidBrush brush = new SolidBrush(cardColor))
            using (Pen pen = new Pen(row?.Item?.Destructive == true ? dangerColor : borderColor, 1.2f))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
            Rectangle keyBox = new Rectangle(rectangle.Left + 10, rectangle.Top + 10, 34, 34);
            using (SolidBrush brush = new SolidBrush(keyColor)) graphics.FillRectangle(brush, keyBox);
            using (Font keyFont = new Font("Consolas", 13f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(accentColor)) DrawCentered(graphics, row?.Key ?? "?", keyFont, text, keyBox);
            Rectangle iconBox = new Rectangle(rectangle.Left + 50, rectangle.Top + 12, 30, 30);
            OperationThumbnailRenderer.Draw(graphics, iconBox, row?.IconHint, row?.Item?.Command?.ID, row?.Name);
            string name = row?.Name ?? "Не назначено";
            using (Font nameFont = new Font("Segoe UI Semibold", 9.5f))
            using (SolidBrush text = new SolidBrush(row == null ? mutedColor : textColor))
                graphics.DrawString(Truncate(name, 22), nameFont, text, rectangle.Left + 88, rectangle.Top + 8);
            string status = row?.Status ?? string.Empty;
            using (Font small = new Font("Segoe UI", 8f))
            using (SolidBrush statusBrush = new SolidBrush(StatusColor(status)))
            {
                graphics.DrawString(status, small, statusBrush, rectangle.Left + 88, rectangle.Top + 34);
            }
            using (Font idFont = new Font("Consolas", 7.8f))
            using (SolidBrush muted = new SolidBrush(mutedColor))
                graphics.DrawString(Truncate(row?.Details ?? string.Empty, 34), idFont, muted,
                    new Rectangle(rectangle.Left + 10, rectangle.Bottom - 23, rectangle.Width - 20, 16));
        }

        private void DrawSearch(Graphics graphics)
        {
            using (Font title = new Font("Segoe UI Semibold", 11f))
            using (SolidBrush text = new SolidBrush(textColor)) graphics.DrawString("Поиск в модуле: " + searchFilter, title, text, 18, 86);
            int y = 122;
            foreach (LeaderSequenceItem item in commands.Take(8))
            {
                Rectangle row = new Rectangle(18, y, Width - 36, 38);
                using (SolidBrush brush = new SolidBrush(cardColor)) graphics.FillRectangle(brush, row);
                using (Font key = new Font("Consolas", 10f, FontStyle.Bold))
                using (SolidBrush accent = new SolidBrush(accentColor)) graphics.DrawString(item.InputKey, key, accent, row.Left + 10, row.Top + 9);
                using (Font name = new Font("Segoe UI", 9.5f))
                using (SolidBrush text = new SolidBrush(textColor)) graphics.DrawString(item.Command?.Name ?? item.Notes, name, text, row.Left + 48, row.Top + 9);
                y += 44;
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
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(35, dangerColor.R, dangerColor.G, dangerColor.B)))
            using (Pen pen = new Pen(dangerColor, 1.7f))
            {
                graphics.FillRectangle(brush, box);
                graphics.DrawRectangle(pen, box);
            }
            using (Font title = new Font("Segoe UI Semibold", 15f))
            using (SolidBrush text = new SolidBrush(textColor)) graphics.DrawString("Требуется подтверждение", title, text, box.Left + 22, box.Top + 22);
            using (Font command = new Font("Segoe UI Semibold", 13f))
            using (SolidBrush text = new SolidBrush(textColor)) graphics.DrawString(confirmationItem?.Command?.Name ?? "Command", command, text, box.Left + 22, box.Top + 74);
            using (Font info = new Font("Consolas", 9f))
            using (SolidBrush muted = new SolidBrush(mutedColor))
            {
                graphics.DrawString(confirmationItem?.Command?.ID ?? string.Empty, info, muted, box.Left + 22, box.Top + 112);
                graphics.DrawString("Enter — выполнить   ·   Esc — отменить", info, muted, box.Left + 22, box.Top + 158);
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
