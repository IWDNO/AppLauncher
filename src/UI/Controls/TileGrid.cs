using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AppLauncher
{
    class TileGrid : Control
    {
        public List<LaunchItem> Items = new List<LaunchItem>();
        readonly HashSet<LaunchItem> selected = new HashSet<LaunchItem>();
        readonly ToolTip tip = new ToolTip { InitialDelay = 700 };

        public event Action<LaunchItem> RunItem;
        public event Action ChecksChanged, Reordered, SelectionChanged;
        public event Action<Point> MenuRequested;
        public event Action<string[]> FilesDropped;

        int scroll, hover = -1, anchor = -1;
        bool hoverPlay, hoverCheck, hoverPill, externalDrag;
        int pressIndex = -1, dropIndex = -1;
        Point pressPoint;
        bool dragging, thumbDrag;
        int thumbOffset;

        int MinTileW { get { return Theme.S(150); } }
        int TileH { get { return Theme.S(156); } }
        int Gap { get { return Theme.S(12); } }
        int PadX { get { return Theme.S(20); } }
        int PadY { get { return Theme.S(4); } }

        public TileGrid()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.Bg;
            AllowDrop = true;
        }

        public List<LaunchItem> Selected { get { return Items.Where(selected.Contains).ToList(); } }

        public void SetItems(List<LaunchItem> items)
        {
            Items = items;
            selected.RemoveWhere(i => !items.Contains(i));
            ClampScroll();
            Invalidate();
        }

        public void Select(LaunchItem it)
        {
            selected.Clear();
            if (it != null) { selected.Add(it); anchor = Items.IndexOf(it); EnsureVisible(anchor); }
            Invalidate();
        }

        // --- геометрия ---
        int AvailW { get { return Math.Max(MinTileW, ClientSize.Width - PadX * 2); } }
        int Cols { get { return Math.Max(1, (AvailW + Gap) / (MinTileW + Gap)); } }
        int TileW { get { return (AvailW - Gap * (Cols - 1)) / Cols; } }
        int Rows { get { return (Items.Count + Cols - 1) / Cols; } }
        int ContentHeight { get { return Items.Count == 0 ? 0 : PadY * 2 + Rows * (TileH + Gap) - Gap; } }
        int MaxScroll { get { return Math.Max(0, ContentHeight - ClientSize.Height); } }
        void ClampScroll() { scroll = Math.Max(0, Math.Min(scroll, MaxScroll)); }

        Rectangle TileRect(int i)
        {
            int c = i % Cols, r = i / Cols;
            return new Rectangle(PadX + c * (TileW + Gap), PadY + r * (TileH + Gap) - scroll, TileW, TileH);
        }
        Rectangle CheckRect(Rectangle r) { int s = Theme.S(22); return new Rectangle(r.X + Theme.S(12), r.Y + Theme.S(12), s, s); }
        Rectangle CheckHit(Rectangle r) { var c = CheckRect(r); c.Inflate(Theme.S(8), Theme.S(8)); return c; }
        Rectangle PlayRect(Rectangle r) { int s = Theme.S(34); return new Rectangle(r.Right - s - Theme.S(8), r.Y + Theme.S(8), s, s); }

        int HitTile(Point p)
        {
            for (int i = 0; i < Items.Count; i++) if (TileRect(i).Contains(p)) return i;
            return -1;
        }

        Rectangle ThumbRect()
        {
            if (MaxScroll <= 0) return Rectangle.Empty;
            int track = ClientSize.Height - Theme.S(16);
            int th = Math.Max(Theme.S(40), track * ClientSize.Height / ContentHeight);
            int y = Theme.S(8) + (track - th) * scroll / MaxScroll;
            int w = Theme.S(6);
            return new Rectangle(ClientSize.Width - w - Theme.S(5), y, w, th);
        }

        public void EnsureVisible(int i)
        {
            if (i < 0) return;
            var r = TileRect(i);
            if (r.Top < 0) scroll += r.Top - PadY;
            else if (r.Bottom > ClientSize.Height) scroll += r.Bottom - ClientSize.Height + PadY;
            ClampScroll();
            Invalidate();
        }

        // Позиция вставки при перетаскивании: перед плиткой под курсором или после неё
        int DropSlot(Point p)
        {
            int col = Math.Max(0, Math.Min(Cols - 1, (p.X - PadX) / (TileW + Gap)));
            int row = Math.Max(0, Math.Min(Rows - 1, (p.Y + scroll - PadY) / (TileH + Gap)));
            int idx = row * Cols + col;
            if (idx >= Items.Count) return Items.Count;
            var r = TileRect(idx);
            if (p.X > r.X + r.Width / 2) idx++;
            return Math.Min(idx, Items.Count);
        }

        // --- отрисовка ---
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);

            if (Items.Count == 0) { PaintEmpty(g); return; }

            for (int i = 0; i < Items.Count; i++)
            {
                var r = TileRect(i);
                if (r.Bottom < 0 || r.Top > ClientSize.Height) continue;
                PaintTile(g, Items[i], r, i == hover, dragging && i == pressIndex);
            }

            if (dragging && dropIndex >= 0) PaintDropMarker(g);

            var thumb = ThumbRect();
            if (!thumb.IsEmpty)
                using (var path = Theme.Round(thumb, thumb.Width / 2f))
                using (var br = new SolidBrush(thumbDrag ? Theme.TextDim : Theme.Border))
                    g.FillPath(br, path);

            if (externalDrag) PaintDropOverlay(g);
        }

        void PaintTile(Graphics g, LaunchItem it, Rectangle r, bool isHover, bool isDragged)
        {
            bool sel = selected.Contains(it);
            Color fill = isHover ? Theme.SurfaceHover : Theme.Surface;
            if (it.Enabled) fill = Theme.Mix(fill, Theme.Accent, 0.10f);
            if (isDragged) fill = Theme.Mix(fill, Theme.Bg, 0.5f);

            using (var path = Theme.Round(r, Theme.S(12)))
            {
                using (var br = new SolidBrush(fill)) g.FillPath(br, path);
                if (sel)
                    using (var pen = new Pen(Theme.Accent, Math.Max(1.5f, Theme.Scale * 2)))
                        g.DrawPath(pen, path);
                else if (it.Enabled)
                    using (var br = new LinearGradientBrush(r, Color.FromArgb(150, Theme.Accent), Color.FromArgb(150, Theme.Accent2), 45f))
                    using (var pen = new Pen(br, Math.Max(1f, Theme.Scale)))
                        g.DrawPath(pen, path);
            }

            // чекбокс
            var cb = CheckRect(r);
            bool cbHover = isHover && hoverCheck;
            using (var path = Theme.Round(cb, Theme.S(5)))
            {
                if (it.Enabled)
                {
                    using (var br = new LinearGradientBrush(cb, Theme.Accent, Theme.Accent2, 45f)) g.FillPath(br, path);
                    float k = cb.Width / 22f;
                    using (var pen = new Pen(Color.White, Theme.S(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(pen, new[] {
                            new PointF(cb.X + 6 * k, cb.Y + 11.5f * k),
                            new PointF(cb.X + 9.5f * k, cb.Y + 15 * k),
                            new PointF(cb.X + 16 * k, cb.Y + 7.5f * k) });
                }
                else
                {
                    if (cbHover) using (var br = new SolidBrush(Theme.SurfaceHover)) g.FillPath(br, path);
                    using (var pen = new Pen(cbHover ? Theme.Text : Theme.TextDim, Math.Max(1f, Theme.Scale))) g.DrawPath(pen, path);
                }
            }

            // кнопка «запустить» при наведении
            if (isHover && !dragging)
            {
                var pr = PlayRect(r);
                using (var path = Theme.Round(pr, pr.Width / 2f))
                {
                    if (hoverPlay)
                        using (var br = new LinearGradientBrush(pr, Theme.Accent, Theme.Accent2, 45f)) g.FillPath(br, path);
                    else
                        using (var br = new SolidBrush(Theme.Border)) g.FillPath(br, path);
                }
                Theme.DrawText(g, "\uF5B0", Theme.IconsSmall, new Rectangle(pr.X + Theme.S(1), pr.Y, pr.Width, pr.Height), Color.White, TextFormatFlags.HorizontalCenter);
            }

            // значок
            int isz = Theme.S(48);
            var ir = new Rectangle(r.X + (r.Width - isz) / 2, r.Y + Theme.S(28), isz, isz);
            if (it.Icon != null) g.DrawImage(it.Icon, ir);
            else Theme.DrawText(g, "\uE7BA", Theme.IconsBig, ir, Theme.Danger, TextFormatFlags.HorizontalCenter);

            // название (до двух строк)
            int tx = r.X + Theme.S(10), tw = r.Width - Theme.S(20);
            var nameR = new Rectangle(tx, ir.Bottom + Theme.S(10), tw, Theme.S(40));
            Color nameColor = it.Missing ? Theme.Danger : (it.Enabled ? Theme.Text : Theme.Mix(Theme.Text, Theme.TextDim, 0.4f));
            TextRenderer.DrawText(g, it.Name, Theme.UiBold, nameR, nameColor,
                TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak |
                TextFormatFlags.EndEllipsis | TextFormatFlags.TextBoxControl);

            // значки настроек запуска (справа сверху; при наведении там кнопка ▶)
            if (!isHover || dragging)
            {
                int bs = Theme.S(20), bx = r.Right - Theme.S(10) - bs, minX = cb.Right + Theme.S(6);
                foreach (var b in Badges(it))
                {
                    if (bx < minX) break;
                    var badge = new Rectangle(bx, cb.Y + (cb.Height - bs) / 2, bs, bs);
                    using (var path = Theme.Round(badge, bs / 2f))
                    using (var br = new SolidBrush(Color.FromArgb(55, Theme.Accent)))
                        g.FillPath(br, path);
                    Theme.DrawText(g, b, Theme.IconsSmall, badge, Theme.Mix(Theme.Accent, Color.White, 0.5f), TextFormatFlags.HorizontalCenter);
                    bx -= bs + Theme.S(3);
                }
            }

            // нижняя строка: аргументы (щелчок — вкл/выкл), «не найден» или тип
            var pill = PillRect(r, it);
            if (!pill.IsEmpty)
            {
                bool on = it.ArgsEnabled;
                bool pillHover = isHover && hoverPill;
                using (var path = Theme.Round(pill, pill.Height / 2f))
                {
                    Color fillC = on ? Color.FromArgb(pillHover ? 80 : 50, Theme.Accent) : (pillHover ? Theme.SurfaceHover : Theme.Bg);
                    using (var br = new SolidBrush(fillC)) g.FillPath(br, path);
                    if (!on) using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
                }
                // «лампочка» включённости
                int d = Theme.S(8);
                var dot = new Rectangle(pill.X + Theme.S(8), pill.Y + (pill.Height - d) / 2, d, d);
                if (on) using (var br = new LinearGradientBrush(dot, Theme.Accent, Theme.Accent2, 45f)) g.FillEllipse(br, dot);
                else using (var pen = new Pen(Theme.TextFaint, Math.Max(1f, Theme.Scale))) g.DrawEllipse(pen, dot);

                var textR = new Rectangle(dot.Right + Theme.S(4), pill.Y, pill.Right - dot.Right - Theme.S(10), pill.Height);
                Theme.DrawText(g, FitPill(it.Args, PillMaxText(r)), on ? Theme.UiSmall : Theme.UiSmallStrike, textR,
                    on ? Theme.Mix(Theme.Accent, Color.White, 0.4f) : Theme.TextFaint, TextFormatFlags.HorizontalCenter);
            }
            else
            {
                var subR = new Rectangle(tx, r.Bottom - Theme.S(28), tw, Theme.S(20));
                Theme.DrawText(g, it.Missing ? "Файл не найден" : Kind(it.Path), Theme.UiSmall, subR,
                    it.Missing ? Theme.Danger : Theme.TextFaint, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        static IEnumerable<string> Badges(LaunchItem it)
        {
            if (it.CloseDelay >= 0) yield return "\uE8BB";
            if (it.StartDelay > 0) yield return "\uE916";
            if (it.Priority == LaunchPriority.High || it.Priority == LaunchPriority.AboveNormal) yield return "\uE74A";
            if (it.Priority == LaunchPriority.Low || it.Priority == LaunchPriority.BelowNormal) yield return "\uE74B";
            if (it.Window == WindowMode.Minimized) yield return "\uE921";
            if (it.Window == WindowMode.Maximized) yield return "\uE922";
            if (it.Window == WindowMode.Hidden) yield return "\uED1A";
            if (it.RunAsAdmin) yield return "\uE7EF";
        }

        int PillMaxText(Rectangle r) { return r.Width - Theme.S(20) - Theme.S(34); }

        static string FitPill(string a, int maxW)
        {
            while (a.Length > 1 && TextRenderer.MeasureText(a, Theme.UiSmall).Width > maxW) a = a.Substring(0, a.Length - 2) + "…";
            return a;
        }

        Rectangle PillRect(Rectangle r, LaunchItem it)
        {
            if (it.Args == "" || it.Missing) return Rectangle.Empty;
            int pw = TextRenderer.MeasureText(FitPill(it.Args, PillMaxText(r)), Theme.UiSmall).Width + Theme.S(34);
            return new Rectangle(r.X + (r.Width - pw) / 2, r.Bottom - Theme.S(28), pw, Theme.S(20));
        }

        static readonly string[] WindowNames = { "обычное", "свёрнутое", "развёрнутое", "скрытое" };
        static readonly string[] PriorityNames = { "обычный", "низкий", "ниже среднего", "выше среднего", "высокий" };

        static string Describe(LaunchItem it)
        {
            var sb = new StringBuilder(it.Name + "\n" + it.Path);
            if (it.Args != "") sb.Append("\nАргументы: " + it.Args + (it.ArgsEnabled ? "" : " (выключены)") + "  — щелчок по ним вкл/выкл");
            if (it.Window != WindowMode.Normal) sb.Append("\nОкно: " + WindowNames[(int)it.Window]);
            if (it.RunAsAdmin) sb.Append("\nОт имени администратора");
            if (it.Priority != LaunchPriority.Normal) sb.Append("\nПриоритет: " + PriorityNames[(int)it.Priority]);
            if (it.StartDelay > 0) sb.Append("\nЗадержка перед запуском: " + it.StartDelay + " с");
            if (it.CloseDelay >= 0) sb.Append("\nОкно закроется через " + it.CloseDelay + " с после появления");
            return sb.ToString();
        }

        static string Kind(string path)
        {
            if (Directory.Exists(path)) return "Папка";
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".exe": return "Программа";
                case ".lnk": return "Ярлык";
                case ".url": return "Ссылка";
                case ".bat": case ".cmd": case ".ps1": case ".vbs": case ".py": case ".js": case ".sh": return "Скрипт " + ext;
                case "": return "Файл";
                default: return "Файл " + ext;
            }
        }

        void PaintDropMarker(Graphics g)
        {
            Rectangle r;
            float x;
            if (dropIndex < Items.Count) { r = TileRect(dropIndex); x = r.X - Gap / 2f; }
            else { r = TileRect(Items.Count - 1); x = r.Right + Gap / 2f; }
            var bar = new RectangleF(x - Theme.S(2), r.Y + Theme.S(8), Theme.S(4), r.Height - Theme.S(16));
            using (var path = Theme.Round(bar, Theme.S(2)))
            using (var br = new LinearGradientBrush(bar, Theme.Accent, Theme.Accent2, LinearGradientMode.Vertical))
                g.FillPath(br, path);
        }

        void PaintEmpty(Graphics g)
        {
            var r = new RectangleF(PadX, PadY, ClientSize.Width - PadX * 2, ClientSize.Height - PadY - Theme.S(16));
            using (var path = Theme.Round(r, Theme.S(14)))
            using (var pen = new Pen(externalDrag ? Theme.Accent : Theme.Border, Math.Max(1.5f, Theme.Scale * 1.5f)) { DashStyle = DashStyle.Dash })
            {
                if (externalDrag) using (var br = new SolidBrush(Color.FromArgb(25, Theme.Accent))) g.FillPath(br, path);
                g.DrawPath(pen, path);
            }
            int cy = (int)(r.Y + r.Height / 2);
            Theme.DrawText(g, "\uE896", Theme.IconsBig, new Rectangle((int)r.X, cy - Theme.S(80), (int)r.Width, Theme.S(60)),
                externalDrag ? Theme.Accent : Theme.TextFaint, TextFormatFlags.HorizontalCenter);
            Theme.DrawText(g, "Перетащите сюда программы, скрипты, файлы или папки", Theme.UiBold,
                new Rectangle((int)r.X, cy - Theme.S(10), (int)r.Width, Theme.S(26)), Theme.Text, TextFormatFlags.HorizontalCenter);
            Theme.DrawText(g, "или нажмите «Добавить» — а можно просто положить их в папку Apps", Theme.Ui,
                new Rectangle((int)r.X, cy + Theme.S(16), (int)r.Width, Theme.S(24)), Theme.TextDim, TextFormatFlags.HorizontalCenter);
        }

        void PaintDropOverlay(Graphics g)
        {
            var r = new RectangleF(Theme.S(8), Theme.S(2), ClientSize.Width - Theme.S(16), ClientSize.Height - Theme.S(6));
            using (var path = Theme.Round(r, Theme.S(14)))
            using (var br = new SolidBrush(Color.FromArgb(40, Theme.Accent)))
            using (var pen = new Pen(Theme.Accent, Math.Max(1.5f, Theme.Scale * 1.5f)) { DashStyle = DashStyle.Dash })
            {
                g.FillPath(br, path);
                g.DrawPath(pen, path);
            }
        }

        // --- мышь ---
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll -= e.Delta * (TileH / 2) / 120;
            ClampScroll();
            UpdateHover(e.Location);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            var thumb = ThumbRect();
            if (e.Button == MouseButtons.Left && !thumb.IsEmpty && e.X >= thumb.Left - Theme.S(4))
            {
                if (thumb.Contains(new Point(thumb.X, e.Y))) { thumbDrag = true; thumbOffset = e.Y - thumb.Y; }
                else { scroll += e.Y < thumb.Y ? -ClientSize.Height : ClientSize.Height; ClampScroll(); }
                Invalidate();
                return;
            }

            int i = HitTile(e.Location);
            if (i < 0)
            {
                if (e.Button == MouseButtons.Left) { selected.Clear(); RaiseSelection(); Invalidate(); }
                return;
            }
            var it = Items[i];
            var r = TileRect(i);

            if (e.Button == MouseButtons.Right)
            {
                if (!selected.Contains(it)) { selected.Clear(); selected.Add(it); anchor = i; RaiseSelection(); }
                Invalidate();
                if (MenuRequested != null) MenuRequested(e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            if (CheckHit(r).Contains(e.Location))
            {
                it.Enabled = !it.Enabled;
                if (ChecksChanged != null) ChecksChanged();
                Invalidate();
                return;
            }
            if (PlayRect(r).Contains(e.Location))
            {
                if (RunItem != null) RunItem(it);
                return;
            }
            if (PillRect(r, it).Contains(e.Location))
            {
                it.ArgsEnabled = !it.ArgsEnabled;
                if (ChecksChanged != null) ChecksChanged();
                tip.SetToolTip(this, Describe(it));
                Invalidate();
                return;
            }

            if ((ModifierKeys & Keys.Control) != 0)
            {
                if (!selected.Remove(it)) selected.Add(it);
                anchor = i;
            }
            else if ((ModifierKeys & Keys.Shift) != 0 && anchor >= 0)
            {
                selected.Clear();
                for (int k = Math.Min(anchor, i); k <= Math.Max(anchor, i); k++) selected.Add(Items[k]);
            }
            else
            {
                if (!selected.Contains(it) || selected.Count > 1) { selected.Clear(); selected.Add(it); }
                anchor = i;
                pressIndex = i;
                pressPoint = e.Location;
            }
            RaiseSelection();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (thumbDrag)
            {
                int track = ClientSize.Height - Theme.S(16);
                int free = track - ThumbRect().Height;
                if (free > 0) scroll = (e.Y - thumbOffset - Theme.S(8)) * MaxScroll / free;
                ClampScroll();
                Invalidate();
                return;
            }
            if (pressIndex >= 0 && e.Button == MouseButtons.Left)
            {
                if (!dragging && (Math.Abs(e.Y - pressPoint.Y) > Theme.S(6) || Math.Abs(e.X - pressPoint.X) > Theme.S(6)))
                {
                    dragging = true;
                    Cursor = Cursors.SizeAll;
                    tip.Hide(this);
                }
                if (dragging)
                {
                    dropIndex = DropSlot(e.Location);
                    if (e.Y < Theme.S(24)) { scroll -= Theme.S(14); ClampScroll(); }
                    else if (e.Y > ClientSize.Height - Theme.S(24)) { scroll += Theme.S(14); ClampScroll(); }
                    Invalidate();
                    return;
                }
            }
            UpdateHover(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (thumbDrag) { thumbDrag = false; Invalidate(); return; }
            if (dragging && pressIndex >= 0 && dropIndex >= 0)
            {
                var it = Items[pressIndex];
                int to = dropIndex > pressIndex ? dropIndex - 1 : dropIndex;
                if (to != pressIndex)
                {
                    Items.RemoveAt(pressIndex);
                    Items.Insert(to, it);
                    anchor = to;
                    if (Reordered != null) Reordered();
                }
            }
            dragging = false;
            pressIndex = dropIndex = -1;
            Cursor = Cursors.Default;
            UpdateHover(e.Location);
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            int i = HitTile(e.Location);
            if (i < 0 || e.Button != MouseButtons.Left) return;
            var r = TileRect(i);
            if (CheckHit(r).Contains(e.Location) || PlayRect(r).Contains(e.Location) || PillRect(r, Items[i]).Contains(e.Location)) return;
            if (RunItem != null) RunItem(Items[i]);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = -1;
            tip.SetToolTip(this, null);
            Invalidate();
            base.OnMouseLeave(e);
        }

        void UpdateHover(Point p)
        {
            int h = HitTile(p);
            bool hp = false, hc = false, hpl = false;
            if (h >= 0)
            {
                var r = TileRect(h);
                hp = PlayRect(r).Contains(p);
                hc = CheckHit(r).Contains(p);
                hpl = PillRect(r, Items[h]).Contains(p);
            }
            if (h != hover) tip.SetToolTip(this, h >= 0 ? Describe(Items[h]) : null);
            if (h != hover || hp != hoverPlay || hc != hoverCheck || hpl != hoverPill)
            {
                hover = h; hoverPlay = hp; hoverCheck = hc; hoverPill = hpl;
                Cursor = hp || hc || hpl ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        void RaiseSelection() { if (SelectionChanged != null) SelectionChanged(); }

        // --- клавиатура ---
        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || k == Keys.Space || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Items.Count == 0 || e.Alt) { base.OnKeyDown(e); return; }
            int step = 0;
            if (e.KeyCode == Keys.Left) step = -1;
            else if (e.KeyCode == Keys.Right) step = 1;
            else if (e.KeyCode == Keys.Up) step = -Cols;
            else if (e.KeyCode == Keys.Down) step = Cols;

            if (step != 0)
            {
                int i = anchor < 0 ? 0 : anchor + step;
                i = Math.Max(0, Math.Min(Items.Count - 1, i));
                Select(Items[i]);
                RaiseSelection();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space)
            {
                foreach (var it in Selected) it.Enabled = !it.Enabled;
                if (ChecksChanged != null) ChecksChanged();
                Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.A && e.Control)
            {
                foreach (var it in Items) selected.Add(it);
                RaiseSelection();
                Invalidate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnResize(EventArgs e) { ClampScroll(); base.OnResize(e); }

        // --- перетаскивание файлов извне ---
        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Link; externalDrag = true; Invalidate(); }
            else e.Effect = DragDropEffects.None;
        }

        protected override void OnDragLeave(EventArgs e) { externalDrag = false; Invalidate(); }

        protected override void OnDragDrop(DragEventArgs e)
        {
            externalDrag = false;
            Invalidate();
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && FilesDropped != null) FilesDropped(files);
        }
    }
}
