using Mono.Unix;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Xabe.FFmpeg.Downloader;
using static TwitchDownloaderCLI.Tools.PathExtensions;

namespace TwitchDownloaderCLI.Tools
{
    internal static class FfmpegHandler
    {
        internal static void DownloadFfmpeg()
        {
            Console.WriteLine("[INFO] - Downloading ffmpeg and exiting");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official).Wait();
                try
                {
                    var ffmpegFileInfo = new UnixFileInfo("ffmpeg")
                    {
                        FileAccessPermissions = FileAccessPermissions.UserRead | FileAccessPermissions.UserWrite | FileAccessPermissions.GroupRead |
                        FileAccessPermissions.OtherRead | FileAccessPermissions.UserExecute | FileAccessPermissions.GroupExecute | FileAccessPermissions.OtherExecute
                    };
                    ffmpegFileInfo.Refresh();
                }
                catch { }
            }
            else
            {
                FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full).Wait();
            }
            Environment.Exit(0);
        }

        internal static void DetectFfmpeg(string ffmpegExecutableName, string ffmpegPath, RunMode runMode)
        {
            if (runMode is RunMode.ChatDownload or RunMode.ClipDownload)
            {
                return;
            }
            if (File.Exists(ffmpegExecutableName) || ExistsOnPath(ffmpegExecutableName) || File.Exists(ffmpegPath))
            {
                return;
            }

            Console.WriteLine("[ERROR] - Unable to find ffmpeg, exiting. You can download ffmpeg automatically with the argument --download-ffmpeg");
            Environment.Exit(1);
        }
    }
}
