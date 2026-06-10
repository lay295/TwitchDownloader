using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable InvokeAsExtensionMember
    public class ReadOnlySpanUnEscapedIndexOfAnyTests
    {
        [Fact]
        public void FindsNextIndex_WhenNoEscapes()
        {
            ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
            const string CHARS_TO_FIND = "abc";
            const int CHAR_INDEX = 19;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void DoesNotFindAnIndex_WhenNotPresent()
        {
            ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
            const string CHARS_TO_FIND = "LP";
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void FindsNextIndex_WithBackslashEscapes()
        {
            ReadOnlySpan<char> str = @"SORRY \FOR TRAFFIC NaM";
            const string CHARS_TO_FIND = "FT";
            const int CHAR_INDEX = 11;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void DoesNotFindIndex_WhenAllEscapedByBackslash()
        {
            ReadOnlySpan<char> str = @"SORRY \FOR TRA\F\F\IC NaM";
            const string CHARS_TO_FIND = "FI";
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void FindsNextIndex_WithUnrelatedQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY FOR \"TRAFFIC\" NaM";
            const string CHARS_TO_FIND = "abc";
            const int CHAR_INDEX = 21;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void FindsNextIndex_WhenEscapedWithQuotes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" TRAFFIC NaM";
            const string CHARS_TO_FIND = "FM";
            const int CHAR_INDEX = 15;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void DoesNotFindAnIndex_WhenEscapedWithQuotes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" \"TRAFFIC\" NaM";
            const string CHARS_TO_FIND = "FA";
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Theory]
        [InlineData("abc\\")]
        [InlineData("abc\'")]
        [InlineData("abc\"")]
        public void Throws_WhenEscapeCharIsPassed(string charsToFind)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ReadOnlySpan<char> str = "SO\\R\\RY \'FOR\' \"TRAFFIC\" NaM";
                ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, charsToFind);
            });
        }

        [Fact]
        public void DoesNotFind_WhenImbalancedQuotes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR TRAFFIC NaM";
            const string CHARS_TO_FIND = "FT";
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
    }
}