using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AppLauncher
{
    static class ShellIcon
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IImageList
        {
            [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
            [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
            [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
            [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
            [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
            [PreserveSig] int Draw(IntPtr pimldp);
            [PreserveSig] int Remove(int i);
            [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO info, uint size, uint flags);

        [DllImport("shell32.dll", EntryPoint = "#727")]
        static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr h);

        const uint SHGFI_ICON = 0x100, SHGFI_SYSICONINDEX = 0x4000;
        const int SHIL_EXTRALARGE = 2;   // 48×48

        public static Bitmap Get(string path)
        {
            var info = new SHFILEINFO();
            uint size = (uint)Marshal.SizeOf(info);

            // Сначала пробуем крупный значок 48×48 из системного списка
            try
            {
                if (SHGetFileInfo(path, 0, ref info, size, SHGFI_SYSICONINDEX) != IntPtr.Zero)
                {
                    var iid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                    IImageList il;
                    if (SHGetImageList(SHIL_EXTRALARGE, ref iid, out il) == 0)
                    {
                        IntPtr h = IntPtr.Zero;
                        il.GetIcon(info.iIcon, 1 /* ILD_TRANSPARENT */, ref h);
                        if (h != IntPtr.Zero) return ToBitmap(h);
                    }
                }
            }
            catch { }

            // Запасной вариант — обычный 32×32
            info = new SHFILEINFO();
            SHGetFileInfo(path, 0, ref info, size, SHGFI_ICON);
            return info.hIcon == IntPtr.Zero ? null : ToBitmap(info.hIcon);
        }

        static Bitmap ToBitmap(IntPtr hIcon)
        {
            try { using (var ic = Icon.FromHandle(hIcon)) return ic.ToBitmap(); }
            finally { DestroyIcon(hIcon); }
        }
    }
}
