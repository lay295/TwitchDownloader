using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests;

public class ReadOnlySpanUnEscapedIndexOfTests {
    [Fact]
    public void CorrectlyFindsNextIndexWithoutEscapes() {
        ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
        const char CHAR_TO_FIND = 'a';
        const int CHAR_INDEX = 19;

        var actual = str.UnEscapedIndexOf(CHAR_TO_FIND);

        Assert.Equal(CHAR_INDEX, actual);
    }

    [Fact]
    public void DoesNotFindAnIndexWhenNotPresent() {
        ReadOnlySpan<char> str = "SORRY FOR TRAFFIC NaM";
        const char CHAR_TO_FIND = 'L';
        const int CHAR_INDEX = -1;

        var actual = str.UnEscapedIndexOf(CHAR_TO_FIND);

        Assert.Equal(CHAR_INDEX, actual);
    }

    [Fact]
    public void CorrectlyFindsNextIndexWithBackslashEscapes() {
        ReadOnlySpan<char> str = @"SORRY \FOR TRAFFIC NaM";
        const char CHAR_TO_FIND = 'F';
        const int CHAR_INDEX = 14;

        var actual = str.UnEscapedIndexOf(CHAR_TO_FIND);

        Assert.Equal(CHAR_INDEX, actual);
    }

    [Fact]
    public void DoesNotFindIndexWithBackslashEscapes() {
        ReadOnlySpan<char> str = @"SORRY \FOR TRA\F\FIC NaM";
        const char CHAR_TO_FIND = 'F';
        const int CHAR_INDEX = -1;

        var actual = str.UnEscapedIndexOf(CHAR_TO_FIND);

        Assert.Equal(CHAR_INDEX, actual);
    }

    [Fact]
    public void CorrectlyFindsNextIndexWithUnrelatedQuoteEscapes() {
        ReadOnlySpan<char> str = "SORRY FOR \"TRAFFIC\" NaM";
        const char CHAR_TO_FIND = 'a';
        const int CHAR_INDEX = 21;

        var actual = str.UnEscapedIndexOf(CHAR_TO_FIND);

        Assert.Equal(CHAR_INDEX, actual);
    }

    [Fact]
    public void CorrectlyFindsNextIndexWithQuoteEscapes() {
        ReadOnlySpan<char> str = "SORRY \"FOR\" TRAFFIC NaM";
        const char CHAR_TO_FIND = 'F';
        const int CHAR_INDEX = 15;

        var actual = str.UnEscapedIndexOf(CHAR_TO_FIND);

        Assert.Equal(CHAR_INDEX, actual);
    }

    [Fact]
    public void DoesNotFindAnIndexWithQuoteEscapes() {
        ReadOnlySpan<char> str = "SORRY \"FOR\" \"TRAFFIC\" NaM";
        const char CHAR_TO_FIND = 'F';
        const int CHAR_INDEX = -1;

        var actual = str.UnEscapedIndexOf(CHAR_TO_FIND);

        Assert.Equal(CHAR_INDEX, actual);
    }

    [Theory]
    [InlineData('\\')]
    [InlineData('\'')]
    [InlineData('\"')]
    public void Throws_WhenEscapeCharIsPassed(char charToFind) {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => {
                ReadOnlySpan<char> str = "SO\\R\\RY \'FOR\' \"TRAFFIC\" NaM";
                str.UnEscapedIndexOf(charToFind);
            }
        );
    }

    [Fact]
    public void Throws_WhenImbalancedQuoteChar() {
        Assert.Throws<FormatException>(
            () => {
                const char CHAR_TO_FIND = 'F';
                ReadOnlySpan<char> str = "SORRY \"FOR TRAFFIC NaM";
                str.UnEscapedIndexOf(CHAR_TO_FIND);
            }
        );
    }
}
