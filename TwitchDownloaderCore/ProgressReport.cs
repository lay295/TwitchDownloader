namespace TwitchDownloaderCore
{
    public enum ReportType
    {
        Log,
        Percent,
        Message
    }

    public class ProgressReport
    {
        public ReportType reportType { get; set; }
        public object data { get; set; }
    }
}