using System;
using System.IO;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCLI.Modes
{
    internal static class DownloadClip
    {
        internal static void Download(ClipDownloadArgs inputOptions)
        {
            var progress = new CliTaskProgress(inputOptions.LogLevel);

            if (inputOptions.EncodeMetadata == true)
            {
                FfmpegHandler.DetectFfmpeg(inputOptions.FfmpegPath, progress);
            }

            var overwriteHandler = new FileOverwriteHandler(inputOptions);
            var downloadOptions = GetDownloadOptions(inputOptions, overwriteHandler, progress);

            var clipDownloader = new ClipDownloader(downloadOptions, progress);
            clipDownloader.DownloadAsync(new CancellationToken()).Wait();
        }

        private static ClipDownloadOptions GetDownloadOptions(ClipDownloadArgs inputOptions, FileOverwriteHandler overwriteHandler, ITaskLogger logger)
        {
            if (inputOptions.Id is null)
            {
                logger.LogError("Clip ID/URL cannot be null!");
                Environment.Exit(1);
            }

            var clipIdMatch = TwitchRegex.MatchClipId(inputOptions.Id);
            if (clipIdMatch is not { Success: true })
            {
                logger.LogError("Unable to parse Clip ID/URL.");
                Environment.Exit(1);
            }

            ClipDownloadOptions downloadOptions = new()
            {
                Id = clipIdMatch.Value,
                Filename = inputOptions.OutputFile,
                Quality = inputOptions.Quality,
                ThrottleKib = inputOptions.ThrottleKib,
                FfmpegPath = string.IsNullOrWhiteSpace(inputOptions.FfmpegPath) ? FfmpegHandler.FfmpegExecutableName : Path.GetFullPath(inputOptions.FfmpegPath),
                EncodeMetadata = inputOptions.EncodeMetadata!.Value,
                TempFolder = inputOptions.TempFolder,
                FileOverwriteCallback = overwriteHandler.HandleOverwriteCallback,
            };

            return downloadOptions;
        }
    }
}
