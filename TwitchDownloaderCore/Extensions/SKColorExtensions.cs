using System;
using System.Numerics;
using SkiaSharp;

namespace TwitchDownloaderCore.Extensions
{
    // ReSharper disable once InconsistentNaming
    public static class SKColorExtensions
    {
        public static SKColor Lerp(this SKColor from, SKColor to, float factor)
        {
            var result = Vector4.Lerp(ToVector4(from), ToVector4(to), factor);
            return FromVector4(result);

            static Vector4 ToVector4(SKColor color)
            {
                var colorF = color.ToSKColorF();
                return new Vector4(colorF.Red, colorF.Green, colorF.Blue, colorF.Alpha);
            }

            static SKColor FromVector4(Vector4 color)
            {
                var colorF = new SKColorF(color.X, color.Y, color.Z, color.W);
                return colorF.ToSKColor();
            }
        }

        // ReSharper disable once InconsistentNaming
        public static SKColorF ToSKColorF(this SKColor color)
        {
            return new SKColorF((float)color.Red / byte.MaxValue, (float)color.Green / byte.MaxValue, (float)color.Blue / byte.MaxValue, (float)color.Alpha / byte.MaxValue);
        }

        // ReSharper disable once InconsistentNaming
        public static SKColor ToSKColor(this SKColorF color)
        {
            return new SKColor((byte)(color.Red * byte.MaxValue), (byte)(color.Green * byte.MaxValue), (byte)(color.Blue * byte.MaxValue), (byte)(color.Alpha * byte.MaxValue));
        }

        // https://www.w3.org/TR/WCAG21/#dfn-relative-luminance
        public static double RelativeLuminance(this SKColor color)
        {
            var colorF = color.ToSKColorF();
            return 0.2126 * ConvertColor(colorF.Red) + 0.7152 * ConvertColor(colorF.Green) + 0.0722 * ConvertColor(colorF.Blue);

            static double ConvertColor(float v)
            {
                return v <= 0.04045
                    ? v / 12.92
                    : Math.Pow((v + 0.055) / 1.055, 2.4);
            }
        }
    }
}

