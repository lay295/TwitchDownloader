using System.Collections.Generic;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tools
{
    public class M3U8StreamQualityComparer : IComparer<M3U8.Stream>
    {
        public int Compare(M3U8.Stream x, M3U8.Stream y)
        {
            if (x?.StreamInfo is null)
            {
                if (y?.StreamInfo is null) return 0;
                return -1;
            }

            if (y?.StreamInfo is null) return 1;

            if (x.IsSource()) return -1;
            if (y.IsSource()) return 1;

            var xResolution = x.StreamInfo.Resolution;
            var yResolution = y.StreamInfo.Resolution;
            var xTotalPixels = xResolution.Width * xResolution.Height;
            var yTotalPixels = yResolution.Width * yResolution.Height;

            if (xTotalPixels < yTotalPixels) return 1;
            if (xTotalPixels > yTotalPixels) return -1;

            var xFramerate = x.StreamInfo.Framerate;
            var yFramerate = y.StreamInfo.Framerate;

            if (xFramerate < yFramerate) return 1;
            if (xFramerate > yFramerate) return -1;

            var xBandwidth = x.StreamInfo.Bandwidth;
            var yBandwidth = y.StreamInfo.Bandwidth;

            if (xBandwidth < yBandwidth) return 1;
            if (xBandwidth > yBandwidth) return -1;

            return 1;
        }
    }
}