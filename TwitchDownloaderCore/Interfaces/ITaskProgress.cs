using System;

namespace TwitchDownloaderCore.Interfaces
{
    // TODO: Add StringSyntaxAttributes when .NET 7+
    public interface ITaskProgress : ITaskLogger
    {
        void SetStatus(string status);
        void SetTemplateStatus(string status, int initialPercent);
        void SetTemplateStatus(string status, int initialPercent, TimeSpan initialTime1, TimeSpan initialTime2);
        void ReportProgress(int percent);
        void ReportProgress(int percent, TimeSpan time1, TimeSpan time2);
    }
}