using System;
using System.Runtime.Versioning;
using System.Windows;

namespace TwitchDownloaderWPF.Services
{
    public static class ClipboardService
    {
        [SupportedOSPlatform("windows")]
        public static bool TrySetText(string text, out Exception exception)
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
                // Clipboard.SetText seems to throw despite succeeding more often than it fails. Blindly return true for now.
                return true;
                // return false;
            }

            exception = null;
            return true;
        }
    }
}