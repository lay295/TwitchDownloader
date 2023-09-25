using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;

namespace TwitchDownloaderCLI.Modes
{
    internal static class DownloadClip
    {
        internal static void Download(ClipDownloadArgs inputOptions)
        {
            if (inputOptions.EncodeMetadata == true)
            {
                FfmpegHandler.DetectFfmpeg(inputOptions.FfmpegPath);
            }

            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;

            var downloadOptions = GetDownloadOptions(inputOptions);

            ClipDownloaderFactory downloadFactory = new ClipDownloaderFactory(progress);
            IClipDownloader clipDownloader = downloadFactory.Create(downloadOptions);
            clipDownloader.DownloadAsync(new CancellationToken()).Wait();
        }

        private static ClipDownloadOptions GetDownloadOptions(ClipDownloadArgs inputOptions)
        {
            if (inputOptions.Id is null)
            {
                Console.WriteLine("[ERROR] - Clip ID/URL cannot be null!");
                Environment.Exit(1);
            }

            bool success = UrlParse.TryParseClip(inputOptions.Id, out VideoPlatform videoPlatform, out string videoId);
            if (!success)
            {
                Console.WriteLine("[ERROR] - Unable to parse Clip ID/URL.");
                Environment.Exit(1);
            }

            if (videoPlatform == VideoPlatform.Kick)
            {
                CurlHandler.DetectCurl(inputOptions.CurlImpersonatePath);
            }

            ClipDownloadOptions downloadOptions = new()
            {
                Id = videoId,
                Filename = inputOptions.OutputFile,
                Quality = inputOptions.Quality,
                ThrottleKib = inputOptions.ThrottleKib,
                FfmpegPath = string.IsNullOrWhiteSpace(inputOptions.FfmpegPath) ? FfmpegHandler.FfmpegExecutableName : Path.GetFullPath(inputOptions.FfmpegPath),
                EncodeMetadata = inputOptions.EncodeMetadata!.Value,
                TempFolder = inputOptions.TempFolder,
                VideoPlatform = videoPlatform,
            };

            return downloadOptions;
        }
    }
}
