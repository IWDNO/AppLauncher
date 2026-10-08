using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AppLauncher
{
    // После запуска следит за процессами и окнами приложения:
    //  • закрывает новое окно «крестиком» (SC_CLOSE) через заданное время — процесс не убивается;
    //  • сворачивает/разворачивает новые окна, если программа проигнорировала режим окна при старте;
    //  • выставляет приоритет новым процессам приложения (включая дочерние).
    class LaunchWatcher
    {
        const int WatchSeconds = 90;       // сколько ждать появления окна
        const int AfterActSeconds = 10;    // сколько ещё следить после первого действия (заставка → главное окно)

        readonly HashSet<IntPtr> beforeWindows = new HashSet<IntPtr>();
        readonly HashSet<int> beforePids = new HashSet<int>();
        readonly string exeName;
        readonly int closeDelayMs;          // -1 — не закрывать
        readonly int windowCmd;             // 0 — не трогать, иначе SC_MINIMIZE / SC_MAXIMIZE
        readonly ProcessPriorityClass? priority;

        public static bool Needed(LaunchItem it)
        {
            return it.CloseDelay >= 0 || it.Window == WindowMode.Minimized || it.Window == WindowMode.Maximized ||
                   it.Priority != LaunchPriority.Normal;
        }

        public LaunchWatcher(LaunchItem it)
        {
            exeName = ResolveExe(it.Path);
            closeDelayMs = it.CloseDelay >= 0 ? it.CloseDelay * 1000 : -1;
            windowCmd = it.Window == WindowMode.Minimized ? Native.SC_MINIMIZE
                      : it.Window == WindowMode.Maximized ? Native.SC_MAXIMIZE : 0;
            priority = ToPriorityClass(it.Priority);
            foreach (var h in Native.VisibleTopWindows()) beforeWindows.Add(h);
            foreach (var p in Native.Processes()) beforePids.Add(p.Pid);
        }

        public void Start(int rootPid)
        {
            // Не фоновый поток: процесс лаунчера доживёт до конца слежки, даже если окно лаунчера закрыто
            var t = new System.Threading.Thread(() => { try { Run(rootPid); } catch { } });
            t.IsBackground = false;
            t.Start();
        }

        void Run(int rootPid)
        {
            var family = new HashSet<int>();
            if (rootPid > 0) family.Add(rootPid);
            if (family.Count == 0 && exeName == null) return;   // не за чем следить

            bool watchWindows = closeDelayMs >= 0 || windowCmd != 0;
            var firstSeen = new Dictionary<IntPtr, DateTime>();
            var done = new HashSet<IntPtr>();
            var prioritized = new HashSet<int>();
            DateTime deadline = watchWindows
                ? DateTime.Now.AddSeconds(WatchSeconds).AddMilliseconds(Math.Max(0, closeDelayMs))
                : DateTime.Now.AddSeconds(20);

            while (DateTime.Now < deadline)
            {
                // запущенный процесс + все его потомки + процессы с тем же exe
                var procs = Native.Processes();
                bool added = true;
                while (added)
                {
                    added = false;
                    foreach (var p in procs)
                    {
                        if (family.Contains(p.Pid)) continue;
                        if (family.Contains(p.ParentPid) ||
                            (exeName != null && string.Equals(p.Exe, exeName, StringComparison.OrdinalIgnoreCase)))
                        {
                            family.Add(p.Pid);
                            added = true;
                        }
                    }
                }

                // приоритет — только новым процессам, уже работавшие экземпляры не трогаем
                if (priority != null)
                {
                    foreach (var pid in family)
                    {
                        if (prioritized.Contains(pid) || (beforePids.Contains(pid) && pid != rootPid)) continue;
                        prioritized.Add(pid);
                        try { using (var pr = Process.GetProcessById(pid)) pr.PriorityClass = priority.Value; } catch { }
                    }
                }

                if (watchWindows)
                {
                    var now = DateTime.Now;
                    var visible = new HashSet<IntPtr>();
                    foreach (var w in Native.VisibleTopWindows())
                    {
                        if (beforeWindows.Contains(w) || done.Contains(w)) continue;
                        if (!family.Contains(Native.WindowPid(w)) || !Native.IsAppWindow(w)) continue;
                        visible.Add(w);

                        DateTime seen;
                        if (!firstSeen.TryGetValue(w, out seen)) { firstSeen[w] = now; seen = now; }

                        if (closeDelayMs >= 0)
                        {
                            // сначала даём окну «прожить» заданное время (и чтобы не закрыть заставку)
                            if (closeDelayMs == 0 && seen == now) continue;
                            if ((now - seen).TotalMilliseconds < closeDelayMs) continue;
                            Native.PressButton(w, Native.SC_CLOSE);
                        }
                        else
                        {
                            // программа сама учла режим окна — ничего не делаем
                            bool already = windowCmd == Native.SC_MINIMIZE ? Native.IsIconic(w) : Native.IsZoomed(w);
                            if (!already) Native.PressButton(w, windowCmd);
                        }
                        done.Add(w);
                        var until = now.AddSeconds(AfterActSeconds);
                        if (until < deadline) deadline = until;
                    }
                    // окно спряталось — отсчёт для него начнётся заново, если оно появится снова
                    foreach (var k in firstSeen.Keys.Where(k => !visible.Contains(k)).ToList()) firstSeen.Remove(k);
                }

                System.Threading.Thread.Sleep(250);
            }
        }

        static ProcessPriorityClass? ToPriorityClass(LaunchPriority p)
        {
            switch (p)
            {
                case LaunchPriority.Low: return ProcessPriorityClass.Idle;
                case LaunchPriority.BelowNormal: return ProcessPriorityClass.BelowNormal;
                case LaunchPriority.AboveNormal: return ProcessPriorityClass.AboveNormal;
                case LaunchPriority.High: return ProcessPriorityClass.High;
                default: return null;
            }
        }

        // Имя exe, за которым следить: сам .exe или цель ярлыка .lnk
        static string ResolveExe(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".exe") return System.IO.Path.GetFileName(path);
            if (ext != ".lnk") return null;
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(t);
                object lnk = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { path });
                string target = lnk.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, lnk, null) as string;
                if (!string.IsNullOrEmpty(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return System.IO.Path.GetFileName(target);
            }
            catch { }
            return null;
        }
    }
}
