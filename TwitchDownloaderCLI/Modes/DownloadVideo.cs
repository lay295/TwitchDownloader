using System;
using System.IO;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCLI.Modes
{
    internal static class DownloadVideo
    {
        internal static void Download(VideoDownloadArgs inputOptions)
        {
            FfmpegHandler.DetectFfmpeg(inputOptions.FfmpegPath);

            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;

            var downloadOptions = GetDownloadOptions(inputOptions);
            downloadOptions.CacheCleanerCallback = directoryInfos =>
            {
                Console.WriteLine(
                    $"[LOG] - {directoryInfos.Length} unmanaged video caches were found at '{downloadOptions.TempFolder}' and can be safely deleted. " +
                    "Run 'TwitchDownloaderCLI cache help' for more information.");

                return Array.Empty<DirectoryInfo>();
            };

            VideoDownloader videoDownloader = new(downloadOptions, progress);
            videoDownloader.DownloadAsync(new CancellationToken()).Wait();
        }

        private static VideoDownloadOptions GetDownloadOptions(VideoDownloadArgs inputOptions)
        {
            if (inputOptions.Id is null)
            {
                Console.WriteLine("[ERROR] - Vod ID/URL cannot be null!");
                Environment.Exit(1);
            }

            var vodIdMatch = TwitchRegex.MatchVideoId(inputOptions.Id);
            if (vodIdMatch is not { Success: true })
            {
                Console.WriteLine("[ERROR] - Unable to parse Vod ID/URL.");
                Environment.Exit(1);
            }

            if (!Path.HasExtension(inputOptions.OutputFile) && inputOptions.Quality is { Length: > 0 })
            {
                if (inputOptions.Quality.Contains("audio", StringComparison.OrdinalIgnoreCase))
                    inputOptions.OutputFile += ".m4a";
                else if (char.IsDigit(inputOptions.Quality[0])
                         || inputOptions.Quality.Contains("source", StringComparison.OrdinalIgnoreCase)
                         || inputOptions.Quality.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                    inputOptions.OutputFile += ".mp4";
            }

            VideoDownloadOptions downloadOptions = new()
            {
                DownloadThreads = inputOptions.DownloadThreads,
                ThrottleKib = inputOptions.ThrottleKib,
                Id = int.Parse(vodIdMatch.ValueSpan),
                Oauth = inputOptions.Oauth,
                Filename = inputOptions.OutputFile,
                Quality = Path.GetExtension(inputOptions.OutputFile)!.ToLower() switch
                {
                    ".mp4" => inputOptions.Quality,
                    ".m4a" => "Audio",
                    _ => throw new ArgumentException("Only MP4 and M4A audio files are supported.")
                },
                KeepCache = inputOptions.KeepCache,
                KeepCacheNoParts = inputOptions.KeepCacheNoParts,
                CropBeginning = inputOptions.CropBeginningTime > 0.0,
                CropBeginningTime = inputOptions.CropBeginningTime,
                CropEnding = inputOptions.CropEndingTime > 0.0,
                CropEndingTime = inputOptions.CropEndingTime,
                FfmpegPath = string.IsNullOrWhiteSpace(inputOptions.FfmpegPath) ? FfmpegHandler.FfmpegExecutableName : Path.GetFullPath(inputOptions.FfmpegPath),
                TempFolder = inputOptions.TempFolder
            };

            return downloadOptions;
        }
    }
}
