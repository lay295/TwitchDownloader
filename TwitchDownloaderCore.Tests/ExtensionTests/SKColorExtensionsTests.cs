using SkiaSharp;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable once InconsistentNaming
    // Tests SKColorExtensions, the color math behind the chat renderer's username color visibility adjustment,
    // alternating backgrounds and highlight blending. Errors here show up as wrong or unreadable chat colors.
    public class SKColorExtensionsTests
    {
        /// <summary>Byte -> float -> byte must be lossless for every channel value, or colors drift each time they are converted.</summary>
        [Fact]
        public void ToSKColorF_ToSKColor_RoundTripsEveryByte()
        {
            for (var i = 0; i <= byte.MaxValue; i++)
            {
                var b = (byte)i;
                var color = new SKColor(b, (byte)(255 - b), b, (byte)(255 - b));

                Assert.Equal(color, color.ToSKColorF().ToSKColor());
            }
        }

        /// <summary>ToSKColorF must normalize each channel to [0,1] independently (no channel swaps).</summary>
        [Fact]
        public void ToSKColorF_NormalizesChannels()
        {
            var colorF = new SKColor(255, 0, 51, 102).ToSKColorF();

            Assert.Equal(new SKColorF(1f, 0f, 0.2f, 0.4f), colorF);
        }

        /// <summary>Lerp returns the endpoints at 0 and 1, and blends every channel (including alpha) in between.</summary>
        [Fact]
        public void Lerp_InterpolatesAllChannels()
        {
            var from = new SKColor(0, 0, 0, 0);
            var to = new SKColor(255, 255, 255, 255);

            Assert.Equal(from, from.Lerp(to, 0));
            Assert.Equal(to, from.Lerp(to, 1));

            var mid = from.Lerp(to, 0.5f);
            Assert.All(new[] { mid.Red, mid.Green, mid.Blue, mid.Alpha }, c => Assert.InRange(c, (byte)127, (byte)128));
        }

        /// <summary>
        /// Luminance follows WCAG 2.1: black is 0, white is 1, and the channel weights are 0.2126/0.7152/0.0722.
        /// The dark sample (10/255) is below the 0.04045 threshold and checks the linear branch of the sRGB curve.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0.0)]
        [InlineData(255, 255, 255, 1.0)]
        [InlineData(255, 0, 0, 0.2126)]
        [InlineData(0, 255, 0, 0.7152)]
        [InlineData(0, 0, 255, 0.0722)]
        [InlineData(10, 10, 10, 10 / 255.0 / 12.92)]
        [InlineData(128, 128, 128, 0.2158605)]
        public void RelativeLuminance_MatchesWcagFormula(byte r, byte g, byte b, double expected)
        {
            Assert.Equal(expected, new SKColor(r, g, b).RelativeLuminance(), 5);
        }

        /// <summary>An opaque foreground hides the background completely; a fully transparent one leaves the background untouched.</summary>
        [Fact]
        public void CompositeOver_HandlesOpaqueAndTransparentForeground()
        {
            var background = new SKColor(10, 20, 30, 255);

            Assert.Equal(SKColors.Red, SKColors.Red.CompositeOver(background));
            Assert.Equal(background, SKColors.Transparent.CompositeOver(background));
        }

        /// <summary>Two fully transparent colors give transparent black instead of dividing by a zero output alpha (NaN colors).</summary>
        [Fact]
        public void CompositeOver_BothTransparent_ReturnsTransparentBlack()
        {
            Assert.Equal(new SKColor(0, 0, 0, 0), new SKColor(255, 255, 255, 0).CompositeOver(new SKColor(1, 2, 3, 0)));
        }

        /// <summary>Semi-transparent colors use straight-alpha "over" blending; values below are worked out by hand from the formula.</summary>
        [Theory]
        [InlineData(128, 255, 128, 0, 127, 255)] // ~50% red over opaque blue
        [InlineData(128, 128, 170, 0, 85, 192)] // ~50% red over ~50% blue: alpha accumulates, color is weighted towards the foreground
        public void CompositeOver_BlendsSemiTransparentColors(byte fgAlpha, byte bgAlpha, byte r, byte g, byte b, byte a)
        {
            var foreground = new SKColor(255, 0, 0, fgAlpha);
            var background = new SKColor(0, 0, 255, bgAlpha);

            Assert.Equal(new SKColor(r, g, b, a), foreground.CompositeOver(background));
        }
    }
}
