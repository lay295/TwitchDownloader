using System.Runtime.InteropServices;

namespace TwitchDownloaderAvalonia.Update.Models
{
    public enum UpdateHostOs
    {
        Windows,
        MacOs,
        Linux,
        Other,
    }

    public static class UpdateHost
    {
        public static UpdateHostOs CurrentOs
        {
            get
            {
                if (OperatingSystem.IsWindows())
                    return UpdateHostOs.Windows;
                if (OperatingSystem.IsMacOS())
                    return UpdateHostOs.MacOs;
                if (OperatingSystem.IsLinux())
                    return UpdateHostOs.Linux;

                return UpdateHostOs.Other;
            }
        }

        public static Architecture CurrentArchitecture => RuntimeInformation.OSArchitecture;

        public static string CurrentRuntimeIdentifier => RuntimeInformation.RuntimeIdentifier;

        public static string MainExecutableFileName => OperatingSystem.IsWindows()
            ? "TwitchDownloaderAvalonia.exe"
            : "TwitchDownloaderAvalonia";

        public static string UpdaterExecutableFileName => OperatingSystem.IsWindows()
            ? "TwitchDownloaderAvalonia.Updater.exe"
            : "TwitchDownloaderAvalonia.Updater";
    }
}
