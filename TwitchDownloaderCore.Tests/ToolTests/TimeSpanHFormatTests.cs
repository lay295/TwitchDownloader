using System.Globalization;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Important notes: When a TimeSpan less than 24 hours in length is passed to TimeSpanHFormat.Format, TimeSpan.ToString is used instead.
    // ReSharper disable StringLiteralTypo
    public class TimeSpanHFormatTests
    {
        // As an IFormatProvider it must hand itself out for ICustomFormatter so string.Format/StringBuilder route through it.
        [Fact]
        public void GetFormatWithICustomFormatterReturnsSelf()
        {
            var type = typeof(ICustomFormatter);
            var formatter = TimeSpanHFormat.ReusableInstance;

            var result = formatter.GetFormat(type);

            Assert.Same(formatter, result);
        }

        // Requests for any other format type return null, deferring to the default provider.
        [Fact]
        public void GetFormatWithNotICustomFormatterReturnsNull()
        {
            var type = typeof(TimeSpanHFormat);
            var formatter = TimeSpanHFormat.ReusableInstance;

            var result = formatter.GetFormat(type);

            Assert.Null(result);
        }

        // The convenience Format(format, TimeSpan) overload must give exactly the ICustomFormatter result.
        [Fact]
        public void CustomFormatOverloadMatchesICustomFormatter()
        {
            var timeSpan = new TimeSpan(17, 49, 12);
            const string FORMAT_STRING = @"HH\:mm\:ss";

            var resultICustomFormatter = ((ICustomFormatter)TimeSpanHFormat.ReusableInstance).Format(FORMAT_STRING, timeSpan, null);
            var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(resultICustomFormatter, resultCustom);
        }

        // Non-TimeSpan IFormattable values (e.g. dates) are formatted with their own ToString(format) so mixed templates still work.
        [Fact]
        public void CorrectlyFormatsNonTimeSpanFormattable()
        {
            const float FLOAT = 3.14159f;
            const string FORMAT_STRING = @"F2";
            var expected = FLOAT.ToString(FORMAT_STRING);

            var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, FLOAT);

            Assert.Equal(expected, resultCustom);
        }

        // Plain objects fall back to ToString().
        [Fact]
        public void CorrectlyFormatsNonTimeSpanNonFormattable()
        {
            var obj = new object();
            const string FORMAT_STRING = "";
            const string EXPECTED = "System.Object";

            var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, obj);

            Assert.Equal(EXPECTED, resultCustom);
        }

        // A null argument formats to an empty string instead of throwing.
        [Fact]
        public void CorrectlyFormatsNull()
        {
            object? obj = null;
            const string FORMAT_STRING = "";
            const string EXPECTED = "";

            var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, obj);

            Assert.Equal(EXPECTED, resultCustom);
        }

        // An empty format string behaves like TimeSpan.ToString().
        [Fact]
        public void ReturnsTimeSpanToString_WhenFormatIsEmpty()
        {
            var timeSpan = new TimeSpan(17, 49, 12);
            const string FORMAT_STRING = "";
            var expected = timeSpan.ToString();

            var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(expected, resultCustom);
        }

        // A null format string behaves like TimeSpan.ToString().
        [Fact]
        public void ReturnsTimeSpanToString_WhenFormatIsNull()
        {
            var timeSpan = new TimeSpan(17, 49, 12);
            const string FORMAT_STRING = null!;
            var expected = timeSpan.ToString();

            var resultCustom = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(expected, resultCustom);
        }

        // Under 24 hours 'H' must give the same result as TimeSpan's 'h' (the fast path rewrites H to h).
        [Fact]
        public void MatchesTimeSpanToString_WhenBigHInFormat()
        {
            var timeSpan = new TimeSpan(17, 49, 12);
            const string FORMAT_STRING = @"HH\:mm\:ss";
            const string EXPECTED = "17:49:12";

            var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(EXPECTED, result);
        }

        // Without 'H' the format is passed straight to TimeSpan.ToString, so every standard specifier keeps working.
        [Fact]
        public void MatchesTimeSpanToString_WhenNoBigHInFormat()
        {
            var timeSpan = new TimeSpan(17, 49, 12);
            const string FORMAT_STRING = @"hh\:mm\:ss";
            const string EXPECTED = "17:49:12";

            var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(EXPECTED, result);
        }

        // A backslash-escaped 'H' is literal text, not an hours specifier, in both the fast and the big-H path.
        [Fact]
        public void CorrectlyEscapesCharsPrependedByBackslash()
        {
            var timeSpan = new TimeSpan(25, 37, 43);
            const string FORMAT_STRING = @"HH\:mm\:ss";
            const string EXPECTED = "25:37:43";

            var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(EXPECTED, result);
        }

        // A quoted 'H' is literal text (both quote styles).
        [Fact]
        public void CorrectlyEscapesSingleCharsContainedWithinQuotes()
        {
            var timeSpan = new TimeSpan(25, 37, 43);
            const string FORMAT_STRING = @"HH'-'mm'-'ss";
            const string EXPECTED = "25-37-43";

            var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(EXPECTED, result);
        }

        // Whole quoted words containing H are literal text.
        [Fact]
        public void CorrectlyEscapesManyCharsContainedWithinQuotes()
        {
            var timeSpan = new TimeSpan(25, 37, 43);
            const string FORMAT_STRING = @"'It has been 'HH' hours.'";
            const string EXPECTED = "It has been 25 hours.";

            var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(EXPECTED, result);
        }

        // A backslash-escaped quote inside a quoted literal does not end the literal.
        [Fact]
        public void CorrectlyEscapesEndQuotesPrependedByBackslashes()
        {
            var timeSpan = new TimeSpan(25, 37, 43);
            const string FORMAT_STRING = @"'I\'ll be back in 'H' Hours.'";
            const string EXPECTED = "I'll be back in 25 Hours.";

            var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(EXPECTED, result);
        }

        // An 'H' run at the very end of the format is flushed (an easy off-by-one in the hand-written parser).
        [Fact]
        public void CorrectlyFormatsTrailingBigH()
        {
            var timeSpan = new TimeSpan(25, 37, 43);
            const string FORMAT_STRING = @"ssmmHH";
            const string EXPECTED = "433725";

            var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(EXPECTED, result);
        }

        // 'HHH' and longer runs zero-pad the total hours; TimeSpan.ToString would throw for 'hhh', so this must take the custom path even under 24h.
        [Fact]
        public void CorrectlyFormatsMoreThan2BigH_WhenHoursUnder24()
        {
            var timeSpan = new TimeSpan(23, 37, 43);
            const string FORMAT_STRING = @"HHH";
            const string EXPECTED = "023";

            var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(EXPECTED, result);
        }

        // Very long 'H' runs pad correctly without overflowing the stack buffers.
        [Fact]
        public void CorrectlyFormatsAbsurdlyLongSequentialBigHFormat()
        {
            var timeSpan = new TimeSpan(25, 37, 43);
            const string FORMAT_STRING = @"HHHHHHHHHHHHHHHHHHHH";
            const string EXPECTED = "00000000000000000025";

            var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(EXPECTED, result);
        }

        // A format string longer than the 256-char stackalloc threshold takes the heap path and still formats identically.
        [Fact]
        public void CorrectlyFormatsAbsurdlyLongFormatString()
        {
            var timeSpan = new TimeSpan(25, 37, 43);
            const string FORMAT_STRING = "'This is a really long format string. You should never put messages in your format string, but this is a unit test " +
                                         "designed to ensure the unit functions as expected in extreme cases. This class could be exposed to user input someday. " +
                                         "The format string should be at least 256 characters in length, as that is the size of the stackalloc for appending " +
                                         "a regular format string as of writing the test but it could change in the future, we never know. SORRY FOR THE TRAFFIC " +
                                         @"NaM. It is now time to include a big H character to ensure we don\'t fall back on TimeSpan.ToString(). 'H";
            const string EXPECTED = "This is a really long format string. You should never put messages in your format string, but this is a unit test " +
                                    "designed to ensure the unit functions as expected in extreme cases. This class could be exposed to user input someday. " +
                                    "The format string should be at least 256 characters in length, as that is the size of the stackalloc for appending " +
                                    "a regular format string as of writing the test but it could change in the future, we never know. SORRY FOR THE TRAFFIC " +
                                    "NaM. It is now time to include a big H character to ensure we don't fall back on TimeSpan.ToString(). 25";

            var result = TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan);

            Assert.Equal(EXPECTED, result);
        }

        // An unterminated quote is a FormatException on the fast (<24h) path, matching TimeSpan.ToString.
        [Fact]
        public void ThrowsOnImbalancedQuoteMarkEscaping_WhenHoursUnder24()
        {
            var timeSpan = new TimeSpan(23, 37, 43);
            const string FORMAT_STRING = "H\" Imbalanced quote escaping.";

            Assert.Throws<FormatException>(() => TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan));
        }

        // An unterminated quote is a FormatException on the big-H (>=24h) path too, instead of silently eating the rest of the format.
        [Fact]
        public void ThrowsOnImbalancedQuoteMarkEscaping_When24HoursOrMore()
        {
            var timeSpan = new TimeSpan(24, 37, 43);
            const string FORMAT_STRING = "H\" Imbalanced quote escaping.";

            Assert.Throws<FormatException>(() => TimeSpanHFormat.ReusableInstance.Format(FORMAT_STRING, timeSpan));
        }
    }
}