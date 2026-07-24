using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.UI
{
    internal sealed class CommandListPreviewPanel : Panel
    {
        private readonly Color background = Color.FromArgb(13, 17, 23);
        private readonly Color surface = Color.FromArgb(22, 27, 34);
        private readonly Color border = Color.FromArgb(48, 54, 61);
        private readonly Color text = Color.FromArgb(240, 246, 252);
        private readonly Color muted = Color.FromArgb(139, 148, 158);
        private readonly Color accent = Color.FromArgb(56, 189, 248);
        private readonly Color success = Color.FromArgb(16, 185, 129);
        private readonly Color warning = Color.FromArgb(245, 158, 11);
        private readonly Color danger = Color.FromArgb(239, 68, 68);
        private readonly Color keyBack = Color.FromArgb(33, 38, 45);

        private string moduleLabel = "Module";
        private string moduleId = string.Empty;
        private bool bridgeReady = true;
        private int selectionCount;
        private string currentPrefix = string.Empty;
        private List<LeaderSequenceItem> commands = new List<LeaderSequenceItem>();

        private sealed class DisplayRow
        {
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Details { get; set; } = string.Empty;
            public string IconHint { get; set; } = string.Empty;
            public int DisplayOrder { get; set; }
            public LeaderSequenceItem Item { get; set; }
        }

        public CommandListPreviewPanel()
        {
            DoubleBuffered = true;
            BackColor = background;
            ForeColor = text;
            MinimumSize = new Size(360, 220);
        }

        public void SetCommands(string label, string id, IEnumerable<LeaderSequenceItem> items, bool isBridgeReady = true,
            int currentSelectionCount = 0, string prefix = "")
        {
            moduleLabel = string.IsNullOrWhiteSpace(label) ? "Module" : label;
            moduleId = id ?? string.Empty;
            bridgeReady = isBridgeReady;
            selectionCount = currentSelectionCount;
            currentPrefix = prefix ?? string.Empty;
            commands = Ordered(items);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(background);

            using (Font title = new Font("Segoe UI Semibold", 11f))
            using (SolidBrush titleBrush = new SolidBrush(text))
                graphics.DrawString(moduleLabel, title, titleBrush, 12, 10);
            using (Font idFont = new Font("Consolas", 8.5f))
            using (SolidBrush idBrush = new SolidBrush(muted))
                graphics.DrawString(moduleId, idFont, idBrush, 12, 34);

            int columnCount = 3;
            int gutter = 10;
            int left = 12;
            int top = 60;
            int columnWidth = Math.Max(120, (Width - left * 2 - gutter * (columnCount - 1)) / columnCount);
            int rowHeight = 70;
            int rowsPerColumn = Math.Max(1, (Height - top - 12) / (rowHeight + 8));
            List<DisplayRow> rows = BuildDisplayRows(commands, currentPrefix);
            for (int index = 0; index < Math.Min(rows.Count, rowsPerColumn * columnCount); index++)
            {
                int column = index / rowsPerColumn;
                int row = index % rowsPerColumn;
                var rectangle = new Rectangle(left + column * (columnWidth + gutter), top + row * (rowHeight + 8), columnWidth, rowHeight);
                DrawRow(graphics, rectangle, rows[index]);
            }
        }

        private void DrawRow(Graphics graphics, Rectangle rectangle, DisplayRow row)
        {
            using (GraphicsPath path = Rounded(rectangle, 8))
            using (SolidBrush brush = new SolidBrush(surface))
            using (Pen pen = new Pen(row.Item?.Destructive == true ? danger : border))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }

            Rectangle keyBox = new Rectangle(rectangle.Left + 8, rectangle.Top + 9, 30, 30);
            using (SolidBrush brush = new SolidBrush(keyBack)) graphics.FillRectangle(brush, keyBox);
            using (Font keyFont = new Font("Consolas", 12f, FontStyle.Bold))
            using (SolidBrush keyBrush = new SolidBrush(accent))
                DrawCentered(graphics, row.Key, keyFont, keyBrush, keyBox);

            Rectangle iconBox = new Rectangle(rectangle.Left + 43, rectangle.Top + 11, 26, 26);
            OperationThumbnailRenderer.Draw(graphics, iconBox, row.IconHint, row.Item?.Command?.ID, row.Name);

            using (Font nameFont = new Font("Segoe UI Semibold", 8.7f))
            using (SolidBrush nameBrush = new SolidBrush(text))
                graphics.DrawString(Trim(row.Name, 20), nameFont, nameBrush, rectangle.Left + 76, rectangle.Top + 7);
            string status = row.Status;
            using (Font statusFont = new Font("Segoe UI", 7.8f))
            using (SolidBrush statusBrush = new SolidBrush(StatusColor(status)))
                graphics.DrawString(status, statusFont, statusBrush, rectangle.Left + 76, rectangle.Top + 30);
            using (Font idFont = new Font("Consolas", 7.4f))
            using (SolidBrush idBrush = new SolidBrush(muted))
                graphics.DrawString(Trim(row.Details, 32), idFont, idBrush,
                    new Rectangle(rectangle.Left + 8, rectangle.Bottom - 20, rectangle.Width - 16, 14));
        }

        private string StatusFor(LeaderSequenceItem item)
        {
            if (!bridgeReady) return "Bridge не загружен";
            if (item.RequiresSelection && selectionCount <= 0) return "Нужен выбор";
            if (item.Destructive || item.ConfirmBeforeExecute) return "Enter";
            return "Готово";
        }

        private List<DisplayRow> BuildDisplayRows(IEnumerable<LeaderSequenceItem> items, string prefix)
        {
            List<string> prefixTokens = Tokenize(prefix);
            var groups = new Dictionary<string, List<LeaderSequenceItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (LeaderSequenceItem item in Ordered(items))
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
                    Name = first?.Command?.Name ?? string.Empty,
                    Status = StatusFor(first),
                    Details = first?.Command?.ID ?? string.Empty,
                    IconHint = first?.IconHint ?? string.Empty,
                    DisplayOrder = first?.DisplayOrder ?? 0,
                    Item = first
                };
            }

            string label = group.Select(item => item.SubmenuLabel).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return new DisplayRow
            {
                Key = key,
                Name = (string.IsNullOrWhiteSpace(label) ? "Подменю " + key : label) + " >",
                Status = "Открыть",
                Details = group.Count + " команд",
                IconHint = first?.IconHint ?? "menu",
                DisplayOrder = first?.DisplayOrder ?? 0,
                Item = first
            };
        }

        private Color StatusColor(string status)
        {
            if (string.Equals(status, "Готово", StringComparison.OrdinalIgnoreCase)) return success;
            if (string.Equals(status, "Bridge не загружен", StringComparison.OrdinalIgnoreCase)) return danger;
            return warning;
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

        private static List<LeaderSequenceItem> Ordered(IEnumerable<LeaderSequenceItem> values) =>
            (values ?? Enumerable.Empty<LeaderSequenceItem>())
                .Where(item => item != null && item.Enabled)
                .OrderBy(item => item.DisplayOrder <= 0 ? int.MaxValue : item.DisplayOrder)
                .ThenBy(item => item.InputKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

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
            SizeF size = graphics.MeasureString(value ?? string.Empty, font);
            graphics.DrawString(value ?? string.Empty, font, brush, rectangle.Left + (rectangle.Width - size.Width) / 2,
                rectangle.Top + (rectangle.Height - size.Height) / 2);
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? string.Empty;
            return value.Substring(0, Math.Max(0, max - 1)) + "...";
        }
    }
}
