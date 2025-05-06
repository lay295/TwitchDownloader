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
using ClipQuality = TwitchDownloaderCore.TwitchObjects.Gql.ClipVideoQuality;

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
                stream => stream.GetResolutionFramerateString(),
                (stream, name) => new M3U8VideoQuality(stream, name)
            );

            return new M3U8VideoQualities(qualities);
        }

        public static IVideoQualities<ClipQuality> FromClip(ClipToken clip)
        {
            if (clip.videoQualities is { Length: > 0 })
            {
                Array.Sort(clip.videoQualities, new ClipQualityComparer());
            }

            var source = clip.videoQualities.FirstOrDefault();
            var qualities = BuildQualityList(
                clip.videoQualities,
                quality => quality.quality,
                (quality, name) => new ClipVideoQuality(quality, name, ReferenceEquals(quality, source))
            );

            return new ClipVideoQualities(qualities);
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