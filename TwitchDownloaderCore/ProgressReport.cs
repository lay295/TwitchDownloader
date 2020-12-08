namespace TwitchDownloaderCore
{
    public enum ReportType
    {
        Log,
        Percent,
        Message,
        MessageInfo
    }

    public class ProgressReport
    {
        public ReportType reportType { get; set; }
        public object data { get; set; }
    }
}