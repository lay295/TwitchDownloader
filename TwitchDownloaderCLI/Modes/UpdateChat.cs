using System;
using System.IO;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal static class UpdateChat
    {
        internal static void Update(ChatUpdateArgs inputOptions)
        {
            using var progress = new CliTaskProgress(inputOptions.LogLevel);

            var collisionHandler = new FileCollisionHandler(inputOptions, progress);
            var updateOptions = GetUpdateOptions(inputOptions, collisionHandler, progress);

            var chatUpdater = new ChatUpdater(updateOptions, progress);
            chatUpdater.ParseJsonAsync().Wait();
            chatUpdater.UpdateAsync(new CancellationToken()).Wait();
        }

        private static ChatUpdateOptions GetUpdateOptions(ChatUpdateArgs inputOptions, FileCollisionHandler collisionHandler, ITaskLogger logger)
        {
            if (!File.Exists(inputOptions.InputFile))
            {
                logger.LogError("Input file does not exist!");
                Environment.Exit(1);
            }

            var inFileExtension = Path.GetExtension(inputOptions.InputFile)!.ToLower();
            var inFormat = inFileExtension switch
            {
                ".html" or ".htm" => ChatFormat.Html,
                ".json" or ".gz" => ChatFormat.Json,
                ".txt" or ".text" or "" => ChatFormat.Text,
                _ => throw new NotSupportedException($"{inFileExtension} is not a valid chat file extension.")
            };
            var outFileExtension = Path.GetExtension(inputOptions.OutputFile)!.ToLower();
            var outFormat = outFileExtension switch
            {
                ".html" or ".htm" => ChatFormat.Html,
                ".json" => ChatFormat.Json,
                ".txt" or ".text" or "" => ChatFormat.Text,
                _ => throw new NotSupportedException($"{outFileExtension} is not a valid chat file extension.")
            };
            if (inFormat != ChatFormat.Json)
            {
                logger.LogError("Input file must be .json or .json.gz!");
                Environment.Exit(1);
            }

            if (Path.GetFullPath(inputOptions.InputFile!) == Path.GetFullPath(inputOptions.OutputFile!))
            {
                logger.LogWarning("Output file path is identical to input file. This is not recommended in case something goes wrong. All data will be permanently overwritten!");
            }

            ChatUpdateOptions updateOptions = new()
            {
                InputFile = inputOptions.InputFile,
                OutputFile = inputOptions.Compression is ChatCompression.Gzip
                    ? inputOptions.OutputFile + ".gz"
                    : inputOptions.OutputFile,
                Compression = inputOptions.Compression,
                OutputFormat = outFormat,
                EmbedMissing = inputOptions.EmbedMissing,
                ReplaceEmbeds = inputOptions.ReplaceEmbeds,
                TrimBeginning = inputOptions.TrimBeginningTime >= TimeSpan.Zero,
                TrimBeginningTime = ((TimeSpan)inputOptions.TrimBeginningTime).TotalSeconds,
                TrimEnding = inputOptions.TrimEndingTime >= TimeSpan.Zero,
                TrimEndingTime = ((TimeSpan)inputOptions.TrimEndingTime).TotalSeconds,
                BttvEmotes = (bool)inputOptions.BttvEmotes!,
                FfzEmotes = (bool)inputOptions.FfzEmotes!,
                StvEmotes = (bool)inputOptions.StvEmotes!,
                TextTimestampFormat = inputOptions.TimeFormat,
                TempFolder = inputOptions.TempFolder,
                FileCollisionCallback = collisionHandler.HandleCollisionCallback,
            };

            return updateOptions;
        }
    }
}