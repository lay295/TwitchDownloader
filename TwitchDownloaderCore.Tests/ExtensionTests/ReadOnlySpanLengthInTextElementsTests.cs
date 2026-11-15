using System.Globalization;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable InvokeAsExtensionMember
    // Tests ReadOnlySpanExtensions.LengthInTextElements, which the chat renderer uses to detect emoji/combining sequences
    // (fewer text elements than chars) and route them to the emoji renderer.
    public class ReadOnlySpanLengthInTextElementsTests
    {
        /// <summary>
        /// The count must match StringInfo.LengthInTextElements for ASCII (fast path), surrogate pairs, combining marks,
        /// ZWJ emoji sequences and flags. A mismatch makes the renderer split emoji into broken halves.
        /// </summary>
        [Theory]
        [InlineData("", 0)]
        [InlineData("abc", 3)]
        [InlineData("😀", 1)]
        [InlineData("\u00E9\u0301", 1)] // Non-ASCII base + combining acute accent
        [InlineData("👨\u200D👩\u200D👧", 1)] // ZWJ family
        [InlineData("🇺🇸🇯🇵", 2)] // Two regional-indicator flags
        [InlineData("a😀b", 3)]
        public void CountsGraphemeClusters(string str, int expected)
        {
            var actual = ReadOnlySpanExtensions.LengthInTextElements(str);

            Assert.Equal(expected, actual);
            Assert.Equal(new StringInfo(str).LengthInTextElements, actual);
        }

        /// <summary>
        /// An ASCII base followed by combining marks is a single text element ("e" + U+0301, or the keycap emoji "1" + U+FE0F + U+20E3).
        /// The ASCII fast path counts the base on its own, so the result disagrees with StringInfo (and trips the method's own Debug.Assert).
        /// </summary>
        [Theory(Skip = "Bug: LengthInTextElements' ASCII fast path splits an ASCII char from following combining marks (e.g. \"e\\u0301\", keycap \"1\\uFE0F\\u20E3\")")]
        [InlineData("e\u0301", 1)]
        [InlineData("1\uFE0F\u20E3", 1)]
        public void CountsAsciiBaseWithCombiningMarksAsOneElement(string str, int expected)
        {
            Assert.Equal(expected, ReadOnlySpanExtensions.LengthInTextElements(str));
        }
    }
}
