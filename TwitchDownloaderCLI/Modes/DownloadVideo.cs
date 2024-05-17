﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCLI.Modes
{
    internal static class DownloadVideo
    {
        internal static void Download(VideoDownloadArgs inputOptions)
        {
            var progress = new CliTaskProgress(inputOptions.LogLevel);

            FfmpegHandler.DetectFfmpeg(inputOptions.FfmpegPath, progress);

            var downloadOptions = GetDownloadOptions(inputOptions, progress);

            var videoDownloader = new VideoDownloader(downloadOptions, progress);
            videoDownloader.DownloadAsync(new CancellationToken()).Wait();
        }

        private static VideoDownloadOptions GetDownloadOptions(VideoDownloadArgs inputOptions, ITaskLogger logger)
        {
            if (inputOptions.Id is null)
            {
                logger.LogError("Vod ID/URL cannot be null!");
                Environment.Exit(1);
            }

            var vodIdMatch = TwitchRegex.MatchVideoId(inputOptions.Id);
            if (vodIdMatch is not { Success: true })
            {
                logger.LogError("Unable to parse Vod ID/URL.");
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
                Id = long.Parse(vodIdMatch.ValueSpan),
                Oauth = inputOptions.Oauth,
                Filename = inputOptions.OutputFile,
                Quality = Path.GetExtension(inputOptions.OutputFile)!.ToLower() switch
                {
                    ".mp4" => inputOptions.Quality,
                    ".m4a" => "Audio",
                    _ => throw new ArgumentException("Only MP4 and M4A audio files are supported.")
                },
                TrimBeginning = inputOptions.TrimBeginningTime > TimeSpan.Zero,
                TrimBeginningTime = inputOptions.TrimBeginningTime,
                TrimEnding = inputOptions.TrimEndingTime > TimeSpan.Zero,
                TrimEndingTime = inputOptions.TrimEndingTime,
                FfmpegPath = string.IsNullOrWhiteSpace(inputOptions.FfmpegPath) ? FfmpegHandler.FfmpegExecutableName : Path.GetFullPath(inputOptions.FfmpegPath),
                TempFolder = inputOptions.TempFolder,
                CacheCleanerCallback = directoryInfos =>
                {
                    logger.LogInfo(
                        $"{directoryInfos.Length} unmanaged video caches were found at '{directoryInfos.FirstOrDefault()?.Parent?.FullName ?? inputOptions.TempFolder}' and can be safely deleted. " +
                        "Run 'TwitchDownloaderCLI cache help' for more information.");

                    return Array.Empty<DirectoryInfo>();
                }
            };

            return downloadOptions;
        }
    }
}