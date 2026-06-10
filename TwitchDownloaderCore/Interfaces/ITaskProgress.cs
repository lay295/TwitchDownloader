using System;
using System.Diagnostics.CodeAnalysis;

namespace TwitchDownloaderCore.Interfaces
{
    public interface ITaskProgress : ITaskLogger
    {
        void SetStatus(string status);
        void SetTemplateStatus([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string statusTemplate, int initialPercent);
        void SetTemplateStatus([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string statusTemplate, int initialPercent, TimeSpan initialTime1, TimeSpan initialTime2);
        void ReportProgress(int percent);
        void ReportProgress(int percent, TimeSpan time1, TimeSpan time2);
        /// <summary>Reports progress with an optional render-speed hint (rendered seconds / wall-clock seconds).</summary>
        void ReportProgress(int percent, TimeSpan time1, TimeSpan time2, double speed)
            => ReportProgress(percent, time1, time2);
    }
}