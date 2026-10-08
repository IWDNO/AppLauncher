using System.Drawing;

namespace AppLauncher
{
    enum WindowMode { Normal, Minimized, Maximized, Hidden }

    enum LaunchPriority { Normal, Low, BelowNormal, AboveNormal, High }

    // Одна плитка: что запускать и с какими настройками
    class LaunchItem
    {
        public bool Enabled;                         // отмечено для запуска кнопкой «Запустить»
        public string Name = "";
        public string Path = "";
        public string Args = "";
        public bool ArgsEnabled = true;              // аргументы можно временно выключить, не стирая
        public WindowMode Window = WindowMode.Normal;
        public bool RunAsAdmin;
        public LaunchPriority Priority = LaunchPriority.Normal;
        public int StartDelay;                       // секунд до запуска после нажатия «Запустить»
        public int CloseDelay = -1;                  // секунд до «крестика» после появления окна; -1 — не закрывать

        // Состояние для отображения, в файл не сохраняется
        public Image Icon;
        public bool Missing;

        public bool Exists { get { return System.IO.File.Exists(Path) || System.IO.Directory.Exists(Path); } }
    }
}
