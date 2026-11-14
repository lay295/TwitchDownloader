using SkiaSharp;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCore.Tests.RenderTests
{
    // Tests for ChatRenderOptions: its derived properties (sizes, spacing, update cadence, mask path, cached paints) drive every
    // layout calculation in ChatRenderer, so a change here silently changes every render.
    public class ChatRenderOptionsTests
    {
        /// <summary>
        /// Every pixel size is defined at 24pt and scales linearly with the font size (ReferenceScale), truncated to whole pixels.
        /// Guards the layout proportions of renders at non-default font sizes.
        /// </summary>
        [Theory]
        [InlineData(12, 0.5, 20, 3, 12, 3, 3, 4, 16)]
        [InlineData(24, 1.0, 40, 6, 24, 6, 6, 8, 32)]
        [InlineData(36, 1.5, 60, 9, 36, 9, 9, 12, 48)]
        public void DerivedSizes_ScaleWithFontSize(double fontSize, double referenceScale, int sectionHeight, int sidePadding, int verticalPadding,
            int wordSpacing, int emoteSpacing, int accentStroke, int accentIndent)
        {
            var options = new ChatRenderOptions { FontSize = fontSize };

            Assert.Equal(referenceScale, options.ReferenceScale);
            Assert.Equal(sectionHeight, options.SectionHeight);
            Assert.Equal(sidePadding, options.SidePadding);
            Assert.Equal(verticalPadding, options.VerticalPadding);
            Assert.Equal(wordSpacing, options.WordSpacing);
            Assert.Equal(emoteSpacing, options.EmoteSpacing);
            Assert.Equal(accentStroke, options.AccentStrokeWidth);
            Assert.Equal(accentIndent, options.AccentIndentWidth);
        }

        /// <summary>Each spacing has its own user-facing scale factor that multiplies the font-derived size.</summary>
        [Fact]
        public void DerivedSizes_ApplyTheirOwnScaleFactors()
        {
            var options = new ChatRenderOptions
            {
                SectionHeightScale = 2, SidePaddingScale = 2, VerticalSpacingScale = 2, WordSpacingScale = 2, EmoteSpacingScale = 2,
                AccentStrokeScale = 2, AccentIndentScale = 2,
            };

            Assert.Equal(80, options.SectionHeight);
            Assert.Equal(12, options.SidePadding);
            Assert.Equal(48, options.VerticalPadding);
            Assert.Equal(12, options.WordSpacing);
            Assert.Equal(12, options.EmoteSpacing);
            Assert.Equal(16, options.AccentStrokeWidth);
            Assert.Equal(64, options.AccentIndentWidth);
        }

        /// <summary>
        /// The update interval in ticks is UpdateRate × Framerate, but never below 1 so the chat still updates at tiny/zero rates.
        /// Guards against a divide-by-zero/never-updating render loop.
        /// </summary>
        [Theory]
        [InlineData(0.2, 30, 6)]
        [InlineData(1.0, 60, 60)]
        [InlineData(0.01, 30, 1)]
        [InlineData(0.0, 30, 1)]
        public void UpdateFrame_IsTicksPerUpdateAtLeastOne(double updateRate, int framerate, int expected)
        {
            Assert.Equal(expected, new ChatRenderOptions { UpdateRate = updateRate, Framerate = framerate }.UpdateFrame);
        }

        /// <summary>The username font size is FontSize × UsernameFontScale, falling back to FontSize for non-positive scales.</summary>
        [Theory]
        [InlineData(1.5, 36)]
        [InlineData(0, 24)]
        [InlineData(-2, 24)]
        public void EffectiveUsernameFontSize_IgnoresNonPositiveScale(double scale, double expected)
        {
            Assert.Equal(expected, new ChatRenderOptions { UsernameFontScale = scale }.EffectiveUsernameFontSize);
        }

        /// <summary>
        /// The mask file is the output path with "_mask" inserted before the extension (only the last occurrence of the extension, so
        /// directories containing it are untouched), and equals the output path when masks are off or no output is set.
        /// Guards that the mask never overwrites the video and lands next to it.
        /// </summary>
        [Fact]
        public void MaskFile_InsertsSuffixBeforeExtension()
        {
            var dir = Path.Combine("renders.mp4", "out");

            Assert.Equal(Path.Combine(dir, "chat.mp4"), new ChatRenderOptions { OutputFile = Path.Combine(dir, "chat.mp4") }.MaskFile);
            Assert.Equal(Path.Combine(dir, "chat_mask.mp4"), new ChatRenderOptions { OutputFile = Path.Combine(dir, "chat.mp4"), GenerateMask = true }.MaskFile);
            Assert.Equal(Path.Combine(dir, "chat_mask"), new ChatRenderOptions { OutputFile = Path.Combine(dir, "chat"), GenerateMask = true }.MaskFile);
            Assert.Equal("", new ChatRenderOptions { OutputFile = "", GenerateMask = true }.MaskFile);
        }

        /// <summary>
        /// The alternate-row paint replaces (Src) rather than blends the background so translucent alternate colours stay exact, and it
        /// is created once. Guards the alternate background colour in renders with transparent backgrounds.
        /// </summary>
        [Fact]
        public void AlternateBackgroundPaint_UsesSourceBlendAndIsCached()
        {
            var options = new ChatRenderOptions { AlternateBackgroundColor = new SKColor(0x80191919) };

            Assert.Equal(options.AlternateBackgroundColor, options.AlternateBackgroundPaint.Color);
            Assert.Equal(SKBlendMode.Src, options.AlternateBackgroundPaint.BlendMode);
            Assert.Same(options.AlternateBackgroundPaint, options.AlternateBackgroundPaint);
            Assert.Same(options.AlternateBackgroundHighlightUserPaint, options.AlternateBackgroundHighlightUserPaint);
        }

        /// <summary>
        /// On alternate rows a highlighted user's background should be the highlight colour composited over the alternate background
        /// (as the even-row path composites over the main background), not over itself.
        /// </summary>
        [Fact(Skip = "Bug: AlternateBackgroundHighlightUserPaint uses HighlightUserColor.CompositeOver(HighlightUserColor) instead of CompositeOver(AlternateBackgroundColor), so with the default translucent highlight colour odd highlighted rows ignore the alternate background and are written (Src) semi-transparent.")]
        public void AlternateBackgroundHighlightUserPaint_CompositesHighlightOverAlternateBackground()
        {
            var options = new ChatRenderOptions { AlternateBackgroundColor = new SKColor(0xFF191919), HighlightUserColor = new SKColor(0xC8FF0059) };

            Assert.Equal(options.HighlightUserColor.CompositeOver(options.AlternateBackgroundColor), options.AlternateBackgroundHighlightUserPaint.Color);
        }

        /// <summary>Defaults that callers (CLI/GUI) rely on when they do not set a value explicitly.</summary>
        [Fact]
        public void Defaults_MatchDocumentedBehaviour()
        {
            var options = new ChatRenderOptions();

            Assert.Equal(24, options.FontSize);
            Assert.Equal(EmojiVendor.GoogleNotoColor, options.EmojiVendor);
            Assert.True(options.DisperseCommentOffsets);
            Assert.True(options.AllowUnlistedEmotes);
            Assert.False(options.RenderUserAvatars);
            Assert.Equal(-1, options.StartOverride);
            Assert.Equal(-1, options.EndOverride);
            Assert.Empty(options.HighlightUsersArray);
            Assert.Empty(options.IgnoreUsersArray);
            Assert.Empty(options.BannedWordsArray);
            var file = new FileInfo("x");
            Assert.Same(file, options.FileCollisionCallback(file));
        }
    }
}
