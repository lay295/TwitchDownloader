using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable StringLiteralTypo InvokeAsExtensionMember
    public class ReadOnlySpanTryReplaceNonEscapedTests
    {
        // A destination that cannot hold the result makes the call fail (false) instead of throwing or writing a truncated string.
        [Fact]
        public void ReturnsFalse_WhenDestinationTooShort()
        {
            ReadOnlySpan<char> str = @"SORRY FOR TRAFFIC NaM";
            var destination = Array.Empty<char>();

            var success = ReadOnlySpanExtensions.TryReplaceNonEscaped(str, destination, 'r', 'w');

            Assert.False(success);
        }

        // When the char does not occur the input is copied unchanged (case-sensitively: 'r' does not match 'R').
        [Fact]
        public void MatchesOriginalString_WhenOldCharNotFound()
        {
            ReadOnlySpan<char> str = @"SORRY FOR TRAFFIC NaM";
            var destination = new char[str.Length];
            const string EXPECTED = @"SORRY FOR TRAFFIC NaM";

            var success = ReadOnlySpanExtensions.TryReplaceNonEscaped(str, destination, 'r', 'w');

            Assert.True(success);
            Assert.Equal(EXPECTED.AsSpan(), destination);
        }

        // A backslash escapes exactly the next char: escaped occurrences stay, every other occurrence is replaced.
        [Fact]
        public void DoesNotReplaceChars_WhenPrependedByBackslash()
        {
            ReadOnlySpan<char> str = @"SO\RRY FO\R TRAFFIC NaM";
            var destination = new char[str.Length];
            const string EXPECTED = @"SO\RWY FO\R TWAFFIC NaM";

            var success = ReadOnlySpanExtensions.TryReplaceNonEscaped(str, destination, 'R', 'W');

            Assert.True(success);
            Assert.Equal(EXPECTED.AsSpan(), destination);
        }

        // Single chars wrapped in double or single quotes are literal text and must not be replaced.
        [Fact]
        public void DoesNotReplaceSingleChars_WhenContainedWithinQuotes()
        {
            ReadOnlySpan<char> str = "SO\"R\"RY FO'R' TRAFFIC NaM";
            var destination = new char[str.Length];
            const string EXPECTED = "SO\"R\"WY FO'R' TWAFFIC NaM";

            var success = ReadOnlySpanExtensions.TryReplaceNonEscaped(str, destination, 'R', 'W');

            Assert.True(success);
            Assert.Equal(EXPECTED.AsSpan(), destination);
        }

        // Whole quoted words are skipped as one literal, for both quote styles, while text between the quoted sections is still replaced.
        [Fact]
        public void DoesNotReplaceManyChars_WhenContainedWithinQuotes()
        {
            ReadOnlySpan<char> str = "SORRY \"FOR\" 'TRAFFIC' NaM";
            var destination = new char[str.Length];
            const string EXPECTED = "SOWWY \"FOR\" 'TRAFFIC' NaM";

            var success = ReadOnlySpanExtensions.TryReplaceNonEscaped(str, destination, 'R', 'W');

            Assert.True(success);
            Assert.Equal(EXPECTED.AsSpan(), destination);
        }

        // A backslash-escaped quote inside a quoted section does not end that section, so replacement only resumes after the real closing quote.
        [Fact]
        public void CorrectlyEscapesEndQuotesPrependedByBackslashes()
        {
            ReadOnlySpan<char> str = @"'It\'s finally over.' It truly is over.";
            var destination = new char[str.Length];
            const string EXPECTED = @"'It\'s finally over.' It twuly is ovew.";

            var success = ReadOnlySpanExtensions.TryReplaceNonEscaped(str, destination, 'r', 'w');

            Assert.True(success);
            Assert.Equal(EXPECTED.AsSpan(), destination);
        }

        // A double quote is only closed by a double quote: with a lone " and a lone ' the whole string is one unterminated literal and nothing is replaced.
        [Fact]
        public void DoesNotEscapeDifferingQuotes()
        {
            ReadOnlySpan<char> str = "\"SORRY FOR TRAFFIC NaM.'";
            var destination = new char[str.Length];

            var success = ReadOnlySpanExtensions.TryReplaceNonEscaped(str, destination, 'R', 'W');

            Assert.True(success);
            Assert.Equal(str, destination);
        }

        // Replacing a quote or backslash itself is ambiguous (is it the escape or the target?), so the method refuses with false.
        [Fact]
        public void DoesNotSupportReplacingEscapeChars()
        {
            ReadOnlySpan<char> str = "\"SORRY FOR\" TRAFFIC NaM.\"";
            var destination = new char[str.Length];

            var success = ReadOnlySpanExtensions.TryReplaceNonEscaped(str, destination, '\"', 'W');

            Assert.False(success);
        }
    }
}