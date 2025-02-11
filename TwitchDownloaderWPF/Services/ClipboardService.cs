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
                // Clipboard.SetText will throw ExternalException if it cannot set the clipboard text or does not
                // receive a confirmation from COM. This is only documented on Clipboard.SetDataObject.
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