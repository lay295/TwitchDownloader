using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Models.Interfaces;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;
using ClipQuality = TwitchDownloaderCore.TwitchObjects.Gql.ShareClipRenderStatusVideoQuality;

namespace TwitchDownloaderCore.Models
{
    public abstract class VideoQualities<T> : IVideoQualities<T>
    {
        public IReadOnlyList<IVideoQuality<T>> Qualities { get; protected init; }

        protected bool TryGetQuality(string qualityString, out IVideoQuality<T> quality)
        {
            var qualitySpan = qualityString.AsSpan().Trim();
            foreach (var videoQuality in Qualities)
            {
                if (videoQuality.Name.AsSpan().Equals(qualitySpan, StringComparison.OrdinalIgnoreCase))
                {
                    quality = videoQuality;
                    return true;
                }
            }

            if (TryGetKeywordQuality(qualityString, out quality))
            {
                return true;
            }

            if (TryGetRegexQuality(qualityString, out quality))
            {
                return true;
            }

            quality = null;
            return false;
        }

        protected virtual bool TryGetKeywordQuality(string qualityString, out IVideoQuality<T> quality)
        {
            if (string.IsNullOrWhiteSpace(qualityString)
                || qualityString.Contains("best", StringComparison.OrdinalIgnoreCase)
                || qualityString.Contains("source", StringComparison.OrdinalIgnoreCase))
            {
                quality = BestQuality();
                return true;
            }

            if (qualityString.Contains("worst", StringComparison.OrdinalIgnoreCase))
            {
                quality = WorstQuality();
                return true;
            }

            quality = null;
            return false;
        }

        private bool TryGetRegexQuality(string qualityString, out IVideoQuality<T> quality)
        {
            // No point trying to regex match if there's no values to compare to
            if (Qualities.All(x => x.Resolution == default && x.Framerate == 0))
            {
                quality = null;
                return false;
            }

            var qualityStringMatch = VideoQualities.UserQualityStringRegex.Match(qualityString);
            if (!qualityStringMatch.Success)
            {
                quality = null;
                return false;
            }

            var desiredWidth = qualityStringMatch.Groups["Width"];
            var desiredHeight = qualityStringMatch.Groups["Height"];
            var desiredFramerate = qualityStringMatch.Groups["Framerate"];

            var filteredQualities = Qualities
                .WhereOnlyIf(x => x.Resolution.Width == int.Parse(desiredWidth.ValueSpan), desiredWidth.Success)
                .WhereOnlyIf(x => x.Resolution.Height == int.Parse(desiredHeight.ValueSpan), desiredHeight.Success)
                .WhereOnlyIf(x => Math.Abs(x.Framerate - int.Parse(desiredFramerate.ValueSpan)) <= 2, desiredFramerate.Success)
                .ToArray();

            quality = filteredQualities.Length switch
            {
                // We matched
                1 => filteredQualities[0],
                // 2+ matches with the same framerate. Streamer is broadcasting multiple source qualities? Pick first.
                >= 2 when filteredQualities.All(x => x.Framerate == filteredQualities[0].Framerate) => filteredQualities[0],
                // 2+ matches with no framerate specified by the user. Pick first 30fps/last quality
                >= 2 when !desiredFramerate.Success => filteredQualities.FirstOrDefault(x => Math.Abs(x.Framerate - 30) <= 2) ?? filteredQualities.Last(),
                // No match
                _ => null
            };

            return quality != null;
        }

        public abstract IVideoQuality<T> GetQuality(string qualityString);

        public abstract IVideoQuality<T> BestQuality();

        public abstract IVideoQuality<T> WorstQuality();

