using System;
using SkiaSharp;

namespace TwitchDownloaderCore.Extensions
{
    // ReSharper disable once InconsistentNaming
    public static class SKCanvasExtensions
    {
        public static void DrawText(this SKCanvas canvas, ReadOnlySpan<char> text, float x, float y, SKPaint paint)
        {
            if (paint.TextAlign != SKTextAlign.Left)
            {
                var num = paint.MeasureText(text);
                if (paint.TextAlign == SKTextAlign.Center)
                    num *= 0.5f;
                x -= num;
            }

            using var text1 = SKTextBlob.Create(text, paint.AsFont());
            if (text1 == null)
                return;

            canvas.DrawText(text1, x, y, paint);
        }
    }
}