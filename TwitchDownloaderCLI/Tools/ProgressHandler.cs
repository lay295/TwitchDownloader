using System;
using TwitchDownloaderCore;

namespace TwitchDownloaderCLI.Tools
{
    internal class ProgressHandler
    {
        private static string previousMessage = "";
        private static bool previousMessageWasStatusInfo = false;

        internal static void Progress_ProgressChanged(object sender, ProgressReport e)
        {
            switch (e.ReportType)
            {
                case ReportType.Log:
                    ReportLog(e);
                    break;
                case ReportType.Status:
                    ReportStatus(e);
                    break;
                case ReportType.StatusInfo:
                    ReportStatusInfo(e);
                    break;
                case ReportType.FfmpegLog:
                    ReportFfmpegLog(e);
                    break;
            }
        }

        private static void ReportLog(ProgressReport e)
        {
            WasLastMessageStatusInfo();

            string currentStatus = "[LOG] - " + e.Data;
            previousMessage = currentStatus;
            Console.WriteLine(currentStatus);
        }

        private static void ReportStatus(ProgressReport e)
        {
            WasLastMessageStatusInfo();

            string currentStatus = "[STATUS] - " + e.Data;
            if (currentStatus != previousMessage)
            {
                previousMessage = currentStatus;
                Console.WriteLine(currentStatus);
            }
        }

        private static void ReportStatusInfo(ProgressReport e)
        {
            string currentStatus = "\r[STATUS] - " + e.Data;
            if (currentStatus != previousMessage)
            {
                previousMessageWasStatusInfo = true;

                // This ensures the previous message is fully overwritten
                currentStatus = currentStatus.PadRight(previousMessage.Length);

                previousMessage = currentStatus.TrimEnd();
                Console.Write(currentStatus);
            }
        }

        private static void ReportFfmpegLog(ProgressReport e)
        {
            WasLastMessageStatusInfo();

            string currentStatus = "<FFMEPG LOG> " + e.Data;
            previousMessage = currentStatus;
            Console.WriteLine(currentStatus);
        }

        private static void WasLastMessageStatusInfo()
        {
            if (previousMessageWasStatusInfo)
            {
                previousMessageWasStatusInfo = false;
                Console.WriteLine();
            }
        }
    }
}
