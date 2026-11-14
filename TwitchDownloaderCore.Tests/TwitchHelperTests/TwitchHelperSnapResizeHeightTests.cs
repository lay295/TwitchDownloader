namespace TwitchDownloaderCore.Tests.TwitchHelperTests
{
    // Tests TwitchHelper.SnapResizeHeight, the pure function emotes/badges/cheermotes use to decide their rendered height.
    // Snapping to an integer multiple of the source height keeps pixel-art emotes crisp; an off-by-one here blurs every emote in a render.
    public class TwitchHelperSnapResizeHeightTests
    {
        // Equal non-zero thresholds snap to the nearest multiple of the image height within +/- threshold, in either direction.
        [Theory]
        [InlineData(30, 28)] // 2 above 28 -> down
        [InlineData(26, 28)] // 2 below 28 -> up
        [InlineData(56, 56)] // already a multiple
        [InlineData(40, 40)] // 12 away from any multiple -> unchanged
        public void EqualThresholds_SnapToNearestMultiple(int desired, int expected)
        {
            Assert.Equal(expected, TwitchHelper.SnapResizeHeight(desired, 4, 4, 28));
        }

        // With independent thresholds, only the enabled direction(s) may snap, and only when within that direction's threshold.
        [Theory]
        [InlineData(30, 0, 0, 30)] // snapping disabled
        [InlineData(30, 0, 4, 28)] // down-only, within threshold
        [InlineData(33, 0, 4, 33)] // down-only, too far
        [InlineData(26, 4, 0, 28)] // up-only, within threshold
        [InlineData(20, 4, 0, 20)] // up-only, too far
        [InlineData(30, 2, 5, 28)] // down wins, already a multiple so no further up-snap
        [InlineData(55, 2, 1, 56)] // down too far, up within threshold
        public void IndependentThresholds_SnapOnlyInEnabledDirection(int desired, int up, int down, int expected)
        {
            Assert.Equal(expected, TwitchHelper.SnapResizeHeight(desired, up, down, 28));
        }
    }
}
