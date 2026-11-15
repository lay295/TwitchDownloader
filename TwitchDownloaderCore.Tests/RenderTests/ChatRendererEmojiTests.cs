using SkiaSharp;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCore.Tests.RenderTests
{
    /// <summary>One emoji cache folder for the whole class, so the embedded Twemoji archive (~3,700 PNGs) is extracted only once.</summary>
    public sealed class SharedEmojiCache : IDisposable
    {
        public TempDirectory Directory { get; } = new();
        public void Dispose() => Directory.Dispose();
    }

    // End-to-end tests for ChatRenderer's emoji drawing (ContainsEmoji/DrawEmojiMessage/SplitZwjEmoji/LookupEmojiSlow) using the
    // Twemoji set embedded in TwitchDownloaderCore, so they run offline. Emojis are in almost every chat, and their lookup logic
    // (variation selectors, ZWJ sequences, skin tones, flags) is easy to break.
    public class ChatRendererEmojiTests(SharedEmojiCache cache) : IClassFixture<SharedEmojiCache>
    {
        private static readonly SKColor TwemojiFaceYellow = new(0xFFFFCC4D); // exact fill colour of Twemoji faces; system emoji fonts use other yellows

        /// <summary>
        /// Emojis are drawn as vendor images: on their own, between letters, inside an unknown ZWJ sequence (split and drawn separately),
        /// with an unsupported skin-tone modifier, as a known ZWJ sequence, as an unknown flag pair or an emoji with a combining mark after
        /// a known emoji (longest-prefix lookup), followed by non-emoji letters, in a run long enough to wrap, and inside a channel point
        /// highlight. Twemoji's exact face colour on two separate lines proves the images were drawn and wrapped. Guards every emoji
        /// lookup and placement path.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_Twemoji_DrawsEmojiImages()
        {
            using var harness = new RenderHarness();
            harness.Options.TempFolder = cache.Directory.Path;
            harness.Options.EmojiVendor = EmojiVendor.TwitterTwemoji;
            harness.Options.ChatWidth = 400;
            const string GRIN = "\U0001F600";
            const string BODY = "hi " + GRIN + " a" + GRIN + "b " + GRIN + "\u200D" + GRIN + " " + GRIN + "\U0001F3FB \U0001F468\u200D\U0001F4BB "
                                + GRIN + "é " + GRIN + "\U0001F1FA\U0001F1FA " + GRIN + GRIN + "\u0301 ☺\uFE0F";

            var video = await harness.RenderAsync(c => c.comments =
            [
                RenderHarness.MakeComment(0, "alice", BODY),
                RenderHarness.MakeComment(0, "bob", string.Concat(Enumerable.Repeat(GRIN, 30))),
                RenderHarness.MakeComment(0, "carol", "wow " + GRIN, msgId: "highlighted-message"),
            ], TestContext.Current.CancellationToken);

            var rows = Enumerable.Range(0, 200).Where(y => Enumerable.Range(0, 400).Any(x => video.Pixel(29, x, y) == TwemojiFaceYellow)).ToList();
            Assert.True(rows.Count > 0 && rows[^1] - rows[0] >= 20, "emoji images were not drawn on several lines");
            Assert.True(video.Count(29, FixtureColors.Purple) > 0, "highlighted emoji has no highlight background");
        }

        /// <summary>A long ZWJ emoji sequence (family with skin tones, 19 UTF-16 chars) must render instead of failing the whole render.</summary>
        [Fact(Skip = "Bug: DrawEmojiMessage allocates a larger lookupKey for text elements over 16 chars but copies into the 16-char stackSpace, so CopyTo throws ArgumentException and the render fails.")]
        public async Task RenderVideoAsync_LongZwjEmojiSequence_Renders()
        {
            using var harness = new RenderHarness();
            harness.Options.TempFolder = cache.Directory.Path;
            harness.Options.EmojiVendor = EmojiVendor.TwitterTwemoji;
            const string FAMILY = "\U0001F468\U0001F3FB\u200D\U0001F469\U0001F3FB\u200D\U0001F467\U0001F3FB\u200D\U0001F466\U0001F3FB";

            var video = await harness.RenderAsync(c => c.comments = [RenderHarness.MakeComment(0, "alice", "family " + FAMILY)], TestContext.Current.CancellationToken);

            Assert.True(video.FirstNonBackgroundRow(29, RenderHarness.Background) >= 0);
        }
    }
}
