using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore
{
    public sealed class TsMerger
    {
        private readonly TsMergeOptions mergeOptions;
        private readonly ITaskProgress _progress;

        public TsMerger(TsMergeOptions tsMergeOptions, ITaskProgress progress)
        {
            mergeOptions = tsMergeOptions;
            _progress = progress;
        }

        public async Task MergeAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(mergeOptions.InputFile))
            {
                throw new FileNotFoundException("Input file does not exist");
            }

            var isM3U8 = false;
            var fileList = new List<string>();
            await using (var fs = File.Open(mergeOptions.InputFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using var sr = new StreamReader(fs);
                while (await sr.ReadLineAsync() is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (isM3U8)
                    {
                        if (line.StartsWith('#')) continue;
                    }
                    else
                    {
                        if (line.StartsWith("#EXTM3U")) isM3U8 = true;
                    }

                    fileList.Add(line);
                }
            }

            _progress.SetTemplateStatus("Verifying Parts {0}% [1/2]", 0);

            await VerifyVideoParts(fileList, cancellationToken);

            _progress.SetTemplateStatus("Combining Parts {0}% [2/2]", 0);

            await CombineVideoParts(fileList, cancellationToken);

            _progress.ReportProgress(100);
        }

        private async Task VerifyVideoParts(IReadOnlyCollection<string> fileList, CancellationToken cancellationToken)
        {
            var resultLock = new object();
            var resultCounts = new Dictionary<TsVerifyResult, int>();
            var missingParts = new List<string>();
            var failedParts = new List<string>();
            var partCount = fileList.Count;
            var doneCount = 0;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(fileList, parallelOptions, async (part, token) =>
            {
                var result = await DownloadTools.VerifyTransportStream(part, _progress);

                lock (resultLock)
                {
                    CollectionsMarshal.GetValueRefOrAddDefault(resultCounts, result, out _)++; // Gets or creates the value and increments it
                    if (result == TsVerifyResult.NotFound)
                    {
                        missingParts.Add(part);
                    }
                    else if (result != TsVerifyResult.Success)
                    {
                        failedParts.Add(part);
                    }
                }

                Interlocked.Add(ref doneCount, 1);
                var percent = (int)(doneCount / (double)partCount * 100);
                _progress.ReportProgress(percent);
            });

            _progress.LogVerbose($"TS verification results: {string.Join(", ", resultCounts.Select(x => $"{x.Key}: {x.Value}"))}");

            if (missingParts.Count > 0)
            {
                _progress.LogWarning($"The following files could not be found: {string.Join(", ", missingParts)}");
            }

            if (failedParts.Count > 0)
            {
                _progress.LogWarning($"The following TS files are potentially corrupted: {string.Join(", ", failedParts)}");
            }
        }

        private async Task CombineVideoParts(IReadOnlyCollection<string> fileList, CancellationToken cancellationToken)
        {
            DriveInfo outputDrive = DriveHelper.GetOutputDrive(mergeOptions.OutputFile);
            string outputFile = mergeOptions.OutputFile;

            int partCount = fileList.Count;
            int doneCount = 0;

            await using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            foreach (var partFile in fileList)
            {
                await DriveHelper.WaitForDrive(outputDrive, _progress, cancellationToken);

                await using (var fs = File.Open(partFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await fs.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                }

                doneCount++;
                int percent = (int)(doneCount / (double)partCount * 100);
                _progress.ReportProgress(percent);

                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
