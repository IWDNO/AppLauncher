using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AppLauncher
{
    static class Native
    {
        public struct ProcInfo { public int Pid, ParentPid; public string Exe; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PROCESSENTRY32W
        {
            public uint dwSize, cntUsage, th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID, cntThreads, th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32FirstW(IntPtr snap, ref PROCESSENTRY32W e);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32NextW(IntPtr snap, ref PROCESSENTRY32W e);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);

        struct RECT { public int L, T, R, B; }

        public static List<ProcInfo> Processes()
        {
            var list = new List<ProcInfo>();
            IntPtr snap = CreateToolhelp32Snapshot(2 /* TH32CS_SNAPPROCESS */, 0);
            if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return list;
            try
            {
                var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32W)) };
                if (Process32FirstW(snap, ref e))
                    do list.Add(new ProcInfo { Pid = (int)e.th32ProcessID, ParentPid = (int)e.th32ParentProcessID, Exe = e.szExeFile });
                    while (Process32NextW(snap, ref e));
            }
            finally { CloseHandle(snap); }
            return list;
        }

        public static List<IntPtr> VisibleTopWindows()
        {
            var list = new List<IntPtr>();
            EnumWindows((h, l) => { if (IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }

        public static int WindowPid(IntPtr h)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            return (int)pid;
        }

        // Обычное окно приложения: без владельца, не панель инструментов, с заголовком, не скрытое DWM и не крошечное
        public static bool IsAppWindow(IntPtr h)
        {
            if (GetWindow(h, 4 /* GW_OWNER */) != IntPtr.Zero) return false;
            if ((GetWindowLong(h, -20 /* GWL_EXSTYLE */) & 0x80 /* WS_EX_TOOLWINDOW */) != 0) return false;
            if (GetWindowTextLength(h) == 0) return false;
            int cloaked;
            if (DwmGetWindowAttribute(h, 14 /* DWMWA_CLOAKED */, out cloaked, 4) == 0 && cloaked != 0) return false;
            if (IsIconic(h)) return true;   // у свёрнутого окна размер крошечный — это нормально
            RECT r;
            if (!GetWindowRect(h, out r) || r.R - r.L < 120 || r.B - r.T < 60) return false;
            return true;
        }

        public const int SC_MINIMIZE = 0xF020, SC_MAXIMIZE = 0xF030, SC_CLOSE = 0xF060;

        // То же самое, что нажатие кнопки в заголовке окна (свернуть / развернуть / «крестик»)
        public static void PressButton(IntPtr h, int command)
        {
            PostMessage(h, 0x0112 /* WM_SYSCOMMAND */, new IntPtr(command), IntPtr.Zero);
        }
    }
}
