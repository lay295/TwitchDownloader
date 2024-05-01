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
    internal static class DownloadChat
    {
        internal static void Download(ChatDownloadArgs inputOptions)
        {
            var progress = new CliTaskProgress(inputOptions.LogLevel);

            var downloadOptions = GetDownloadOptions(inputOptions, progress);

            var chatDownloader = new ChatDownloader(downloadOptions, progress);
            chatDownloader.DownloadAsync(CancellationToken.None).Wait();
        }

        private static ChatDownloadOptions GetDownloadOptions(ChatDownloadArgs inputOptions, ITaskLogger logger)
        {
            if (inputOptions.Id is null)
            {
                logger.LogError("Vod/Clip ID/URL cannot be null!");
                Environment.Exit(1);
            }

            var vodClipIdMatch = TwitchRegex.MatchVideoOrClipId(inputOptions.Id);
            if (vodClipIdMatch is not { Success: true })
            {
                logger.LogError("Unable to parse Vod/Clip ID/URL.");
                Environment.Exit(1);
            }

            var fileExtension = Path.GetExtension(inputOptions.OutputFile)!.ToLower();

            ChatDownloadOptions downloadOptions = new()
            {
                DownloadFormat = fileExtension switch
                {
                    ".html" or ".htm" => ChatFormat.Html,
                    ".json" => ChatFormat.Json,
                    ".txt" or ".text" or "" => ChatFormat.Text,
                    _ => throw new NotSupportedException($"{fileExtension} is not a valid chat file extension.")
                },
                Id = vodClipIdMatch.Value,
                TrimBeginning = inputOptions.TrimBeginningTime > TimeSpan.Zero,
                TrimBeginningTime = ((TimeSpan)inputOptions.TrimBeginningTime).TotalSeconds,
                TrimEnding = inputOptions.TrimEndingTime > TimeSpan.Zero,
                TrimEndingTime = ((TimeSpan)inputOptions.TrimEndingTime).TotalSeconds,
                EmbedData = inputOptions.EmbedData,
                Filename = inputOptions.Compression is ChatCompression.Gzip
                    ? inputOptions.OutputFile + ".gz"
                    : inputOptions.OutputFile,
                Compression = inputOptions.Compression,
                TimeFormat = inputOptions.TimeFormat,
                ConnectionCount = inputOptions.ChatConnections,
                Silent = inputOptions.Silent,
                BttvEmotes = (bool)inputOptions.BttvEmotes!,
                FfzEmotes = (bool)inputOptions.FfzEmotes!,
                StvEmotes = (bool)inputOptions.StvEmotes!,
                TempFolder = inputOptions.TempFolder
            };

            return downloadOptions;
        }
    }
}