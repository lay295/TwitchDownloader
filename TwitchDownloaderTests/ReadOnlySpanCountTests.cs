using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderTests
{
    public class ReadOnlySpanCountTests
    {
        [Fact]
        public void ReturnsNegativeOneWhenNotPresent()
        {
            ReadOnlySpan<char> str = "SORRY FOR THE TRAFFIC NaM";
            const int EXPECTED = -1;

            var actual = str.Count('L');

            Assert.Equal(EXPECTED, actual);
        }

        [Fact]
        public void ReturnsNegativeOneForEmptyString()
        {
            ReadOnlySpan<char> str = "";
            const int EXPECTED = -1;

            var actual = str.Count('L');

            Assert.Equal(EXPECTED, actual);
        }

        [Theory]
        [InlineData('S', 1)]
        [InlineData('R', 4)]
        [InlineData('a', 1)]
        [InlineData('F', 3)]
        [InlineData('M', 1)]
        public void ReturnsCorrectCharacterCount(char character, int expectedCount)
        {
            ReadOnlySpan<char> str = "SORRY FOR THE TRAFFIC NaM";

            var actual = str.Count(character);

            Assert.Equal(expectedCount, actual);
        }
    }
}