using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Windows;

namespace TwitchDownloaderWPF.Services
{
    public static class ClipboardService
    {
        [SupportedOSPlatform("windows")]
        public static bool TrySetText(string text, [MaybeNullWhen(false)] out Exception exception)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception e)
            {
                exception = e;
                return false;
            }

            exception = null;
            return true;
        }
    }
}