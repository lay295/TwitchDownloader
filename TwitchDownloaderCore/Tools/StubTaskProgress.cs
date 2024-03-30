using System;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCore.Tools
{
    public class StubTaskProgress : ITaskProgress
    {
        public static readonly StubTaskProgress Instance = new();

        private StubTaskProgress() { }

        public void LogVerbose(string logMessage) { }

        public void LogInfo(string logMessage) { }

        public void LogWarning(string logMessage) { }

        public void LogError(string logMessage) { }

        public void LogFfmpeg(string logMessage) { }

        public void SetStatus(string status) { }

        public void SetTemplateStatus(string status, int initialPercent) { }

        public void SetTemplateStatus(string status, int initialPercent, TimeSpan initialTime1, TimeSpan initialTime2) { }

        public void ReportProgress(int percent) { }

        public void ReportProgress(int percent, TimeSpan time1, TimeSpan time2) { }
    }
}