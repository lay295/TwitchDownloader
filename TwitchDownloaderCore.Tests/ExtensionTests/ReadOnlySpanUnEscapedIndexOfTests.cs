using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable InvokeAsExtensionMember
    public class ReadOnlySpanUnEscapedIndexOfTests
    {
        // Without quotes or backslashes the result matches a plain IndexOf.
        [Fact]
        public void FindsNextIndex_WithoutEscapes()
        {
            ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
            const char CHAR_TO_FIND = 'a';
            const int CHAR_INDEX = 19;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // A char that never occurs yields -1, like IndexOf.

        [Fact]
        public void DoesNotFindAnIndex_WhenNotPresent()
        {
            ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
            const char CHAR_TO_FIND = 'L';
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // A backslash-escaped occurrence is skipped and the next unescaped one is returned.

        [Fact]
        public void FindsNextIndex_WithBackslashEscapes()
        {
            ReadOnlySpan<char> str = @"SORRY \FOR TRAFFIC NaM";
            const char CHAR_TO_FIND = 'F';
            const int CHAR_INDEX = 14;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // When every occurrence is escaped the result is -1, not the position of an escaped char.

        [Fact]
        public void DoesNotFindIndex_WithBackslashEscapes()
        {
            ReadOnlySpan<char> str = @"SORRY \FOR TRA\F\FIC NaM";
            const char CHAR_TO_FIND = 'F';
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // Quoted text that does not contain the char is skipped over without affecting the index of a later match.

        [Fact]
        public void FindsNextIndex_WithUnrelatedQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY FOR \"TRAFFIC\" NaM";
            const char CHAR_TO_FIND = 'a';
            const int CHAR_INDEX = 21;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // An occurrence inside quotes is ignored; the first occurrence outside the quotes is returned.

        [Fact]
        public void FindsNextIndex_WithQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" TRAFFIC NaM";
            const char CHAR_TO_FIND = 'F';
            const int CHAR_INDEX = 15;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // When every occurrence sits inside quoted sections the result is -1.

        [Fact]
        public void DoesNotFindAnIndex_WithQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" \"TRAFFIC\" NaM";
            const char CHAR_TO_FIND = 'F';
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
        // Searching for a quote or backslash is ambiguous with the escaping rules, so it is rejected up front.

        [Theory]
        [InlineData('\\')]
        [InlineData('\'')]
        [InlineData('\"')]
        public void Throws_WhenEscapeCharIsPassed(char charToFind)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ReadOnlySpan<char> str = "SO\\R\\RY \'FOR\' \"TRAFFIC\" NaM";
                ReadOnlySpanExtensions.UnEscapedIndexOf(str, charToFind);
            });
        }
        // An unterminated quote swallows the rest of the input, so nothing after it can be found (-1) instead of guessing.

        [Fact]
        public void DoesNotFind_WhenImbalancedQuotes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR TRAFFIC NaM";
            const char CHAR_TO_FIND = 'F';
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }
    }
}