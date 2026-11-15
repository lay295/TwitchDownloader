using SkiaSharp;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.TwitchObjectTests
{
    // Tests TwitchEmote, the decoded (possibly animated) image behind every emote and cheermote the chat renderer draws.
    // Its frame timing decides how fast animated emotes play and its resize methods decide how big they are drawn, so
    // mistakes show up directly in rendered videos.
    public class TwitchEmoteTests
    {
        /// <summary>
        /// A static PNG decodes to one frame with no frame timings and exposes the metadata it was constructed with.
        /// The original bytes are kept so the chat downloader can embed them unchanged.
        /// </summary>
        [Fact]
        public void StaticImage_DecodesSingleFrame()
        {
            var png = TestImages.Png(56, 40);
            using var emote = new TwitchEmote(png, null, EmoteProvider.ThirdParty, 2, "id1", "TestEmote", isZeroWidth: true);

            Assert.Equal(1, emote.FrameCount);
            Assert.Empty(emote.EmoteFrameDurations);
            Assert.Equal(0, emote.TotalDuration);
            Assert.Equal(56, emote.Width);
            Assert.Equal(40, emote.Height);
            Assert.Equal(new SKSizeI(56, 40), emote.Info.Size);
            Assert.Single(emote.EmoteFrames);
            Assert.Same(png, emote.ImageData);
            Assert.Equal(("id1", "TestEmote", 2, EmoteProvider.ThirdParty, true), (emote.Id, emote.Name, emote.ImageScale, emote.EmoteProvider, emote.IsZeroWidth));
        }

        /// <summary>
        /// Frame durations are converted from Skia's milliseconds to the renderer's 1/100 s units, and individual 0-delay frames
        /// are bumped to 10 (100 ms) like browsers do, so they are not skipped or played infinitely fast.
        /// </summary>
        [Fact]
        public void AnimatedGif_ConvertsDelaysAndReplacesZeroFrames()
        {
            using var emote = new TwitchEmote(TestImages.Gif("anim_delays_2_0_5.gif"), null, EmoteProvider.FirstParty, 1, "id", "anim");

            Assert.Equal(3, emote.FrameCount);
            Assert.Equal([2, 10, 5], emote.EmoteFrameDurations);
            Assert.Equal(17, emote.TotalDuration);
            Assert.Equal(3, emote.EmoteFrames.Length);
        }

        /// <summary>
        /// GIFs whose frames all have a 0 or 1 (1/100 s) delay are treated as "unspecified" and played at 10 per frame,
        /// matching browser behaviour; otherwise such emotes would flicker at an absurd speed in renders.
        /// </summary>
        [Theory]
        [InlineData("anim_delays_all_0.gif")]
        [InlineData("anim_delays_all_1.gif")]
        public void AnimatedGif_WithUnspecifiedDelays_UsesDefaultSpeed(string gif)
        {
            using var emote = new TwitchEmote(TestImages.Gif(gif), null, EmoteProvider.FirstParty, 1, "id", "anim");

            Assert.Equal([10, 10, 10], emote.EmoteFrameDurations);
            Assert.Equal(30, emote.TotalDuration);
        }

        /// <summary>
        /// Callers that already have an SKCodec (from the image cache) pass it in; it must be used as-is rather than decoding
        /// the bytes a second time.
        /// </summary>
        [Fact]
        public void ProvidedCodec_IsUsed()
        {
            var png = TestImages.Png(10, 10);
            var codec = SKCodec.Create(new MemoryStream(png));
            using var emote = new TwitchEmote(png, codec, EmoteProvider.FirstParty, 1, "id", "name");

            Assert.Same(codec, emote.Codec);
        }

        /// <summary>
        /// Twitch occasionally serves corrupt images. Construction must fail with a message naming the emote so the callers
        /// (which catch and log it) can tell the user which emote was skipped.
        /// </summary>
        [Fact]
        public void CorruptData_ThrowsNamingTheEmote()
        {
            var ex = Assert.Throws<Exception>(() => new TwitchEmote([1, 2, 3, 4], null, EmoteProvider.FirstParty, 1, "badId", "BadEmote"));

            Assert.Contains("BadEmote (badId)", ex.Message);
        }

        /// <summary>
        /// SnapResize scales every frame to the requested height keeping the aspect ratio, and drops the cached SKImage frames
        /// so the next draw uses the resized bitmaps instead of stale full-size ones.
        /// </summary>
        [Fact]
        public void SnapResize_ResizesAllFramesAndRefreshesCachedImages()
        {
            using var emote = new TwitchEmote(TestImages.Gif("anim_delays_2_0_5.gif"), null, EmoteProvider.FirstParty, 1, "id", "anim");
            var before = emote.EmoteFrames;

            emote.SnapResize(12, 0, 0);

            Assert.Equal((16, 12), (emote.Width, emote.Height));
            Assert.NotSame(before, emote.EmoteFrames);
            Assert.All(emote.EmoteFrames, f => Assert.Equal((16, 12), (f.Width, f.Height)));
        }

        /// <summary>
        /// With equal up/down snap thresholds, a target within the threshold of an integer multiple of the source height snaps
        /// to it (55 -> 56 for a 56 px image), so emotes are drawn pixel-perfect instead of blurred by a 98% scale.
        /// </summary>
        [Fact]
        public void SnapResize_SnapsToIntegerScaleWithinThreshold()
        {
            using var emote = new TwitchEmote(TestImages.Png(56, 56), null, EmoteProvider.FirstParty, 2, "id", "name");

            emote.SnapResize(55, 2, 2);

            Assert.Equal(56, emote.Height);
        }

        /// <summary>
        /// Scale(1) must be a no-op (keeping the cached frames) because the renderer calls it for every emote even at 100%;
        /// Scale(0.5) halves the size.
        /// </summary>
        [Fact]
        public void Scale_IsNoOpAtOneAndResizesOtherwise()
        {
            using var emote = new TwitchEmote(TestImages.Png(56, 40), null, EmoteProvider.FirstParty, 2, "id", "name");
            var frames = emote.EmoteFrames;

            emote.Scale(1.0);
            Assert.Same(frames, emote.EmoteFrames);

            emote.Scale(0.5);
            Assert.Equal((28, 20), (emote.Width, emote.Height));
            Assert.NotSame(frames, emote.EmoteFrames);
        }

        /// <summary>SnapScale applies the snapping thresholds to the scaled height (56 * 0.98 = 54 snaps back to 56).</summary>
        [Fact]
        public void SnapScale_SnapsScaledHeight()
        {
            using var emote = new TwitchEmote(TestImages.Png(56, 56), null, EmoteProvider.FirstParty, 2, "id", "name");

            emote.SnapScale(0.98, 2, 2);

            Assert.Equal(56, emote.Height);
        }

        /// <summary>Dispose is idempotent and marks the emote disposed, since emote caches may dispose shared emotes twice.</summary>
        [Fact]
        public void Dispose_IsIdempotent()
        {
            var emote = new TwitchEmote(TestImages.Png(4, 4), null, EmoteProvider.FirstParty, 1, "id", "name");
            _ = emote.EmoteFrames;

            emote.Dispose();
            emote.Dispose();

            Assert.True(emote.Disposed);
        }
    }
}
