using System.Diagnostics;
using TwitchDownloaderCore.Interfaces;

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

        /// <summary>
        /// Blocks the thread until the <paramref name="drive"/> is ready.
        /// Mostly only needed by slow USB drives or hard drives getting pegged at 100% by other processes.
        /// </summary>
        public static void WaitForDrive(DriveInfo drive, ITaskLogger logger)
        {
            var waitStart = Stopwatch.GetTimestamp();
            var logMillis = 250d;
            while (!drive.IsReady)
            {
                var millis = Stopwatch.GetElapsedTime(waitStart).TotalMilliseconds;
                if (millis >= logMillis)
                {
                    logger.LogInfo($"Waiting for output drive ({millis:F0}ms)");
                    logMillis += 250;
                }

                Thread.Sleep(10);

                if (millis >= 10_000)
                {
                    throw new DriveNotFoundException("The output drive disconnected for 10 or more consecutive seconds.");
                }
            }
        }
    }
}