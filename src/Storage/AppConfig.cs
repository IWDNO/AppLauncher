using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AppLauncher
{
    // Настройки и список приложений в человекочитаемом INI-файле:
    //
    //   [settings]
    //   close_after_launch = yes
    //
    //   [app]
    //   name = Telegram
    //   path = C:\...\Telegram.exe
    //   ...
    //
    // Каждая секция [app] — одна плитка, порядок секций = порядок плиток.
    class AppConfig
    {
        public static readonly string Dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppLauncher");
        public static readonly string FilePath = System.IO.Path.Combine(Dir, "apps.ini");

        public bool CloseAfterLaunch = true;
        public List<LaunchItem> Items = new List<LaunchItem>();

        const string Header =
@"; App Launcher — настройки и список приложений.
; Файл можно править вручную. Чтобы лаунчер подхватил изменения, нажмите в нём «Обновить» (F5).
;
; Параметры приложения (секция [app], одна на плитку, в порядке отображения):
;   name          название на плитке
;   path          путь к программе, скрипту, документу или папке
;   checked       отмечено для запуска: yes / no
;   args          аргументы командной строки
;   args_enabled  передавать ли аргументы: yes / no
;   window        окно при запуске: normal / minimized / maximized / hidden
;   admin         от имени администратора: yes / no
;   priority      приоритет: normal / low / below_normal / above_normal / high
;   start_delay   задержка перед запуском, секунд
;   close_window  через сколько секунд после появления окна нажать «крестик»; пусто — не закрывать
";

        // ---------- чтение ----------

        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            if (!File.Exists(FilePath)) return cfg;

            string section = null;
            LaunchItem app = null;
            foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line == "" || line.StartsWith(";") || line.StartsWith("#")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    app = null;
                    if (section == "app")
                    {
                        app = new LaunchItem();
                        cfg.Items.Add(app);
                    }
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();

                if (section == "settings") cfg.ReadSetting(key, value);
                else if (app != null) ReadAppKey(app, key, value);
            }
            cfg.Items.RemoveAll(i => i.Path == "");
            foreach (var it in cfg.Items)
                if (it.Name == "") it.Name = DisplayName(it.Path);
            return cfg;
        }

        void ReadSetting(string key, string value)
        {
            if (key == "close_after_launch") CloseAfterLaunch = ParseBool(value, true);
        }

        static void ReadAppKey(LaunchItem it, string key, string value)
        {
            switch (key)
            {
                case "name": it.Name = value; break;
                case "path": it.Path = value; break;
                case "checked": it.Enabled = ParseBool(value, false); break;
                case "args": it.Args = value; break;
                case "args_enabled": it.ArgsEnabled = ParseBool(value, true); break;
                case "window": it.Window = ParseWindow(value); break;
                case "admin": it.RunAsAdmin = ParseBool(value, false); break;
                case "priority": it.Priority = ParsePriority(value); break;
                case "start_delay": it.StartDelay = Math.Max(0, ParseSeconds(value)); break;
                case "close_window": it.CloseDelay = ParseSeconds(value); break;
            }
        }

        // ---------- запись ----------

        public void Save()
        {
            var sb = new StringBuilder();
            sb.Append(Header.Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
            sb.AppendLine();
            sb.AppendLine("[settings]");
            sb.AppendLine("close_after_launch = " + Bool(CloseAfterLaunch));

            foreach (var it in Items)
            {
                sb.AppendLine();
                sb.AppendLine("[app]");
                sb.AppendLine("name         = " + OneLine(it.Name));
                sb.AppendLine("path         = " + it.Path);
                sb.AppendLine("checked      = " + Bool(it.Enabled));
                sb.AppendLine("args         = " + OneLine(it.Args));
                sb.AppendLine("args_enabled = " + Bool(it.ArgsEnabled));
                sb.AppendLine("window       = " + WindowKey(it.Window));
                sb.AppendLine("admin        = " + Bool(it.RunAsAdmin));
                sb.AppendLine("priority     = " + PriorityKey(it.Priority));
                sb.AppendLine("start_delay  = " + it.StartDelay);
                sb.AppendLine("close_window = " + (it.CloseDelay >= 0 ? it.CloseDelay.ToString() : ""));
            }

            Directory.CreateDirectory(Dir);
            // пишем во временный файл и подменяем — чтобы не потерять список, если запись оборвётся
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }

        // ---------- значения ----------

        static string Bool(bool v) { return v ? "yes" : "no"; }

        static bool ParseBool(string s, bool fallback)
        {
            switch (s.ToLowerInvariant())
            {
                case "yes": case "true": case "1": case "on": case "да": return true;
                case "no": case "false": case "0": case "off": case "нет": return false;
                default: return fallback;
            }
        }

        static int ParseSeconds(string s)
        {
            int v;
            return int.TryParse(s, out v) && v >= 0 ? v : -1;
        }

        static string WindowKey(WindowMode w)
        {
            switch (w)
            {
                case WindowMode.Minimized: return "minimized";
                case WindowMode.Maximized: return "maximized";
                case WindowMode.Hidden: return "hidden";
                default: return "normal";
            }
        }

        static WindowMode ParseWindow(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "minimized": return WindowMode.Minimized;
                case "maximized": return WindowMode.Maximized;
                case "hidden": return WindowMode.Hidden;
                default: return WindowMode.Normal;
            }
        }

        static string PriorityKey(LaunchPriority p)
        {
            switch (p)
            {
                case LaunchPriority.Low: return "low";
                case LaunchPriority.BelowNormal: return "below_normal";
                case LaunchPriority.AboveNormal: return "above_normal";
                case LaunchPriority.High: return "high";
                default: return "normal";
            }
        }

        static LaunchPriority ParsePriority(string s)
        {
            switch (s.ToLowerInvariant().Replace("-", "_"))
            {
                case "low": return LaunchPriority.Low;
                case "below_normal": return LaunchPriority.BelowNormal;
                case "above_normal": return LaunchPriority.AboveNormal;
                case "high": return LaunchPriority.High;
                default: return LaunchPriority.Normal;
            }
        }

        static string OneLine(string s) { return (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim(); }

        public static string DisplayName(string path)
        {
            if (Directory.Exists(path)) return System.IO.Path.GetFileName(path.TrimEnd('\\'));
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".lnk" || ext == ".url" || ext == ".exe") return System.IO.Path.GetFileNameWithoutExtension(path);
            return System.IO.Path.GetFileName(path);
        }
    }
}
