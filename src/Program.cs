using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AppLauncher
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Theme.Init();
            bool startup = args.Any(a => a.Equals("--startup", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(startup));
        }
    }
}
