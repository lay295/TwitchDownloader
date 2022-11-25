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
            reportType = ReportType.Percent;
            data = percent;
        }

        public ProgressReport(ReportType type, string _data)
        {
            reportType = type;
            data = _data;
        }

        public ReportType reportType { get; set; }
        public object data { get; set; }
    }
}