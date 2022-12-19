﻿namespace TwitchDownloaderCore
{
    public enum ReportType
    {
        Log,
        Percent,
        Status,
        StatusInfo,
        FfmpegLog
    }

    public class ProgressReport
    {
        public ReportType ReportType { get; set; }
        public object Data { get; set; }

        public ProgressReport() { }

        public ProgressReport(int percent)
        {
            ReportType = ReportType.Percent;
            Data = percent;
        }

        public ProgressReport(ReportType reportType, string message)
        {
            ReportType = reportType;
            Data = message;
        }

        ~ProgressReport()
        {
            Data = null;
        }
    }
}