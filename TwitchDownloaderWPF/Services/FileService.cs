using System;
using System.Diagnostics;
using System.IO;

namespace TwitchDownloaderWPF.Services
{
    public static class FileService
    {
        public static void OpenExplorerForFile(FileInfo fileInfo)
        {
            var directoryInfo = fileInfo.Directory;
            if (directoryInfo is null || !directoryInfo.Exists)
            {
                return;
            }

            fileInfo.Refresh();
            var args = fileInfo.Exists
                ? $"/select,\"{fileInfo.FullName}\""
                : $"\"{directoryInfo.FullName}\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                Arguments = args,
                UseShellExecute = true,
                WorkingDirectory = directoryInfo.FullName
            });
        }
    }
}