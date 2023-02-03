using System;
using TwitchDownloaderCore;

namespace TwitchDownloaderCLI.Tools
{
    internal class ProgressHandler
    {
        private static string previousMessage = "";

        internal static void Progress_ProgressChanged(object sender, ProgressReport e)
        {
            switch (e.ReportType)
            {
                case ReportType.Log:
                    ReportLog(e);
                    break;
                case ReportType.NewLineStatus:
                    ReportNewLineStatus(e);
                    break;
                case ReportType.SameLineStatus:
                    ReportSameLineStatus(e);
                    break;
                case ReportType.FfmpegLog:
                    ReportFfmpegLog(e);
                    break;
            }
        }

        private static void ReportLog(ProgressReport e)
        {
            string currentStatus = Environment.NewLine + "[LOG] - " + e.Data;
            previousMessage = currentStatus;
            Console.Write(currentStatus);
        }

        private static void ReportNewLineStatus(ProgressReport e)
        {
            string currentStatus = Environment.NewLine + "[STATUS] - " + e.Data;
            if (currentStatus != previousMessage)
            {
                previousMessage = currentStatus;
                Console.Write(currentStatus);
            }
        }

        private static void ReportSameLineStatus(ProgressReport e)
        {
            string currentStatus = "\r[STATUS] - " + e.Data;
            if (currentStatus != previousMessage)
            {
                // This ensures the previous message is fully overwritten
                currentStatus = currentStatus.PadRight(previousMessage.Length);

                previousMessage = currentStatus.TrimEnd();
                Console.Write(currentStatus);
            }
        }

        private static void ReportFfmpegLog(ProgressReport e)
        {
            string currentStatus = Environment.NewLine + "<FFMEPG LOG> " + e.Data;
            previousMessage = currentStatus;
            Console.Write(currentStatus);
        }
    }
}
