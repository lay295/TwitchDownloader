using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal static class DownloadVideo
    {
        internal static void Download(VideoDownloadArgs inputOptions)
        {
            FfmpegHandler.DetectFfmpeg(inputOptions.FfmpegPath);

            VideoDownloadOptions downloadOptions = GetDownloadOptions(inputOptions);

            VideoDownloader videoDownloader = new(downloadOptions);
            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;
            videoDownloader.DownloadAsync(progress, new CancellationToken()).Wait();
        }

        private static VideoDownloadOptions GetDownloadOptions(VideoDownloadArgs inputOptions)
        {
            if (inputOptions.Id is null)
            {
                Console.WriteLine("[ERROR] - Vod ID/URL cannot be null!");
                Environment.Exit(1);
            }

            var vodIdRegex = new Regex(@"(?:^|(?:twitch.tv\/videos\/))(\d+)(?:$|\?)");
            var vodIdMatch = vodIdRegex.Match(inputOptions.Id);
            if (!vodIdMatch.Success)
            {
                Console.WriteLine("[ERROR] - Unable to parse Vod ID/URL.");
                Environment.Exit(1);
            }

            VideoDownloadOptions downloadOptions = new()
            {
                DownloadThreads = inputOptions.DownloadThreads,
                Id = int.Parse(vodIdMatch.Groups[1].ToString()),
                Oauth = inputOptions.Oauth,
                Filename = inputOptions.OutputFile,
                Quality = inputOptions.Quality,
                CropBeginning = inputOptions.CropBeginningTime > 0.0,
                CropBeginningTime = inputOptions.CropBeginningTime,
                CropEnding = inputOptions.CropEndingTime > 0.0,
                CropEndingTime = inputOptions.CropEndingTime,
                FfmpegPath = string.IsNullOrWhiteSpace(inputOptions.FfmpegPath) ? FfmpegHandler.ffmpegExecutableName : Path.GetFullPath(inputOptions.FfmpegPath),
                TempFolder = inputOptions.TempFolder
            };

            return downloadOptions;
        }
    }
}
