using System;
using System.IO;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal class UpdateChat
    {
        internal static void Update(ChatUpdateArgs inputOptions)
        {
            DownloadFormat inFormat = Path.GetExtension(inputOptions.InputFile)!.ToLower() switch
            {
                ".json" => DownloadFormat.Json,
                ".html" => DownloadFormat.Html,
                ".htm" => DownloadFormat.Html,
                _ => DownloadFormat.Text
            };
            DownloadFormat outFormat = Path.GetExtension(inputOptions.OutputFile)!.ToLower() switch
            {
                ".json" => DownloadFormat.Json,
                ".html" => DownloadFormat.Html,
                ".htm" => DownloadFormat.Html,
                _ => DownloadFormat.Text
            };
            if (inFormat != DownloadFormat.Json || outFormat != DownloadFormat.Json)
            {
                Console.WriteLine("[ERROR] - {0} format must be be json!", inFormat != DownloadFormat.Json ? "Input" : "Output");
                Environment.Exit(1);
            }
            if (!File.Exists(inputOptions.InputFile))
            {
                Console.WriteLine("[ERROR] - Input file does not exist!");
                Environment.Exit(1);
            }
            if (inputOptions.InputFile == inputOptions.OutputFile)
            {
                Console.WriteLine("[WARNING] - Output file path is identical to input file. This is not recommended in case something goes wrong. All data will be permanantly overwritten!");
            }
            if (!inputOptions.EmbedMissing && !inputOptions.ReplaceEmbeds)
            {
                Console.WriteLine("[ERROR] - Please enable either embed-missing or replace-embeds");
                Environment.Exit(1);
            }

            // Ensure beginning crop <= ending crop
            inputOptions.CropBeginningTime = Math.Min(inputOptions.CropBeginningTime, inputOptions.CropEndingTime);
            inputOptions.CropEndingTime = Math.Max(inputOptions.CropBeginningTime, inputOptions.CropEndingTime);

            ChatUpdateOptions updateOptions = new ChatUpdateOptions()
            {
                InputFile = inputOptions.InputFile,
                OutputFile = inputOptions.OutputFile,
                FileFormat = inFormat,
                ReplaceEmbeds = inputOptions.ReplaceEmbeds,
                CropBeginning = inputOptions.CropBeginningTime > 0.0,
                CropBeginningTime = inputOptions.CropBeginningTime,
                CropEnding = inputOptions.CropEndingTime > 0.0,
                CropEndingTime = inputOptions.CropEndingTime,
                BttvEmotes = inputOptions.BttvEmotes,
                FfzEmotes = inputOptions.FfzEmotes,
                StvEmotes = inputOptions.StvEmotes,
                TempFolder = inputOptions.TempFolder
            };

            ChatUpdater chatUpdater = new(updateOptions);
            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;
            chatUpdater.ParseJsonAsync().Wait();
            chatUpdater.UpdateAsync(progress, new CancellationToken()).Wait();
        }
    }
}
