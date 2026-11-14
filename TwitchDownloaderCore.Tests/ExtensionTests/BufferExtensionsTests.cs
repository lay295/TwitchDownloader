using SkiaSharp;
using TwitchDownloaderCore.Extensions;
using Buffer = HarfBuzzSharp.Buffer;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // Tests BufferExtensions.Add, which feeds chat text into HarfBuzz before shaping. If the text is encoded wrong
    // (or pooled-array garbage leaks in), the chat renderer draws the wrong glyphs for every message.
    public class BufferExtensionsTests
    {
        // "a", "é" (2 UTF-8 bytes) and "😀" (surrogate pair, 4 UTF-8 bytes) exercise every encoding's multi-unit paths.
        private const string TEXT = "aé😀";
        private static readonly uint[] ExpectedCodepoints = [0x61, 0xE9, 0x1F600];

        /// <summary>
        /// Every supported encoding must add exactly the text's Unicode codepoints. The UTF-8/UTF-32 paths rent oversized
        /// arrays from ArrayPool; this guards against passing the whole rented array (trailing garbage) instead of the encoded slice.
        /// </summary>
        [Theory]
        [InlineData(SKTextEncoding.Utf8)]
        [InlineData(SKTextEncoding.Utf16)]
        [InlineData(SKTextEncoding.Utf32)]
        public void AddsExactCodepointsForEachEncoding(SKTextEncoding encoding)
        {
            using var buffer = new Buffer();

            buffer.Add(TEXT, encoding);

            Assert.Equal(ExpectedCodepoints, buffer.GlyphInfos.Select(x => x.Codepoint));
        }

        /// <summary>
        /// Add must call GuessSegmentProperties so right-to-left scripts are shaped right-to-left;
        /// without it Hebrew/Arabic chat messages would render with reversed glyph order.
        /// </summary>
        [Theory]
        [InlineData("hello", HarfBuzzSharp.Direction.LeftToRight)]
        [InlineData("שלום", HarfBuzzSharp.Direction.RightToLeft)]
        public void GuessesSegmentDirection(string text, HarfBuzzSharp.Direction expectedDirection)
        {
            using var buffer = new Buffer();

            buffer.Add(text, SKTextEncoding.Utf16);

            Assert.Equal(expectedDirection, buffer.Direction);
        }

        /// <summary>GlyphId text is not text at all, so it must be rejected loudly rather than silently mis-shaped.</summary>
        [Fact]
        public void ThrowsForGlyphIdEncoding()
        {
            using var buffer = new Buffer();

            Assert.Throws<NotSupportedException>(() => buffer.Add(TEXT, SKTextEncoding.GlyphId));
        }
    }
}
