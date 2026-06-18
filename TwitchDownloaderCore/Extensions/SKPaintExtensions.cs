using System;
using System.Reflection;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace TwitchDownloaderCore.Extensions
{
    // ReSharper disable once InconsistentNaming
    public static class SKPaintExtensions
    {
        private static readonly MethodInfo GetFontMethodInfo = typeof(SKPaint).GetMethod("GetFont", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Func<SKPaint, SKFont> GetFontDelegate = (Func<SKPaint, SKFont>)Delegate.CreateDelegate(typeof(Func<SKPaint, SKFont>), GetFontMethodInfo);

        /// <returns>A reference to the <see cref="SKFont"/> held internally by the <paramref name="paint"/>.</returns>
        /// <remarks>The returned <see cref="SKFont"/> should NOT be disposed of.</remarks>
        public static SKFont AsFont(this SKPaint paint)
        {
            return GetFontDelegate.Invoke(paint);
        }

        // Heavily modified from SkiaSharp.HarfBuzz.CanvasExtensions.DrawShapedText
        public static SKPath GetShapedTextPath(this SKPaint paint, ReadOnlySpan<char> text, float x, float y)
        {
            var returnPath = new SKPath();

            if (text.IsEmpty || text.IsWhiteSpace())
                return returnPath;

            using var shaper = new SKShaper(paint.Typeface);
            using var buffer = new HarfBuzzSharp.Buffer();
            buffer.Add(text, paint.TextEncoding);
            var result = shaper.Shape(buffer, x, y, paint);

            var glyphSpan = result.Codepoints.AsSpan();
            var pointSpan = result.Points.AsSpan();

            var xOffset = 0.0f;
            if (paint.TextAlign != SKTextAlign.Left)
            {
                var width = result.Width;
                if (paint.TextAlign == SKTextAlign.Center)
                    width *= 0.5f;
                xOffset -= width;
            }

            // We cannot dispose because it is a reference, not a clone.
            var font = paint.AsFont();
            for (var i = 0; i < pointSpan.Length; i++)
            {
                using var glyphPath = font.GetGlyphPath((ushort)glyphSpan[i]);
                if (glyphPath.IsEmpty)
                    continue;

                var point = pointSpan[i];
                glyphPath.Transform(new SKMatrix(
                    1, 0, point.X + xOffset,
                    0, 1, point.Y,
                    0, 0, 1
                ));
                returnPath.AddPath(glyphPath);
            }

            return returnPath;
        }
    }
}