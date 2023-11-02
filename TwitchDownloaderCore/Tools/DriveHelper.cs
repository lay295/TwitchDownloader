using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCore.Tools
{
    public static class DriveHelper
    {
        public static DriveInfo GetOutputDrive(string outputPath)
        {
            var outputDrive = DriveInfo.GetDrives()[0];

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (outputPath.StartsWith(drive.Name))
                {
                    // In Linux, the root drive is '/' while mounted drives are located in '/mnt/' or '/run/media/'
                    // So we need to do a length check to not misinterpret a mounted drive as the root drive
                    if (drive.Name.Length < outputDrive.Name.Length)
                    {
                        continue;
                    }

                    outputDrive = drive;
                }
            }

            return outputDrive;
        }

        public static async Task WaitForDrive(DriveInfo drive, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            var driveNotReadyCount = 0;
            while (!drive.IsReady)
            {
                progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Waiting for output drive ({(driveNotReadyCount + 1) / 2f:F1}s)"));
                await Task.Delay(500, cancellationToken);

                if (++driveNotReadyCount >= 20)
                {
                    throw new DriveNotFoundException("The output drive disconnected for 10 or more consecutive seconds.");
                }
            }
        }

        public static void CheckAvailableStorageSpace(VideoDownloadOptions downloadOptions, int bandwidth, TimeSpan videoLength, IProgress<ProgressReport> progress = null)
        {
            var videoSizeInBytes = VideoSizeEstimator.EstimateVideoSize(bandwidth,
                downloadOptions.CropBeginning ? TimeSpan.FromSeconds(downloadOptions.CropBeginningTime) : TimeSpan.Zero,
                downloadOptions.CropEnding ? TimeSpan.FromSeconds(downloadOptions.CropEndingTime) : videoLength);
            var tempFolderDrive = GetOutputDrive(downloadOptions.TempFolder);
            var destinationDrive = GetOutputDrive(downloadOptions.Filename);

            if (tempFolderDrive.Name == destinationDrive.Name)
            {
                if (tempFolderDrive.AvailableFreeSpace < videoSizeInBytes * 2)
                {
                    progress?.Report(new ProgressReport(ReportType.Log, $"The drive '{tempFolderDrive.Name}' may not have enough free space to complete the download."));
                }
            }
            else
            {
                if (tempFolderDrive.AvailableFreeSpace < videoSizeInBytes)
                {
                    // More drive space is needed by the raw ts files due to repeat metadata, but the amount of metadata packets can vary between files so we won't bother.
                    progress?.Report(new ProgressReport(ReportType.Log, $"The drive '{tempFolderDrive.Name}' may not have enough free space to complete the download."));
                }

                if (destinationDrive.AvailableFreeSpace < videoSizeInBytes)
                {
                    progress?.Report(new ProgressReport(ReportType.Log, $"The drive '{destinationDrive.Name}' may not have enough free space to complete finalization."));
                }
            }
        }
    }
}