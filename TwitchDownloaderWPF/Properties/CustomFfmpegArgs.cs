using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchDownloaderWPF;

namespace TwitchDownloader.Properties
{
    public class CustomFfmpegArgs
    {
        public string CodecName { get; set; }
        public string ContainerName { get; set; }
        public string InputArgs { get; set; }
        public string OutputArgs { get; set; }
    }
}
