using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Models.Interfaces;

namespace TwitchDownloaderCore.Models
{
    public sealed class StreamVideoQualities : VideoQualities<StreamQuality>, IVideoQualities<StreamQuality>
    {
        public StreamVideoQualities(IReadOnlyList<IVideoQuality<StreamQuality>> qualities)
        {
            Qualities = qualities;
        }

        public override IVideoQuality<StreamQuality> BestQuality()
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

        public override IVideoQuality<StreamQuality> GetQuality(string qualityString)
        {
            if (TryGetQuality(qualityString, out var foundQuality))
            {
                return foundQuality;
            }

            foreach (var quality in Qualities)
            {
                var framerate = (int)Math.Round(quality.Framerate);
                var framerateString = qualityString!.EndsWith('p') && framerate == 30
                    ? ""
                    : framerate.ToString("F0");

                if ($"{quality.Item.Resolution.Height}p{framerateString}" == qualityString)
                {
                    return quality;
                }
            }

            return null;
        }

        public override IVideoQuality<StreamQuality> WorstQuality()
        {
            if (Qualities is null)
            {
                return null;
            }

            var worstQuality = Qualities
                .Where(x => !x.Item.IsAudio)
                .WhereOnlyIf(x => x.Resolution.Width > x.Resolution.Height, Qualities.All(x => x.Resolution.HasWidth))
                .MinBy(x => x.Resolution.Height);

            worstQuality ??= Qualities.Where(x => !x.Item.IsAudio).MinBy(x => x.Resolution.Height);

            return worstQuality ?? Qualities.LastOrDefault(x => !x.Item.IsAudio);
        }

        protected override bool TryGetKeywordQuality(string qualityString, out IVideoQuality<StreamQuality> quality)
        {
            if (string.IsNullOrWhiteSpace(qualityString))
            {
                quality = BestQuality();
                return true;
            }

            // TODO: support portrait streams
            if (qualityString.Contains("best", StringComparison.OrdinalIgnoreCase)
                || qualityString.Contains("source", StringComparison.OrdinalIgnoreCase)
                || qualityString.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                quality = BestQuality();
                return true;
            }

            if (qualityString.Contains("worst", StringComparison.OrdinalIgnoreCase))
            {
                quality = WorstQuality();
                return true;
            }

            if (qualityString.Contains("audio", StringComparison.OrdinalIgnoreCase)
                && Qualities.FirstOrDefault(x => x.Item.IsAudio) is { } audioStream)
            {
                quality = audioStream;
                return true;
            }

            quality = null;
            return false;
        }
    }
}
