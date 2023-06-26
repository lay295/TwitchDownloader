using System;
using System.Runtime.InteropServices;

namespace TwitchDownloaderWPF.Services
{
    public static class NativeFunctions
    {
        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute", PreserveSig = true)]
        public static extern int SetWindowAttribute(IntPtr handle, int attribute, ref bool attributeValue, int attributeSize);
    }
}