using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests M3U8StreamQualityComparer, which sorts VOD/live master playlists best-first before quality names are built.
    // The order decides which stream is shown first and which duplicates get "-1"/"-2" suffixes.
    public class M3U8StreamQualityComparerTests
    {
        private static M3U8.Stream S(string id, string[] groups, uint w, uint h, decimal fps, int bandwidth) =>
            new(new M3U8.Stream.ExtStreamInfo(0, bandwidth, [], (w, h), fps, id, id, groups, ""), $"https://x.net/a/{id}/index.m3u8");

        /// <summary>
        /// Landscape streams come before portrait ones and audio-only goes last; within an orientation, more pixels win, then
        /// higher framerate, then higher bandwidth.
        /// </summary>
        [Fact]
        public void SortsLandscapeThenPortraitThenAudio_BestFirst()
        {
            var streams = new[]
            {
                S("audio_only", ["landscape", "portrait"], 0, 0, 0, 200),
                S("720p30-portrait", ["portrait"], 720, 1280, 30, 2000),
                S("720p30", ["landscape"], 1280, 720, 30, 2000),
                S("1080p60-portrait", ["portrait"], 1080, 1920, 60, 6000),
                S("720p60-low", ["landscape"], 1280, 720, 60, 3000),
                S("1080p60", ["landscape"], 1920, 1080, 60, 6000),
                S("720p60-high", ["landscape"], 1280, 720, 60, 3500),
            };

            Array.Sort(streams, new M3U8StreamQualityComparer());

            Assert.Equal(["1080p60", "720p60-high", "720p60-low", "720p30", "1080p60-portrait", "720p30-portrait", "audio_only"],
                streams.Select(x => x.StreamInfo.StableVariantId));
        }

        /// <summary>
        /// Streams without stream info (media playlists) sort first and compare equal to each other. Identical qualities compare as 1
        /// rather than 0 (the comparer never reports a tie).
        /// </summary>
        [Fact]
        public void HandlesMissingStreamInfoAndTies()
        {
            var comparer = new M3U8StreamQualityComparer();
            var noInfo = new M3U8.Stream(new M3U8.Stream.ExtPartInfo(2, false), default, default, "0.ts");
            var a = S("a", ["landscape"], 1280, 720, 30, 1000);

            Assert.Equal(0, comparer.Compare(null, noInfo));
            Assert.Equal(-1, comparer.Compare(noInfo, a));
            Assert.Equal(1, comparer.Compare(a, null));
            Assert.Equal(1, comparer.Compare(a, a with { }));
        }
    }
}
