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
    }
}