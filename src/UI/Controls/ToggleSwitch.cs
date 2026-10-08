using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppLauncher
{
    class ToggleSwitch : Control
    {
        bool on, hover;
        float pos;
        readonly Timer timer = new Timer { Interval = 15 };
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Font = Theme.Ui;
            Height = Theme.S(26);
            timer.Tick += (s, e) =>
            {
                float target = on ? 1f : 0f;
                pos += (target - pos) * 0.35f;
                if (Math.Abs(target - pos) < 0.02f) { pos = target; timer.Stop(); }
                Invalidate();
            };
        }

        public bool Checked { get { return on; } }

        public void SetChecked(bool value, bool animate)
        {
            on = value;
            if (animate) timer.Start(); else { pos = on ? 1 : 0; Invalidate(); }
        }

        public void FitWidth() { Width = Theme.S(54) + TextRenderer.MeasureText(Text, Font).Width + Theme.S(4); }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            SetChecked(!on, true);
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);
            int tw = Theme.S(40), th = Theme.S(20);
            var track = new RectangleF(1, (Height - th) / 2f, tw, th);
            using (var path = Theme.Round(track, th / 2f))
            {
                if (pos > 0.01f)
                {
                    using (var br = new LinearGradientBrush(track,
                        Color.FromArgb((int)(255 * pos), Theme.Accent), Color.FromArgb((int)(255 * pos), Theme.Accent2), LinearGradientMode.Horizontal))
                        g.FillPath(br, path);
                }
                using (var pen = new Pen(Color.FromArgb((int)(255 * (1 - pos)), hover ? Theme.Text : Theme.TextDim), Math.Max(1f, Theme.Scale)))
                    g.DrawPath(pen, path);
            }
            float knob = Theme.S(hover ? 14 : 12) + pos * Theme.S(1);
            float cx = track.X + th / 2f + pos * (tw - th);
            float cy = track.Y + th / 2f;
            using (var br = new SolidBrush(Theme.Mix(Theme.TextDim, Color.White, pos)))
                g.FillEllipse(br, cx - knob / 2, cy - knob / 2, knob, knob);

            Theme.DrawText(g, Text, Font, new Rectangle(Theme.S(52), 0, Width - Theme.S(52), Height), Theme.Text, TextFormatFlags.Left);
        }
    }
}
