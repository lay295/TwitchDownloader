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
            var progress = new CliTaskProgress(inputOptions.LogLevel);

            progress.LogInfo("The TS merger is experimental and is subject to change without notice in future releases.");

            var mergeOptions = GetMergeOptions(inputOptions);

            var tsMerger = new TsMerger(mergeOptions, progress);
            tsMerger.MergeAsync(new CancellationToken()).Wait();
        }

        private static TsMergeOptions GetMergeOptions(TsMergeArgs inputOptions)
        {
            TsMergeOptions mergeOptions = new()
            {
                OutputFile = inputOptions.OutputFile,
                InputFile = inputOptions.InputList
            };

            return mergeOptions;
        }
    }
}
