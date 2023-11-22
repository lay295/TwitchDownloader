using System;
using System.IO;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;

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
            VideoDownloaderFactory downloadFactory = new VideoDownloaderFactory(progress);
            IVideoDownloader videoDownloader = downloadFactory.Create(downloadOptions);
            videoDownloader.DownloadAsync(new CancellationToken()).Wait();
        }

        private static VideoDownloadOptions GetDownloadOptions(VideoDownloadArgs inputOptions)
        {
            if (inputOptions.Id is null)
            {
                Console.WriteLine("[ERROR] - Vod ID/URL cannot be null!");
                Environment.Exit(1);
            }

            if (IdParse.TryParseVod(inputOptions.Id, out var videoPlatform, out var videoId))
            {
                Console.WriteLine("[ERROR] - Unable to parse Vod ID/URL.");
                Environment.Exit(1);
            }

            if (!Path.HasExtension(inputOptions.OutputFile) && inputOptions.Quality is { Length: > 0 })
            {
                if (char.IsDigit(inputOptions.Quality[0]))
                    inputOptions.OutputFile += ".mp4";
                else if (char.ToLower(inputOptions.Quality[0]) is 'a')
                    inputOptions.OutputFile += ".m4a";
            }

            VideoDownloadOptions downloadOptions = new()
            {
                DownloadThreads = inputOptions.DownloadThreads,
                ThrottleKib = inputOptions.ThrottleKib,
                Id = videoId,
                VideoPlatform = videoPlatform,
                Oauth = inputOptions.Oauth,
                Filename = inputOptions.OutputFile,
                Quality = Path.GetExtension(inputOptions.OutputFile)!.ToLower() switch
                {
                    ".m4a" when videoPlatform is VideoPlatform.Twitch => "Audio",
                    ".mp4" or ".m4a" => inputOptions.Quality,
                    _ => throw new ArgumentException("Only MP4 and M4A audio files are supported.")
                },
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
