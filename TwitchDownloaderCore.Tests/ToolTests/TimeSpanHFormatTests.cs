using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests;

// Important notes: When a TimeSpan less than 24 hours in length is passed to TimeSpanHFormat.Format, TimeSpan.ToString is used instead.
// ReSharper disable StringLiteralTypo
public class TimeSpanHFormatTests {
    [Fact]
    public void GetFormatWithICustomFormatterReturnsSelf() {
        var type = typeof(ICustomFormatter);
        var formatter = TimeSpanHFormat.ReusableInstance;

        var result = formatter.GetFormat(type);

        Assert.Same(formatter, result);
    }

    [Fact]
    public void GetFormatWithNotICustomFormatterReturnsNull() {
        var type = typeof(TimeSpanHFormat);
        var formatter = TimeSpanHFormat.ReusableInstance;

        var result = formatter.GetFormat(type);

        Assert.Null(result);
    }

    [Fact]
    public void CustomFormatOverloadMatchesICustomFormatter() {
        var timeSpan = new TimeSpan(17, 49, 12);
        const string FORMAT_STRING = @"HH\:mm\:ss";

        var resultICustomFormatter
            = ((ICustomFormatter)TimeSpanHFormat.ReusableInstance).Format(FORMAT_STRING, timeSpan, null);
        var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(resultICustomFormatter, resultCustom);
    }

    [Fact]
    public void CorrectlyFormatsNonTimeSpanFormattable() {
        const float FLOAT = 3.14159f;
        const string FORMAT_STRING = @"F2";
        const string EXPECTED = @"3.14";

        var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, FLOAT);

