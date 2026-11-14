using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    file static class Extensions
    {
        public static Index ToIndex(this int i) => i < 0 ? new Index(~i + 1, true) : new Index(i);
    }

    // ReSharper disable InvokeAsExtensionMember
    public class StringReplaceNthOccurrenceTests
    {
        // Each range (counted from the start or, with ^n, from the end) is replaced while the delimiters and the other ranges are kept,
        // including empty ranges between adjacent delimiters and the real playlist URL case.
        [Theory]
        [InlineData(0, "1/2/3", "A/2/3")]
        [InlineData(1, "1/2/3", "1/A/3")]
        [InlineData(2, "1/2/3", "1/2/A")]
        [InlineData(-1, "1/2/3", "1/2/A")]
        [InlineData(-2, "1/2/3", "1/A/3")]
        [InlineData(-3, "1/2/3", "A/2/3")]
        [InlineData(0, "//", "A//")]
        [InlineData(1, "//", "/A/")]
        [InlineData(2, "//", "//A")]
        [InlineData(-1, "//", "//A")]
        [InlineData(-2, "//", "/A/")]
        [InlineData(-3, "//", "A//")]
        [InlineData(-2, "*.cloudfront.net/abc_123/quality/index-dvr.m3u8", "*.cloudfront.net/abc_123/A/index-dvr.m3u8")]
        public void ReturnsModifiedString_WhenIndexFound(int index, string str, string expected)
        {
            var actual = StringExtensions.ReplaceNthOccurrence(str, '/', index.ToIndex(), "A");

            Assert.Equal(expected, actual);
        }
        // A string without the delimiter is a single range, so replacing range 0 (or ^1) replaces the whole string.

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ReturnsModifiedString_WhenDelimiterNotFound_AndIndexZero(int index)
        {
            const string STRING = "123";
            const string EXPECTED = "A";

            var actual = StringExtensions.ReplaceNthOccurrence(STRING, '/', index.ToIndex(), "A");

            Assert.Equal(EXPECTED, actual);
        }
        // Without a delimiter only range 0 exists; other indexes leave the string unchanged rather than throwing, unlike GetNthOccurrence.

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(-2)]
        [InlineData(-3)]
        [InlineData(-10)]
        public void ReturnsOriginalString_WhenDelimiterNotFound_AndIndexNonZero(int index)
        {
            const string STRING = "123";
            const string EXPECTED = "123";

            var actual = StringExtensions.ReplaceNthOccurrence(STRING, '/', index.ToIndex(), "A");

            Assert.Equal(EXPECTED, actual);
        }
        // An index beyond the available ranges (from either end) returns the input unchanged, so callers need no range check.

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(-4)]
        [InlineData(-5)]
        [InlineData(-10)]
        public void ReturnsOriginalString_WhenIndexNotFound(int index)
        {
            const string STRING = "1/2/3";
            const string EXPECTED = "1/2/3";

            var actual = StringExtensions.ReplaceNthOccurrence(STRING, '/', index.ToIndex(), "A");

            Assert.Equal(EXPECTED, actual);
        }

        // Null and empty strings are returned as-is instead of being replaced by newValue or throwing,
        // so callers can pass optional paths straight through.
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsInput_WhenNullOrEmpty(string? str)
        {
            var actual = StringExtensions.ReplaceNthOccurrence(str!, '/', 0, "A");

            Assert.Equal(str, actual);
        }
    }
}
