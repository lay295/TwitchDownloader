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

            ClipDownloader clipDownloader = new(downloadOptions, progress);
            clipDownloader.DownloadAsync(new CancellationToken()).Wait();
        }

        private static ClipDownloadOptions GetDownloadOptions(ClipDownloadArgs inputOptions)
        {
            if (inputOptions.Id is null)
            {
                Console.WriteLine("[ERROR] - Clip ID/URL cannot be null!");
                Environment.Exit(1);
            }

            var clipIdRegex = new Regex(@"(?<=^|(?:clips\.)?twitch\.tv\/(?:\S+\/clip)?\/?)[\w-]+?(?=$|\?)");
            var clipIdMatch = clipIdRegex.Match(inputOptions.Id);
            if (!clipIdMatch.Success)
            {
                Console.WriteLine("[ERROR] - Unable to parse Clip ID/URL.");
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
                TempFolder = inputOptions.TempFolder
            };

            return downloadOptions;
        }
    }
}
