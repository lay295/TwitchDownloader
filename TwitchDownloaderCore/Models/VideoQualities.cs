using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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

        public abstract IVideoQuality<T> GetQuality(string qualityString);

        public abstract IVideoQuality<T> BestQuality();

        public abstract IVideoQuality<T> WorstQuality();

        public IEnumerator<IVideoQuality<T>> GetEnumerator() => Qualities.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class VideoQualities
    {
        public static IVideoQualities<M3U8.Stream> FromM3U8(M3U8 m3u8)
        {
            m3u8.SortStreamsByQuality();

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

            var landscapeAssets = clip.assets.FirstOrDefault(x => x.aspectRatio > 1) ?? clip.assets.FirstOrDefault();
            var sourceQuality = landscapeAssets?.videoQualities.FirstOrDefault();

            var qualityCount = clip.assets.Sum(x => x.videoQualities.Length);
            var qualities = new List<IVideoQuality<ClipQuality>>(qualityCount);
            foreach (var asset in clip.assets)
            {
                var aspectRatio = asset.aspectRatio;

                var assetQualities = BuildQualityList(
                    asset.videoQualities,
                    quality => aspectRatio <= 1 ? $"{quality.quality}p{quality.frameRate:F0}-{PORTRAIT_SUFFIX}" : $"{quality.quality}p{quality.frameRate:F0}",
                    (quality, name) => new ClipVideoQuality(quality, name, BuildClipResolution(quality, aspectRatio), ReferenceEquals(quality, sourceQuality))
                );

                qualities.AddRange(assetQualities);
            }

            var sortedQualities = qualities
                .OrderByDescending(x => x.Name.Contains(PORTRAIT_SUFFIX))
                .ThenBy(x => x.Resolution)
                .ThenBy(x => x.Name)
                .ToArray();

            return new ClipVideoQualities(sortedQualities);

            static Resolution BuildClipResolution(ClipQuality clipQuality, decimal aspectRatio)
            {
                if (!uint.TryParse(clipQuality.quality, out var height))
                {
                    return default;
                }

                if (aspectRatio > 0)
                {
                    var width = (uint)Math.Round(height * aspectRatio);
                    return new Resolution(height, width);
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