        Assert.Equal(EXPECTED, resultCustom);
    }

    [Fact]
    public void CorrectlyFormatsNonTimeSpanNonFormattable() {
        var obj = new object();
        const string FORMAT_STRING = "";
        const string EXPECTED = "System.Object";

        var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, obj);

        Assert.Equal(EXPECTED, resultCustom);
    }

    [Fact]
    public void CorrectlyFormatsNull() {
        object? obj = null;
        const string FORMAT_STRING = "";
        const string EXPECTED = "";

        var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, obj);

        Assert.Equal(EXPECTED, resultCustom);
    }

    [Fact]
    public void ReturnsTimeSpanToString_WhenFormatIsEmpty() {
        var timeSpan = new TimeSpan(17, 49, 12);
        const string FORMAT_STRING = "";
        var expected = timeSpan.ToString();

        var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(expected, resultCustom);
    }

    [Fact]
    public void ReturnsTimeSpanToString_WhenFormatIsNull() {
        var timeSpan = new TimeSpan(17, 49, 12);
        const string FORMAT_STRING = null!;
        var expected = timeSpan.ToString();

        var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(expected, resultCustom);
    }

    [Fact]
    public void MatchesTimeSpanToString_WhenBigHInFormat() {
        var timeSpan = new TimeSpan(17, 49, 12);
        const string FORMAT_STRING = @"HH\:mm\:ss";
        const string EXPECTED = "17:49:12";

        var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void MatchesTimeSpanToString_WhenNoBigHInFormat() {
        var timeSpan = new TimeSpan(17, 49, 12);
        const string FORMAT_STRING = @"hh\:mm\:ss";
        const string EXPECTED = "17:49:12";

        var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void CorrectlyEscapesCharsPrependedByBackslash() {
        var timeSpan = new TimeSpan(25, 37, 43);
        const string FORMAT_STRING = @"HH\:mm\:ss";
        const string EXPECTED = "25:37:43";

        var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void CorrectlyEscapesSingleCharsContainedWithinQuotes() {
        var timeSpan = new TimeSpan(25, 37, 43);
        const string FORMAT_STRING = @"HH'-'mm'-'ss";
        const string EXPECTED = "25-37-43";

        var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void CorrectlyEscapesManyCharsContainedWithinQuotes() {
        var timeSpan = new TimeSpan(25, 37, 43);
        const string FORMAT_STRING = @"'It has been 'HH' hours.'";
        const string EXPECTED = "It has been 25 hours.";

        var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void CorrectlyEscapesEndQuotesPrependedByBackslashes() {
        var timeSpan = new TimeSpan(25, 37, 43);
        const string FORMAT_STRING = @"'I\'ll be back in 'H' Hours.'";
        const string EXPECTED = "I'll be back in 25 Hours.";

        var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void CorrectlyFormatsTrailingBigH() {
        var timeSpan = new TimeSpan(25, 37, 43);
        const string FORMAT_STRING = @"ssmmHH";
        const string EXPECTED = "433725";

        var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void CorrectlyFormatsMoreThan2BigH_WhenHoursUnder24() {
        var timeSpan = new TimeSpan(23, 37, 43);
        const string FORMAT_STRING = @"HHH";
        const string EXPECTED = "023";

        var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void CorrectlyFormatsAbsurdlyLongSequentialBigHFormat() {
        var timeSpan = new TimeSpan(25, 37, 43);
        const string FORMAT_STRING = @"HHHHHHHHHHHHHHHHHHHH";
        const string EXPECTED = "00000000000000000025";

        var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void CorrectlyFormatsAbsurdlyLongFormatString() {
        var timeSpan = new TimeSpan(25, 37, 43);
        const string FORMAT_STRING
            = "'This is a really long format string. You should never put messages in your format string, but this is a unit test "
            + "designed to ensure the unit functions as expected in extreme cases. This class could be exposed to user input someday. "
            + "The format string should be at least 256 characters in length, as that is the size of the stackalloc for appending "
            + "a regular format string as of writing the test but it could change in the future, we never know. SORRY FOR THE TRAFFIC "
            + @"NaM. It is now time to include a big H character to ensure we don\'t fall back on TimeSpan.ToString(). 'H";
        const string EXPECTED
            = "This is a really long format string. You should never put messages in your format string, but this is a unit test "
            + "designed to ensure the unit functions as expected in extreme cases. This class could be exposed to user input someday. "
            + "The format string should be at least 256 characters in length, as that is the size of the stackalloc for appending "
            + "a regular format string as of writing the test but it could change in the future, we never know. SORRY FOR THE TRAFFIC "
            + "NaM. It is now time to include a big H character to ensure we don't fall back on TimeSpan.ToString(). 25";

        var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void ThrowsOnImbalancedQuoteMarkEscaping_WhenHoursUnder24() {
        var timeSpan = new TimeSpan(23, 37, 43);
        const string FORMAT_STRING = "H\" Imbalanced quote escaping.";
        var expectedExceptionType = typeof(FormatException);
        const string EXPECTED_SOURCE_NAME = nameof(TwitchDownloaderCore);

        var exception = Assert.Throws(
            expectedExceptionType,
            () => TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan)
        );

        // Ensure the FormatException originated from TimeSpanHFormat and not TimeSpan.ToString()
        Assert.IsType<FormatException>(exception);
        Assert.Equal(EXPECTED_SOURCE_NAME, exception.Source);
    }

    [Fact]
    public void ThrowsOnImbalancedQuoteMarkEscaping_When24HoursOrMore() {
        var timeSpan = new TimeSpan(24, 37, 43);
        const string FORMAT_STRING = "H\" Imbalanced quote escaping.";
        var expectedExceptionType = typeof(FormatException);
        const string EXPECTED_SOURCE_NAME = nameof(TwitchDownloaderCore);

        var exception = Assert.Throws(
            expectedExceptionType,
            () => TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan)
        );

        // Ensure the FormatException originated from TimeSpanHFormat and not TimeSpan.ToString()
        Assert.IsType<FormatException>(exception);
        Assert.Equal(EXPECTED_SOURCE_NAME, exception.Source);
    }
}
