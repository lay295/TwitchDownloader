using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    public class ChatRenderVideoTicksTests
    {
        [Fact]
        public void UsesChatBoundsWhenNeitherOverrideIsSet()
        {
            var (startTick, totalTicks) = ChatRenderVideoTicks.Calculate(-1, -1, 10.4, 100.2, 30);

            Assert.Equal(300, startTick);
            Assert.Equal(2706, totalTicks);
        }

        [Fact]
        public void HonorsEndOverrideWhenStartIsUnchecked()
        {
            var (startTick, totalTicks) = ChatRenderVideoTicks.Calculate(-1, 300, 0, 7200, 60);

            Assert.Equal(0, startTick);
            Assert.Equal(18_000, totalTicks);
        }

        [Fact]
        public void UsesChatStartWhenOnlyEndIsOverridden()
        {
            var (startTick, totalTicks) = ChatRenderVideoTicks.Calculate(-1, 300, 120, 7200, 60);

            Assert.Equal(7_200, startTick);
            Assert.Equal(10_800, totalTicks);
        }

        [Fact]
        public void HonorsStartOverrideWhenEndIsUnchecked()
        {
            var (startTick, totalTicks) = ChatRenderVideoTicks.Calculate(60, -1, 0, 3600, 60);

            Assert.Equal(3_600, startTick);
            Assert.Equal(212_400, totalTicks);
        }

        [Fact]
        public void HonorsBothOverridesIndependently()
        {
            var (startTick, totalTicks) = ChatRenderVideoTicks.Calculate(10, 70, 0, 3600, 10);

            Assert.Equal(100, startTick);
            Assert.Equal(600, totalTicks);
        }
    }
}
