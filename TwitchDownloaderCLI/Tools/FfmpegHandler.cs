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
        public static readonly string FfmpegExecutableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

        public static void ParseArgs(FfmpegArgs args)
        {
            if (args.DownloadFfmpeg)
            {
                DownloadFfmpeg();
            }
        }

        private static void DownloadFfmpeg()
        {
            Console.WriteLine("[INFO] - Downloading ffmpeg");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full).Wait();
                return;
            }

            FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official).Wait();
            try
            {
                var ffmpegFileInfo = new UnixFileInfo("ffmpeg")
                {
                    FileAccessPermissions = FileAccessPermissions.UserRead | FileAccessPermissions.UserWrite | FileAccessPermissions.GroupRead |  FileAccessPermissions.OtherRead |
                                            FileAccessPermissions.UserExecute | FileAccessPermissions.GroupExecute | FileAccessPermissions.OtherExecute
                };
                ffmpegFileInfo.Refresh();
            }
            catch
            {
                Console.WriteLine("[ERROR] - Unable to update ffmpeg file permissions. Run 'sudo chmod +x ffmpeg' if further ffmpeg errors occur.");
            }
        }

        public static void DetectFfmpeg(string ffmpegPath)
        {
            if (File.Exists(ffmpegPath) || File.Exists(FfmpegExecutableName) || PathUtils.ExistsOnPATH(FfmpegExecutableName))
            {
                return;
            }

            Console.WriteLine("[ERROR] - Unable to find ffmpeg, exiting. You can download ffmpeg automatically with the command \"TwitchDownloaderCLI ffmpeg -d\"");
            Environment.Exit(1);
        }
    }
}