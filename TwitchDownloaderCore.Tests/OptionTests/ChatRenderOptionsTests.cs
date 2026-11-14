using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCore.Tests.OptionTests
{
    public class ChatRenderOptionsTests
    {
        [Theory]
        [InlineData(700, 50, 350)]
        [InlineData(700, 100, 700)]
        [InlineData(300, 25, 75)]
        [InlineData(300, 80, 240)]
        public void GifMaxHeightIsThePercentageOfTheChatHeight(int chatHeight, int percent, int expected)
        {
            var options = new ChatRenderOptions { ChatHeight = chatHeight, GifMaxHeightPercent = percent };

            Assert.Equal(expected, options.GifMaxHeight);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        public void GifMaxHeightTreatsAnOutOfRangePercentageAsOne(int percent)
        {
            var options = new ChatRenderOptions { ChatHeight = 700, GifMaxHeightPercent = percent };

            Assert.Equal(7, options.GifMaxHeight);
        }

        [Fact]
        public void GifMaxHeightIsNeverLessThanOnePixel()
        {
            // Scaling divides by it, so a chat too short to give the percentage a whole pixel must still not yield zero
            var options = new ChatRenderOptions { ChatHeight = 10, GifMaxHeightPercent = 1 };

            Assert.Equal(1, options.GifMaxHeight);
        }

        [Fact]
        public void GifMaxHeightPercentDefaultsToHalfTheChat()
        {
            var options = new ChatRenderOptions { ChatHeight = 600 };

            Assert.Equal(50, options.GifMaxHeightPercent);
            Assert.Equal(300, options.GifMaxHeight);
        }

        [Fact]
        public void GiphyGifsIsOnByDefault()
        {
            Assert.True(new ChatRenderOptions().GiphyGifs);
        }
    }
}
