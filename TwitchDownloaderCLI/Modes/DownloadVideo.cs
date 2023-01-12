using System;
using System.IO;
using System.Linq;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using System.Text.RegularExpressions;

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
            string pattern = @"\/(\d+)";
            var match = Regex.Match(inputOptions.Id, pattern);
            if (!match.Success)
            {
                Console.WriteLine("[ERROR] - Invalid VOD URL, unable to parse the video ID from it.");
                Environment.Exit(1);
            }
            var videoId = match.Groups[1].Value;
            if (!int.TryParse(videoId, out int videoIdInt))
            {
                Console.WriteLine("[ERROR] - Invalid VOD Id, unable to parse. Must be only numbers.");
                Environment.Exit(1);
            }
            VideoDownloadOptions downloadOptions = new()
            {
                Id = videoIdInt,
                DownloadThreads = inputOptions.DownloadThreads,
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

