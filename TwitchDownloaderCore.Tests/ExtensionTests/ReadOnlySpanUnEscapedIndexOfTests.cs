using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable InvokeAsExtensionMember
    public class ReadOnlySpanUnEscapedIndexOfTests
    {
        [Fact]
        public void FindsNextIndex_WithoutEscapes()
        {
            ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
            const char CHAR_TO_FIND = 'a';
            const int CHAR_INDEX = 19;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void DoesNotFindAnIndex_WhenNotPresent()
        {
            ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
            const char CHAR_TO_FIND = 'L';
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void FindsNextIndex_WithBackslashEscapes()
        {
            ReadOnlySpan<char> str = @"SORRY \FOR TRAFFIC NaM";
            const char CHAR_TO_FIND = 'F';
            const int CHAR_INDEX = 14;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void DoesNotFindIndex_WithBackslashEscapes()
        {
            ReadOnlySpan<char> str = @"SORRY \FOR TRA\F\FIC NaM";
            const char CHAR_TO_FIND = 'F';
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void FindsNextIndex_WithUnrelatedQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY FOR \"TRAFFIC\" NaM";
            const char CHAR_TO_FIND = 'a';
            const int CHAR_INDEX = 21;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void FindsNextIndex_WithQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" TRAFFIC NaM";
            const char CHAR_TO_FIND = 'F';
            const int CHAR_INDEX = 15;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

        [Fact]
        public void DoesNotFindAnIndex_WithQuoteEscapes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" \"TRAFFIC\" NaM";
            const char CHAR_TO_FIND = 'F';
            const int CHAR_INDEX = -1;

            var actual = ReadOnlySpanExtensions.UnEscapedIndexOf(str, CHAR_TO_FIND);

            Assert.Equal(CHAR_INDEX, actual);
        }

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