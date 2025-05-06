using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Models.Interfaces;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Models
{
    public sealed class M3U8VideoQualities : VideoQualities<M3U8.Stream>
    {
        public M3U8VideoQualities(IReadOnlyList<IVideoQuality<M3U8.Stream>> qualities)
        {
            Qualities = qualities;
        }

        private static readonly Regex UserQualityStringRegex = new(@"(?:^|\s)(?:(?<Width>\d{3,4})x)?(?<Height>\d{3,4})p?(?<Framerate>\d{1,3})?(?:$|\s)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public override IVideoQuality<M3U8.Stream> GetQuality(string qualityString)
        {
            if (TryGetQuality(qualityString, out var quality1))
            {
                return quality1;
            }

            var qualitySpan = qualityString.AsSpan().Trim();
            foreach (var quality2 in Qualities)
            {
                if (qualitySpan.Equals(quality2.Item.StreamInfo.Video, StringComparison.OrdinalIgnoreCase)
                    || qualitySpan.Equals(quality2.Item.MediaInfo.Name, StringComparison.OrdinalIgnoreCase)
                    || qualitySpan.Equals(quality2.Item.MediaInfo.GroupId, StringComparison.OrdinalIgnoreCase))
                {
                    return quality2;
                }
            }

            var qualityStringMatch = UserQualityStringRegex.Match(qualityString);
            if (!qualityStringMatch.Success)
            {
                return null;
            }

            var desiredWidth = qualityStringMatch.Groups["Width"];
            var desiredHeight = qualityStringMatch.Groups["Height"];
            var desiredFramerate = qualityStringMatch.Groups["Framerate"];

            var filteredStreams = Qualities
                .WhereOnlyIf(x => x.Item.StreamInfo.Resolution.Width == int.Parse(desiredWidth.ValueSpan), desiredWidth.Success)
                .WhereOnlyIf(x => x.Item.StreamInfo.Resolution.Height == int.Parse(desiredHeight.ValueSpan), desiredHeight.Success)
                .WhereOnlyIf(x => Math.Abs(x.Item.StreamInfo.Framerate - int.Parse(desiredFramerate.ValueSpan)) <= 2, desiredFramerate.Success)
                .ToArray();

            return filteredStreams.Length switch
            {
                1 => filteredStreams[0],
                2 when filteredStreams[0].Item.StreamInfo.Framerate != 0 && filteredStreams[0].Item.StreamInfo.Framerate == filteredStreams[1].Item.StreamInfo.Framerate => filteredStreams.MaxBy(x => x.Item.StreamInfo.Bandwidth),
                2 when !desiredFramerate.Success => filteredStreams.FirstOrDefault(x => Math.Abs(x.Item.StreamInfo.Framerate - 30) <= 2, filteredStreams.Last()),
                _ => null
            };
        }

        protected override bool TryGetKeywordQuality(string qualityString, out IVideoQuality<M3U8.Stream> quality)
        {
            if (base.TryGetKeywordQuality(qualityString, out quality))
            {
                return true;
            }

            if (qualityString.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                quality = BestQuality();
                return true;
            }

            if (qualityString.Contains("audio", StringComparison.OrdinalIgnoreCase)
                && Qualities.FirstOrDefault(x => x.Item.IsAudioOnly()) is { } audioStream)
            {
                quality = audioStream;
                return true;
            }

            quality = null;
            return false;
        }

        public override IVideoQuality<M3U8.Stream> BestQuality()
        {
            if (Qualities is null)
            {
                return null;
            }

            var source = Qualities.FirstOrDefault(x => x.Item.IsSource());

            source ??= Qualities.MaxBy(x => x.Item.StreamInfo.Resolution.Width * x.Item.StreamInfo.Resolution.Height * x.Item.StreamInfo.Framerate);

            return source;
        }

        public override IVideoQuality<M3U8.Stream> WorstQuality()
        {
            if (Qualities is null)
            {
                return null;
            }

            var worstQuality = Qualities
                .Where(x => !x.Item.IsSource() && !x.Item.IsAudioOnly())
                .MinBy(x => x.Item.StreamInfo.Resolution.Width * x.Item.StreamInfo.Resolution.Height * x.Item.StreamInfo.Framerate);

            worstQuality ??= Qualities.FirstOrDefault();

            return worstQuality;
        }
    }
}