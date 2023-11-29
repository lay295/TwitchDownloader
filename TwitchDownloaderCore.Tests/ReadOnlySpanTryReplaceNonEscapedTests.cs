using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests
{
    // ReSharper disable StringLiteralTypo
    public class ReadOnlySpanTryReplaceNonEscapedTests
    {
        [Fact]
        public void ReturnsFalse_WhenDestinationTooShort()
        {
            ReadOnlySpan<char> str = @"SORRY FOR TRAFFIC NaM";
            var destination = Array.Empty<char>();

            var success = str.TryReplaceNonEscaped(destination, 'r', 'w');

            Assert.False(success);
        }

        [Fact]
        public void MatchesOriginalString_WhenOldCharNotFound()
        {
            ReadOnlySpan<char> str = @"SORRY FOR TRAFFIC NaM";
            var destination = new char[str.Length];
            const string EXPECTED = @"SORRY FOR TRAFFIC NaM";

            var success = str.TryReplaceNonEscaped(destination, 'r', 'w');

            Assert.True(success);
            Assert.Equal(EXPECTED.AsSpan(), destination);
        }

        [Fact]
        public void CorrectlyEscapeCharsPrependedByBackslash()
        {
            ReadOnlySpan<char> str = @"SO\RRY FO\R TRAFFIC NaM";
            var destination = new char[str.Length];
            const string EXPECTED = @"SO\RWY FO\R TWAFFIC NaM";

            var success = str.TryReplaceNonEscaped(destination, 'R', 'W');

            Assert.True(success);
            Assert.Equal(EXPECTED.AsSpan(), destination);
        }

        [Fact]
        public void CorrectlyEscapeSingleCharsContainedWithinQuotes()
        {
            ReadOnlySpan<char> str = "SO\"R\"RY FO'R' TRAFFIC NaM";
            var destination = new char[str.Length];
            const string EXPECTED = "SO\"R\"WY FO'R' TWAFFIC NaM";

            var success = str.TryReplaceNonEscaped(destination, 'R', 'W');

            Assert.True(success);
            Assert.Equal(EXPECTED.AsSpan(), destination);
        }

        [Fact]
        public void CorrectlyEscapeManyCharsContainedWithinQuotes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" 'TRAFFIC' NaM";
            var destination = new char[str.Length];
            const string EXPECTED = "SOWWY \"FOR\" 'TRAFFIC' NaM";

            var success = str.TryReplaceNonEscaped(destination, 'R', 'W');

            Assert.True(success);
            Assert.Equal(EXPECTED.AsSpan(), destination);
        }

        [Fact]
        public void CorrectlyEscapesEndQuotesPrependedByBackslashes()
        {
            ReadOnlySpan<char> str = @"'It\'s finally over.' It truly is over.";
            var destination = new char[str.Length];
            const string EXPECTED = @"'It\'s finally over.' It twuly is ovew.";

            var success = str.TryReplaceNonEscaped(destination, 'r', 'w');

            Assert.True(success);
            Assert.Equal(EXPECTED.AsSpan(), destination);
        }

        [Fact]
        public void DoesNotEscapeDifferingQuotes()
        {
            ReadOnlySpan<char> str = "\"SORRY FOR TRAFFIC NaM.'";
            var destination = new char[str.Length];

            var success = str.TryReplaceNonEscaped(destination, 'R', 'W');

            Assert.False(success);
        }
    }
}