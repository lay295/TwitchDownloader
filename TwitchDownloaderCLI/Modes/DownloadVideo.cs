using System;
using System.IO;
using System.Linq;
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
            if (string.IsNullOrWhiteSpace(inputOptions.Id) || !inputOptions.Id.All(char.IsDigit))
            {
                Console.WriteLine("[ERROR] - Invalid VOD ID, unable to parse. Must be only numbers.");
                Environment.Exit(1);
            }

            VideoDownloadOptions downloadOptions = new()
            {
                DownloadThreads = inputOptions.DownloadThreads,
                Id = int.Parse(inputOptions.Id),
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
