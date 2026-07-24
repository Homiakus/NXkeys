using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.UI
{
    /// <summary>
    /// Draws and exports command-specific thumbnails. CadIconPainter supplies the
    /// CAD subject; this layer adds the exact action and a stable command identity.
    /// </summary>
    public static class OperationThumbnailRenderer
    {
        private static readonly ConcurrentDictionary<string, Image> ImageCache =
            new ConcurrentDictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        private static readonly Color Accent = Color.FromArgb(91, 201, 223);
        private static readonly Color Gold = Color.FromArgb(240, 196, 90);
        private static readonly Color Success = Color.FromArgb(91, 214, 157);
        private static readonly Color Danger = Color.FromArgb(255, 124, 139);
        private static readonly Color BadgeBack = Color.FromArgb(238, 13, 24, 33);

        private sealed class ThumbnailCommand
        {
            public string ID { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Hint { get; set; } = string.Empty;
            public string Module { get; set; } = string.Empty;
        }

        private sealed class ActionBadge
        {
            public string Text { get; set; } = string.Empty;
            public Color Color { get; set; } = Gold;
            public bool IsArrow { get; set; }
            public bool IsMove { get; set; }
            public bool IsRefresh { get; set; }
        }

        public static void ClearCache()
        {
            foreach (Image image in ImageCache.Values)
            {
                try { image.Dispose(); }
                catch { }
            }
            ImageCache.Clear();
            CadIconPainter.ClearCache();
        }

        public static void Draw(Graphics graphics, Rectangle box, string hint, string commandId, string commandName = "")
        {
            if (graphics == null || box.Width <= 0 || box.Height <= 0) return;

            Image generated = TryLoadGeneratedThumbnail(commandId, commandName);
            if (generated != null)
            {
                InterpolationMode previous = graphics.InterpolationMode;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(generated, box);
                graphics.InterpolationMode = previous;
                return;
            }

            DrawProcedural(graphics, box, hint, commandId, commandName);
        }

        public static Bitmap RenderIconBitmap(int size, string hint, string commandId, string commandName = "")
        {
            int safeSize = Math.Max(24, size);
            var bitmap = new Bitmap(safeSize, safeSize, PixelFormat.Format32bppArgb);
            bitmap.SetResolution(96f, 96f);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                DrawProcedural(graphics, new Rectangle(0, 0, safeSize, safeSize), hint, commandId, commandName);
            }
            return bitmap;
        }

        public static int ExportAllIcons(Config config, int size = 128)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.ApplyDefaults();

            List<ThumbnailCommand> commands = CollectCommands(config);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string workspaceRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            string[] folders =
            {
                Path.Combine(workspaceRoot, "assets", "icons", "commands"),
                Path.Combine(workspaceRoot, "docs", "assets", "icons", "commands"),
                Path.Combine(workspaceRoot, "NX2512_HotkeyStudio", "Resources", "Icons", "commands"),
                Path.Combine(baseDir, "assets", "icons", "commands")
            };

            foreach (string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(folder);
                foreach (ThumbnailCommand command in commands)
                {
                    using (Bitmap bitmap = RenderIconBitmap(size, command.Hint, command.ID, command.Name))
                        bitmap.Save(Path.Combine(folder, BuildFileName(command.ID, command.Name)), ImageFormat.Png);
                }

                string manifest = JsonSerializer.Serialize(new
                {
                    generated_utc = DateTime.UtcNow,
                    icon_size = Math.Max(24, size),
                    count = commands.Count,
                    commands = commands.Select(command => new
                    {
                        id = command.ID,
                        name = command.Name,
                        hint = command.Hint,
                        module = command.Module,
                        file = BuildFileName(command.ID, command.Name)
                    })
                }, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(folder, "manifest.json"), manifest + Environment.NewLine, new UTF8Encoding(false));
            }

            ClearCache();
            return commands.Count;
        }

        private static void DrawProcedural(Graphics graphics, Rectangle box, string hint, string commandId, string commandName)
        {
            CadIconPainter.Draw(graphics, box, hint, commandId, commandName);

            float scale = Math.Max(0.55f, Math.Min(box.Width, box.Height) / 32f);
            string effectiveHint = string.IsNullOrWhiteSpace(hint)
                ? CommandIconHints.FromCommand(commandId, commandName)
                : hint.Trim();

            if (string.Equals(effectiveHint, "command", StringComparison.OrdinalIgnoreCase))
                DrawMonogram(graphics, box, scale, commandName, commandId);

            ActionBadge badge = ResolveAction(commandId, commandName);
            if (badge != null) DrawBadge(graphics, box, scale, badge);
            DrawIdentityMarks(graphics, box, scale, commandId, commandName);
        }

        private static List<ThumbnailCommand> CollectCommands(Config config)
        {
            var result = new Dictionary<string, ThumbnailCommand>(StringComparer.OrdinalIgnoreCase);

            void Add(CommandRef command, string hint, string module)
            {
                if (command == null || (string.IsNullOrWhiteSpace(command.ID) && string.IsNullOrWhiteSpace(command.Name))) return;
                string key = string.IsNullOrWhiteSpace(command.ID) ? "NAME:" + command.Name.Trim() : "ID:" + command.ID.Trim();
                if (result.ContainsKey(key)) return;
                result[key] = new ThumbnailCommand
                {
                    ID = command.ID?.Trim() ?? string.Empty,
                    Name = command.Name?.Trim() ?? string.Empty,
                    Hint = string.IsNullOrWhiteSpace(hint)
                        ? CommandIconHints.FromCommand(command.ID, command.Name)
                        : hint.Trim(),
                    Module = module ?? string.Empty
                };
            }

            foreach (Binding binding in config.Keyboard ?? Enumerable.Empty<Binding>())
                if (binding != null && binding.Enabled) Add(binding.Command, string.Empty, "keyboard");

            foreach (ModuleConfig module in config.Modules ?? Enumerable.Empty<ModuleConfig>())
            {
                if (module == null || !module.Enabled) continue;
                Add(module.SwitchCommand, "menu", module.ID);
                IEnumerable<ModuleCommand> commands = module.CommandSets?
                    .Where(set => set?.Commands != null)
                    .SelectMany(set => set.Commands)
                    .Where(command => command != null && command.Enabled)
                    ?? Enumerable.Empty<ModuleCommand>();
                foreach (ModuleCommand command in commands) Add(command.Command, command.IconHint, module.ID);
            }

            if (config.WorkflowControls != null)
            {
                Add(config.WorkflowControls.AcceptOK, "command", "workflow");
                Add(config.WorkflowControls.Apply, "command", "workflow");
                Add(config.WorkflowControls.Cancel, "command", "workflow");
                Add(config.WorkflowControls.BackPreviousStep, "command", "workflow");
            }

            return result.Values
                .OrderBy(command => command.Module, StringComparer.OrdinalIgnoreCase)
                .ThenBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(command => command.ID, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ActionBadge ResolveAction(string commandId, string commandName)
        {
            string value = string.Join(" ", commandId ?? string.Empty, commandName ?? string.Empty).ToUpperInvariant();
            bool Has(params string[] words) => words.Any(word => value.Contains(word, StringComparison.Ordinal));

            if (Has("DELETE", "REMOVE", "DESELECT", "UNSUPPRESS", "CANCEL")) return new ActionBadge { Text = "×", Color = Danger };
            if (Has("ADD", "CREATE", "NEW", "INSERT", "ASSIGN")) return new ActionBadge { Text = "+", Color = Success };
            if (Has("MOVE", "TRANSLATE", "POSITION", "PAN")) return new ActionBadge { IsMove = true, Color = Gold };
            if (Has("REPLACE", "REFRESH", "RELOAD", "UPDATE", "REGENERATE", "REDO")) return new ActionBadge { IsRefresh = true, Color = Gold };
            if (Has("OPEN", "IMPORT")) return new ActionBadge { IsArrow = true, Color = Success };
            if (Has("EXPORT", "SAVE_AS", "PUBLISH")) return new ActionBadge { IsArrow = true, Color = Gold, Text = "out" };
            if (Has("COPY", "DUPLICATE")) return new ActionBadge { Text = "2", Color = Gold };
            if (Has("MIRROR")) return new ActionBadge { Text = "M", Color = Gold };
            if (Has("PATTERN", "ARRAY")) return new ActionBadge { Text = "4", Color = Gold };
            if (Has("MEASURE", "ANALYSIS", "INFO")) return new ActionBadge { Text = "i", Color = Gold };
            if (Has("GENERATE", "SOLVE", "RUN")) return new ActionBadge { Text = "▶", Color = Success };
            if (Has("VERIFY", "CHECK", "VALIDATE", "ACCEPT", "APPLY", "OK")) return new ActionBadge { Text = "✓", Color = Success };
            if (Has("HIDE", "SUPPRESS")) return new ActionBadge { Text = "−", Color = Danger };
            if (Has("SHOW", "UNHIDE")) return new ActionBadge { Text = "+", Color = Success };
            if (Has("EDIT", "MODIFY", "RENAME")) return new ActionBadge { Text = "E", Color = Gold };
            if (Has("FIT", "ZOOM")) return new ActionBadge { Text = "↗", Color = Gold };
            if (Has("ROTATE", "REVOLVE")) return new ActionBadge { Text = "↻", Color = Gold };
            if (Has("SELECT_ALL")) return new ActionBadge { Text = "A", Color = Success };
            if (Has("RESET")) return new ActionBadge { IsRefresh = true, Color = Gold };
            return null;
        }

        private static void DrawBadge(Graphics graphics, Rectangle box, float scale, ActionBadge badge)
        {
            RectangleF bounds = new RectangleF(
                box.Right - 11 * scale,
                box.Bottom - 11 * scale,
                10 * scale,
                10 * scale);

            using (var background = new SolidBrush(BadgeBack)) graphics.FillEllipse(background, bounds);
            using (var pen = new Pen(badge.Color, Math.Max(1f, 1.25f * scale)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawEllipse(pen, bounds);
                if (badge.IsMove)
                {
                    float cx = bounds.Left + bounds.Width / 2;
                    float cy = bounds.Top + bounds.Height / 2;
                    graphics.DrawLine(pen, cx - 3 * scale, cy, cx + 3 * scale, cy);
                    graphics.DrawLine(pen, cx, cy - 3 * scale, cx, cy + 3 * scale);
                    return;
                }
                if (badge.IsRefresh)
                {
                    graphics.DrawArc(pen, RectangleF.Inflate(bounds, -2 * scale, -2 * scale), 25, 285);
                    return;
                }
                if (badge.IsArrow)
                {
                    bool outward = string.Equals(badge.Text, "out", StringComparison.OrdinalIgnoreCase);
                    PointF from = outward
                        ? new PointF(bounds.Left + 2 * scale, bounds.Bottom - 2 * scale)
                        : new PointF(bounds.Right - 2 * scale, bounds.Top + 2 * scale);
                    PointF to = outward
                        ? new PointF(bounds.Right - 2 * scale, bounds.Top + 2 * scale)
                        : new PointF(bounds.Left + 2 * scale, bounds.Bottom - 2 * scale);
                    graphics.DrawLine(pen, from, to);
                    graphics.DrawLine(pen, to, new PointF(to.X, to.Y + (outward ? 3 : -3) * scale));
                    graphics.DrawLine(pen, to, new PointF(to.X + (outward ? -3 : 3) * scale, to.Y));
                    return;
                }
            }

            using (var font = new Font("Segoe UI Semibold", Math.Max(5f, 6.2f * scale), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(badge.Color))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                graphics.DrawString(badge.Text ?? string.Empty, font, brush, bounds, format);
        }

        private static void DrawMonogram(Graphics graphics, Rectangle box, float scale, string commandName, string commandId)
        {
            string source = string.IsNullOrWhiteSpace(commandName) ? commandId : commandName;
            string[] words = (source ?? "NX").Split(new[] { ' ', '_', '-', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string initials = string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
            if (string.IsNullOrWhiteSpace(initials)) initials = "NX";

            RectangleF center = new RectangleF(box.Left + 7 * scale, box.Top + 7 * scale, 18 * scale, 18 * scale);
            using (var font = new Font("Segoe UI Semibold", Math.Max(6f, 7.5f * scale), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Gold))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                graphics.DrawString(initials, font, brush, center, format);
        }

        private static void DrawIdentityMarks(Graphics graphics, Rectangle box, float scale, string commandId, string commandName)
        {
            uint hash = StableHash((commandId ?? string.Empty) + "|" + (commandName ?? string.Empty));
            using var brush = new SolidBrush(Color.FromArgb(205, (hash & 1) == 0 ? Accent : Gold));
            for (int index = 0; index < 4; index++)
            {
                if (((hash >> index) & 1) == 0) continue;
                float x = box.Right - (4 + index * 2.7f) * scale;
                float y = box.Top + 2.2f * scale;
                graphics.FillRectangle(brush, x, y, 1.4f * scale, 3.3f * scale);
            }
        }

        private static Image TryLoadGeneratedThumbnail(string commandId, string commandName)
        {
            string fileName = BuildFileName(commandId, commandName);
            if (ImageCache.TryGetValue(fileName, out Image cached)) return cached;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] paths =
            {
                Path.Combine(baseDir, "assets", "icons", "commands", fileName),
                Path.Combine(baseDir, "..", "..", "..", "..", "assets", "icons", "commands", fileName),
                Path.Combine(baseDir, "Resources", "Icons", "commands", fileName),
                Path.Combine(Environment.CurrentDirectory, "assets", "icons", "commands", fileName)
            };

            foreach (string path in paths)
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using Image source = Image.FromStream(stream);
                    var clone = new Bitmap(source);
                    ImageCache[fileName] = clone;
                    return clone;
                }
                catch { }
            }
            return null;
        }

        private static string BuildFileName(string commandId, string commandName)
        {
            string source = string.IsNullOrWhiteSpace(commandId) ? commandName : commandId;
            if (string.IsNullOrWhiteSpace(source)) source = "unknown_command";
            var builder = new StringBuilder(source.Length);
            foreach (char character in source.Trim())
            {
                if (char.IsLetterOrDigit(character) || character == '_' || character == '-') builder.Append(character);
                else if (builder.Length == 0 || builder[builder.Length - 1] != '_') builder.Append('_');
            }
            string stem = builder.ToString().Trim('_');
            if (stem.Length > 80) stem = stem.Substring(0, 80);
            if (string.IsNullOrWhiteSpace(stem)) stem = "command";
            return stem + "_" + StableHash(source).ToString("X8") + ".png";
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= char.ToUpperInvariant(character);
                    hash *= 16777619;
                }
                return hash;
            }
        }
    }
}
