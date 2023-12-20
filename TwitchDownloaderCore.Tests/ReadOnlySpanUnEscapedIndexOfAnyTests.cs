using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests
{
    public class ReadOnlySpanUnEscapedIndexOfAnyTests
    {
        [Fact]
        public void CorrectlyFindsNextIndexWithoutEscapes()
        {
            ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
            const string CHARS_TO_FIND = "abc";
            const int CHAR_INDEX = 19;

            var actual = str.UnEscapedIndexOfAny(CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void DoesNotFindAnIndexWhenNotPresent()
        {
            ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
            const string CHARS_TO_FIND = "LP";
            const int CHAR_INDEX = -1;

            var actual = str.UnEscapedIndexOfAny(CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void CorrectlyFindsNextIndexWithBackslashEscapes()
        {
            ReadOnlySpan<char> str = @"SORRY \FOR TRAFFIC NaM";
            const string CHARS_TO_FIND = "FT";
            const int CHAR_INDEX = 11;

            var actual = str.UnEscapedIndexOfAny(CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void DoesNotFindIndexWithBackslashEscapes()
        {
            ReadOnlySpan<char> str = @"SORRY \FOR TRA\F\F\IC NaM";
            const string CHARS_TO_FIND = "FI";
            const int CHAR_INDEX = -1;

            var actual = str.UnEscapedIndexOfAny(CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void CorrectlyFindsNextIndexWithUnrelatedQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY FOR \"TRAFFIC\" NaM";
            const string CHARS_TO_FIND = "abc";
            const int CHAR_INDEX = 21;

            var actual = str.UnEscapedIndexOfAny(CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void CorrectlyFindsNextIndexWithQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" TRAFFIC NaM";
            const string CHARS_TO_FIND = "FM";
            const int CHAR_INDEX = 15;

            var actual = str.UnEscapedIndexOfAny(CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void DoesNotFindAnIndexWithQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" \"TRAFFIC\" NaM";
            const string CHARS_TO_FIND = "FA";
            const int CHAR_INDEX = -1;

            var actual = str.UnEscapedIndexOfAny(CHARS_TO_FIND);

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
                str.UnEscapedIndexOfAny(charsToFind);
            });
        }

        [Fact]
        public void Throws_WhenImbalancedQuoteChar()
        {
            Assert.Throws<FormatException>(() =>
            {
                const string CHARS_TO_FIND = "FT";
                ReadOnlySpan<char> str = "SORRY \"FOR TRAFFIC NaM";
                str.UnEscapedIndexOfAny(CHARS_TO_FIND);
            });
        }
    }
}