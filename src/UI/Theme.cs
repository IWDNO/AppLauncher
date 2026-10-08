using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AppLauncher
{
    static class Theme
    {
        public static float Scale = 1f;

        public static readonly Color Bg = Hex(0x1B1B1F);
        public static readonly Color FooterBg = Hex(0x202025);
        public static readonly Color Surface = Hex(0x26262C);
        public static readonly Color SurfaceHover = Hex(0x2F2F36);
        public static readonly Color SurfaceSel = Hex(0x2C3048);
        public static readonly Color Border = Hex(0x393942);
        public static readonly Color Text = Hex(0xF3F3F6);
        public static readonly Color TextDim = Hex(0xA0A0AD);
        public static readonly Color TextFaint = Hex(0x6E6E7A);
        public static readonly Color Accent = Hex(0x7B83FF);
        public static readonly Color Accent2 = Hex(0x4FB6FF);
        public static readonly Color Danger = Hex(0xFF7070);

        public static Font Ui, UiSmall, UiSmallStrike,UiBold, Title, Icons, IconsSmall, IconsBig;

        public static void Init()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;

            var installed = new HashSet<string>(FontFamily.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            Func<string[], string> pick = names =>
            {
                foreach (var n in names) if (installed.Contains(n)) return n;
                return "Segoe UI";
            };
            string text = pick(new[] { "Segoe UI Variable Text", "Segoe UI" });
            string semi = pick(new[] { "Segoe UI Variable Text Semibold", "Segoe UI Semibold" });
            string disp = pick(new[] { "Segoe UI Variable Display Semibold", "Segoe UI Variable Display Semib", "Segoe UI Semibold" });
            string icons = pick(new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" });

            Ui = new Font(text, 10f);
            UiSmall = new Font(text, 8.5f);
            UiSmallStrike = new Font(text, 8.5f, FontStyle.Strikeout);
            UiBold = new Font(semi, 10.5f);
            Title = new Font(disp, 19f);
            Icons = new Font(icons, 11f);
            IconsSmall = new Font(icons, 8f);
            IconsBig = new Font(icons, 28f);
        }

        public static int S(float v) { return (int)Math.Round(v * Scale); }

        public static Color Hex(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static void DrawText(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags extra)
        {
            TextRenderer.DrawText(g, s, f, r, c, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | extra);
        }

        public static Bitmap GlyphImage(string glyph, Color color)
        {
            int sz = S(16);
            var bmp = new Bitmap(sz, sz);
            using (var g = Graphics.FromImage(bmp))
            using (var b = new SolidBrush(color))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(glyph, IconsSmall, b, new RectangleF(0, 0, sz, sz), sf);
            }
            return bmp;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void DarkWindow(IntPtr hwnd, Color caption)
        {
            try
            {
                int on = 1;
                DwmSetWindowAttribute(hwnd, 20, ref on, 4);               // тёмный заголовок
                int c = caption.R | (caption.G << 8) | (caption.B << 16);
                DwmSetWindowAttribute(hwnd, 35, ref c, 4);                // цвет заголовка
                int round = 2;
                DwmSetWindowAttribute(hwnd, 33, ref round, 4);            // скруглённые углы
            }
            catch { }
        }

        public static void RoundCorners(IntPtr hwnd)
        {
            try { int round = 3; DwmSetWindowAttribute(hwnd, 33, ref round, 4); } catch { }
        }
    }
}
