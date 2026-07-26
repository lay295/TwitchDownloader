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
            if (TryGetQuality(qualityString, out var foundQuality))
            {
                return foundQuality;
            }

            var qualitySpan = qualityString.AsSpan().Trim();
            foreach (var quality in Qualities)
            {
                if (qualitySpan.Equals(quality.Item.StreamInfo.StableVariantId, StringComparison.OrdinalIgnoreCase)
                    || qualitySpan.Equals(quality.Item.StreamInfo.IvsName, StringComparison.OrdinalIgnoreCase)
                    || (quality.Path.Count('/') >= 1 && qualitySpan.Equals(quality.Path.GetNthOccurrence('/', ^2), StringComparison.OrdinalIgnoreCase)))
                {
                    return quality;
                }
            }

            return null;
        }

        protected override bool TryGetKeywordQuality(string qualityString, out IVideoQuality<M3U8.Stream> quality)
        {
            if (string.IsNullOrWhiteSpace(qualityString))
            {
                quality = BestQuality();
                return true;
            }

            if (qualityString.Contains("best", StringComparison.OrdinalIgnoreCase)
                || qualityString.Contains("source", StringComparison.OrdinalIgnoreCase)
                || qualityString.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                quality = qualityString.Contains("portrait", StringComparison.OrdinalIgnoreCase)
                    ? BestPortraitQuality()
                    : BestQuality();

                return true;
            }

            if (qualityString.Contains("worst", StringComparison.OrdinalIgnoreCase))
            {
                quality = qualityString.Contains("portrait", StringComparison.OrdinalIgnoreCase)
                    ? WorstPortraitQuality()
                    : WorstQuality();

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

            var source = Qualities
                .Where(x => x.Orientation is VideoOrientation.Landscape)
                .MaxBy(x => x.Resolution.Width * x.Resolution.Height * x.Framerate);

            source ??= Qualities
                .Where(x => x.Orientation is VideoOrientation.Landscape)
                .MaxBy(x => x.BitRate);

            return source;
        }

        private IVideoQuality<M3U8.Stream> BestPortraitQuality()
        {
            if (Qualities is null)
            {
                return null;
            }

            var bestQuality = Qualities
                .WhereOnlyIf(x => x.Resolution.Width < x.Resolution.Height, Qualities.All(x => x.Resolution.HasWidth))
                .Where(x => x.Orientation is VideoOrientation.Portrait)
                .MaxBy(x => x.Resolution.Height);

            bestQuality ??= Qualities
                .Where(x => x.Orientation is VideoOrientation.Portrait)
                .MaxBy(x => x.Resolution.Height);

            return bestQuality ?? Qualities.FirstOrDefault();
        }

        public override IVideoQuality<M3U8.Stream> WorstQuality()
        {
            if (Qualities is null)
            {
                return null;
            }

            var worstQuality = Qualities
                .Where(x => !x.Item.IsAudioOnly())
                .Where(x => x.Orientation is VideoOrientation.Landscape)
                .MinBy(x => x.Resolution.Width * x.Resolution.Height * x.Framerate);

            worstQuality ??= Qualities
                .Where(x => !x.Item.IsAudioOnly())
                .Where(x => x.Orientation is VideoOrientation.Landscape)
                .MinBy(x => x.BitRate);

            return worstQuality ?? Qualities.LastOrDefault();
        }

        private IVideoQuality<M3U8.Stream> WorstPortraitQuality()
        {
            if (Qualities is null)
            {
                return null;
            }

            var worstQuality = Qualities
                .WhereOnlyIf(x => x.Resolution.Width < x.Resolution.Height, Qualities.All(x => x.Resolution.HasWidth))
                .Where(x => x.Orientation is VideoOrientation.Portrait)
                .MinBy(x => x.Resolution.Height);

            worstQuality ??= Qualities
                .Where(x => x.Orientation is VideoOrientation.Portrait)
                .MinBy(x => x.Resolution.Height);

            return worstQuality ?? Qualities.LastOrDefault();
        }
    }
}