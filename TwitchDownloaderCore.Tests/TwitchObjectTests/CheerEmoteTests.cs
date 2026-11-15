using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.TwitchObjectTests
{
    // Tests CheerEmote, a cheermote prefix (e.g. "Cheer") with one image per bit tier. The renderer asks it which tier image
    // to draw for "Cheer250", so the tier lookup decides whether viewers see the right gem colour.
    public class CheerEmoteTests
    {
        private static CheerEmote Create(params int[] tiers) => new()
        {
            prefix = "Cheer",
            tierList = tiers.Select(t => new KeyValuePair<int, TwitchEmote>(t, new TwitchEmote(TestImages.Png(56, 56), null, EmoteProvider.FirstParty, 2, $"Cheer{t}", $"Cheer{t}"))).ToList()
        };

        /// <summary>
        /// GetTier returns the highest tier whose threshold is &lt;= the amount (Twitch's rule: 1-99 grey, 100-999 purple, ...).
        /// Amounts below the lowest tier fall back to the first tier rather than throwing.
        /// </summary>
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(99, 1)]
        [InlineData(100, 100)]
        [InlineData(999, 100)]
        [InlineData(1000, 1000)]
        [InlineData(250000, 1000)]
        public void GetTier_ReturnsHighestReachedTier(int bits, int expectedTier)
        {
            using var cheer = Create(1, 100, 1000);

            var (tier, emote) = cheer.GetTier(bits);

            Assert.Equal(expectedTier, tier);
            Assert.Equal($"Cheer{expectedTier}", emote.Name);
        }

        /// <summary>Scale, SnapScale and SnapResize must reach every tier image, or some cheer amounts render at the wrong size.</summary>
        [Fact]
        public void Resizing_AppliesToEveryTier()
        {
            using var cheer = Create(1, 100);

            cheer.Scale(0.5);
            Assert.All(cheer.tierList, t => Assert.Equal(28, t.Value.Height));

            cheer.SnapResize(14, 0, 0);
            Assert.All(cheer.tierList, t => Assert.Equal(14, t.Value.Height));

            cheer.SnapScale(0.98, 2, 2);
            Assert.All(cheer.tierList, t => Assert.Equal(56, t.Value.Height)); // 56 * 0.98 snaps back to the source height
        }

        /// <summary>Disposing the cheermote disposes every tier emote (they own native Skia memory) and is idempotent.</summary>
        [Fact]
        public void Dispose_DisposesEveryTier()
        {
            var cheer = Create(1, 100);

            cheer.Dispose();
            cheer.Dispose();

            Assert.True(cheer.Disposed);
            Assert.All(cheer.tierList, t => Assert.True(t.Value.Disposed));
        }
    }
}
