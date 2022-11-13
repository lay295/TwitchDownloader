using System;
using TwitchDownloaderCore;

namespace TwitchDownloaderCLI.Tools
{
    internal class ProgressHandler
    {
        private static string previousStatus = string.Empty;
        private static bool was_last_message_percent = false;

        internal static void Progress_ProgressChanged(object sender, ProgressReport e)
        {
            if (e.reportType == ReportType.Message)
            {
                if (was_last_message_percent)
                {
                    was_last_message_percent = false;
                    Console.WriteLine();
                }
                string currentStatus = "[STATUS] - " + e.data;
                if (currentStatus != previousStatus)
                {
                    previousStatus = currentStatus;
                    Console.WriteLine(currentStatus);
                }
            }
            else if (e.reportType == ReportType.Log)
            {
                if (was_last_message_percent)
                {
                    was_last_message_percent = false;
                    Console.WriteLine();
                }
                Console.WriteLine("[LOG] - " + e.data);
            }
            else if (e.reportType == ReportType.MessageInfo)
            {
                Console.Write("\r[STATUS] - " + e.data);
                was_last_message_percent = true;
            }
        }
    }
}
