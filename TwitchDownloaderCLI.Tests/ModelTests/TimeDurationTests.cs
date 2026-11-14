using System.Globalization;
using TwitchDownloaderCLI.Models;
using static System.TimeSpan;

namespace TwitchDownloaderCLI.Tests.ModelTests
{
    // Tests TimeDuration, the type behind every trim option (-b/--beginning, -e/--ending). It accepts "90s", "1:30", "1.5m" and friends,
    // so a parsing mistake silently trims a download or render at the wrong time.
    public class TimeDurationTests
    {
        private const string EN_US = "en-US";
        private const string DE_DE = "de-DE";

        // Every supported notation (unit suffixes, decimal seconds in the given culture, m:ss, h:mm:ss, d:hh:mm:ss) parses to the right duration.
        // Guards the custom regexes that stop TimeSpan.Parse from reading "10:30" as 10 hours or "36:01:02" as 36 days.
        [Theory]
        [InlineData(EN_US, "200ms", 200 * TicksPerMillisecond)]
        [InlineData(EN_US, "55", 55 * TicksPerSecond)]
        [InlineData(EN_US, "0.2", 2 * TicksPerSecond / 10)]
        [InlineData(DE_DE, "0,2", 2 * TicksPerSecond / 10)]
        [InlineData(EN_US, "23.189", 23189 * TicksPerSecond / 1000)]
        [InlineData(DE_DE, "23,189", 23189 * TicksPerSecond / 1000)]
        [InlineData(EN_US, "55s", 55 * TicksPerSecond)]
        [InlineData(EN_US, "17m", 17 * TicksPerMinute)]
        [InlineData(EN_US, "31h", 31 * TicksPerHour)]
        [InlineData(EN_US, "6.5h", 6 * TicksPerHour + 30 * TicksPerMinute)]
        [InlineData(DE_DE, "6,5h", 6 * TicksPerHour + 30 * TicksPerMinute)]
        [InlineData(EN_US, "0:09:27", 9 * TicksPerMinute + 27 * TicksPerSecond)]
        [InlineData(EN_US, "11:30", 11 * TicksPerMinute + 30 * TicksPerSecond)]
        [InlineData(EN_US, "10:24.12345", 10 * TicksPerMinute + 24 * TicksPerSecond + 123 * TicksPerMillisecond)]
        [InlineData(DE_DE, "10:24,12345", 10 * TicksPerMinute + 24 * TicksPerSecond + 123 * TicksPerMillisecond)]
        [InlineData(EN_US, "12:03:45", 12 * TicksPerHour + 3 * TicksPerMinute + 45 * TicksPerSecond)]
        [InlineData(EN_US, "39:23:02", 39 * TicksPerHour + 23 * TicksPerMinute + 2 * TicksPerSecond)]
        [InlineData(EN_US, "47:22:08.123", 47 * TicksPerHour + 22 * TicksPerMinute + 8 * TicksPerSecond + 123 * TicksPerMillisecond)]
        [InlineData(DE_DE, "47:22:08,123", 47 * TicksPerHour + 22 * TicksPerMinute + 8 * TicksPerSecond + 123 * TicksPerMillisecond)]
        [InlineData(EN_US, "47:22:08.12345", 47 * TicksPerHour + 22 * TicksPerMinute + 8 * TicksPerSecond + 123 * TicksPerMillisecond)]
        [InlineData(DE_DE, "47:22:08,12345", 47 * TicksPerHour + 22 * TicksPerMinute + 8 * TicksPerSecond + 123 * TicksPerMillisecond)]
        [InlineData(EN_US, "1.2:3:4.5", 1 * TicksPerDay + 2 * TicksPerHour + 3 * TicksPerMinute + 4 * TicksPerSecond + 500 * TicksPerMillisecond)]
        [InlineData(DE_DE, "1:2:3:4,5", 1 * TicksPerDay + 2 * TicksPerHour + 3 * TicksPerMinute + 4 * TicksPerSecond + 500 * TicksPerMillisecond)]
        [InlineData(EN_US, "2:03:54:27.26", 2 * TicksPerDay + 3 * TicksPerHour + 54 * TicksPerMinute + 27 * TicksPerSecond + 260 * TicksPerMillisecond)]
        [InlineData(DE_DE, "2:03:54:27,26", 2 * TicksPerDay + 3 * TicksPerHour + 54 * TicksPerMinute + 27 * TicksPerSecond + 260 * TicksPerMillisecond)]
        public void CorrectlyParsesTimeStrings(string culture, string input, long expectedTicks)
        {
            var expected = new TimeDuration(expectedTicks);
            var cultureInfo = new CultureInfo(culture);

            var actual = TimeDuration.Parse(input, cultureInfo);

            Assert.Equal(expected, actual);
        }

        // Malformed input must throw (CommandLineParser turns the exception into a parse error) instead of yielding a bogus duration.
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("100 s")]
        [InlineData("123d")]
        [InlineData("0:12345")]
        [InlineData("x5")]
        [InlineData("1.2.3s")]
        public void ThrowsOnBadFormat(string input)
        {
            Assert.ThrowsAny<Exception>(() => TimeDuration.Parse(input, CultureInfo.InvariantCulture));
        }

        // CommandLineParser builds option values through the string constructor, so it must behave exactly like Parse.
        [Fact]
        public void StringConstructor_MatchesParse()
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

                Assert.Equal(TimeDuration.Parse("1:30"), new TimeDuration("1:30"));
                Assert.Equal(new TimeDuration(15 * TicksPerSecond / 10), new TimeDuration("1.5"));
                Assert.Throws<FormatException>(() => new TimeDuration("nope"));
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        // The implicit TimeSpan conversion and ToString are what the CLI modes use to hand trims to Core; MinusOneSeconds is the
        // "not set" sentinel used by chatupdate/chatrender, so its value must stay exactly -1 second.
        [Fact]
        public void ConvertsToTimeSpan()
        {
            TimeSpan timeSpan = new TimeDuration(FromSeconds(90));

            Assert.Equal(FromSeconds(90), timeSpan);
            Assert.Equal("00:01:30", new TimeDuration(FromSeconds(90)).ToString());
            Assert.Equal(FromSeconds(-1), (TimeSpan)TimeDuration.MinusOneSeconds);
        }
    }
}