        public IEnumerator<IVideoQuality<T>> GetEnumerator() => Qualities.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class VideoQualities
    {
        internal static readonly Regex UserQualityStringRegex = new(@"(?:^|\s)(?:(?<Width>\d{3,4})x)?(?<Height>\d{3,4})p?(?<Framerate>\d{1,3})?(?:$|\s)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IVideoQualities<M3U8.Stream> FromM3U8(M3U8 m3u8)
        {
            // Sort input
            m3u8.SortStreamsByQuality();

            // Build quality list
            var qualities = BuildQualityList(
                m3u8.Streams,
                stream => stream.GetResolutionFramerateString(false),
                (stream, name) => new M3U8VideoQuality(stream, name)
            );

            return new M3U8VideoQualities(qualities);
        }

        public static IVideoQualities<ClipQuality> FromClip(ShareClipRenderStatusClip clip)
        {
            const string PORTRAIT_SUFFIX = "Portrait";

            // Sort input
            if (clip.assets is { Length: > 0 })
            {
                foreach (var asset in clip.assets)
                {
                    if (asset.videoQualities is { Length: > 0 })
                    {
                        Array.Sort(asset.videoQualities, new ClipVideoQualityComparer());
                    }
                }
            }

            // Find source quality
            var landscapeAssets = clip.assets.FirstOrDefault(IsLandscape) ?? clip.assets.FirstOrDefault();
            var sourceQuality = landscapeAssets?.videoQualities.MaxBy(x => uint.TryParse(x.quality, out var frameHeight) ? frameHeight : 0);

            // Build quality list
            var qualityCount = clip.assets.Sum(x => x.videoQualities.Length);
            var qualities = new List<IVideoQuality<ClipQuality>>(qualityCount);
            foreach (var asset in clip.assets)
            {
                var aspectRatio = asset.aspectRatio;
                var isPortrait = IsPortrait(asset);

                var assetQualities = BuildQualityList(
                    asset.videoQualities,
                    quality => isPortrait ? $"{quality.quality}p{quality.frameRate:F0}-{PORTRAIT_SUFFIX}" : $"{quality.quality}p{quality.frameRate:F0}",
                    (quality, name) => new ClipVideoQuality(quality, name, BuildClipResolution(quality, aspectRatio), ReferenceEquals(quality, sourceQuality))
                );

                qualities.AddRange(assetQualities);
            }

            // Sort quality list
            var sortedQualities = qualities
                .OrderBy(x => x.Name.Contains(PORTRAIT_SUFFIX))
                .ThenByDescending(x => x.Resolution.Height)
                .ThenByDescending(x => x.Framerate)
                .ThenByDescending(x => x.Name)
                .ToArray();

            return new ClipVideoQualities(sortedQualities);

            static bool IsLandscape(ShareClipRenderStatusAssets asset)
            {
                return asset.type == "SOURCE" || asset.portraitMetadata is null || asset.aspectRatio > 1;
            }

            static bool IsPortrait(ShareClipRenderStatusAssets asset)
            {
                return asset.type == "RECOMPOSED" || asset.portraitMetadata is not null || asset.aspectRatio is < 1 and not 0;
            }

            static Resolution BuildClipResolution(ClipQuality clipQuality, decimal aspectRatio)
            {
                if (!uint.TryParse(clipQuality.quality, out var height))
                {
                    return default;
                }

                if (aspectRatio > 0)
                {
                    var width = (uint)Math.Round(height * aspectRatio);
                    return new Resolution(width, height);
                }

                return new Resolution(height);
            }
        }

        private static List<IVideoQuality<T>> BuildQualityList<T>(IReadOnlyList<T> source, Func<T, string> getQualityName, Func<T, string, IVideoQuality<T>> constructQuality)
        {
            if (source is not { Count: > 0 })
            {
                return new List<IVideoQuality<T>>();
            }

            // Build name count dictionary
            var counts = new Dictionary<string, int>();
            foreach (var quality in source)
            {
                var name = getQualityName(quality);
                CollectionsMarshal.GetValueRefOrAddDefault(counts, name, out _)++;
            }

            // Build quality list
            var qualities = new List<IVideoQuality<T>>(source.Count);
            var duplicates = new HashSet<string>();
            foreach (var quality in source)
            {
                var name = getQualityName(quality);
                ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, name, out var exists);
                Debug.Assert(exists);

                // No duplicate names
                if (count == 1)
                {
                    qualities.Add(constructQuality(quality, name));
                    continue;
                }

                // 1 or more duplicate names
                if (duplicates.Add(name))
                {
                    count = 1;
                }

                qualities.Add(constructQuality(quality, $"{name}-{count}"));
                count++;
            }

            return qualities;
        }
    }
}