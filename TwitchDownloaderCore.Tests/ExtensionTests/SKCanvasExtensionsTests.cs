using SkiaSharp;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable once InconsistentNaming
    // Tests SKCanvasExtensions.DrawText(ReadOnlySpan<char>), the allocation-free text drawing used for every chat message.
    public class SKCanvasExtensionsTests
    {
        private const int WIDTH = 400;
        private const int HEIGHT = 60;

        // Returns the horizontal extent of all non-transparent pixels, or null if nothing was drawn.
        private static (int left, int right)? InkExtent(SKBitmap bitmap)
        {
            int left = int.MaxValue, right = -1;
            for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }

            return right == -1 ? null : (left, right);
        }

        private static SKBitmap Draw(string text, SKTextAlign align, float x)
        {
            var bitmap = new SKBitmap(WIDTH, HEIGHT);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            using var typeface = SKPaintExtensionsTests.LoadInter();
            using var paint = new SKPaint { Typeface = typeface, TextSize = 24, TextAlign = align, Color = SKColors.White };

            canvas.DrawText(text.AsSpan(), x, 40, paint);
            return bitmap;
        }

        /// <summary>
        /// The span overload must position text exactly like SkiaSharp's string overload: starting at x (left), centered on x (center)
        /// or ending at x (right). A sign error in the alignment offset would shift every centered/right-aligned label.
        /// </summary>
        [Theory]
        [InlineData(SKTextAlign.Left)]
        [InlineData(SKTextAlign.Center)]
        [InlineData(SKTextAlign.Right)]
        public void DrawText_HonorsTextAlign(SKTextAlign align)
        {
            const float X = WIDTH / 2f;
            const int TOLERANCE = 4; // Glyph side bearings + anti-aliasing

            using var bitmap = Draw("Hello", align, X);
            var extent = InkExtent(bitmap);

            Assert.NotNull(extent);
            var (left, right) = extent.Value;
            switch (align)
            {
                case SKTextAlign.Left:
                    Assert.InRange(left, X - TOLERANCE, X + TOLERANCE);
                    break;
                case SKTextAlign.Center:
                    Assert.InRange((left + right) / 2f, X - TOLERANCE, X + TOLERANCE);
                    break;
                case SKTextAlign.Right:
                    Assert.InRange(right, X - TOLERANCE, X + TOLERANCE);
                    break;
            }
        }

        /// <summary>Empty text produces no text blob; the method must return quietly instead of passing null to SkiaSharp.</summary>
        [Fact]
        public void DrawText_EmptyText_DrawsNothing()
        {
            using var bitmap = Draw("", SKTextAlign.Center, WIDTH / 2f);

            Assert.Null(InkExtent(bitmap));
        }
    }
}
