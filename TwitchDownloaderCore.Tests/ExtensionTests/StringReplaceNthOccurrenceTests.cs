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
    }
}