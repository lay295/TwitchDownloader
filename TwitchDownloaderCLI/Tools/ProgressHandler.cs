using System;
using TwitchDownloaderCore;

namespace TwitchDownloaderCLI.Tools
{
    internal static class ProgressHandler
    {
        private static string _previousMessage = "";

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
            var currentStatus = Environment.NewLine + "[LOG] - " + e.Data + Environment.NewLine;
            _previousMessage = currentStatus;
            Console.Write(currentStatus);
        }

        private static void ReportNewLineStatus(ProgressReport e)
        {
            var currentStatus = Environment.NewLine + "[STATUS] - " + e.Data;
            if (currentStatus != _previousMessage)
            {
                _previousMessage = currentStatus;
                Console.Write(currentStatus);
            }
        }

        private static void ReportSameLineStatus(ProgressReport e)
        {
            var currentStatus = "\r[STATUS] - " + e.Data;
            if (currentStatus != _previousMessage)
            {
                // This ensures the previous message is fully overwritten
                currentStatus = currentStatus.PadRight(_previousMessage.Length);

                _previousMessage = currentStatus.TrimEnd();
                Console.Write(currentStatus);
            }
        }

        private static void ReportFfmpegLog(ProgressReport e)
        {
            var currentStatus = Environment.NewLine + "<FFMPEG LOG> " + e.Data;
            _previousMessage = currentStatus;
            Console.Write(currentStatus);
        }
    }
}
