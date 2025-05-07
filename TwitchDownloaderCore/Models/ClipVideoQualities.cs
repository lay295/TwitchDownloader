using System;
using System.Collections.Generic;
using System.Linq;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Models.Interfaces;
using ClipQuality = TwitchDownloaderCore.TwitchObjects.Gql.ShareClipRenderStatusVideoQuality;

namespace TwitchDownloaderCore.Models
{
    public sealed class ClipVideoQualities : VideoQualities<ClipQuality>, IVideoQualities<ClipQuality>
    {
        public ClipVideoQualities(IReadOnlyList<IVideoQuality<ClipQuality>> qualities)
        {
            Qualities = qualities;
        }

        public override IVideoQuality<ClipQuality> GetQuality(string qualityString)
        {
            if (TryGetQuality(qualityString, out var quality1))
            {
                return quality1;
            }

            foreach (var quality in Qualities)
            {
                var framerate = (int)Math.Round(quality.Framerate);
                var framerateString = qualityString.EndsWith('p') && framerate == 30
                    ? ""
                    : framerate.ToString("F0");

                if ($"{quality.Item.quality}p{framerateString}" == qualityString)
                {
                    return quality;
                }
            }

            return null;
        }

        public override IVideoQuality<ClipQuality> BestQuality()
        {
            if (Qualities is null)
            {
                return null;
            }

            var bestQuality = Qualities.FirstOrDefault(x => x.IsSource);

            bestQuality ??= Qualities
                .WhereOnlyIf(x => x.Resolution.Width > x.Resolution.Height, Qualities.All(x => x.Resolution.HasWidth))
                .MaxBy(x => x.Resolution.Height);

            bestQuality ??= Qualities.MaxBy(x => x.Resolution.Height);

            return bestQuality ?? Qualities.FirstOrDefault();
        }

        public override IVideoQuality<ClipQuality> WorstQuality()
        {
            if (Qualities is null)
            {
                return null;
            }

            var worstQuality = Qualities
                .WhereOnlyIf(x => x.Resolution.Width > x.Resolution.Height, Qualities.All(x => x.Resolution.HasWidth))
                .MinBy(x => x.Resolution.Height);

            worstQuality ??= Qualities.MinBy(x => x.Resolution.Height);

            return worstQuality ?? Qualities.LastOrDefault();
        }
    }
}