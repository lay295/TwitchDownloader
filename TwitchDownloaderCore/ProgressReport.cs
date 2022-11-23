namespace TwitchDownloaderCore
{
    public enum ReportType
    {
        Log,
        Percent,
        Status,
        StatusInfo
    }

    public class ProgressReport
    {
        public ReportType reportType { get; set; }
        public object data { get; set; }
    }
}