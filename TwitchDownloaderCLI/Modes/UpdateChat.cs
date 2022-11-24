﻿using System;
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
            ChatFormat inFormat = Path.GetExtension(inputOptions.InputFile)!.ToLower() switch
            {
                ".json" => ChatFormat.Json,
                ".html" => ChatFormat.Html,
                ".htm" => ChatFormat.Html,
                _ => ChatFormat.Text
            };
            ChatFormat outFormat = Path.GetExtension(inputOptions.OutputFile)!.ToLower() switch
            {
                ".json" => ChatFormat.Json,
                ".html" => ChatFormat.Html,
                ".htm" => ChatFormat.Html,
                _ => ChatFormat.Text
            };
            if (inFormat != outFormat)
            {
                Console.WriteLine("[ERROR] - Input file extension must match output file extension!");
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
            if (!inputOptions.EmbedMissing && !inputOptions.ReplaceEmbeds && double.IsNegative(inputOptions.CropBeginningTime) && double.IsNegative(inputOptions.CropEndingTime))
            {
                Console.WriteLine("[ERROR] - No update options were passed. Please pass --embed-missing, --replace-embeds, -b, or -e");
                Environment.Exit(1);
            }

            ChatUpdateOptions updateOptions = new ChatUpdateOptions()
            {
                InputFile = inputOptions.InputFile,
                OutputFile = inputOptions.OutputFile,
                FileFormat = inFormat,
                EmbedMissing = inputOptions.EmbedMissing,
                ReplaceEmbeds = inputOptions.ReplaceEmbeds,
                CropBeginning = !double.IsNegative(inputOptions.CropBeginningTime),
                CropBeginningTime = inputOptions.CropBeginningTime,
                CropEnding = !double.IsNegative(inputOptions.CropEndingTime),
                CropEndingTime = inputOptions.CropEndingTime,
                BttvEmotes = (bool)inputOptions.BttvEmotes,
                FfzEmotes = (bool)inputOptions.FfzEmotes,
                StvEmotes = (bool)inputOptions.StvEmotes,
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
