using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.VideoPlatforms.Interfaces
{
    public class VideoQuality
    {
        public string Quality { get; set; }
        public double Framerate { get; set; }
        public string SourceUrl { get; set; }
        public int Bandwidth { get; set; }
    }
}
