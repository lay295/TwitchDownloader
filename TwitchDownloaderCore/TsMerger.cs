using System.Collections.Generic;
using System.IO;
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

            var outputFileInfo = TwitchHelper.ClaimFile(mergeOptions.OutputFile, mergeOptions.FileCollisionCallback, _progress);
            mergeOptions.OutputFile = outputFileInfo.FullName;

            // Open the destination file so that it exists in the filesystem.
            await using var outputFs = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

            try
            {
                await MergeAsyncImpl(outputFs, cancellationToken);
            }
            catch
            {
                await Task.Delay(100, CancellationToken.None);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, outputFs, _progress);

                throw;
            }
        }

        private async Task MergeAsyncImpl(FileStream outputFs, CancellationToken cancellationToken)
        {
            var isM3U8 = false;
            var isFirst = true;
            var fileList = new List<string>();
            await using (var fs = File.Open(mergeOptions.InputFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using var sr = new StreamReader(fs);
                while (await sr.ReadLineAsync() is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (isFirst && line.StartsWith("#EXTM3U")) isM3U8 = true;

                    isFirst = false;

                    if (isM3U8 && line.StartsWith('#')) continue;

                    fileList.Add(line);
                }
            }

            _progress.SetTemplateStatus("Verifying Parts {0}% [1/2]", 0);

            await VerifyVideoParts(fileList, cancellationToken);

            _progress.SetTemplateStatus("Combining Parts {0}% [2/2]", 0);

            await CombineVideoParts(fileList, outputFs, cancellationToken);

            _progress.ReportProgress(100);
        }

        private async Task VerifyVideoParts(IReadOnlyCollection<string> fileList, CancellationToken cancellationToken)
        {
            var failedParts = new List<string>();
            var partCount = fileList.Count;
            var doneCount = 0;

            foreach (var part in fileList)
            {
                var isValidTs = await VerifyVideoPart(part);
                if (!isValidTs)
                {
                    failedParts.Add(part);
                }

                doneCount++;
                var percent = (int)(doneCount / (double)partCount * 100);
                _progress.ReportProgress(percent);

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (failedParts.Count != 0)
            {
                if (failedParts.Count == fileList.Count)
                {
                    // Every video part returned corrupted, probably a false positive.
                    return;
                }

                _progress.LogInfo($"The following TS files are invalid or corrupted: {string.Join(", ", failedParts)}");
            }
        }

        private static async Task<bool> VerifyVideoPart(string filePath)
        {
            const int TS_PACKET_LENGTH = 188; // MPEG TS packets are made of a header and a body: [ 4B ][   184B   ] - https://tsduck.io/download/docs/mpegts-introduction.pdf

            if (!File.Exists(filePath))
            {
                return false;
            }

            await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileLength = fs.Length;
            if (fileLength == 0 || fileLength % TS_PACKET_LENGTH != 0)
            {
                return false;
            }

            return true;
        }

        private async Task CombineVideoParts(IReadOnlyCollection<string> fileList, FileStream outputStream, CancellationToken cancellationToken)
        {
            DriveInfo outputDrive = DriveHelper.GetOutputDrive(mergeOptions.OutputFile);
            string outputFile = mergeOptions.OutputFile;

            int partCount = fileList.Count;
            int doneCount = 0;

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
