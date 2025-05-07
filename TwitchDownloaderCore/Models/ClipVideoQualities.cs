using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        private static readonly Regex UserQualityStringRegex = new(@"(?:^|\s)(?<Height>\d{3,4})p?(?<Framerate>\d{1,3})?(?:$|\s)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public override IVideoQuality<ClipQuality> GetQuality(string qualityString)
        {
            if (TryGetQuality(qualityString, out var quality1))
            {
                return quality1;
            }

            var qualityStringMatch = UserQualityStringRegex.Match(qualityString);
            if (!qualityStringMatch.Success)
            {
                return null;
            }

            var desiredHeight = qualityStringMatch.Groups["Height"];
            var desiredFramerate = qualityStringMatch.Groups["Framerate"];

            // TODO: Merge with M3U8VideoQualities logic
            if (qualityString.Contains('p'))
            {
                foreach (var quality in Qualities)
                {
                    var framerate = (int)Math.Round(quality.Item.frameRate);
                    var framerateString = qualityString.EndsWith('p') && framerate == 30
                        ? ""
                        : framerate.ToString("F0");

                    if ($"{quality.Item.quality}p{framerateString}" == qualityString)
                    {
                        return quality;
                    }
                }
            }

            return null;
        }

        public override IVideoQuality<ClipQuality> BestQuality()
        {
            return Qualities.FirstOrDefault();
        }

        public override IVideoQuality<ClipQuality> WorstQuality()
        {
            return Qualities.LastOrDefault();
        }
    }
}