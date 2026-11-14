using SkiaSharp;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable once InconsistentNaming
    // Tests SKPaintExtensions: AsFont (an UnsafeAccessor into SkiaSharp internals that breaks silently if SkiaSharp renames it)
    // and GetShapedTextPath, which the chat renderer uses to draw outlined/shaped text.
    public class SKPaintExtensionsTests
    {
        // The bundled Inter font keeps glyph output identical on every OS (system default fonts differ or may be missing on CI).
        internal static SKTypeface LoadInter() => SKTypeface.FromStream(new MemoryStream(Properties.Resources.Inter));

        /// <summary>
        /// AsFont must return the paint's live internal font (not a copy): later TextSize/Typeface changes on the paint are visible through it.
        /// Fails if the SkiaSharp internal "GetFont" accessor target changes.
        /// </summary>
        [Fact]
        public void AsFont_ReturnsLiveFontOfPaint()
        {
            using var typeface = LoadInter();
            using var paint = new SKPaint { Typeface = typeface, TextSize = 20 };

            var font = paint.AsFont();
            paint.TextSize = 33;

            Assert.Same(font, paint.AsFont());
            Assert.Equal(33, font.Size);
            Assert.Equal(typeface.FamilyName, font.Typeface.FamilyName);
        }

        /// <summary>Empty or whitespace-only text yields an empty path instead of shaping nothing (or throwing in HarfBuzz).</summary>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void GetShapedTextPath_ReturnsEmptyPath_ForBlankText(string text)
        {
            using var paint = new SKPaint();

            using var path = paint.GetShapedTextPath(text, 0, 0);

            Assert.True(path.IsEmpty);
        }

        /// <summary>
        /// Shaped text must start at x for left alignment, be centered on x for center alignment and end at x for right alignment,
        /// mirroring SKCanvas.DrawText. Guards the xOffset maths for the outlined-text renderer.
        /// </summary>
        [Theory]
        [InlineData(SKTextAlign.Left)]
        [InlineData(SKTextAlign.Center)]
        [InlineData(SKTextAlign.Right)]
        public void GetShapedTextPath_HonorsTextAlign(SKTextAlign align)
        {
            const float X = 200;
            const float Y = 50;
            using var typeface = LoadInter();
            using var paint = new SKPaint { Typeface = typeface, TextSize = 24, TextAlign = align };

            using var path = paint.GetShapedTextPath("Hello World", X, Y); // The space glyph has no outline and must be skipped
            var bounds = path.Bounds;

            Assert.False(path.IsEmpty);
            Assert.InRange(bounds.Bottom, Y - 2, Y + 2); // Baseline at y (no descenders)
            const float TOLERANCE = 4; // Glyph side bearings
            switch (align)
            {
                case SKTextAlign.Left:
                    Assert.InRange(bounds.Left, X - TOLERANCE, X + TOLERANCE);
                    break;
                case SKTextAlign.Center:
                    Assert.InRange(bounds.MidX, X - TOLERANCE, X + TOLERANCE);
                    break;
                case SKTextAlign.Right:
                    Assert.InRange(bounds.Right, X - TOLERANCE, X + TOLERANCE);
                    break;
            }
        }
    }
}
