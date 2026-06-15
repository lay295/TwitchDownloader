using SkiaSharp;
using System;
using System.Numerics;

namespace TwitchDownloaderCore.Extensions
{
    // ReSharper disable once InconsistentNaming
    public static class SKColorExtensions
    {
        extension(SKColor color)
        {
            public SKColor Lerp(SKColor to, float factor)
            {
                var result = Vector4.Lerp(ToVector4(color), ToVector4(to), factor);
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
            public SKColorF ToSKColorF()
            {
                return new SKColorF((float)color.Red / byte.MaxValue, (float)color.Green / byte.MaxValue, (float)color.Blue / byte.MaxValue, (float)color.Alpha / byte.MaxValue);
            }

            // https://www.w3.org/TR/WCAG21/#dfn-relative-luminance
            public double RelativeLuminance()
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

        extension(SKColorF color)
        {
            // ReSharper disable once InconsistentNaming
            public SKColor ToSKColor()
            {
                return new SKColor((byte)(color.Red * byte.MaxValue), (byte)(color.Green * byte.MaxValue), (byte)(color.Blue * byte.MaxValue), (byte)(color.Alpha * byte.MaxValue));
            }
        }
    }
}

