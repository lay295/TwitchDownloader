using System.Diagnostics;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCLI.Modes
{
    internal static class DownloadStream
    {
        internal static void Download(StreamDownloadArgs inputOptions)
        {
            using var progress = new CliTaskProgress(inputOptions.LogLevel);

            if (inputOptions.DelayDownload)
            {
                progress.SetStatus($"Waiting for {inputOptions.ChannelLogin} to go live... [0/2]");
                WaitForStreamOnline(inputOptions.ChannelLogin).Wait();
            }

            FfmpegHandler.DetectFfmpeg(inputOptions.FfmpegPath, progress);

            var collisionHandler = new FileCollisionHandler(inputOptions, progress);
            var downloadOptions = GetDownloadOptions(inputOptions, collisionHandler, progress);

            var cts = new CancellationTokenSource();

            void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
            {
                if (cts.IsCancellationRequested)
                {
                    // https://stackoverflow.com/questions/60098078/why-does-adding-a-console-cancelkeypress-handler-block-debugger-ctrlc-exit-when
                    if (Debugger.IsAttached)
                        Environment.Exit(1);
                }

                e.Cancel = true;
                cts.Cancel();
            }

            Console.CancelKeyPress += Console_CancelKeyPress;

            var streamDownloader = new StreamDownloader(downloadOptions, progress);
            streamDownloader.DownloadAsync(cts.Token, CancellationToken.None).Wait();
        }

        private static StreamDownloadOptions GetDownloadOptions(StreamDownloadArgs inputOptions, FileCollisionHandler collisionHandler, ITaskLogger logger)
        {
            if (inputOptions.ChannelLogin is null)
            {
                logger.LogError("Channel login cannot be null!");
                Environment.Exit(1);
            }

            if (!Path.HasExtension(inputOptions.OutputFile) && inputOptions.Quality is { Length: > 0 })
            {
                inputOptions.OutputFile += FilenameService.GuessVodFileExtension(inputOptions.Quality);
            }

            StreamDownloadOptions downloadOptions = new()
            {
                DownloadThreads = inputOptions.DownloadThreads,
                ChannelLogin = inputOptions.ChannelLogin,
                Oauth = inputOptions.Oauth,
                Filename = inputOptions.OutputFile,
                Quality = Path.GetExtension(inputOptions.OutputFile)!.ToLower() switch
                {
                    ".mp4" => inputOptions.Quality,
                    ".m4a" => "Audio",
                    _ => throw new ArgumentException("Only MP4 and M4A audio files are supported.")
                },
                StreamEndWaitTime = TimeDuration.Parse(inputOptions.StreamEndWaitTime),
                FfmpegPath = string.IsNullOrWhiteSpace(inputOptions.FfmpegPath) ? FfmpegHandler.FfmpegExecutableName : Path.GetFullPath(inputOptions.FfmpegPath),
                TempFolder = inputOptions.TempFolder,
                CacheCleanerCallback = directoryInfos =>
                {
                    logger.LogInfo(
                        $"{directoryInfos.Length} unmanaged video caches were found at '{directoryInfos.FirstOrDefault()?.Parent?.FullName ?? inputOptions.TempFolder}' and can be safely deleted. " +
                        "Run 'TwitchDownloaderCLI cache help' for more information.");

                    return [];
                },
                FileCollisionCallback = collisionHandler.HandleCollisionCallback,
            };

            return downloadOptions;
        }

        private static async Task WaitForStreamOnline(string channelLogin)
        {
            GqlStreamResponse streamResponse;
            while (true)
            {
                streamResponse = await TwitchHelper.GetStreamInfo(channelLogin);

                if (streamResponse.data.user is null)
                {
                    throw new Exception("Channel does not exist");
                }

                if (streamResponse.data.user.stream is not null)
                {
                    break;
                }

                await Task.Delay(Random.Shared.Next(10_000, 15_000));
            }

            // Wait for at least 15 seconds of stream time
            var delay = TimeSpan.FromSeconds(15) - (DateTime.Now - streamResponse.data.user.stream.createdAt);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }
        }
    }
}