using System;
using System.Collections.Generic;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore.Tools
{
    public class ClipVideoQualityComparer : IComparer<ShareClipRenderStatusVideoQuality>
    {
        public int Compare(ShareClipRenderStatusVideoQuality x, ShareClipRenderStatusVideoQuality y)
        {
            if (x is null)
            {
                if (y is null) return 0;
                return -1;
            }

            if (y is null) return 1;

            if (int.TryParse(x.quality, out var xQuality) | int.TryParse(y.quality, out var yQuality))
            {
                if (xQuality < yQuality) return 1;
                if (xQuality > yQuality) return -1;

                if (x.frameRate < y.frameRate) return 1;
                if (x.frameRate > y.frameRate) return -1;
                return 0;
            }

            return Math.Clamp(string.Compare(x.quality, y.quality, StringComparison.Ordinal), -1, 1) * -1;
        }
    }
}