using System;

namespace TwitchDownloaderCore.Interfaces
{
    public interface ITaskProgress : ITaskLogger
    {
        // TODO: Add StringSyntaxAttribute when .NET 7+
        void SetStatus(string status, bool isTemplate);
        void ReportProgress(int percent);
        void ReportProgress(int percent, TimeSpan time1, TimeSpan time2);
    }
}