using SkiaSharp;
using System.Numerics;

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

        public static SKColor CompositeOver(this SKColor foreground, SKColor background)
        {
            var fa = foreground.Alpha / 255f;
            var ba = background.Alpha / 255f;

            var outA = fa + ba * (1f - fa);
            if (outA <= 0f)
                return new SKColor(0, 0, 0, 0);

            var fr = foreground.Red / 255f;
            var fg = foreground.Green / 255f;
            var fb = foreground.Blue / 255f;

            var br = background.Red / 255f;
            var bg = background.Green / 255f;
            var bb = background.Blue / 255f;

            var outR = (fr * fa + br * ba * (1f - fa)) / outA;
            var outG = (fg * fa + bg * ba * (1f - fa)) / outA;
            var outB = (fb * fa + bb * ba * (1f - fa)) / outA;

            var a = (byte)Math.Round(outA * 255f);
            var r = (byte)Math.Round(outR * 255f);
            var g = (byte)Math.Round(outG * 255f);
            var b = (byte)Math.Round(outB * 255f);

            return new SKColor(r, g, b, a);
        }
    }
}

