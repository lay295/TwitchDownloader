using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore;

public sealed class TsMerger {
    private readonly ITaskProgress _progress;
    private readonly TsMergeOptions mergeOptions;

    public TsMerger(TsMergeOptions tsMergeOptions, ITaskProgress progress) {
        this.mergeOptions = tsMergeOptions;
        this._progress = progress;
    }

    public async Task MergeAsync(CancellationToken cancellationToken) {
        if (!File.Exists(this.mergeOptions.InputFile))
            throw new FileNotFoundException("Input file does not exist");

        var outputFileInfo = TwitchHelper.ClaimFile(
            this.mergeOptions.OutputFile,
            this.mergeOptions.FileCollisionCallback,
            this._progress
        );
        this.mergeOptions.OutputFile = outputFileInfo.FullName;

        // Open the destination file so that it exists in the filesystem.
        await using var outputFs = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

        try {
            await this.MergeAsyncImpl(outputFs, cancellationToken);
        } catch {
            await Task.Delay(100, CancellationToken.None);

            TwitchHelper.CleanUpClaimedFile(outputFileInfo, outputFs, this._progress);

            throw;
        }
    }

    private async Task MergeAsyncImpl(FileStream outputFs, CancellationToken cancellationToken) {
        var isM3U8 = false;
        var isFirst = true;
        var fileList = new List<string>();
        await using (var fs = File.Open(
                this.mergeOptions.InputFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite
            )) {
            using var sr = new StreamReader(fs);
            while (await sr.ReadLineAsync() is { } line) {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (isFirst && line.StartsWith("#EXTM3U")) isM3U8 = true;

                isFirst = false;

                if (isM3U8 && line.StartsWith('#')) continue;

                fileList.Add(line);
            }
        }

        this._progress.SetTemplateStatus("Verifying Parts {0}% [1/2]", 0);

        await this.VerifyVideoParts(fileList, cancellationToken);

        this._progress.SetTemplateStatus("Combining Parts {0}% [2/2]", 0);

        await this.CombineVideoParts(fileList, outputFs, cancellationToken);

        this._progress.ReportProgress(100);
    }

    private async Task VerifyVideoParts(IReadOnlyCollection<string> fileList, CancellationToken cancellationToken) {
        var failedParts = new List<string>();
        var partCount = fileList.Count;
        var doneCount = 0;

        foreach (var part in fileList) {
            var isValidTs = await VerifyVideoPart(part);
            if (!isValidTs)
                failedParts.Add(part);

            doneCount++;
            var percent = (int)(doneCount / (double)partCount * 100);
            this._progress.ReportProgress(percent);

            cancellationToken.ThrowIfCancellationRequested();
        }

        if (failedParts.Count != 0) {
            if (failedParts.Count == fileList.Count)
                // Every video part returned corrupted, probably a false positive.
                return;

            this._progress.LogInfo(
                $"The following TS files are invalid or corrupted: {string.Join(", ", failedParts)}"
            );
        }
    }

    private static async Task<bool> VerifyVideoPart(string filePath) {
        const int
            TS_PACKET_LENGTH
                = 188; // MPEG TS packets are made of a header and a body: [ 4B ][   184B   ] - https://tsduck.io/download/docs/mpegts-introduction.pdf

        if (!File.Exists(filePath))
            return false;

        await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var fileLength = fs.Length;
        return fileLength != 0 && fileLength % TS_PACKET_LENGTH == 0;

    }

    private async Task CombineVideoParts(
        IReadOnlyCollection<string> fileList,
        FileStream outputStream,
        CancellationToken cancellationToken
    ) {
        var outputDrive = DriveHelper.GetOutputDrive(this.mergeOptions.OutputFile);
        var outputFile = this.mergeOptions.OutputFile;

        var partCount = fileList.Count;
        var doneCount = 0;

        foreach (var partFile in fileList) {
            await DriveHelper.WaitForDrive(outputDrive, this._progress, cancellationToken);

            await using (var fs = File.Open(partFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                await fs.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);

            ++doneCount;
            var percent = (int)(doneCount / (double)partCount * 100);
            this._progress.ReportProgress(percent);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
