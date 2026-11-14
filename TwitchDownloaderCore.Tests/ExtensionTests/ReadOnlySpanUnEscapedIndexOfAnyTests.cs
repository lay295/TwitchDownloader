using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable InvokeAsExtensionMember
    public class ReadOnlySpanUnEscapedIndexOfAnyTests
    {
        // Without quotes or backslashes the result matches a plain IndexOf.
        [Fact]
        public void FindsNextIndex_WhenNoEscapes()
        {
            ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
            const string CHARS_TO_FIND = "abc";
            const int CHAR_INDEX = 19;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // A char that never occurs yields -1, like IndexOf.

        [Fact]
        public void DoesNotFindAnIndex_WhenNotPresent()
        {
            ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
            const string CHARS_TO_FIND = "LP";
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // A backslash-escaped occurrence is skipped and the next unescaped one is returned.

        [Fact]
        public void FindsNextIndex_WithBackslashEscapes()
        {
            ReadOnlySpan<char> str = @"SORRY \FOR TRAFFIC NaM";
            const string CHARS_TO_FIND = "FT";
            const int CHAR_INDEX = 11;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // When every occurrence is escaped the result is -1, not the position of an escaped char.

        [Fact]
        public void DoesNotFindIndex_WhenAllEscapedByBackslash()
        {
            ReadOnlySpan<char> str = @"SORRY \FOR TRA\F\F\IC NaM";
            const string CHARS_TO_FIND = "FI";
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // Quoted text that does not contain the char is skipped over without affecting the index of a later match.

        [Fact]
        public void FindsNextIndex_WithUnrelatedQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY FOR \"TRAFFIC\" NaM";
            const string CHARS_TO_FIND = "abc";
            const int CHAR_INDEX = 21;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // An occurrence inside quotes is ignored; the first occurrence outside the quotes is returned.

        [Fact]
        public void FindsNextIndex_WhenEscapedWithQuotes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" TRAFFIC NaM";
            const string CHARS_TO_FIND = "FM";
            const int CHAR_INDEX = 15;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // When every occurrence sits inside quoted sections the result is -1.

        [Fact]
        public void DoesNotFindAnIndex_WhenEscapedWithQuotes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" \"TRAFFIC\" NaM";
            const string CHARS_TO_FIND = "FA";
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOfAny(str, CHARS_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // Searching for a quote or backslash is ambiguous with the escaping rules, so it is rejected up front.

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
        // An unterminated quote swallows the rest of the input, so nothing after it can be found (-1) instead of guessing.

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