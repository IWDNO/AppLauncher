using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AppLauncher
{
    // Запуск лаунчера при входе в Windows (HKCU\...\Run)
    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "AppLauncher";

        public static bool IsEnabled
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    var v = k == null ? null : k.GetValue(RunValue) as string;
                    return v != null && v.IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
        }

        public static void Set(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\" --startup");
                else k.DeleteValue(RunValue, false);
            }
        }
    }
}
