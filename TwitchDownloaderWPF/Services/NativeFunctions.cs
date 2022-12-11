using System;
using System.Runtime.InteropServices;

namespace TwitchDownloader.Tools
{
    // native as in native to Windows
    public static class NativeFunctions
    {
        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute", PreserveSig = true)]
        public static extern int SetWindowAttribute(IntPtr handle, int attribute, ref bool attributeValue, int attributeSize);
    }
}
