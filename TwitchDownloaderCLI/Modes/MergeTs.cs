using System;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal static class MergeTs
    {
        internal static void Merge(TsMergeArgs inputOptions)
        {
            Console.WriteLine("[INFO] The TS merger is experimental and is subject to change without notice in future releases.");

            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;

            var mergeOptions = GetMergeOptions(inputOptions);
            TsMerger tsMerger = new(mergeOptions, progress);
            tsMerger.MergeAsync(new CancellationToken()).Wait();
        }

        private static TsMergeOptions GetMergeOptions(TsMergeArgs inputOptions)
        {
            TsMergeOptions mergeOptions = new()
            {
                OutputFile = inputOptions.OutputFile,
                InputFile = inputOptions.InputList,
                IgnoreMissingParts = inputOptions.IgnoreMissingParts
            };

            return mergeOptions;
        }
    }
}
