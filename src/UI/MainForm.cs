using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AppLauncher
{
    class MainForm : Form
    {
        readonly bool startupMode;
        AppConfig config = new AppConfig();
        readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        TileGrid grid;
        FlatButton btnAdd, btnSelectAll, btnEditFile, btnRefresh, btnLaunch;
        ToggleSwitch tglAutostart, tglClose;
        ContextMenuStrip itemMenu, addMenu;
        ToolStripMenuItem argsToggle, adminItem;
        string subtitle = "";

        List<LaunchItem> Items { get { return config.Items; } }

        int HeaderH { get { return Theme.S(96); } }
        int FooterH { get { return Theme.S(88); } }

        public MainForm(bool startup)
        {
            startupMode = startup;
            BuildUi();
            Reload();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkWindow(Handle, Theme.Bg);
        }

        // ---------- UI ----------

        void BuildUi()
        {
            Text = "App Launcher";
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.Ui;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Theme.S(840), Theme.S(620));
            MinimumSize = new Size(Theme.S(600), Theme.S(420));
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            grid = new TileGrid();
            grid.ChecksChanged += () => { Save(); UpdateStatus(); };
            grid.Reordered += Save;
            grid.RunItem += it => LaunchOne(it);
            grid.FilesDropped += AddPaths;
            grid.MenuRequested += p => itemMenu.Show(grid, p);

            var tips = new ToolTip();

            btnAdd = new FlatButton { Text = "Добавить", Glyph = "" };
            btnAdd.FitWidth();
            btnAdd.Click += (s, e) => addMenu.Show(btnAdd, new Point(0, btnAdd.Height + Theme.S(4)));

            btnSelectAll = new FlatButton { Text = "Выбрать все", Glyph = "" };
            btnSelectAll.Width = Math.Max(TextRenderer.MeasureText("Выбрать все", Theme.Ui).Width,
                                          TextRenderer.MeasureText("Снять все", Theme.Ui).Width) + Theme.S(58);
            btnSelectAll.Click += (s, e) => SetAllChecked(!Items.All(i => i.Enabled));

            btnEditFile = new FlatButton { Glyph = "", IconOnly = true };
            btnEditFile.FitWidth();
            btnEditFile.Click += (s, e) => OpenConfigFile();
            tips.SetToolTip(btnEditFile, "Открыть файл со списком (apps.ini)");

            btnRefresh = new FlatButton { Glyph = "", IconOnly = true };
            btnRefresh.FitWidth();
            btnRefresh.Click += (s, e) => Reload();
            tips.SetToolTip(btnRefresh, "Перечитать список из файла (F5)");

            btnLaunch = new FlatButton { Primary = true, Glyph = "", Height = Theme.S(48), Width = Theme.S(260), BackColor = Theme.FooterBg };
            btnLaunch.Click += (s, e) => LaunchChecked();

            tglAutostart = new ToggleSwitch { Text = "Открывать при старте Windows", BackColor = Theme.FooterBg };
            tglAutostart.FitWidth();
            tglAutostart.SetChecked(Autostart.IsEnabled, false);
            tglAutostart.CheckedChanged += (s, e) =>
            {
                try { Autostart.Set(tglAutostart.Checked); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Автозапуск", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };

            tglClose = new ToggleSwitch { Text = "Закрывать окно после запуска", BackColor = Theme.FooterBg };
            tglClose.FitWidth();
            tglClose.CheckedChanged += (s, e) => { config.CloseAfterLaunch = tglClose.Checked; Save(); };

            foreach (var b in new[] { btnAdd, btnSelectAll, btnEditFile, btnRefresh }) b.BackColor = Theme.Bg;

            Controls.AddRange(new Control[] { grid, btnAdd, btnSelectAll, btnEditFile, btnRefresh, btnLaunch, tglAutostart, tglClose });

            BuildMenus();

            KeyDown += OnFormKey;
            Shown += (s, e) =>
            {
                if (startupMode) { TopMost = true; Activate(); TopMost = false; }
                grid.Focus();
            };
            DoLayout();
        }

        void BuildMenus()
        {
            addMenu = Menus.Create();
            Menus.Add(addMenu, "", "Файлы, программы, скрипты…", AddFilesDialog);
            Menus.Add(addMenu, "", "Папку…", AddFolderDialog);

            itemMenu = Menus.Create();
            Menus.Add(itemMenu, "", "Запустить", () => { foreach (var it in grid.Selected) LaunchOne(it); });
            Menus.Add(itemMenu, "", "Переименовать\tF2", Rename);
            Menus.Add(itemMenu, "", "Аргументы запуска…", EditArgs);
            argsToggle = Menus.Add(itemMenu, "", "Аргументы включены\tщелчок по метке", ToggleArgs);
            itemMenu.Items.Add(new ToolStripSeparator());
            Menus.AddChoice(itemMenu, "", "Окно при запуске", new[] { "Обычное", "Свёрнутое", "Развёрнутое", "Скрытое" },
                () => FirstSel(i => (int)i.Window), v => ApplySel(i => i.Window = (WindowMode)v));
            Menus.AddChoice(itemMenu, "", "Приоритет", new[] { "Обычный", "Низкий", "Ниже среднего", "Выше среднего", "Высокий" },
                () => FirstSel(i => (int)i.Priority), v => ApplySel(i => i.Priority = (LaunchPriority)v));
            adminItem = Menus.Add(itemMenu, "", "От имени администратора", ToggleAdmin);
            Menus.Add(itemMenu, "", "Задержка перед запуском…", EditStartDelay);
            Menus.Add(itemMenu, "", "Закрывать окно после запуска…", EditCloseDelay);
            itemMenu.Items.Add(new ToolStripSeparator());
            Menus.Add(itemMenu, "", "Показать в проводнике", ShowInExplorer);
            itemMenu.Items.Add(new ToolStripSeparator());
            Menus.Add(itemMenu, "", "Переместить выше", () => MoveSelected(-1));
            Menus.Add(itemMenu, "", "Переместить ниже", () => MoveSelected(1));
            itemMenu.Items.Add(new ToolStripSeparator());
            var del = Menus.Add(itemMenu, "", "Удалить из списка\tDel", RemoveSelected);
            del.Tag = "danger";
            del.Image = Theme.GlyphImage("", Theme.Danger);

            itemMenu.Opening += (s, e) =>
            {
                var it = FirstSelItem();
                if (it == null) { e.Cancel = true; return; }
                argsToggle.Enabled = it.Args != "";
                argsToggle.Checked = it.Args != "" && it.ArgsEnabled;
                adminItem.Checked = it.RunAsAdmin;
            };
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (grid != null) { DoLayout(); Invalidate(); }
        }

        void DoLayout()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            int pad = Theme.S(20);
            int x = w - pad;
            foreach (var b in new Control[] { btnRefresh, btnEditFile, btnSelectAll, btnAdd })
            {
                x -= b.Width;
                b.Location = new Point(x, Theme.S(30));
                x -= Theme.S(8);
            }
            grid.Bounds = new Rectangle(0, HeaderH, w, h - HeaderH - FooterH);
            int fy = h - FooterH;
            btnLaunch.Location = new Point(w - pad - btnLaunch.Width, fy + (FooterH - btnLaunch.Height) / 2);
            tglAutostart.Location = new Point(pad + Theme.S(4), fy + Theme.S(15));
            tglClose.Location = new Point(pad + Theme.S(4), fy + Theme.S(47));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int pad = Theme.S(24);
            Theme.DrawText(g, "Мои приложения", Theme.Title, new Rectangle(pad, Theme.S(18), Theme.S(400), Theme.S(40)), Theme.Text, TextFormatFlags.Left);
            Theme.DrawText(g, subtitle, Theme.Ui, new Rectangle(pad, Theme.S(56), btnAdd.Left - pad, Theme.S(24)), Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            int fy = ClientSize.Height - FooterH;
            using (var br = new SolidBrush(Theme.FooterBg)) g.FillRectangle(br, 0, fy, ClientSize.Width, FooterH);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, fy, ClientSize.Width, fy);
        }

        void OnFormKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.KeyCode == Keys.Enter) { if (btnLaunch.Enabled) LaunchChecked(); }
            else if (e.KeyCode == Keys.Delete) RemoveSelected();
            else if (e.KeyCode == Keys.F2) Rename();
            else if (e.KeyCode == Keys.F5) Reload();
            else if (e.Alt && e.KeyCode == Keys.Up) MoveSelected(-1);
            else if (e.Alt && e.KeyCode == Keys.Down) MoveSelected(1);
            else return;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        // ---------- данные ----------

        void Reload()
        {
            try { config = AppConfig.Load(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось прочитать " + AppConfig.FilePath + ":\n" + ex.Message, "App Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            iconCache.Clear();
            RefreshGrid();
        }

        void Save()
        {
            try { config.Save(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось сохранить " + AppConfig.FilePath + ":\n" + ex.Message, "App Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OpenConfigFile()
        {
            Save();
            try { Process.Start(new ProcessStartInfo("notepad.exe", "\"" + AppConfig.FilePath + "\"") { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "App Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void RefreshGrid()
        {
            foreach (var it in Items)
            {
                it.Missing = !it.Exists;
                it.Icon = it.Missing ? null : GetIcon(it.Path);
            }
            grid.SetItems(Items);
            tglClose.SetChecked(config.CloseAfterLaunch, false);
            UpdateStatus();
        }

        Image GetIcon(string path)
        {
            Image img;
            if (iconCache.TryGetValue(path, out img)) return img;
            try { img = ShellIcon.Get(path); } catch { img = null; }
            iconCache[path] = img;
            return img;
        }

        void UpdateStatus()
        {
            int n = Items.Count(i => i.Enabled);
            subtitle = Items.Count == 0
                ? "Список пуст — добавьте то, что хотите запускать"
                : "Выбрано " + n + " из " + Items.Count + "  ·  порядок меняется перетаскиванием";
            btnLaunch.Text = n > 0 ? "Запустить  (" + n + ")" : "Ничего не выбрано";
            btnLaunch.Enabled = n > 0;
            bool all = Items.Count > 0 && Items.All(i => i.Enabled);
            btnSelectAll.Text = all ? "Снять все" : "Выбрать все";
            btnSelectAll.Glyph = all ? "" : "";
            btnSelectAll.Enabled = Items.Count > 0;
            Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderH));
        }

        // ---------- действия со списком ----------

        void SetAllChecked(bool value)
        {
            foreach (var it in Items) it.Enabled = value;
            Save();
            grid.Invalidate();
            UpdateStatus();
        }

        void AddFilesDialog()
        {
            using (var dlg = new OpenFileDialog { Multiselect = true, Title = "Выберите файлы для запуска", Filter = "Все файлы (*.*)|*.*", DereferenceLinks = false })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) AddPaths(dlg.FileNames);
            }
        }

        void AddFolderDialog()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Папка будет открываться в проводнике" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { dlg.SelectedPath });
            }
        }

        void AddPaths(string[] paths)
        {
            LaunchItem last = null;
            foreach (var p in paths)
            {
                string full = Path.GetFullPath(p);
                var existing = Items.FirstOrDefault(i => SamePath(i.Path, full));
                if (existing != null) { last = existing; continue; }
                last = new LaunchItem { Enabled = true, Name = AppConfig.DisplayName(full), Path = full };
                Items.Add(last);
            }
            Save();
            RefreshGrid();
            grid.Select(last);
        }

        void RemoveSelected()
        {
            var sel = grid.Selected;
            if (sel.Count == 0) return;
            foreach (var it in sel) Items.Remove(it);
            Save();
            RefreshGrid();
        }

        void MoveSelected(int dir)
        {
            var it = FirstSelItem();
            if (it == null || grid.Selected.Count != 1) return;
            int i = Items.IndexOf(it), j = i + dir;
            if (j < 0 || j >= Items.Count) return;
            Items[i] = Items[j];
            Items[j] = it;
            Save();
            grid.Select(it);
        }

        void ShowInExplorer()
        {
            var it = FirstSelItem();
            if (it != null && it.Exists) Process.Start("explorer.exe", "/select,\"" + it.Path + "\"");
        }

        // ---------- настройки плиток ----------

        LaunchItem FirstSelItem()
        {
            var sel = grid.Selected;
            return sel.Count > 0 ? sel[0] : null;
        }

        int FirstSel(Func<LaunchItem, int> get)
        {
            var it = FirstSelItem();
            return it == null ? -1 : get(it);
        }

        // Применить настройку ко всем выделенным плиткам
        void ApplySel(Action<LaunchItem> change)
        {
            foreach (var it in grid.Selected) change(it);
            Save();
            grid.Invalidate();
        }

        void Rename()
        {
            var it = FirstSelItem();
            if (it == null) return;
            string v = Prompt.Show(this, "Переименовать", "Название в списке", it.Name);
            if (v == null || v.Trim() == "") return;
            it.Name = v.Trim();
            Save();
            grid.Invalidate();
        }

        void EditArgs()
        {
            var it = FirstSelItem();
            if (it == null) return;
            string v = Prompt.Show(this, "Аргументы запуска", "Параметры командной строки для «" + it.Name + "»", it.Args);
            if (v == null) return;
            it.Args = v.Trim();
            Save();
            grid.Invalidate();
        }

        void ToggleArgs()
        {
            var first = FirstSelItem();
            if (first == null) return;
            bool v = !first.ArgsEnabled;
            ApplySel(i => i.ArgsEnabled = v);
        }

        void ToggleAdmin()
        {
            var first = FirstSelItem();
            if (first == null) return;
            bool v = !first.RunAsAdmin;
            ApplySel(i => i.RunAsAdmin = v);
        }

        void EditStartDelay()
        {
            var first = FirstSelItem();
            if (first == null) return;
            string v = Prompt.Show(this, "Задержка перед запуском",
                "Через сколько секунд после нажатия «Запустить» открыть (0 — сразу)", first.StartDelay.ToString());
            int sec;
            if (v == null || !int.TryParse(v.Trim() == "" ? "0" : v.Trim(), out sec) || sec < 0) return;
            ApplySel(i => i.StartDelay = sec);
        }

        void EditCloseDelay()
        {
            var first = FirstSelItem();
            if (first == null) return;
            string v = Prompt.Show(this, "Закрыть окно после запуска",
                "Через сколько секунд после появления окна нажать «крестик» (пусто — не закрывать)",
                first.CloseDelay >= 0 ? first.CloseDelay.ToString() : "3");
            if (v == null) return;
            int sec;
            if (v.Trim() == "") sec = -1;
            else if (!int.TryParse(v.Trim(), out sec) || sec < 0) return;
            ApplySel(i => i.CloseDelay = sec);
        }

        // ---------- запуск ----------

        void LaunchChecked()
        {
            var errors = new List<string>();
            foreach (var it in Items.Where(i => i.Enabled).ToList())
            {
                if (it.StartDelay > 0) { Launcher.StartLater(it); continue; }
                string err = Launcher.Start(it);
                if (err != null) errors.Add("• " + it.Name + ": " + err);
            }
            if (errors.Count > 0)
            {
                MessageBox.Show(this, "Не удалось запустить:\n\n" + string.Join("\n", errors), "App Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (config.CloseAfterLaunch) Close();
        }

        // Ручной запуск одной плитки (двойной клик, ▶, меню) — без задержки
        void LaunchOne(LaunchItem it)
        {
            string err = Launcher.Start(it);
            if (err != null)
                MessageBox.Show(this, it.Name + ":\n" + err, "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        static bool SamePath(string a, string b)
        {
            return string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }
    }
}
