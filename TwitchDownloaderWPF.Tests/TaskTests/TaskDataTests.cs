using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF.Tests.TaskTests
{
    // Tests TaskData.LengthFormatted, the duration label shown for every video/clip in the mass-download and URL-list windows.
    public class TaskDataTests
    {
        // Verifies the three display formats and their boundaries: seconds only ("7s"), mm:ss below an hour, and h:mm:ss with total hours
        // (not wrapped at 24) above.
        // Why: TimeSpan's own Hours component wraps at 24, so a regression to time.Hours would show a 25-hour stream as "1:00:01";
        // the boundaries (59s, 60s, 3599s, 3600s) are where off-by-one format switches happen.
        [Theory]
        [InlineData(0, "0s")]
        [InlineData(7, "7s")]
        [InlineData(59, "59s")]
        [InlineData(60, "01:00")]
        [InlineData(61, "01:01")]
        [InlineData(3599, "59:59")]
        [InlineData(3600, "1:00:00")]
        [InlineData(36_061, "10:01:01")]
        [InlineData(90_001, "25:00:01")]
        public void LengthFormatted_FormatsByMagnitude(int lengthSeconds, string expected)
        {
            Assert.Equal(expected, new TaskData { Length = lengthSeconds }.LengthFormatted);
        }
    }
}
