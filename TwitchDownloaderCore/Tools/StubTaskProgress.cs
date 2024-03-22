using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCore.Tools
{
    public class StubTaskProgress : ITaskProgress
    {
        public static StubTaskProgress Instance = new();

        private StubTaskProgress() { }

        public void LogInfo(string logMessage) { }

        public void LogWarning(string logMessage) { }

        public void LogError(string logMessage) { }

        public void LogFfmpeg(string logMessage) { }

        public void SetStatus(string status, bool isTemplate) { }

        public void ReportProgress(int percent) { }

        public void ReportProgress<T1, T2>(int percent, T1 arg1, T2 arg2) { }
    }
}