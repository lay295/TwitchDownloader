using System;
using System.IO;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal class DownloadChat
    {
        internal static void Download(ChatDownloadArgs inputOptions)
        {
            if (string.IsNullOrWhiteSpace(inputOptions.Id))
            {
                Console.WriteLine("[ERROR] - Invalid ID, unable to parse.");
                Environment.Exit(1);
            }

            ChatDownloadOptions downloadOptions = new()
            {
                DownloadFormat = Path.GetExtension(inputOptions.OutputFile)!.ToLower() switch
                {
                    ".json" => DownloadFormat.Json,
                    ".html" => DownloadFormat.Html,
                    ".htm" => DownloadFormat.Html,
                    _ => DownloadFormat.Text
                },
                Id = inputOptions.Id,
                CropBeginning = inputOptions.CropBeginningTime > 0.0,
                CropBeginningTime = inputOptions.CropBeginningTime,
                CropEnding = inputOptions.CropEndingTime > 0.0,
                CropEndingTime = inputOptions.CropEndingTime,
                Timestamp = inputOptions.Timestamp,
                EmbedData = inputOptions.EmbedData,
                Filename = inputOptions.OutputFile,
                TimeFormat = inputOptions.TimeFormat,
                ConnectionCount = inputOptions.ChatConnections,
                BttvEmotes = (bool)inputOptions.BttvEmotes,
                FfzEmotes = (bool)inputOptions.FfzEmotes,
                StvEmotes = (bool)inputOptions.StvEmotes,
                TempFolder = inputOptions.TempFolder
            };

            ChatDownloader chatDownloader = new(downloadOptions);
            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;
            chatDownloader.DownloadAsync(progress, new CancellationToken()).Wait();
        }
    }
}
