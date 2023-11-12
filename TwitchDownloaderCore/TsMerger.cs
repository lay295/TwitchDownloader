using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore
{
    public sealed class TsMerger
    {
        private readonly TsMergeOptions downloadOptions;
        private readonly IProgress<ProgressReport> _progress;

        public TsMerger(TsMergeOptions tsMergeOptions, IProgress<ProgressReport> progress)
        {
            downloadOptions = tsMergeOptions;
            _progress = progress;
        }

        public async Task MergeAsync(CancellationToken cancellationToken)
        {
            string InputList = downloadOptions.InputList;
            List<string> videoPartsList = System.IO.File.ReadLines(InputList).ToList();
            videoPartsList.RemoveAll(string.IsNullOrWhiteSpace);

            _progress.Report(new ProgressReport(ReportType.SameLineStatus, "Verifying Parts 0% [1/2]"));

            VerifyDownloadedParts(videoPartsList, cancellationToken);

            _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Combining Parts 0% [2/2]" });

            await CombineVideoParts(videoPartsList, cancellationToken);

            _progress.Report(new ProgressReport(100));
        }

        private void VerifyDownloadedParts(List<string> videoParts, CancellationToken cancellationToken)
        {
            var failedParts = new List<string>();
            var partCount = videoParts.Count;
            var doneCount = 0;

            foreach (var part in videoParts)
            {
                if (!VerifyVideoPart(part))
                {
                    failedParts.Add(part);
                }

                doneCount++;
                var percent = (int)(doneCount / (double)partCount * 100);
                _progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Verifying Parts {percent}% [1/2]"));
                _progress.Report(new ProgressReport(percent));

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (failedParts.Count != 0)
            {
                if (failedParts.Count == videoParts.Count)
                {
                    // Every video part returned corrupted, probably a false positive.
                    return;
                }

                _progress.Report(new ProgressReport(ReportType.Log, $"The following parts appear to be invalid TS files: {string.Join(", ", failedParts)}"));
            }
        }

        private static bool VerifyVideoPart(string partFile)
        {
            const int TS_PACKET_LENGTH = 188; // MPEG TS packets are made of a header and a body: [ 4B ][   184B   ] - https://tsduck.io/download/docs/mpegts-introduction.pdf

            if (!File.Exists(partFile))
            {
                return false;
            }

            using var fs = File.Open(partFile, FileMode.Open, FileAccess.Read, FileShare.None);
            var fileLength = fs.Length;
            if (fileLength == 0 || fileLength % TS_PACKET_LENGTH != 0)
            {
                return false;
            }

            return true;
        }

        private async Task CombineVideoParts(List<string> videoParts, CancellationToken cancellationToken)
        {
            DriveInfo outputDrive = DriveHelper.GetOutputDrive(downloadOptions.OutputFile);
            string outputFile = downloadOptions.OutputFile;

            int partCount = videoParts.Count;
            int doneCount = 0;

            await using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            foreach (var partFile in videoParts)
            {
                await DriveHelper.WaitForDrive(outputDrive, _progress, cancellationToken);

                await using (var fs = File.Open(partFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await fs.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                }

                doneCount++;
                int percent = (int)(doneCount / (double)partCount * 100);
                _progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Combining Parts {percent}% [2/2]"));
                _progress.Report(new ProgressReport(percent));

                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
