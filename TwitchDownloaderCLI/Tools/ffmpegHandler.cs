using Mono.Unix;
using System;
using System.IO;
using System.Runtime.InteropServices;
using TwitchDownloaderCLI.Modes.Arguments;
using Xabe.FFmpeg.Downloader;

namespace TwitchDownloaderCLI.Tools
{
    public static class FfmpegHandler
    {
        public static readonly string ffmpegExecutableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

        public static void ParseArgs(FfmpegArgs args)
        {
            if (args.DownloadFfmpeg)
            {
                DownloadFfmpeg();
            }
        }

        public static void DownloadFfmpeg()
        {
            Console.WriteLine("[INFO] - Downloading ffmpeg");
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
        }

        public static void DetectFfmpeg(string ffmpegPath)
        {
            if (File.Exists(ffmpegExecutableName) || PathExtensions.ExistsOnPath(ffmpegExecutableName) || File.Exists(ffmpegPath))
            {
                return;
            }

            Console.WriteLine("[ERROR] - Unable to find ffmpeg, exiting. You can download ffmpeg automatically with the command \"TwitchDownloaderCLI ffmpeg -d\"");
            Environment.Exit(1);
        }
    }
}
