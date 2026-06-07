
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    file static class Extensions
    {
        public static Index ToIndex(this int i) => i < 0 ? new Index(~i + 1, true) : new Index(i);
    }

    // ReSharper disable InvokeAsExtensionMember
    public class ReadOnlySpanGetNthOccurrenceTests
    {
        [Theory]
        [InlineData(0, "1/2/3", "1")]
        [InlineData(1, "1/2/3", "2")]
        [InlineData(2, "1/2/3", "3")]
        [InlineData(-1, "1/2/3", "3")]
        [InlineData(-2, "1/2/3", "2")]
        [InlineData(-3, "1/2/3", "1")]
        [InlineData(0, "//", "")]
        [InlineData(1, "//", "")]
        [InlineData(2, "//", "")]
        [InlineData(-1, "//", "")]
        [InlineData(-2, "//", "")]
        [InlineData(-3, "//", "")]
        [InlineData(-2, "*.cloudfront.net/abc_123/quality/index-dvr.m3u8", "quality")]
        [InlineData(-1, "*.cloudfront.net/abc_123/quality/index-dvr.m3u8", "index-dvr.m3u8")]
        [InlineData(-1, "https://github.com/lay295/TwitchDownloader/releases/download/1.56.4/TwitchDownloaderCLI-1.56.4-{0}.zip", "TwitchDownloaderCLI-1.56.4-{0}.zip")]
        public void ReturnsSubstring_WhenIndexFound(int index, string str, string expected)
        {
            var actual = ReadOnlySpanExtensions.GetNthOccurrence(str, '/', index.ToIndex());

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ReturnsOriginalString_WhenDelimiterNotFound_AndIndexZero(int index)
        {
            const string STRING = "123";
            const string EXPECTED = "123";

            var actual = ReadOnlySpanExtensions.GetNthOccurrence(STRING, '/', index.ToIndex());

            Assert.Equal(EXPECTED, actual);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(-2)]
        [InlineData(-3)]
        [InlineData(-10)]
        public void Throws_WhenDelimiterNotFound_AndIndexNonZero(int index)
        {
            const string STRING = "123";

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ReadOnlySpanExtensions.GetNthOccurrence(STRING, '/', index.ToIndex());
            });
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(-4)]
        [InlineData(-5)]
        [InlineData(-10)]
        public void Throws_WhenIndexNotFound(int index)
        {
            const string STRING = "1/2/3";

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ReadOnlySpanExtensions.GetNthOccurrence(STRING, '/', index.ToIndex());
            });
        }

        [Fact]
        public void Throws_WhenIndexFromEndZero()
        {
            const string STRING = "1/2/3";

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ReadOnlySpanExtensions.GetNthOccurrence(STRING, '/', Index.FromEnd(0));
            });
        }
    }
}