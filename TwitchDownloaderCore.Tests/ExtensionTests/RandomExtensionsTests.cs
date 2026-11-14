using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // Tests RandomExtensions.NextDouble(min, max), used by the WPF app to randomize retry and live-monitor delays.
    public class RandomExtensionsTests
    {
        /// <summary>Values must always land in [min, max); a broken scale/offset would make retry waits zero or negative.</summary>
        [Fact]
        public void NextDouble_StaysWithinRange()
        {
            var random = new Random(1234); // Seeded for determinism

            for (var i = 0; i < 10_000; i++)
            {
                var value = random.NextDouble(8, 14);
                Assert.InRange(value, 8d, 14d);
                Assert.NotEqual(14d, value);
            }
        }

        /// <summary>The result must be min + NextDouble() * (max - min), i.e. the same sample as the base generator, just rescaled.</summary>
        [Fact]
        public void NextDouble_RescalesBaseSample()
        {
            var expected = new Random(42).NextDouble() * (40 - 30) + 30;

            var actual = new Random(42).NextDouble(30, 40);

            Assert.Equal(expected, actual);
        }
    }
}
