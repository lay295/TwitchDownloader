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
    }
}