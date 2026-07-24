using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace NX2512_HotkeyStudio.UI
{
    public static class CadIconPainter
    {
        private static readonly ConcurrentDictionary<string, Image> imageCache =
            new ConcurrentDictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public static void ClearCache() => imageCache.Clear();

        public static void Draw(Graphics g, Rectangle box, string hint, string commandId, string commandName = "")
        {
            SmoothingMode prevMode = g.SmoothingMode;
            PixelOffsetMode prevPixel = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Image png = GetIconPng(hint, commandId, commandName);
            if (png != null)
            {
                DrawBackgroundContainer(g, box);
                InterpolationMode prevInterp = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                Rectangle imgBox = new Rectangle(box.Left + 2, box.Top + 2, box.Width - 4, box.Height - 4);
                g.DrawImage(png, imgBox);
                g.InterpolationMode = prevInterp;
                g.SmoothingMode = prevMode;
                g.PixelOffsetMode = prevPixel;
                return;
                }

            DrawBackgroundContainer(g, box);

            Color cyan = Color.FromArgb(91, 201, 223);
            Color gold = Color.FromArgb(240, 196, 90);
            Color danger = Color.FromArgb(255, 124, 139);

            string id = (commandId ?? string.Empty).ToUpperInvariant();
            string name = (commandName ?? string.Empty).ToUpperInvariant();
            string h = (string.IsNullOrWhiteSpace(hint) ? Models.CommandIconHints.FromCommand(commandId, commandName) : hint).Trim().ToLowerInvariant();

            // Specific Command Variations
            if (id.Contains("ADD_COMPONENT")) DrawAssyAdd(g, box, cyan, gold);
            else if (id.Contains("REMOVE_COMPONENT")) DrawAssyRemove(g, box, cyan, danger);
            else if (id.Contains("MOVE_COMPONENT")) DrawAssyMove(g, box, cyan, gold);
            else if (id.Contains("REPLACE_COMPONENT")) DrawAssyReplace(g, box, cyan, gold);
            else if (id.Contains("NEW_COMPONENT")) DrawAssyNew(g, box, cyan, gold);
            else if (id.Contains("LINE_FROM_MIDPOINT")) DrawLineMidpoint(g, box, cyan, gold);
            else if (id.Contains("CIRCLE_BY_THREE_POINTS")) DrawCircle3P(g, box, cyan, gold);
            else if (id.Contains("RECTANGLE_FROM_CENTER")) DrawRectCenter(g, box, cyan, gold);
            else if (id.Contains("PARALLEL_CONSTRAINT")) DrawConstraintParallel(g, box, cyan, gold);
            else if (id.Contains("PERPENDICULAR_CONSTRAINT")) DrawConstraintPerp(g, box, cyan, gold);
            else if (id.Contains("TANGENT_CONSTRAINT")) DrawConstraintTangent(g, box, cyan, gold);
            else if (id.Contains("COINCIDENT_CONSTRAINT")) DrawConstraintCoincident(g, box, cyan, gold);
            else if (id.Contains("HORIZONTAL_CONSTRAINT")) DrawConstraintHoriz(g, box, cyan, gold);
            else if (id.Contains("VERTICAL_CONSTRAINT")) DrawConstraintVert(g, box, cyan, gold);
            else if (id.Contains("SECTION_VIEW")) DrawViewSection(g, box, cyan, gold);
            else if (id.Contains("DETAIL_VIEW")) DrawViewDetail(g, box, cyan, gold);
            else if (id.Contains("PROJECTED_VIEW")) DrawViewProjected(g, box, cyan, gold);
            else if (id.Contains("CAM_CREATE_TOOL")) DrawCamTool(g, box, cyan, gold);
            else if (id.Contains("CAM_GENERATE") || id.Contains("TOOL_PATH")) DrawCamPath(g, box, cyan, gold);
            else if (id.Contains("EXTRUDE") || name.Contains("EXTRUDE")) DrawExtrude(g, box, cyan, gold);
            else if (id.Contains("REVOLVE") || name.Contains("REVOLVE")) DrawRevolve(g, box, cyan, gold);
            else if (id.Contains("HOLE") || name.Contains("HOLE")) DrawHole(g, box, cyan, gold);
            else if (id.Contains("BLEND") || id.Contains("FILLET") || name.Contains("BLEND") || name.Contains("FILLET")) DrawBlend(g, box, cyan, gold);
            else if (id.Contains("CHAMFER") || name.Contains("CHAMFER")) DrawChamfer(g, box, cyan, gold);
            else if (id.Contains("RECTANGLE") || name.Contains("RECTANGLE")) DrawRectangle(g, box, cyan, gold);
            else if (id.Contains("CIRCLE") || name.Contains("CIRCLE")) DrawCircle(g, box, cyan, gold);
            else if (id.Contains("ARC") || name.Contains("ARC")) DrawArc(g, box, cyan, gold);
            else if (id.Contains("LINE") || name.Contains("LINE")) DrawLine(g, box, cyan, gold);
            else if (id.Contains("CONSTRAINT") || name.Contains("CONSTRAINT")) DrawConstraint(g, box, cyan, gold);
            else if (id.Contains("PATTERN") || name.Contains("PATTERN")) DrawPattern(g, box, cyan, gold);
            else if (id.Contains("MIRROR") || name.Contains("MIRROR")) DrawMirror(g, box, cyan, gold);
            else if (id.Contains("WAVE") || name.Contains("WAVE")) DrawWave(g, box, cyan, gold);
            else if (id.Contains("LAYER") || name.Contains("LAYER")) DrawLayer(g, box, cyan, gold);
            else if (id.Contains("MATERIAL") || name.Contains("MATERIAL")) DrawMaterial(g, box, cyan, gold);
            else if (id.Contains("SBSM") || id.Contains("FLANGE") || id.Contains("SHEET") || name.Contains("SHEET")) DrawSheetMetal(g, box, cyan, gold);
            else if (id.Contains("BODY_PRIORITY") || name.Contains("BODY")) DrawSelBody(g, box, cyan, gold);
            else if (id.Contains("FACE_PRIORITY") || name.Contains("FACE")) DrawSelFace(g, box, cyan, gold);
            else if (id.Contains("EDGE_PRIORITY") || name.Contains("EDGE")) DrawSelEdge(g, box, cyan, gold);
            else if (id.Contains("DESELECT") || name.Contains("СНЯТЬ")) DrawSelDeselect(g, box, cyan, danger);
            else if (id.Contains("SELECT") || h == "selection") DrawSelection(g, box, cyan, gold);
            else if (id.Contains("COMPONENT") || h == "assembly") DrawAssembly(g, box, cyan, gold);
            else if (id.Contains("MEASURE") || id.Contains("INFO") || h == "inspect") DrawInspect(g, box, cyan, gold);
            else if (id.Contains("FIT") || id.Contains("ORIENT") || id.Contains("VIEW") || h == "view") DrawView(g, box, cyan, gold);
            else if (h == "menu") DrawMenu(g, box, cyan, gold);
            else
            {
                switch (h)
                {
                    case "sketch": DrawSketch(g, box, cyan, gold); break;
                    case "feature": DrawExtrude(g, box, cyan, gold); break;
                    case "pattern": DrawPattern(g, box, cyan, gold); break;
                    case "selection": DrawSelection(g, box, cyan, gold); break;
                    case "assembly": DrawAssembly(g, box, cyan, gold); break;
                    case "sheet_metal": DrawSheetMetal(g, box, cyan, gold); break;
                    case "wave": DrawWave(g, box, cyan, gold); break;
                    case "layer": DrawLayer(g, box, cyan, gold); break;
                    case "material": DrawMaterial(g, box, cyan, gold); break;
                    case "inspect": DrawInspect(g, box, cyan, gold); break;
                    case "view": DrawView(g, box, cyan, gold); break;
                    default: DrawDefaultCommand(g, box, cyan, gold); break;
                }
            }

            g.SmoothingMode = prevMode;
            g.PixelOffsetMode = prevPixel;
        }

        private static void DrawBackgroundContainer(Graphics g, Rectangle box)
        {
            if (box.Width <= 0 || box.Height <= 0) return;
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(box, Color.FromArgb(24, 44, 60), Color.FromArgb(14, 26, 36), 45f))
            using (Pen borderPen = new Pen(Color.FromArgb(52, 90, 112), 1.2f))
            {
                g.FillRectangle(bgBrush, box);
                g.DrawRectangle(borderPen, box.Left, box.Top, box.Width - 1, box.Height - 1);
            }
        }

        // Sub-command specific detailed drawings
        private static void DrawAssyAdd(Graphics g, Rectangle b, Color c, Color gold)
        {
            DrawAssembly(g, b, c, gold);
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.2f * scale))
            {
                float cx = b.Right - 8 * scale;
                float cy = b.Bottom - 8 * scale;
                g.DrawLine(goldPen, cx - 4 * scale, cy, cx + 4 * scale, cy);
                g.DrawLine(goldPen, cx, cy - 4 * scale, cx, cy + 4 * scale);
            }
        }

        private static void DrawAssyRemove(Graphics g, Rectangle b, Color c, Color danger)
        {
            DrawAssembly(g, b, c, Color.FromArgb(240, 196, 90));
            float scale = b.Width / 32f;
            using (Pen dangerPen = new Pen(danger, 2.5f * scale))
            {
                float cx = b.Right - 8 * scale;
                float cy = b.Bottom - 8 * scale;
                g.DrawLine(dangerPen, cx - 3 * scale, cy - 3 * scale, cx + 3 * scale, cy + 3 * scale);
                g.DrawLine(dangerPen, cx + 3 * scale, cy - 3 * scale, cx - 3 * scale, cy + 3 * scale);
            }
        }

        private static void DrawAssyMove(Graphics g, Rectangle b, Color c, Color gold)
        {
            DrawAssembly(g, b, c, gold);
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            {
                float cx = b.Left + 16 * scale;
                float cy = b.Top + 16 * scale;
                g.DrawLine(goldPen, cx - 6 * scale, cy, cx + 6 * scale, cy);
                g.DrawLine(goldPen, cx, cy - 6 * scale, cx, cy + 6 * scale);
            }
        }

        private static void DrawAssyReplace(Graphics g, Rectangle b, Color c, Color gold)
        {
            DrawAssembly(g, b, c, gold);
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            {
                g.DrawArc(goldPen, b.Left + 10 * scale, b.Top + 10 * scale, 12 * scale, 12 * scale, 30, 220);
            }
        }

        private static void DrawAssyNew(Graphics g, Rectangle b, Color c, Color gold)
        {
            DrawAssembly(g, b, c, gold);
            float scale = b.Width / 32f;
            using (SolidBrush goldBrush = new SolidBrush(gold))
            {
                g.FillEllipse(goldBrush, b.Right - 10 * scale, b.Top + 6 * scale, 5 * scale, 5 * scale);
            }
        }

        private static void DrawLineMidpoint(Graphics g, Rectangle b, Color c, Color gold)
        {
            DrawLine(g, b, c, gold);
            float scale = b.Width / 32f;
            using (SolidBrush goldBrush = new SolidBrush(gold))
            {
                g.FillEllipse(goldBrush, b.Left + 14 * scale, b.Top + 14 * scale, 5 * scale, 5 * scale);
            }
        }

        private static void DrawCircle3P(Graphics g, Rectangle b, Color c, Color gold)
        {
            DrawCircle(g, b, c, gold);
            float scale = b.Width / 32f;
            using (SolidBrush goldBrush = new SolidBrush(gold))
            {
                g.FillEllipse(goldBrush, b.Left + 6 * scale, b.Top + 14 * scale, 4 * scale, 4 * scale);
                g.FillEllipse(goldBrush, b.Right - 9 * scale, b.Top + 14 * scale, 4 * scale, 4 * scale);
                g.FillEllipse(goldBrush, b.Left + 14 * scale, b.Top + 6 * scale, 4 * scale, 4 * scale);
            }
        }

        private static void DrawRectCenter(Graphics g, Rectangle b, Color c, Color gold)
        {
            DrawRectangle(g, b, c, gold);
            float scale = b.Width / 32f;
            using (SolidBrush goldBrush = new SolidBrush(gold))
            {
                g.FillEllipse(goldBrush, b.Left + 14 * scale, b.Top + 14 * scale, 5 * scale, 5 * scale);
            }
        }

        private static void DrawConstraintParallel(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.5f * scale))
            {
                g.DrawLine(goldPen, b.Left + 10 * scale, b.Top + 8 * scale, b.Left + 10 * scale, b.Bottom - 8 * scale);
                g.DrawLine(goldPen, b.Left + 18 * scale, b.Top + 8 * scale, b.Left + 18 * scale, b.Bottom - 8 * scale);
            }
        }

        private static void DrawConstraintPerp(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.5f * scale))
            {
                g.DrawLine(goldPen, b.Left + 8 * scale, b.Bottom - 8 * scale, b.Right - 8 * scale, b.Bottom - 8 * scale);
                g.DrawLine(goldPen, b.Left + 16 * scale, b.Top + 8 * scale, b.Left + 16 * scale, b.Bottom - 8 * scale);
            }
        }

        private static void DrawConstraintTangent(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            {
                g.DrawEllipse(goldPen, b.Left + 6 * scale, b.Top + 6 * scale, 12 * scale, 12 * scale);
                g.DrawLine(goldPen, b.Left + 6 * scale, b.Top + 18 * scale, b.Right - 6 * scale, b.Top + 18 * scale);
            }
        }

        private static void DrawConstraintCoincident(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (SolidBrush cyanDot = new SolidBrush(c))
            using (SolidBrush goldDot = new SolidBrush(gold))
            {
                g.FillEllipse(cyanDot, b.Left + 9 * scale, b.Top + 11 * scale, 10 * scale, 10 * scale);
                g.FillEllipse(goldDot, b.Left + 13 * scale, b.Top + 11 * scale, 10 * scale, 10 * scale);
            }
        }

        private static void DrawConstraintHoriz(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.8f * scale))
            {
                g.DrawLine(goldPen, b.Left + 6 * scale, b.Top + 16 * scale, b.Right - 6 * scale, b.Top + 16 * scale);
            }
        }

        private static void DrawConstraintVert(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.8f * scale))
            {
                g.DrawLine(goldPen, b.Left + 16 * scale, b.Top + 6 * scale, b.Left + 16 * scale, b.Bottom - 6 * scale);
            }
        }

        private static void DrawViewSection(Graphics g, Rectangle b, Color c, Color gold)
        {
            DrawView(g, b, c, gold);
            float scale = b.Width / 32f;
            using (Pen goldDash = new Pen(gold, 1.8f * scale) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(goldDash, b.Left + 4 * scale, b.Top + 16 * scale, b.Right - 4 * scale, b.Top + 16 * scale);
            }
        }

        private static void DrawViewDetail(Graphics g, Rectangle b, Color c, Color gold)
        {
            DrawView(g, b, c, gold);
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            {
                g.DrawEllipse(goldPen, b.Left + 10 * scale, b.Top + 10 * scale, 12 * scale, 12 * scale);
            }
        }

        private static void DrawViewProjected(Graphics g, Rectangle b, Color c, Color gold)
        {
            DrawView(g, b, c, gold);
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            {
                g.DrawLine(goldPen, b.Left + 6 * scale, b.Top + 16 * scale, b.Right - 6 * scale, b.Top + 16 * scale);
                g.DrawLine(goldPen, b.Right - 9 * scale, b.Top + 12 * scale, b.Right - 6 * scale, b.Top + 16 * scale);
                g.DrawLine(goldPen, b.Right - 9 * scale, b.Top + 20 * scale, b.Right - 6 * scale, b.Top + 16 * scale);
            }
        }

        private static void DrawCamTool(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.8f * scale))
            using (SolidBrush goldFill = new SolidBrush(gold))
            {
                PointF[] cutter = {
                    new PointF(b.Left + 12 * scale, b.Top + 6 * scale),
                    new PointF(b.Right - 12 * scale, b.Top + 6 * scale),
                    new PointF(b.Right - 12 * scale, b.Top + 20 * scale),
                    new PointF(b.Left + 16 * scale, b.Bottom - 5 * scale),
                    new PointF(b.Left + 12 * scale, b.Top + 20 * scale)
                };
                g.FillPolygon(goldFill, cutter);
                g.DrawPolygon(cyanPen, cutter);
            }
        }

        private static void DrawCamPath(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.2f * scale))
            {
                PointF[] toolpath = {
                    new PointF(b.Left + 6 * scale, b.Top + 8 * scale),
                    new PointF(b.Right - 6 * scale, b.Top + 8 * scale),
                    new PointF(b.Right - 6 * scale, b.Top + 16 * scale),
                    new PointF(b.Left + 6 * scale, b.Top + 16 * scale),
                    new PointF(b.Left + 6 * scale, b.Bottom - 8 * scale),
                    new PointF(b.Right - 6 * scale, b.Bottom - 8 * scale)
                };
                g.DrawLines(goldPen, toolpath);
            }
        }

        private static void DrawExtrude(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.6f * scale))
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            using (SolidBrush topFill = new SolidBrush(Color.FromArgb(70, c)))
            using (SolidBrush sideFill = new SolidBrush(Color.FromArgb(30, c)))
            {
                PointF[] top = {
                    new PointF(b.Left + 16 * scale, b.Top + 6 * scale),
                    new PointF(b.Left + 26 * scale, b.Top + 11 * scale),
                    new PointF(b.Left + 16 * scale, b.Top + 16 * scale),
                    new PointF(b.Left + 6 * scale, b.Top + 11 * scale)
                };
                g.FillPolygon(topFill, top);
                g.DrawPolygon(cyanPen, top);

                PointF[] leftSide = {
                    new PointF(b.Left + 6 * scale, b.Top + 11 * scale),
                    new PointF(b.Left + 16 * scale, b.Top + 16 * scale),
                    new PointF(b.Left + 16 * scale, b.Top + 24 * scale),
                    new PointF(b.Left + 6 * scale, b.Top + 19 * scale)
                };
                g.FillPolygon(sideFill, leftSide);
                g.DrawPolygon(cyanPen, leftSide);

                PointF[] rightSide = {
                    new PointF(b.Left + 16 * scale, b.Top + 16 * scale),
                    new PointF(b.Left + 26 * scale, b.Top + 11 * scale),
                    new PointF(b.Left + 26 * scale, b.Top + 19 * scale),
                    new PointF(b.Left + 16 * scale, b.Top + 24 * scale)
                };
                g.FillPolygon(sideFill, rightSide);
                g.DrawPolygon(cyanPen, rightSide);

                // Upward Golden Extrusion Arrow
                float ax = b.Left + 16 * scale;
                float ay1 = b.Top + 26 * scale;
                float ay2 = b.Top + 12 * scale;
                g.DrawLine(goldPen, ax, ay1, ax, ay2);
                g.DrawLine(goldPen, ax - 3 * scale, ay2 + 4 * scale, ax, ay2);
                g.DrawLine(goldPen, ax + 3 * scale, ay2 + 4 * scale, ax, ay2);
            }
        }

        private static void DrawRevolve(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.8f * scale))
            using (Pen goldDash = new Pen(gold, 1.5f * scale) { DashStyle = DashStyle.Dash })
            using (Pen goldArrow = new Pen(gold, 2.0f * scale))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(40, c)))
            {
                // Central Axis
                g.DrawLine(goldDash, b.Left + 16 * scale, b.Top + 4 * scale, b.Left + 16 * scale, b.Bottom - 4 * scale);

                // Revolving Torus Arc
                RectangleF arcBox = new RectangleF(b.Left + 6 * scale, b.Top + 7 * scale, 20 * scale, 18 * scale);
                g.FillEllipse(fill, arcBox);
                g.DrawEllipse(cyanPen, arcBox);

                // Revolve Arrow Arc
                g.DrawArc(goldArrow, b.Left + 4 * scale, b.Top + 12 * scale, 24 * scale, 12 * scale, 0, 160);
                float endX = b.Left + 28 * scale;
                float endY = b.Top + 18 * scale;
                g.DrawLine(goldArrow, endX - 3 * scale, endY - 3 * scale, endX, endY);
                g.DrawLine(goldArrow, endX - 3 * scale, endY + 3 * scale, endX, endY);
            }
        }

        private static void DrawHole(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.6f * scale))
            using (Pen goldPen = new Pen(gold, 1.8f * scale))
            using (Pen goldDash = new Pen(gold, 1.2f * scale) { DashStyle = DashStyle.Dash })
            using (SolidBrush blockFill = new SolidBrush(Color.FromArgb(40, c)))
            using (SolidBrush holeDark = new SolidBrush(Color.FromArgb(12, 20, 28)))
            {
                // Outer Plate Block
                RectangleF block = new RectangleF(b.Left + 5 * scale, b.Top + 6 * scale, 22 * scale, 20 * scale);
                g.FillRectangle(blockFill, block);
                g.DrawRectangle(cyanPen, block.X, block.Y, block.Width, block.Height);

                // Drilled Hole Ellipse Top
                RectangleF hole = new RectangleF(b.Left + 11 * scale, b.Top + 11 * scale, 10 * scale, 6 * scale);
                g.FillEllipse(holeDark, hole);
                g.DrawEllipse(goldPen, hole);

                // Depth Dashed Lines
                g.DrawLine(goldDash, hole.X, hole.Y + 3 * scale, hole.X, hole.Y + 11 * scale);
                g.DrawLine(goldDash, hole.Right, hole.Y + 3 * scale, hole.Right, hole.Y + 11 * scale);
                g.DrawArc(goldDash, hole.X, hole.Y + 8 * scale, hole.Width, hole.Height, 0, 180);
            }
        }

        private static void DrawBlend(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.8f * scale))
            using (Pen goldPen = new Pen(gold, 2.5f * scale))
            using (SolidBrush goldDot = new SolidBrush(gold))
            {
                // Corner Base Lines
                g.DrawLine(cyanPen, b.Left + 6 * scale, b.Top + 6 * scale, b.Left + 6 * scale, b.Bottom - 6 * scale);
                g.DrawLine(cyanPen, b.Left + 6 * scale, b.Bottom - 6 * scale, b.Right - 6 * scale, b.Bottom - 6 * scale);

                // Fillet Arc Highlight
                g.DrawArc(goldPen, b.Left + 6 * scale, b.Top + 6 * scale, 20 * scale, 20 * scale, 90, 90);

                // Control Tangent Dots
                g.FillEllipse(goldDot, b.Left + 5 * scale, b.Top + 16 * scale, 3 * scale, 3 * scale);
                g.FillEllipse(goldDot, b.Left + 16 * scale, b.Bottom - 7 * scale, 3 * scale, 3 * scale);
            }
        }

        private static void DrawChamfer(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.8f * scale))
            using (Pen goldPen = new Pen(gold, 2.5f * scale))
            using (SolidBrush goldDot = new SolidBrush(gold))
            {
                // Corner Base Lines
                g.DrawLine(cyanPen, b.Left + 6 * scale, b.Top + 6 * scale, b.Left + 6 * scale, b.Top + 14 * scale);
                g.DrawLine(cyanPen, b.Left + 14 * scale, b.Bottom - 6 * scale, b.Right - 6 * scale, b.Bottom - 6 * scale);

                // Bevel Flat Line Cut
                g.DrawLine(goldPen, b.Left + 6 * scale, b.Top + 14 * scale, b.Left + 14 * scale, b.Bottom - 6 * scale);

                // Corner Vertices
                g.FillEllipse(goldDot, b.Left + 5 * scale, b.Top + 13 * scale, 3 * scale, 3 * scale);
                g.FillEllipse(goldDot, b.Left + 13 * scale, b.Bottom - 7 * scale, 3 * scale, 3 * scale);
            }
        }

        private static void DrawRectangle(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 2.0f * scale))
            using (SolidBrush goldBrush = new SolidBrush(gold))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(35, c)))
            {
                RectangleF r = new RectangleF(b.Left + 7 * scale, b.Top + 8 * scale, 18 * scale, 16 * scale);
                g.FillRectangle(fill, r);
                g.DrawRectangle(cyanPen, r.X, r.Y, r.Width, r.Height);

                // Corner Vertex Handles
                g.FillEllipse(goldBrush, r.Left - 2 * scale, r.Top - 2 * scale, 5 * scale, 5 * scale);
                g.FillEllipse(goldBrush, r.Right - 3 * scale, r.Top - 2 * scale, 5 * scale, 5 * scale);
                g.FillEllipse(goldBrush, r.Left - 2 * scale, r.Bottom - 3 * scale, 5 * scale, 5 * scale);
                g.FillEllipse(goldBrush, r.Right - 3 * scale, r.Bottom - 3 * scale, 5 * scale, 5 * scale);
            }
        }

        private static void DrawCircle(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 2.0f * scale))
            using (Pen goldDash = new Pen(gold, 1.2f * scale) { DashStyle = DashStyle.Dash })
            using (SolidBrush goldBrush = new SolidBrush(gold))
            {
                RectangleF r = new RectangleF(b.Left + 6 * scale, b.Top + 6 * scale, 20 * scale, 20 * scale);
                g.DrawEllipse(cyanPen, r);

                // Center point and crosshair Ticks
                float cx = r.Left + r.Width / 2;
                float cy = r.Top + r.Height / 2;
                g.DrawLine(goldDash, cx - 6 * scale, cy, cx + 6 * scale, cy);
                g.DrawLine(goldDash, cx, cy - 6 * scale, cx, cy + 6 * scale);
                g.FillEllipse(goldBrush, cx - 2 * scale, cy - 2 * scale, 4 * scale, 4 * scale);
            }
        }

        private static void DrawArc(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 2.2f * scale))
            using (SolidBrush goldBrush = new SolidBrush(gold))
            {
                RectangleF r = new RectangleF(b.Left + 6 * scale, b.Top + 6 * scale, 20 * scale, 20 * scale);
                g.DrawArc(cyanPen, r, 200, 140);

                // 3 Arc Points
                g.FillEllipse(goldBrush, b.Left + 8 * scale, b.Top + 16 * scale, 4 * scale, 4 * scale);
                g.FillEllipse(goldBrush, b.Left + 16 * scale, b.Top + 6 * scale, 4 * scale, 4 * scale);
                g.FillEllipse(goldBrush, b.Right - 10 * scale, b.Top + 16 * scale, 4 * scale, 4 * scale);
            }
        }

        private static void DrawLine(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 2.4f * scale))
            using (SolidBrush goldBrush = new SolidBrush(gold))
            {
                PointF p1 = new PointF(b.Left + 7 * scale, b.Bottom - 7 * scale);
                PointF p2 = new PointF(b.Right - 7 * scale, b.Top + 7 * scale);
                g.DrawLine(cyanPen, p1, p2);

                g.FillEllipse(goldBrush, p1.X - 2.5f * scale, p1.Y - 2.5f * scale, 5 * scale, 5 * scale);
                g.FillEllipse(goldBrush, p2.X - 2.5f * scale, p2.Y - 2.5f * scale, 5 * scale, 5 * scale);
            }
        }

        private static void DrawConstraint(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 2.0f * scale))
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            using (SolidBrush goldBrush = new SolidBrush(gold))
            {
                // Parallel Sketch Lines
                g.DrawLine(cyanPen, b.Left + 6 * scale, b.Bottom - 8 * scale, b.Left + 14 * scale, b.Top + 8 * scale);
                g.DrawLine(cyanPen, b.Left + 12 * scale, b.Bottom - 8 * scale, b.Left + 20 * scale, b.Top + 8 * scale);

                // Lock / Constraint Badge
                RectangleF lockRect = new RectangleF(b.Right - 12 * scale, b.Bottom - 12 * scale, 8 * scale, 7 * scale);
                g.FillRectangle(goldBrush, lockRect);
                g.DrawArc(goldPen, lockRect.X + 1.5f * scale, lockRect.Y - 4 * scale, 5 * scale, 5 * scale, 180, 180);
            }
        }

        private static void DrawPattern(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.5f * scale))
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            using (SolidBrush cyanFill = new SolidBrush(Color.FromArgb(45, c)))
            using (SolidBrush goldFill = new SolidBrush(Color.FromArgb(90, gold)))
            {
                // 2x2 Matrix of Feature Instances
                g.FillRectangle(cyanFill, b.Left + 6 * scale, b.Top + 6 * scale, 8 * scale, 8 * scale);
                g.DrawRectangle(cyanPen, b.Left + 6 * scale, b.Top + 6 * scale, 8 * scale, 8 * scale);

                g.FillRectangle(cyanFill, b.Right - 14 * scale, b.Top + 6 * scale, 8 * scale, 8 * scale);
                g.DrawRectangle(cyanPen, b.Right - 14 * scale, b.Top + 6 * scale, 8 * scale, 8 * scale);

                g.FillRectangle(cyanFill, b.Left + 6 * scale, b.Bottom - 14 * scale, 8 * scale, 8 * scale);
                g.DrawRectangle(cyanPen, b.Left + 6 * scale, b.Bottom - 14 * scale, 8 * scale, 8 * scale);

                g.FillRectangle(goldFill, b.Right - 14 * scale, b.Bottom - 14 * scale, 8 * scale, 8 * scale);
                g.DrawRectangle(goldPen, b.Right - 14 * scale, b.Bottom - 14 * scale, 8 * scale, 8 * scale);
            }
        }

        private static void DrawMirror(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen dashPen = new Pen(gold, 1.5f * scale) { DashStyle = DashStyle.Dash })
            using (Pen cyanPen = new Pen(c, 1.8f * scale))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(50, c)))
            {
                // Symmetry Line
                g.DrawLine(dashPen, b.Left + 16 * scale, b.Top + 4 * scale, b.Left + 16 * scale, b.Bottom - 4 * scale);

                PointF[] p1 = { new PointF(b.Left + 5 * scale, b.Top + 8 * scale), new PointF(b.Left + 12 * scale, b.Top + 16 * scale), new PointF(b.Left + 5 * scale, b.Bottom - 8 * scale) };
                PointF[] p2 = { new PointF(b.Right - 5 * scale, b.Top + 8 * scale), new PointF(b.Right - 12 * scale, b.Top + 16 * scale), new PointF(b.Right - 5 * scale, b.Bottom - 8 * scale) };

                g.FillPolygon(fill, p1); g.DrawPolygon(cyanPen, p1);
                g.FillPolygon(fill, p2); g.DrawPolygon(cyanPen, p2);
            }
        }

        private static void DrawWave(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 2.4f * scale))
            using (SolidBrush goldBrush = new SolidBrush(gold))
            {
                PointF[] pts = {
                    new PointF(b.Left + 5 * scale, b.Top + 18 * scale),
                    new PointF(b.Left + 11 * scale, b.Top + 8 * scale),
                    new PointF(b.Left + 21 * scale, b.Top + 24 * scale),
                    new PointF(b.Right - 5 * scale, b.Top + 12 * scale)
                };
                g.DrawCurve(cyanPen, pts);

                g.FillEllipse(goldBrush, b.Left + 9 * scale, b.Top + 7 * scale, 5 * scale, 5 * scale);
                g.FillEllipse(goldBrush, b.Right - 11 * scale, b.Top + 14 * scale, 5 * scale, 5 * scale);
            }
        }

        private static void DrawLayer(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.6f * scale))
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            using (SolidBrush goldFill = new SolidBrush(Color.FromArgb(60, gold)))
            using (SolidBrush cyanFill = new SolidBrush(Color.FromArgb(30, c)))
            {
                // Top Gold Layer Plane
                PointF[] top = { new PointF(b.Left + 16 * scale, b.Top + 5 * scale), new PointF(b.Right - 5 * scale, b.Top + 10 * scale), new PointF(b.Left + 16 * scale, b.Top + 15 * scale), new PointF(b.Left + 5 * scale, b.Top + 10 * scale) };
                g.FillPolygon(goldFill, top); g.DrawPolygon(goldPen, top);

                // Middle Layer Line
                g.DrawLine(cyanPen, b.Left + 5 * scale, b.Top + 15 * scale, b.Left + 16 * scale, b.Top + 20 * scale);
                g.DrawLine(cyanPen, b.Right - 5 * scale, b.Top + 15 * scale, b.Left + 16 * scale, b.Top + 20 * scale);

                // Bottom Layer Line
                g.DrawLine(cyanPen, b.Left + 5 * scale, b.Top + 20 * scale, b.Left + 16 * scale, b.Top + 25 * scale);
                g.DrawLine(cyanPen, b.Right - 5 * scale, b.Top + 20 * scale, b.Left + 16 * scale, b.Top + 25 * scale);
            }
        }

        private static void DrawMaterial(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.8f * scale))
            using (LinearGradientBrush fill = new LinearGradientBrush(b, Color.FromArgb(70, c), Color.FromArgb(20, c), 45f))
            using (SolidBrush goldHighlight = new SolidBrush(gold))
            {
                RectangleF sphere = new RectangleF(b.Left + 6 * scale, b.Top + 6 * scale, 20 * scale, 20 * scale);
                g.FillEllipse(fill, sphere);
                g.DrawEllipse(cyanPen, sphere);

                g.FillEllipse(goldHighlight, sphere.X + 5 * scale, sphere.Y + 5 * scale, 5 * scale, 5 * scale);
            }
        }

        private static void DrawSheetMetal(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.8f * scale))
            using (Pen goldPen = new Pen(gold, 2.2f * scale))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(40, c)))
            {
                // Base Sheet Panel
                g.FillRectangle(fill, b.Left + 5 * scale, b.Top + 7 * scale, 10 * scale, 18 * scale);
                g.DrawRectangle(cyanPen, b.Left + 5 * scale, b.Top + 7 * scale, 10 * scale, 18 * scale);

                // Flange Bend Lines
                g.DrawLine(goldPen, b.Left + 15 * scale, b.Top + 7 * scale, b.Right - 5 * scale, b.Top + 13 * scale);
                g.DrawLine(goldPen, b.Left + 15 * scale, b.Bottom - 7 * scale, b.Right - 5 * scale, b.Bottom - 1 * scale);
                g.DrawLine(goldPen, b.Right - 5 * scale, b.Top + 13 * scale, b.Right - 5 * scale, b.Bottom - 1 * scale);
            }
        }

        private static void DrawSelBody(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 2.0f * scale))
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            using (SolidBrush bodyFill = new SolidBrush(Color.FromArgb(70, c)))
            {
                PointF[] cube = {
                    new PointF(b.Left + 16 * scale, b.Top + 5 * scale),
                    new PointF(b.Right - 6 * scale, b.Top + 10 * scale),
                    new PointF(b.Right - 6 * scale, b.Bottom - 10 * scale),
                    new PointF(b.Left + 16 * scale, b.Bottom - 5 * scale),
                    new PointF(b.Left + 6 * scale, b.Bottom - 10 * scale),
                    new PointF(b.Left + 6 * scale, b.Top + 10 * scale)
                };
                g.FillPolygon(bodyFill, cube);
                g.DrawPolygon(cyanPen, cube);

                // Highlighted Bounding Select Ring
                g.DrawRectangle(goldPen, b.Left + 4 * scale, b.Top + 3 * scale, 24 * scale, 26 * scale);
            }
        }

        private static void DrawSelFace(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.8f * scale))
            using (Pen goldPen = new Pen(gold, 2.2f * scale))
            using (SolidBrush goldFace = new SolidBrush(Color.FromArgb(140, gold)))
            {
                PointF[] topFace = {
                    new PointF(b.Left + 16 * scale, b.Top + 5 * scale),
                    new PointF(b.Right - 6 * scale, b.Top + 10 * scale),
                    new PointF(b.Left + 16 * scale, b.Top + 16 * scale),
                    new PointF(b.Left + 6 * scale, b.Top + 10 * scale)
                };
                g.FillPolygon(goldFace, topFace);
                g.DrawPolygon(goldPen, topFace);

                // Body Outline
                g.DrawLine(cyanPen, b.Left + 6 * scale, b.Top + 10 * scale, b.Left + 6 * scale, b.Bottom - 8 * scale);
                g.DrawLine(cyanPen, b.Right - 6 * scale, b.Top + 10 * scale, b.Right - 6 * scale, b.Bottom - 8 * scale);
                g.DrawLine(cyanPen, b.Left + 16 * scale, b.Top + 16 * scale, b.Left + 16 * scale, b.Bottom - 3 * scale);
            }
        }

        private static void DrawSelEdge(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.6f * scale))
            using (Pen goldEdge = new Pen(gold, 3.2f * scale))
            {
                // Body cube lines
                g.DrawLine(cyanPen, b.Left + 6 * scale, b.Top + 10 * scale, b.Left + 16 * scale, b.Top + 5 * scale);
                g.DrawLine(cyanPen, b.Left + 16 * scale, b.Top + 5 * scale, b.Right - 6 * scale, b.Top + 10 * scale);

                // Glowing Selected Front Edge
                g.DrawLine(goldEdge, b.Left + 16 * scale, b.Top + 16 * scale, b.Left + 16 * scale, b.Bottom - 4 * scale);
            }
        }

        private static void DrawSelDeselect(Graphics g, Rectangle b, Color c, Color danger)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.8f * scale))
            using (Pen dangerPen = new Pen(danger, 2.8f * scale))
            {
                // Cursor Pointer Arrow
                PointF[] cursor = {
                    new PointF(b.Left + 6 * scale, b.Top + 5 * scale),
                    new PointF(b.Left + 16 * scale, b.Top + 19 * scale),
                    new PointF(b.Left + 12 * scale, b.Top + 17 * scale),
                    new PointF(b.Left + 6 * scale, b.Bottom - 6 * scale)
                };
                g.DrawPolygon(cyanPen, cursor);

                // Red/Danger X cancel symbol
                g.DrawLine(dangerPen, b.Right - 12 * scale, b.Bottom - 12 * scale, b.Right - 4 * scale, b.Bottom - 4 * scale);
                g.DrawLine(dangerPen, b.Right - 4 * scale, b.Bottom - 12 * scale, b.Right - 12 * scale, b.Bottom - 4 * scale);
            }
        }

        private static void DrawSelection(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen dashPen = new Pen(c, 1.5f * scale) { DashStyle = DashStyle.Dash })
            using (Pen goldCross = new Pen(gold, 2.0f * scale))
            using (SolidBrush goldDot = new SolidBrush(gold))
            {
                // Dashed Marquee
                g.DrawRectangle(dashPen, b.Left + 5 * scale, b.Top + 5 * scale, 22 * scale, 22 * scale);

                // Central Focus Target
                float cx = b.Left + 16 * scale;
                float cy = b.Top + 16 * scale;
                g.DrawLine(goldCross, cx - 4 * scale, cy, cx + 4 * scale, cy);
                g.DrawLine(goldCross, cx, cy - 4 * scale, cx, cy + 4 * scale);
                g.FillEllipse(goldDot, cx - 2 * scale, cy - 2 * scale, 4 * scale, 4 * scale);
            }
        }

        private static void DrawAssembly(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.6f * scale))
            using (Pen goldPen = new Pen(gold, 1.6f * scale))
            using (SolidBrush cyanFill = new SolidBrush(Color.FromArgb(50, c)))
            using (SolidBrush goldFill = new SolidBrush(Color.FromArgb(60, gold)))
            {
                // Component 1 (Cyan)
                RectangleF comp1 = new RectangleF(b.Left + 5 * scale, b.Top + 12 * scale, 12 * scale, 12 * scale);
                g.FillRectangle(cyanFill, comp1); g.DrawRectangle(cyanPen, comp1.X, comp1.Y, comp1.Width, comp1.Height);

                // Component 2 (Gold Interlocking)
                RectangleF comp2 = new RectangleF(b.Right - 17 * scale, b.Top + 5 * scale, 12 * scale, 12 * scale);
                g.FillRectangle(goldFill, comp2); g.DrawRectangle(goldPen, comp2.X, comp2.Y, comp2.Width, comp2.Height);
            }
        }

        private static void DrawInspect(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            using (Pen cyanPen = new Pen(c, 1.5f * scale))
            {
                // Caliper Extension Lines
                g.DrawLine(goldPen, b.Left + 6 * scale, b.Top + 6 * scale, b.Left + 6 * scale, b.Bottom - 6 * scale);
                g.DrawLine(goldPen, b.Right - 6 * scale, b.Top + 6 * scale, b.Right - 6 * scale, b.Bottom - 6 * scale);

                // Dimension Arrow Line
                float cy = b.Top + 16 * scale;
                g.DrawLine(goldPen, b.Left + 6 * scale, cy, b.Right - 6 * scale, cy);
                g.DrawLine(goldPen, b.Left + 6 * scale, cy, b.Left + 10 * scale, cy - 3 * scale);
                g.DrawLine(goldPen, b.Left + 6 * scale, cy, b.Left + 10 * scale, cy + 3 * scale);
                g.DrawLine(goldPen, b.Right - 6 * scale, cy, b.Right - 10 * scale, cy - 3 * scale);
                g.DrawLine(goldPen, b.Right - 6 * scale, cy, b.Right - 10 * scale, cy + 3 * scale);
            }
        }

        private static void DrawView(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.8f * scale))
            using (SolidBrush goldBrush = new SolidBrush(gold))
            {
                // 3D View Axis / Bounding Corner
                float cx = b.Left + 16 * scale;
                float cy = b.Top + 16 * scale;
                g.DrawLine(cyanPen, cx, cy, b.Left + 7 * scale, b.Bottom - 7 * scale);
                g.DrawLine(cyanPen, cx, cy, b.Right - 7 * scale, b.Bottom - 7 * scale);
                g.DrawLine(cyanPen, cx, cy, cx, b.Top + 6 * scale);

                // View Eye Node
                g.FillEllipse(goldBrush, cx - 3 * scale, cy - 3 * scale, 6 * scale, 6 * scale);
            }
        }

        private static void DrawMenu(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 1.6f * scale))
            using (Pen goldPen = new Pen(gold, 2.0f * scale))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(35, c)))
            {
                RectangleF menuBox = new RectangleF(b.Left + 5 * scale, b.Top + 6 * scale, 22 * scale, 20 * scale);
                g.FillRectangle(fill, menuBox);
                g.DrawRectangle(cyanPen, menuBox.X, menuBox.Y, menuBox.Width, menuBox.Height);

                g.DrawLine(cyanPen, b.Left + 9 * scale, b.Top + 11 * scale, b.Left + 18 * scale, b.Top + 11 * scale);
                g.DrawLine(cyanPen, b.Left + 9 * scale, b.Top + 16 * scale, b.Left + 20 * scale, b.Top + 16 * scale);
                g.DrawLine(cyanPen, b.Left + 9 * scale, b.Top + 21 * scale, b.Left + 15 * scale, b.Top + 21 * scale);

                // Submenu Arrow Indicator
                g.DrawLine(goldPen, b.Right - 9 * scale, b.Top + 12 * scale, b.Right - 6 * scale, b.Top + 16 * scale);
                g.DrawLine(goldPen, b.Right - 6 * scale, b.Top + 16 * scale, b.Right - 9 * scale, b.Top + 20 * scale);
            }
        }

        private static void DrawSketch(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen dashPen = new Pen(c, 1.2f * scale) { DashStyle = DashStyle.Dash })
            using (Pen goldPen = new Pen(gold, 2.4f * scale))
            using (SolidBrush cyanBrush = new SolidBrush(c))
            {
                // Sketch Coordinate Grid Border
                g.DrawRectangle(dashPen, b.Left + 5 * scale, b.Top + 5 * scale, 22 * scale, 22 * scale);

                // Spline Line
                PointF[] spline = {
                    new PointF(b.Left + 7 * scale, b.Bottom - 7 * scale),
                    new PointF(b.Left + 14 * scale, b.Top + 10 * scale),
                    new PointF(b.Right - 7 * scale, b.Top + 7 * scale)
                };
                g.DrawCurve(goldPen, spline);

                g.FillEllipse(cyanBrush, spline[0].X - 2.5f * scale, spline[0].Y - 2.5f * scale, 5 * scale, 5 * scale);
                g.FillEllipse(cyanBrush, spline[2].X - 2.5f * scale, spline[2].Y - 2.5f * scale, 5 * scale, 5 * scale);
            }
        }

        private static void DrawDefaultCommand(Graphics g, Rectangle b, Color c, Color gold)
        {
            float scale = b.Width / 32f;
            using (Pen cyanPen = new Pen(c, 2.0f * scale))
            using (SolidBrush goldBrush = new SolidBrush(gold))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(40, c)))
            {
                PointF[] diamond = {
                    new PointF(b.Left + 16 * scale, b.Top + 5 * scale),
                    new PointF(b.Right - 5 * scale, b.Top + 16 * scale),
                    new PointF(b.Left + 16 * scale, b.Bottom - 5 * scale),
                    new PointF(b.Left + 5 * scale, b.Top + 16 * scale)
                };
                g.FillPolygon(fill, diamond);
                g.DrawPolygon(cyanPen, diamond);

                g.FillEllipse(goldBrush, b.Left + 14 * scale, b.Top + 14 * scale, 4 * scale, 4 * scale);
            }
        }

        private static Image GetIconPng(string hint, string commandId, string commandName)
        {
            string fileName = GetPngFileName(hint, commandId, commandName);
            if (imageCache.TryGetValue(fileName, out Image img)) return img;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchPaths = new string[]
            {
                System.IO.Path.Combine(baseDir, "assets", "icons", fileName),
                System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "assets", "icons", fileName),
                System.IO.Path.Combine(baseDir, "Resources", "Icons", fileName),
                System.IO.Path.Combine(Environment.CurrentDirectory, "assets", "icons", fileName)
            };

            foreach (string path in searchPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    try
                    {
                        var info = new System.IO.FileInfo(path);
                        if (info.Length > 50000) continue; // Bypass old legacy duplicate files
                        using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                        {
                            Image loaded = Image.FromStream(stream);
                            imageCache[fileName] = loaded;
                            return loaded;
                        }
                    }
                    catch { }
                }
            }
            return null;
        }

        private static string GetPngFileName(string hint, string commandId, string commandName)
        {
            string id = (commandId ?? string.Empty).ToUpperInvariant();
            string name = (commandName ?? string.Empty).ToUpperInvariant();
            string h = (string.IsNullOrWhiteSpace(hint) ? Models.CommandIconHints.FromCommand(commandId, commandName) : hint).Trim().ToLowerInvariant();

            if (id.Contains("EXTRUDE") || name.Contains("EXTRUDE")) return "extrude.png";
            if (id.Contains("REVOLVE") || name.Contains("REVOLVE")) return "revolve.png";
            if (id.Contains("HOLE") || name.Contains("HOLE")) return "hole.png";
            if (id.Contains("BLEND") || id.Contains("FILLET") || name.Contains("BLEND") || name.Contains("FILLET")) return "blend.png";
            if (id.Contains("CHAMFER") || name.Contains("CHAMFER")) return "chamfer.png";
            if (id.Contains("RECTANGLE") || name.Contains("RECTANGLE")) return "rectangle.png";
            if (id.Contains("CIRCLE") || name.Contains("CIRCLE")) return "circle.png";
            if (id.Contains("ARC") || name.Contains("ARC")) return "arc.png";
            if (id.Contains("LINE") || name.Contains("LINE")) return "line.png";
            if (id.Contains("CONSTRAINT") || name.Contains("CONSTRAINT")) return "constraint.png";
            if (id.Contains("PATTERN") || name.Contains("PATTERN")) return "pattern.png";
            if (id.Contains("MIRROR") || name.Contains("MIRROR")) return "mirror.png";
            if (id.Contains("WAVE") || name.Contains("WAVE")) return "wave.png";
            if (id.Contains("LAYER") || name.Contains("LAYER")) return "layer.png";
            if (id.Contains("MATERIAL") || name.Contains("MATERIAL")) return "material.png";
            if (id.Contains("SBSM") || id.Contains("FLANGE") || id.Contains("SHEET") || name.Contains("SHEET")) return "sheet_metal.png";
            if (id.Contains("BODY_PRIORITY") || name.Contains("BODY")) return "sel_body.png";
            if (id.Contains("FACE_PRIORITY") || name.Contains("FACE")) return "sel_face.png";
            if (id.Contains("EDGE_PRIORITY") || name.Contains("EDGE")) return "sel_edge.png";
            if (id.Contains("DESELECT") || name.Contains("СНЯТЬ")) return "sel_deselect.png";
            if (id.Contains("SELECT") || h == "selection") return "selection.png";
            if (id.Contains("ADD_COMPONENT") || id.Contains("COMPONENT") || h == "assembly") return "assembly.png";
            if (id.Contains("MEASURE") || id.Contains("INFO") || h == "inspect") return "inspect.png";
            if (id.Contains("FIT") || id.Contains("ORIENT") || id.Contains("VIEW") || h == "view") return "view.png";
            if (h == "menu") return "menu.png";
            if (h == "sketch") return "sketch.png";
            return "default.png";
        }

        public static Bitmap RenderIconBitmap(int size, string hint, string commandId, string commandName = "")
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Draw(g, new Rectangle(0, 0, size, size), hint, commandId, commandName);
            }
            return bmp;
        }

        public static void ExportAllIcons(int size = 128)
        {
            ClearCache();
            string rootDir = AppDomain.CurrentDomain.BaseDirectory;
            string workspaceRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(rootDir, "..", "..", "..", ".."));

            string[] targetFolders = new string[]
            {
                System.IO.Path.Combine(workspaceRoot, "assets", "icons"),
                System.IO.Path.Combine(workspaceRoot, "docs", "assets", "icons"),
                System.IO.Path.Combine(workspaceRoot, "NX2512_HotkeyStudio", "Resources", "Icons"),
                System.IO.Path.Combine(rootDir, "assets", "icons")
            };

            var icons = new (string filename, string hint, string commandId, string name)[]
            {
                ("extrude.png", "feature", "UG_MODELING_EXTRUDED_FEATURE", "Extrude"),
                ("revolve.png", "feature", "UG_MODELING_REVOLVED_FEATURE", "Revolve"),
                ("hole.png", "feature", "UG_MODELING_HOLE_FEATURE", "Hole"),
                ("blend.png", "feature", "UG_MODELING_BLEND_FEATURE", "Edge Blend"),
                ("chamfer.png", "feature", "UG_MODELING_CHAMFER_FEATURE", "Chamfer"),
                ("rectangle.png", "sketch", "UG_SKETCH_RECTANGLE", "Rectangle"),
                ("circle.png", "sketch", "UG_SKETCH_CIRCLE", "Circle"),
                ("arc.png", "sketch", "UG_SKETCH_ARC", "Arc"),
                ("line.png", "sketch", "UG_SKETCH_LINE", "Line"),
                ("constraint.png", "sketch", "UG_SKETCH_CONSTRAINTS", "Constraints"),
                ("pattern.png", "pattern", "UG_MODELING_PATTERNFEATURE_FEATURE", "Pattern Feature"),
                ("mirror.png", "pattern", "UG_MODELING_MIRRORFEATURE_FEATURE", "Mirror Feature"),
                ("assembly.png", "assembly", "UG_ADD_COMPONENT", "Add Component"),
                ("sheet_metal.png", "sheet_metal", "UG_APP_SBSM", "Sheet Metal"),
                ("wave.png", "wave", "UG_ASSY_WAVE_LINKER", "WAVE Linker"),
                ("layer.png", "layer", "UG_LAYER_SETTINGS", "Layer Settings"),
                ("material.png", "material", "UG_MATERIAL_ASSIGN", "Material Assign"),
                ("sel_body.png", "selection", "UG_SEL_BODY_PRIORITY", "Body Priority"),
                ("sel_face.png", "selection", "UG_SEL_FACE_PRIORITY", "Face Priority"),
                ("sel_edge.png", "selection", "UG_SEL_EDGE_PRIORITY", "Edge Priority"),
                ("sel_deselect.png", "selection", "UG_SEL_DESELECT_ALL", "Deselect All"),
                ("selection.png", "selection", "UG_SEL_RESET", "Selection Filters"),
                ("inspect.png", "inspect", "UG_INFO_ANALYSIS", "Inspect"),
                ("view.png", "view", "UG_VIEW_FIT", "Fit View"),
                ("sketch.png", "sketch", "UG_CREATE_SKETCH", "Sketch"),
                ("menu.png", "menu", "", "Submenu"),
                ("default.png", "command", "", "Default Command")
            };

            foreach (string folder in targetFolders)
            {
                try
                {
                    System.IO.Directory.CreateDirectory(folder);
                    foreach (var (filename, hint, commandId, name) in icons)
                    {
                        using (Bitmap bmp = RenderIconBitmap(size, hint, commandId, name))
                        {
                            bmp.Save(System.IO.Path.Combine(folder, filename), System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }
                catch { }
            }
        }
    }
}

