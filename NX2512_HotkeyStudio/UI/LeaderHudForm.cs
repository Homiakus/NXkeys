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
        #region Win32 Constants & Interop for Non-Activating TopMost Window
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }
        #endregion

        // Palette matching GitHub/VS Code Dark
        private readonly Color backColor = Color.FromArgb(13, 17, 23);       // #0D1117
        private readonly Color cardColor = Color.FromArgb(22, 27, 34);       // #161B22
        private readonly Color borderColor = Color.FromArgb(48, 54, 61);      // #30363D
        private readonly Color textColor = Color.FromArgb(240, 246, 252);     // #F0F6FC
        private readonly Color mutedColor = Color.FromArgb(139, 148, 158);    // #8B949E
        private readonly Color accentColor = Color.FromArgb(56, 189, 248);    // #38BDF8 Sky Cyan
        private readonly Color stickyColor = Color.FromArgb(16, 185, 129);    // #10B981 Emerald Green
        private readonly Color dangerColor = Color.FromArgb(239, 68, 68);     // #EF4444 Rose Red
        private readonly Color keyBadgeBg = Color.FromArgb(33, 38, 45);      // #21262D

        private string triggerKeyName = "CapsLock";
        private string buffer = string.Empty;
        private string activeModuleId = "modeling";
        private string activeModuleLabel = "Modeling";
        private bool isSticky = false;
        private string searchFilter = null;
        private LeaderSequenceItem confirmationItem = null;
        private float timeoutPct = 1.0f;

        private List<LeaderSequenceItem> availableMatches = new List<LeaderSequenceItem>();
        private List<LeaderSequenceItem> allSequences = new List<LeaderSequenceItem>();

        private Timer fadeTimer;
        private double targetOpacity = 0.92;

        public LeaderHudForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            Size = new Size(540, 360);
            BackColor = backColor;
            ForeColor = textColor;
            Opacity = 0;

            fadeTimer = new Timer { Interval = 15 };
            fadeTimer.Tick += FadeTimer_Tick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }

        public void DisplayHud(string triggerKey, bool sticky, List<LeaderSequenceItem> fullList, double opacity = 0.92, string moduleLabel = "Modeling", string moduleId = "modeling")
        {
            triggerKeyName = string.IsNullOrWhiteSpace(triggerKey) ? "Leader" : triggerKey;
            isSticky = sticky;
            activeModuleId = string.IsNullOrWhiteSpace(moduleId) ? "modeling" : moduleId;
            activeModuleLabel = string.IsNullOrWhiteSpace(moduleLabel) ? "Modeling" : moduleLabel;
            allSequences = fullList ?? new List<LeaderSequenceItem>();
            availableMatches = allSequences.Where(s => s.Enabled).ToList();
            buffer = string.Empty;
            searchFilter = null;
            confirmationItem = null;
            timeoutPct = 1.0f;
            targetOpacity = opacity;

            PositionNearCursor();
            if (!Visible)
            {
                Show();
            }
            Opacity = targetOpacity;
            Invalidate();
        }

        public void UpdateState(string currentBuffer, List<LeaderSequenceItem> matches, bool sticky, string moduleLabel = null, string moduleId = null)
        {
            buffer = currentBuffer ?? string.Empty;
            isSticky = sticky;
            if (!string.IsNullOrWhiteSpace(moduleId)) activeModuleId = moduleId;
            if (!string.IsNullOrWhiteSpace(moduleLabel)) activeModuleLabel = moduleLabel;
            availableMatches = matches ?? new List<LeaderSequenceItem>();
            searchFilter = null;
            confirmationItem = null;
            timeoutPct = 1.0f;
            Invalidate();
        }

        public void SetSearchMode(string query, List<LeaderSequenceItem> matches, string moduleLabel = null, string moduleId = null)
        {
            searchFilter = query;
            if (!string.IsNullOrWhiteSpace(moduleId)) activeModuleId = moduleId;
            if (!string.IsNullOrWhiteSpace(moduleLabel)) activeModuleLabel = moduleLabel;
            confirmationItem = null;
            availableMatches = matches ?? new List<LeaderSequenceItem>();
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

        public void UpdateTimeoutProgress(float pct)
        {
            timeoutPct = Math.Max(0.0f, Math.Min(1.0f, pct));
            Invalidate();
        }

        public void DismissHud()
        {
            Opacity = 0;
            Hide();
        }

        private void PositionNearCursor()
        {
            if (GetCursorPos(out POINT pt))
            {
                Screen screen = Screen.FromPoint(new Point(pt.X, pt.Y));
                int targetX = pt.X + 24;
                int targetY = pt.Y + 24;

                if (targetX + Width > screen.WorkingArea.Right)
                {
                    targetX = pt.X - Width - 12;
                }
                if (targetY + Height > screen.WorkingArea.Bottom)
                {
                    targetY = pt.Y - Height - 12;
                }

                Location = new Point(
                    Math.Max(screen.WorkingArea.Left + 10, targetX),
                    Math.Max(screen.WorkingArea.Top + 10, targetY)
                );
            }
            else
            {
                Location = new Point(100, 100);
            }
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            if (Opacity < targetOpacity)
            {
                Opacity = Math.Min(targetOpacity, Opacity + 0.15);
            }
            else
            {
                fadeTimer.Stop();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Outer rounded border
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GetRoundedPath(rect, 10))
            {
                using (SolidBrush bgBrush = new SolidBrush(backColor))
                {
                    g.FillPath(bgBrush, path);
                }
                using (Pen borderPen = new Pen(borderColor, 1.5f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Top Header Bar
            int headerHeight = 54;
            Rectangle headerRect = new Rectangle(1, 1, Width - 2, headerHeight);
            using (GraphicsPath headerPath = GetTopRoundedPath(headerRect, 10))
            {
                using (SolidBrush headerBrush = new SolidBrush(cardColor))
                {
                    g.FillPath(headerBrush, headerPath);
                }
            }
            using (Pen divPen = new Pen(borderColor, 1.0f))
            {
                g.DrawLine(divPen, 1, headerHeight, Width - 2, headerHeight);
            }

            // Title text
            using (Font fontTitle = new Font("Segoe UI Semibold", 11.5f))
            using (SolidBrush brushTitle = new SolidBrush(accentColor))
            {
                g.DrawString("NX LEADER", fontTitle, brushTitle, 16, 14);
            }

            // Trigger & Sequence Path
            string seqPathText = string.IsNullOrWhiteSpace(buffer) ? $"{activeModuleLabel} / {triggerKeyName} → ..." : $"{activeModuleLabel} / {triggerKeyName} → {buffer} → ...";
            if (searchFilter != null) seqPathText = $"ПОИСК: \"{searchFilter}\"";
            if (confirmationItem != null) seqPathText = $"ПОДТВЕРДИТЬ: {confirmationItem.Command?.Name}";

            using (Font fontSeq = new Font("Consolas", 10.5f, FontStyle.Bold))
            using (SolidBrush brushSeq = new SolidBrush(textColor))
            {
                g.DrawString(seqPathText, fontSeq, brushSeq, 130, 15);
            }

            // Sticky Mode Badge
            if (isSticky)
            {
                Rectangle stickyBadge = new Rectangle(Width - 125, 14, 105, 24);
                using (GraphicsPath bPath = GetRoundedPath(stickyBadge, 6))
                {
                    using (SolidBrush sb = new SolidBrush(stickyColor))
                    {
                        g.FillPath(sb, bPath);
                    }
                }
                using (Font bFont = new Font("Segoe UI Semibold", 8f))
                using (SolidBrush bTxt = new SolidBrush(Color.Black))
                {
                    g.DrawString("STICKY MODE", bFont, bTxt, Width - 117, 18);
                }
            }

            // Content Area Rendering
            int contentY = headerHeight + 10;
            int contentHeight = Height - headerHeight - 24;

            if (confirmationItem != null)
            {
                RenderConfirmation(g, contentY, contentHeight);
            }
            else if (searchFilter != null)
            {
                RenderSearchResults(g, contentY, contentHeight);
            }
            else if (string.IsNullOrEmpty(buffer))
            {
                RenderCategoryOverview(g, contentY, contentHeight);
            }
            else
            {
                RenderCategorySubitems(g, contentY, contentHeight);
            }

            // Bottom Timeout Progress Bar
            int pbY = Height - 8;
            int pbWidth = (int)((Width - 4) * timeoutPct);
            if (pbWidth > 0)
            {
                using (SolidBrush pbBrush = new SolidBrush(isSticky ? stickyColor : accentColor))
                {
                    g.FillRectangle(pbBrush, 2, pbY, pbWidth, 5);
                }
            }

            // Footer hints
            using (Font hintFont = new Font("Segoe UI", 8.25f))
            using (SolidBrush hintBrush = new SolidBrush(mutedColor))
            {
                g.DrawString("Tab: Модуль   Space: Поиск   Enter: OK   Backspace: Назад   Esc: Отмена", hintFont, hintBrush, 16, Height - 22);
            }
        }

        private void RenderConfirmation(Graphics g, int topY, int height)
        {
            Rectangle box = new Rectangle(18, topY + 24, Width - 36, 118);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(40, dangerColor.R, dangerColor.G, dangerColor.B)))
            using (Pen pen = new Pen(dangerColor, 1.5f))
            {
                g.FillRectangle(bg, box);
                g.DrawRectangle(pen, box);
            }

            using (Font title = new Font("Segoe UI Semibold", 12f))
            using (SolidBrush brush = new SolidBrush(textColor))
            {
                g.DrawString("Команда требует подтверждения", title, brush, box.Left + 16, box.Top + 16);
            }
            using (Font body = new Font("Segoe UI", 9.5f))
            using (SolidBrush brush = new SolidBrush(mutedColor))
            {
                string name = confirmationItem?.Command?.Name ?? confirmationItem?.Notes ?? "Command";
                g.DrawString(TruncateString(name, 54), body, brush, box.Left + 16, box.Top + 48);
                g.DrawString("Enter: выполнить   Esc: отменить", body, brush, box.Left + 16, box.Top + 76);
            }
        }

        private void RenderCategoryOverview(Graphics g, int topY, int height)
        {
            List<CategoryCard> categories = BuildVisibleCategoryCards();
            if (categories.Count == 0)
            {
                using (Font font = new Font("Segoe UI", 10f))
                using (SolidBrush brush = new SolidBrush(mutedColor))
                {
                    g.DrawString("Нет команд для текущего модуля", font, brush, 20, topY + 20);
                }
                return;
            }

            int colWidth = (Width - 40) / 2;
            int rowHeight = 32;

            for (int i = 0; i < Math.Min(8, categories.Count); i++)
            {
                int col = i % 2;
                int row = i / 2;
                int x = 16 + col * (colWidth + 8);
                int y = topY + row * (rowHeight + 6);

                Rectangle cardRect = new Rectangle(x, y, colWidth, rowHeight);
                using (GraphicsPath cPath = GetRoundedPath(cardRect, 6))
                {
                    using (SolidBrush cBg = new SolidBrush(cardColor))
                    {
                        g.FillPath(cBg, cPath);
                    }
                    using (Pen cBorder = new Pen(borderColor, 1.0f))
                    {
                        g.DrawPath(cBorder, cPath);
                    }
                }

                // Key badge
                Rectangle keyBox = new Rectangle(x + 6, y + 5, 22, 22);
                using (GraphicsPath kbPath = GetRoundedPath(keyBox, 4))
                {
                    using (SolidBrush kbBg = new SolidBrush(keyBadgeBg))
                    {
                        g.FillPath(kbBg, kbPath);
                    }
                }
                using (Font kFont = new Font("Consolas", 10f, FontStyle.Bold))
                using (SolidBrush kTxt = new SolidBrush(accentColor))
                {
                    g.DrawString(categories[i].Key, kFont, kTxt, x + 11, y + 7);
                }

                using (Font nFont = new Font(categories[i].IsActive ? "Segoe UI Semibold" : "Segoe UI", 9f))
                using (SolidBrush nTxt = new SolidBrush(categories[i].IsActive ? accentColor : textColor))
                {
                    string label = categories[i].IsActive ? categories[i].Name + "  active" : categories[i].Name;
                    g.DrawString(TruncateString(label, 28), nFont, nTxt, x + 34, y + 7);
                }
            }
        }

        private List<CategoryCard> BuildVisibleCategoryCards()
        {
            IEnumerable<LeaderSequenceItem> source = allSequences != null && allSequences.Count > 0
                ? allSequences
                : availableMatches ?? new List<LeaderSequenceItem>();

            List<CategoryCard> cards = source
                .Where(s => s != null && s.Enabled && !string.IsNullOrWhiteSpace(s.Sequence))
                .GroupBy(s => FirstSequenceToken(s.Sequence), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    LeaderSequenceItem first = g.First();
                    string moduleId = string.IsNullOrWhiteSpace(first.ModuleID) ? ModuleDefaults.ModuleIdForCategory(first.Category) : first.ModuleID;
                    bool active = string.Equals(moduleId, activeModuleId, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(first.Category, activeModuleLabel, StringComparison.OrdinalIgnoreCase);
                    return new CategoryCard
                    {
                        Key = g.Key,
                        Name = $"{first.Category} ({g.Count()})",
                        ModuleId = moduleId,
                        IsActive = active
                    };
                })
                .OrderBy(c => c.IsActive ? 0 : SharedOrder(c.ModuleId))
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return cards;
        }

        private static string FirstSequenceToken(string sequence)
        {
            string[] parts = (sequence ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) return parts[0].Trim().ToUpperInvariant();
            return string.Empty;
        }

        private static int SharedOrder(string moduleId)
        {
            switch ((moduleId ?? string.Empty).ToLowerInvariant())
            {
                case "selection_object": return 1;
                case "inspect_view": return 2;
                case "reuse": return 3;
                default: return 4;
            }
        }

        private sealed class CategoryCard
        {
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string ModuleId { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }

        private void RenderCategorySubitems(Graphics g, int topY, int height)
        {
            if (availableMatches == null || availableMatches.Count == 0)
            {
                using (Font font = new Font("Segoe UI", 10f))
                using (SolidBrush brush = new SolidBrush(mutedColor))
                {
                    g.DrawString("Нет доступных команд для данной комбинации", font, brush, 20, topY + 20);
                }
                return;
            }

            int itemsCount = availableMatches.Count;
            int cols = itemsCount > 8 ? 3 : 2;
            int colWidth = (Width - 40 - (cols - 1) * 8) / cols;
            int rowHeight = 30;

            for (int i = 0; i < Math.Min(15, itemsCount); i++)
            {
                var item = availableMatches[i];
                int col = i % cols;
                int row = i / cols;
                int x = 16 + col * (colWidth + 8);
                int y = topY + row * (rowHeight + 4);

                Rectangle cardRect = new Rectangle(x, y, colWidth, rowHeight);
                using (GraphicsPath cPath = GetRoundedPath(cardRect, 5))
                {
                    using (SolidBrush cBg = new SolidBrush(cardColor))
                    {
                        g.FillPath(cBg, cPath);
                    }
                    using (Pen cBorder = new Pen(borderColor, 1.0f))
                    {
                        g.DrawPath(cBorder, cPath);
                    }
                }

                // Get subkey (e.g. if buffer is "M" and sequence is "M E", subkey is "E")
                string subKey = item.Sequence;
                if (!string.IsNullOrEmpty(buffer) && subKey.StartsWith(buffer, StringComparison.OrdinalIgnoreCase))
                {
                    subKey = subKey.Substring(buffer.Length).Trim();
                }

                Rectangle keyBox = new Rectangle(x + 5, y + 4, 22, 22);
                using (GraphicsPath kbPath = GetRoundedPath(keyBox, 4))
                {
                    using (SolidBrush kbBg = new SolidBrush(keyBadgeBg))
                    {
                        g.FillPath(kbBg, kbPath);
                    }
                }
                using (Font kFont = new Font("Consolas", 9.5f, FontStyle.Bold))
                using (SolidBrush kTxt = new SolidBrush(accentColor))
                {
                    g.DrawString(subKey, kFont, kTxt, x + 9, y + 6);
                }

                string nameText = item.Command?.Name ?? item.Notes;
                using (Font nFont = new Font("Segoe UI", 8.75f))
                using (SolidBrush nTxt = new SolidBrush(textColor))
                {
                    g.DrawString(TruncateString(nameText, 18), nFont, nTxt, x + 32, y + 6);
                }
            }
        }

        private void RenderSearchResults(Graphics g, int topY, int height)
        {
            if (availableMatches == null || availableMatches.Count == 0)
            {
                using (Font font = new Font("Segoe UI", 10f))
                using (SolidBrush brush = new SolidBrush(mutedColor))
                {
                    g.DrawString($"Совпадений для \"{searchFilter}\" не найдено", font, brush, 20, topY + 20);
                }
                return;
            }

            int y = topY;
            foreach (var item in availableMatches.Take(6))
            {
                Rectangle cardRect = new Rectangle(16, y, Width - 32, 34);
                using (GraphicsPath cPath = GetRoundedPath(cardRect, 6))
                {
                    using (SolidBrush cBg = new SolidBrush(cardColor))
                    {
                        g.FillPath(cBg, cPath);
                    }
                    using (Pen cBorder = new Pen(borderColor, 1.0f))
                    {
                        g.DrawPath(cBorder, cPath);
                    }
                }

                using (Font kFont = new Font("Consolas", 10f, FontStyle.Bold))
                using (SolidBrush kTxt = new SolidBrush(accentColor))
                {
                    g.DrawString(item.Sequence, kFont, kTxt, 26, y + 8);
                }

                using (Font nFont = new Font("Segoe UI Semibold", 9.25f))
                using (SolidBrush nTxt = new SolidBrush(textColor))
                {
                    g.DrawString(item.Command?.Name ?? item.Notes, nFont, nTxt, 110, y + 8);
                }

                using (Font catFont = new Font("Segoe UI", 8.5f))
                using (SolidBrush catTxt = new SolidBrush(mutedColor))
                {
                    g.DrawString($"[{item.Category}]", catFont, catTxt, Width - 140, y + 9);
                }

                y += 40;
            }
        }

        private static string TruncateString(string str, int maxLen)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Length <= maxLen ? str : str.Substring(0, maxLen - 1) + "…";
        }

        private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath GetTopRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
            path.CloseFigure();
            return path;
        }
    }
}
