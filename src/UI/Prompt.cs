using System.Drawing;
using System.Windows.Forms;

namespace AppLauncher
{
    static class Prompt
    {
        public static string Show(IWin32Window owner, string title, string label, string value)
        {
            using (var f = new Form())
            {
                f.Text = title;
                f.AutoScaleMode = AutoScaleMode.None;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.BackColor = Theme.Bg;
                f.ForeColor = Theme.Text;
                f.Font = Theme.Ui;
                f.ClientSize = new Size(Theme.S(460), Theme.S(170));
                f.HandleCreated += (s, e) => Theme.DarkWindow(f.Handle, Theme.Bg);

                int pad = Theme.S(20);
                var lbl = new Label { Text = label, AutoEllipsis = true, ForeColor = Theme.TextDim, Location = new Point(pad, Theme.S(16)), Size = new Size(f.ClientSize.Width - pad * 2, Theme.S(24)) };

                var box = new Panel { BackColor = Theme.Surface, Location = new Point(pad, Theme.S(46)), Size = new Size(f.ClientSize.Width - pad * 2, Theme.S(38)) };
                var tb = new TextBox
                {
                    Text = value,
                    BorderStyle = BorderStyle.None,
                    BackColor = Theme.Surface,
                    ForeColor = Theme.Text,
                    Location = new Point(Theme.S(10), Theme.S(9)),
                    Width = box.Width - Theme.S(20)
                };
                box.Controls.Add(tb);
                box.Paint += (s, e) =>
                {
                    using (var pen = new Pen(Theme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
                    using (var pen = new Pen(Theme.Accent, Theme.S(2))) e.Graphics.DrawLine(pen, 0, box.Height - 1, box.Width, box.Height - 1);
                };

                var ok = new FlatButton { Text = "Сохранить", Primary = true, BackColor = Theme.Bg, Width = Theme.S(120) };
                var cancel = new FlatButton { Text = "Отмена", BackColor = Theme.Bg, Width = Theme.S(100) };
                int by = f.ClientSize.Height - pad - ok.Height;
                ok.Location = new Point(f.ClientSize.Width - pad - ok.Width, by);
                cancel.Location = new Point(ok.Left - Theme.S(8) - cancel.Width, by);
                ok.Click += (s, e) => { f.DialogResult = DialogResult.OK; };
                cancel.Click += (s, e) => { f.DialogResult = DialogResult.Cancel; };
                tb.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter) { f.DialogResult = DialogResult.OK; e.SuppressKeyPress = true; }
                    else if (e.KeyCode == Keys.Escape) { f.DialogResult = DialogResult.Cancel; e.SuppressKeyPress = true; }
                };

                f.Controls.AddRange(new Control[] { lbl, box, ok, cancel });
                f.Shown += (s, e) => { tb.Focus(); tb.SelectAll(); };
                return f.ShowDialog(owner) == DialogResult.OK ? tb.Text : null;
            }
        }
    }
}
