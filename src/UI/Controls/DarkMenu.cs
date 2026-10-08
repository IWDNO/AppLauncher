using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppLauncher
{
    class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.SurfaceHover; } }
        public override Color MenuItemBorder { get { return Theme.SurfaceHover; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Surface; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.SurfaceHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.SurfaceHover; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.SurfaceHover; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.SurfaceHover; } }
        public override Color CheckBackground { get { return Theme.Mix(Theme.Surface, Theme.Accent, 0.3f); } }
        public override Color CheckSelectedBackground { get { return Theme.Mix(Theme.SurfaceHover, Theme.Accent, 0.3f); } }
        public override Color CheckPressedBackground { get { return Theme.Mix(Theme.SurfaceHover, Theme.Accent, 0.3f); } }
        public override Color ButtonSelectedBorder { get { return Theme.Mix(Theme.Surface, Theme.Accent, 0.3f); } }
        public override Color ButtonPressedBorder { get { return Theme.Mix(Theme.Surface, Theme.Accent, 0.3f); } }
        public override Color ButtonSelectedHighlightBorder { get { return Theme.Mix(Theme.Surface, Theme.Accent, 0.3f); } }
    }

    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool shortcut = e.Item is ToolStripMenuItem && e.Text == ((ToolStripMenuItem)e.Item).ShortcutKeyDisplayString;
            e.TextColor = !e.Item.Enabled || shortcut ? Theme.TextFaint : e.Item.Tag as string == "danger" ? Theme.Danger : Theme.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.TextDim;
            base.OnRenderArrow(e);
        }

        // Отмеченный пункт: подсветка под значком, а если значка нет — галочка
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = e.ImageRectangle;
            r.Inflate(Theme.S(3), Theme.S(3));
            using (var path = Theme.Round(r, Theme.S(4)))
            using (var br = new SolidBrush(Color.FromArgb(70, Theme.Accent)))
                g.FillPath(br, path);
            if (e.Image == null)
                Theme.DrawText(g, "\uE73E", Theme.IconsSmall, r, Theme.Mix(Theme.Accent, Color.White, 0.5f), TextFormatFlags.HorizontalCenter);
            else
                g.DrawImage(e.Image, e.ImageRectangle);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = new RectangleF(Theme.S(4), 1, e.Item.Width - Theme.S(8), e.Item.Height - 2);
            using (var path = Theme.Round(r, Theme.S(5)))
            using (var br = new SolidBrush(Theme.SurfaceHover))
                g.FillPath(br, path);
        }
    }

    static class Menus
    {
        public static ContextMenuStrip Create()
        {
            var m = new ContextMenuStrip
            {
                Renderer = new DarkRenderer(),
                Font = Theme.Ui,
                ShowImageMargin = true,
                ImageScalingSize = new Size(Theme.S(16), Theme.S(16)),
                Padding = new Padding(0, Theme.S(4), 0, Theme.S(4))
            };
            m.Opened += (s, e) => Theme.RoundCorners(m.Handle);
            return m;
        }

        public static ToolStripMenuItem Add(ContextMenuStrip m, string glyph, string text, Action onClick)
        {
            var parts = text.Split('\t');
            var it = new ToolStripMenuItem(parts[0], Theme.GlyphImage(glyph, Theme.TextDim), (s, e) => onClick());
            if (parts.Length > 1) it.ShortcutKeyDisplayString = parts[1];
            it.Padding = new Padding(0, Theme.S(3), 0, Theme.S(3));
            m.Items.Add(it);
            return it;
        }

        // Подменю с вариантами «одно из» (галочка у текущего)
        public static ToolStripMenuItem AddChoice(ContextMenuStrip m, string glyph, string text,
            string[] labels, Func<int> current, Action<int> choose)
        {
            var parent = new ToolStripMenuItem(text, Theme.GlyphImage(glyph, Theme.TextDim));
            parent.Padding = new Padding(0, Theme.S(3), 0, Theme.S(3));
            var dd = (ToolStripDropDownMenu)parent.DropDown;
            dd.Renderer = new DarkRenderer();
            dd.Font = Theme.Ui;
            dd.ShowImageMargin = true;
            dd.ImageScalingSize = new Size(Theme.S(16), Theme.S(16));
            dd.Padding = new Padding(0, Theme.S(4), 0, Theme.S(4));
            dd.Opened += (s, e) => Theme.RoundCorners(dd.Handle);
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var sub = new ToolStripMenuItem(labels[i], null, (s, e) => choose(idx));
                sub.Padding = new Padding(0, Theme.S(3), 0, Theme.S(3));
                dd.Items.Add(sub);
            }
            parent.DropDownOpening += (s, e) =>
            {
                int cur = current();
                for (int i = 0; i < dd.Items.Count; i++) ((ToolStripMenuItem)dd.Items[i]).Checked = i == cur;
            };
            m.Items.Add(parent);
            return parent;
        }
    }
}
