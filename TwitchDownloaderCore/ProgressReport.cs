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

        public ReportType ReportType { get; set; }
        public object Data { get; set; }
    }
}