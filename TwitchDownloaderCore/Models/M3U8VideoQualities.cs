using System;
using System.Collections.Generic;
using System.Linq;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Models.Interfaces;

namespace TwitchDownloaderCore.Models
{
    public sealed class M3U8VideoQualities : VideoQualities<M3U8.Stream>
    {
        public M3U8VideoQualities(IReadOnlyList<IVideoQuality<M3U8.Stream>> qualities)
        {
            Qualities = qualities;
        }

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

            return null;
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

            return worstQuality ?? Qualities.FirstOrDefault();
        }
    }
}