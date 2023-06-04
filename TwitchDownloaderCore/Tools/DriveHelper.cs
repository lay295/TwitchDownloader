using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.Tools
{
    public static class DriveHelper
    {
        public static DriveInfo GetOutputDrive(FfmpegProcess ffmpegProcess)
            => GetOutputDrive(Path.GetFullPath(ffmpegProcess.SavePath));

        public static DriveInfo GetOutputDrive(string outputPath)
        {
            // Cannot instantiate a null DriveInfo
            DriveInfo outputDrive = DriveInfo.GetDrives()[0];

            // Get the name of the drive we are writing to
            foreach (var drive in DriveInfo.GetDrives())
            {
                // If our output path starts with the drive name
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
            if (drive.IsReady)
            {
                return;
            }

            int driveNotReadyCount = 0;
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
    }
}