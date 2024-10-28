using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TwitchDownloaderWPF.Services
{
    [SupportedOSPlatform("windows")]
    public static unsafe class NativeFunctions
    {
        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute", PreserveSig = true)]
        public static extern int SetWindowAttribute(IntPtr handle, int attribute, void* attributeValue, uint attributeSize);

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow", PreserveSig = true)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "FlashWindowEx", PreserveSig = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlashWindowEx([In] ref FlashWInfo info);

        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-flashwinfo
        [StructLayout(LayoutKind.Sequential)]
        public record struct FlashWInfo
        {
            public uint StructSize;
            public IntPtr WindowHandle;
            public uint Flags;
            public uint FlashCount;
            public uint Timeout;

            // ReSharper disable InconsistentNaming
            public const uint FLASHW_STOP = 0;
            public const uint FLASHW_CAPTION = 1;
            public const uint FLASHW_TRAY = 2;
            public const uint FLASHW_ALL = 3;
            public const uint FLASHW_TIMER = 4;
            public const uint FLASHW_TIMERNOFG = 12;
            // ReSharper restore InconsistentNaming
        }
    }
}