using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloader.Tasks
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
