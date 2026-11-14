using System.Buffers;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable InvokeAsExtensionMember
    // Tests ReadOnlySpanExtensions.CopyToExcept, which the chat renderer uses to strip variation selectors / ZWJs from emoji
    // before looking them up in the emoji cache. A wrong count or copy here means emoji silently fail to render.
    public class ReadOnlySpanCopyToExceptTests
    {
        private static readonly SearchValues<char> Excluded = SearchValues.Create("\uFE0F\u200D");

        /// <summary>Excluded chars are removed wherever they occur and the returned count is the number of chars written.</summary>
        [Theory]
        [InlineData("abc", "abc")]
        [InlineData("\uFE0Fabc", "abc")]
        [InlineData("a\uFE0Fb\u200Dc\uFE0F", "abc")]
        [InlineData("\uFE0F\u200D", "")]
        [InlineData("", "")]
        public void CopiesAllButExcludedChars(string str, string expected)
        {
            var destination = new char[str.Length];

            var written = ReadOnlySpanExtensions.CopyToExcept(str, destination, Excluded);

            Assert.Equal(expected, new string(destination, 0, written));
        }

        /// <summary>
        /// When exclusions exist, a destination shorter than the source is rejected up front (the loop indexes by source position),
        /// instead of writing out of bounds part way through.
        /// </summary>
        [Fact]
        public void Throws_WhenDestinationShorterThanSource()
        {
            Assert.Throws<ArgumentException>(() => ReadOnlySpanExtensions.CopyToExcept("a\uFE0Fb", new char[2], Excluded));
        }
    }
}
