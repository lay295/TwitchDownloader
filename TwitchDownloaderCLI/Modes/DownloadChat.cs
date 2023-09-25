using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Twitch.Downloaders;

namespace TwitchDownloaderCLI.Modes
{
    internal static class DownloadChat
    {
        internal static void Download(ChatDownloadArgs inputOptions)
        {
            var downloadOptions = GetDownloadOptions(inputOptions);

            Progress<ProgressReport> progress = new();
            ChatDownloaderFactory downloadFactory = new ChatDownloaderFactory(progress);
            IChatDownloader chatDownloader = downloadFactory.Create(downloadOptions);
            chatDownloader.DownloadAsync(new CancellationToken()).Wait();
        }

        private static ChatDownloadOptions GetDownloadOptions(ChatDownloadArgs inputOptions)
        {
            if (inputOptions.Id is null)
            {
                Console.WriteLine("[ERROR] - Vod/Clip ID/URL cannot be null!");
                Environment.Exit(1);
            }

            var vodClipIdRegex = new Regex(@"(?<=^|(?:clips\.)?twitch\.tv\/(?:videos|\S+\/clip)?\/?)[\w-]+?(?=$|\?)");
            var vodClipIdMatch = vodClipIdRegex.Match(inputOptions.Id);
            if (!vodClipIdMatch.Success)
            {
                Console.WriteLine("[ERROR] - Unable to parse Vod/Clip ID/URL.");
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
                CropBeginning = inputOptions.CropBeginningTime > 0.0,
                CropBeginningTime = inputOptions.CropBeginningTime,
                CropEnding = inputOptions.CropEndingTime > 0.0,
                CropEndingTime = inputOptions.CropEndingTime,
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