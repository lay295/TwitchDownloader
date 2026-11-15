using SkiaSharp;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.TwitchObjectTests
{
    // Tests ChatBadge, the decoded set of images for one Twitch badge (all its versions) used by the chat renderer.
    // Its Type drives the renderer's badge filtering options (e.g. hide subscriber/prime badges), and its resize methods
    // decide how badges line up with the text, so both must stay correct.
    public class ChatBadgeTests
    {
        private static Dictionary<string, ChatBadgeData> Versions(params string[] names)
            => names.ToDictionary(n => n, _ => new ChatBadgeData { bytes = TestImages.Png(36, 36), title = "t" });

        /// <summary>
        /// Badge names map to the ChatBadgeType flags the user can filter on; "no_video" and "no_audio" share one flag and
        /// anything unknown is "Other". A wrong mapping would hide or show the wrong badges in renders.
        /// </summary>
        [Theory]
        [InlineData("broadcaster", ChatBadgeType.Broadcaster)]
        [InlineData("moderator", ChatBadgeType.Moderator)]
        [InlineData("vip", ChatBadgeType.VIP)]
        [InlineData("subscriber", ChatBadgeType.Subscriber)]
        [InlineData("predictions", ChatBadgeType.Predictions)]
        [InlineData("no_video", ChatBadgeType.NoAudioVisual)]
        [InlineData("no_audio", ChatBadgeType.NoAudioVisual)]
        [InlineData("premium", ChatBadgeType.PrimeGaming)]
        [InlineData("bits", ChatBadgeType.Other)]
        public void Type_IsDerivedFromName(string name, ChatBadgeType expected)
        {
            using var badge = new ChatBadge(name, Versions("1"));

            Assert.Equal(expected, badge.Type);
        }

        /// <summary>
        /// Every version is decoded to an image of the source size, and VersionsData keeps the original data so the chat
        /// downloader/updater can re-embed it without re-downloading.
        /// </summary>
        [Fact]
        public void Constructor_DecodesEveryVersion()
        {
            var data = Versions("0", "3", "6");
            using var badge = new ChatBadge("subscriber", data);

            Assert.Equal("subscriber", badge.Name);
            Assert.Same(data, badge.VersionsData);
            Assert.Equal(["0", "3", "6"], badge.Versions.Keys.Order());
            Assert.All(badge.Versions.Values, img => Assert.Equal((36, 36), (img.Width, img.Height)));
        }

        /// <summary>When a pre-created SKCodec is supplied (image cache path) it is decoded instead of the raw bytes.</summary>
        [Fact]
        public void Constructor_UsesProvidedCodec()
        {
            var codec = SKCodec.Create(new MemoryStream(TestImages.Png(20, 18)));
            using var badge = new ChatBadge("vip", new Dictionary<string, ChatBadgeData> { ["1"] = new() { Codec = codec } });

            Assert.Equal((20, 18), (badge.Versions["1"].Width, badge.Versions["1"].Height));
        }

        /// <summary>
        /// Twitch sometimes serves corrupt badge images. The constructor must throw a message naming the badge and version so
        /// TwitchHelper can log which badge was skipped instead of crashing the render.
        /// </summary>
        [Fact]
        public void Constructor_CorruptImage_ThrowsNamingTheBadge()
        {
            var ex = Assert.Throws<Exception>(() => new ChatBadge("bits", new Dictionary<string, ChatBadgeData> { ["100"] = new() { bytes = [1, 2, 3] } }));

            Assert.Contains("100 (bits)", ex.Message);
        }

        /// <summary>
        /// SnapResize scales every version to the target height and discards the cached SKImages, so later draws use the
        /// resized bitmaps rather than stale full-size images.
        /// </summary>
        [Fact]
        public void SnapResize_ResizesAllVersions()
        {
            using var badge = new ChatBadge("subscriber", Versions("0", "3"));
            var before = badge.Versions;

            badge.SnapResize(18, 0, 0);

            Assert.NotSame(before, badge.Versions);
            Assert.All(badge.Versions.Values, img => Assert.Equal((18, 18), (img.Width, img.Height)));
        }

        /// <summary>Scale(1) is a no-op that keeps cached images; any other factor resizes every version.</summary>
        [Fact]
        public void Scale_IsNoOpAtOneAndResizesOtherwise()
        {
            using var badge = new ChatBadge("subscriber", Versions("0"));
            var before = badge.Versions;

            badge.Scale(1.0);
            Assert.Same(before, badge.Versions);

            badge.Scale(0.5);
            Assert.Equal(18, badge.Versions["0"].Height);
        }

        /// <summary>SnapScale snaps a near-integer scale back to the source size (36 * 0.98 = 35 -> 36) to keep badges crisp.</summary>
        [Fact]
        public void SnapScale_SnapsToIntegerScale()
        {
            using var badge = new ChatBadge("subscriber", Versions("0"));

            badge.SnapScale(0.98, 2, 2);

            Assert.Equal(36, badge.Versions["0"].Height);
        }

        /// <summary>Dispose is idempotent (badge dictionaries can be disposed from several owners) and marks the badge disposed.</summary>
        [Fact]
        public void Dispose_IsIdempotent()
        {
            var badge = new ChatBadge("vip", new Dictionary<string, ChatBadgeData>
            {
                ["1"] = new() { Codec = SKCodec.Create(new MemoryStream(TestImages.Png(4, 4))) }
            });
            _ = badge.Versions;

            badge.Dispose();
            badge.Dispose();

            Assert.True(badge.Disposed);
        }
    }
}
