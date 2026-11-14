using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests VideoDownloadState, the shared work queue and per-part bookkeeping that all VOD download threads pull from.
    public class VideoDownloadStateTests
    {
        private static readonly Uri BaseUrl = new("https://abc.cloudfront.net/abc_streamer_123/720p60/");

        private static M3U8.Stream[] Playlist(params string[] paths) => paths.Select(p => new M3U8.Stream(new M3U8.Stream.ExtPartInfo(10, false), default, default, p)).ToArray();

        /// <summary>
        /// Only the cropped range of the playlist is queued, and "-muted" parts are queued first: they may need extra unmute
        /// attempts, so starting them early keeps them from becoming the long tail of the download.
        /// </summary>
        [Fact]
        public void QueuesCroppedPartsWithMutedFirst()
        {
            var playlist = Playlist("0.ts", "1.ts", "2-muted.ts", "3.ts", "4-muted.ts", "5.ts");

            var state = new VideoDownloadState(["720p60"], playlist, 1..5, BaseUrl, null!, DateTimeOffset.UtcNow);

            Assert.Equal(["2-muted.ts", "4-muted.ts", "1.ts", "3.ts"], state.PartQueue);
            Assert.Equal(4, state.PartCount);
            Assert.Equal(state.PartQueue.Order(), state.PartStates.Keys.Order());
        }

        /// <summary>
        /// Unmuting is only attempted for VODs aired within the last 24 hours (Twitch serves the original audio only for a short time),
        /// so older VODs do not waste requests on every muted part.
        /// </summary>
        [Theory]
        [InlineData(1, true)]
        [InlineData(48, false)]
        public void TryUnmute_OnlyForRecentVods(int hoursAgo, bool expected)
        {
            var state = new VideoDownloadState(["720p60"], Playlist("0.ts"), .., BaseUrl, null!, DateTimeOffset.UtcNow.AddHours(-hoursAgo));

            Assert.Equal(expected, state.PartStates["0.ts"].TryUnmute);
        }

        /// <summary>The download quality is the known quality path the base URL ends with, else the URL's last segment.</summary>
        [Theory]
        [InlineData("https://abc.cloudfront.net/abc_streamer_123/720p60/", "720p60")]
        [InlineData("https://abc.cloudfront.net/abc_streamer_123/chunked/", "chunked/")]
        public void DownloadQuality_ComesFromBaseUrl(string baseUrl, string expected)
        {
            var state = new VideoDownloadState(["1080p60", "720p60"], Playlist("0.ts"), .., new Uri(baseUrl), null!, DateTimeOffset.UtcNow);

            Assert.Equal(expected, state.DownloadQuality);
            Assert.Equal(["1080p60", "720p60"], state.AllQualities);
        }

        /// <summary>The fMP4 init header's size is recorded when the file exists (used to validate it), and left at 0 otherwise.</summary>
        [Fact]
        public void RecordsHeaderFileSizeWhenPresent()
        {
            using var temp = new TempDirectory();
            var header = temp.Combine("init-0.mp4");
            File.WriteAllBytes(header, new byte[123]);
            var airDate = DateTimeOffset.UtcNow;

            var withHeader = new VideoDownloadState(["720p60"], Playlist("0.ts"), .., BaseUrl, header, airDate);
            var missingHeader = new VideoDownloadState(["720p60"], Playlist("0.ts"), .., BaseUrl, temp.Combine("missing.mp4"), airDate);

            Assert.Equal(123, withHeader.HeaderFileSize);
            Assert.Equal(header, withHeader.HeaderFile);
            Assert.Equal(0, missingHeader.HeaderFileSize);
            Assert.Equal(BaseUrl, withHeader.BaseUrl);
            Assert.Equal(airDate, withHeader.VodAirDate);
        }
    }
}
