using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace AppLauncher
{
    // Запуск плиток со всеми их настройками
    static class Launcher
    {
        // Расширения, у которых в Windows есть действие «Запуск от имени администратора»
        static readonly HashSet<string> Elevatable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".exe", ".com", ".bat", ".cmd", ".msi", ".lnk" };

        // Запускает сразу; возвращает текст ошибки или null
        public static string Start(LaunchItem it)
        {
            try
            {
                if (!it.Exists) return "файл не найден — " + it.Path;

                bool isDir = Directory.Exists(it.Path);
                string ext = isDir ? "" : Path.GetExtension(it.Path).ToLowerInvariant();
                string args = it.ArgsEnabled ? it.Args : "";
                bool admin = it.RunAsAdmin && !isDir;

                var psi = new ProcessStartInfo { UseShellExecute = true };
                psi.WorkingDirectory = isDir ? it.Path : Path.GetDirectoryName(it.Path);
                psi.WindowStyle = it.Window == WindowMode.Minimized ? ProcessWindowStyle.Minimized
                                : it.Window == WindowMode.Maximized ? ProcessWindowStyle.Maximized
                                : it.Window == WindowMode.Hidden ? ProcessWindowStyle.Hidden
                                : ProcessWindowStyle.Normal;

                if (ext == ".ps1")
                {
                    // По умолчанию .ps1 открывается в блокноте — запускаем через PowerShell
                    psi.FileName = "powershell.exe";
                    psi.Arguments = "-NoProfile -ExecutionPolicy Bypass " + (it.Window == WindowMode.Hidden ? "-WindowStyle Hidden " : "") +
                                    "-File \"" + it.Path + "\" " + args;
                }
                else if (admin && !Elevatable.Contains(ext))
                {
                    // У документов и прочих файлов нет «Запуска от имени администратора» — открываем через повышенный cmd
                    string mode = it.Window == WindowMode.Minimized ? "/min " : it.Window == WindowMode.Maximized ? "/max " : "";
                    psi.FileName = "cmd.exe";
                    psi.Arguments = "/c start \"\" " + mode + "\"" + it.Path + "\" " + args;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                }
                else
                {
                    // Любой другой файл/папка/ярлык открывается программой по умолчанию
                    psi.FileName = it.Path;
                    psi.Arguments = args;
                }
                if (admin) psi.Verb = "runas";

                // снимок окон/процессов делается до запуска, чтобы потом отличить новые
                var watcher = LaunchWatcher.Needed(it) ? new LaunchWatcher(it) : null;
                var proc = Process.Start(psi);
                if (watcher != null)
                {
                    int pid = 0;
                    try { if (proc != null) pid = proc.Id; } catch { }
                    watcher.Start(pid);
                }
                return null;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                return ex.NativeErrorCode == 1223 ? "запуск от администратора отменён" : ex.Message;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // Запуск через it.StartDelay секунд в отдельном потоке: работает, даже если окно лаунчера уже закрыто
        public static void StartLater(LaunchItem it)
        {
            var t = new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(it.StartDelay * 1000);
                string err = Start(it);
                if (err != null)
                    MessageBox.Show(it.Name + ":\n" + err, "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
            t.IsBackground = false;
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
        }
    }
}
