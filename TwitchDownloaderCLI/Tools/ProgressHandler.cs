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
            if (e.ReportType == ReportType.Status)
            {
                if (previousMessageWasStatusInfo)
                {
                    previousMessageWasStatusInfo = false;
                    Console.WriteLine();
                }

                string currentStatus = "[STATUS] - " + e.Data;
                if (currentStatus != previousMessage)
                {
                    previousMessage = currentStatus;
                    Console.WriteLine(currentStatus);
                }
            }
            else if (e.ReportType == ReportType.StatusInfo)
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
            else if (e.ReportType == ReportType.Log)
            {
                if (previousMessageWasStatusInfo)
                {
                    previousMessageWasStatusInfo = false;
                    Console.WriteLine();
                }

                string currentStatus = "[LOG] - " + e.Data;
                previousMessage = currentStatus;
                Console.WriteLine(currentStatus);
            }
        }
    }
}
