using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppLauncher
{
    class FlatButton : Control
    {
        public string Glyph = "";
        public bool Primary;
        public bool IconOnly;
        bool hover, down;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Font = Theme.Ui;
            ForeColor = Theme.Text;
            Height = Theme.S(36);
        }

        public void FitWidth()
        {
            if (IconOnly) { Width = Height; return; }
            int w = TextRenderer.MeasureText(Text, Font).Width + Theme.S(32);
            if (Glyph != "") w += Theme.S(26);
            Width = w;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Primary ? Theme.S(10) : Theme.S(8);
            Color fg = ForeColor;

            using (var path = Theme.Round(r, rad))
            {
                if (Primary)
                {
                    if (Enabled)
                    {
                        Color a = Theme.Accent, b = Theme.Accent2;
                        if (hover) { a = Theme.Mix(a, Color.White, 0.12f); b = Theme.Mix(b, Color.White, 0.12f); }
                        if (down) { a = Theme.Mix(a, Color.Black, 0.15f); b = Theme.Mix(b, Color.Black, 0.15f); }
                        using (var br = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), a, b, LinearGradientMode.Horizontal))
                            g.FillPath(br, path);
                        using (var hl = new Pen(Color.FromArgb(60, 255, 255, 255)))
                            g.DrawLine(hl, rad, 1, Width - rad, 1);
                        fg = Color.White;
                    }
                    else
                    {
                        using (var br = new SolidBrush(Theme.Surface)) g.FillPath(br, path);
                        fg = Theme.TextFaint;
                    }
                }
                else
                {
                    Color fill = down ? Theme.Surface : hover ? Theme.SurfaceHover : (IconOnly ? BackColor : Theme.Surface);
                    using (var br = new SolidBrush(fill)) g.FillPath(br, path);
                    if (!IconOnly || hover)
                        using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
                    if (!Enabled) fg = Theme.TextFaint;
                }
            }

            Font font = Primary ? Theme.UiBold : Font;
            if (IconOnly)
            {
                Theme.DrawText(g, Glyph, Theme.Icons, ClientRectangle, fg, TextFormatFlags.HorizontalCenter);
                return;
            }
            int tw = TextRenderer.MeasureText(Text, font).Width;
            int gw = Glyph == "" ? 0 : Theme.S(26);
            int x = (Width - tw - gw) / 2;
            if (gw > 0)
                Theme.DrawText(g, Glyph, Theme.Icons, new Rectangle(x, 0, Theme.S(20), Height), fg, TextFormatFlags.HorizontalCenter);
            Theme.DrawText(g, Text, font, new Rectangle(x + gw, 0, tw + 4, Height), fg, TextFormatFlags.Left);
        }
    }
}
