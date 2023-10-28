using System;
using System.IO;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCLI.Modes
{
    internal static class MergeTS
    {
        internal static void Merge(TsMergeArgs inputOptions)
        {
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
                InputList = inputOptions.InputList
            };

            return mergeOptions;
        }
    }
}
