using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal class DownloadChat
    {
        internal static void Download(ChatDownloadArgs inputOptions)
        {
            ChatDownloadOptions downloadOptions = GetDownloadOptions(inputOptions);

            ChatDownloader chatDownloader = new(downloadOptions);
            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;
            chatDownloader.DownloadAsync(progress, new CancellationToken()).Wait();
        }

        private static ChatDownloadOptions GetDownloadOptions(ChatDownloadArgs inputOptions)
        {
            if (inputOptions.Id is null)
            {
                Console.WriteLine("[ERROR] - Vod/Clip ID/URL cannot be null!");
                Environment.Exit(1);
            }

            var vodClipIdRegex = new Regex(@"(?:^|(?:twitch.tv\/(?:videos|\w+\/clip)\/))(\w+(?:-\w+)?)(?:$|\?)");
            var vodClipIdMatch = vodClipIdRegex.Match(inputOptions.Id);
            if (!vodClipIdMatch.Success)
            {
                Console.WriteLine("[ERROR] - Unable to parse Vod/Clip ID/URL.");
                Environment.Exit(1);
            }

            ChatDownloadOptions downloadOptions = new()
            {
                DownloadFormat = Path.GetExtension(inputOptions.OutputFile)!.ToLower() switch
                {
                    ".html" or ".htm" => ChatFormat.Html,
                    ".json" => ChatFormat.Json,
                    _ => ChatFormat.Text
                },
                Id = vodClipIdMatch.Groups[1].ToString(),
                CropBeginning = inputOptions.CropBeginningTime > 0.0,
                CropBeginningTime = inputOptions.CropBeginningTime,
                CropEnding = inputOptions.CropEndingTime > 0.0,
                CropEndingTime = inputOptions.CropEndingTime,
                EmbedData = inputOptions.EmbedData,
                Filename = inputOptions.Compression != ChatCompression.None ? inputOptions.OutputFile + ".gz" : inputOptions.OutputFile,
                TimeFormat = inputOptions.TimeFormat,
                ConnectionCount = inputOptions.ChatConnections,
                BttvEmotes = (bool)inputOptions.BttvEmotes,
                FfzEmotes = (bool)inputOptions.FfzEmotes,
                StvEmotes = (bool)inputOptions.StvEmotes,
                TempFolder = inputOptions.TempFolder
            };

            return downloadOptions;
        }
    }
}
