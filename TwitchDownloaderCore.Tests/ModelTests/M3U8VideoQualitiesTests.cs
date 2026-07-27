using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    public static class M3U8VideoQualitiesTests
    {
        [Theory]
        [InlineData("720", "720p30")]
        [InlineData("720p", "720p30")]
        [InlineData("720p30", "720p30")]
        [InlineData("1280x720", "720p30")]
        [InlineData("1280x720p", "720p30")]
        [InlineData("1280x720p30", "720p30")]
        [InlineData("720p60", "720p60")]
        [InlineData("1280x720p60", "720p60")]
        [InlineData("1080", "1080p60")]
        [InlineData("1080p", "1080p60")]
        [InlineData("1080p60", "1080p60")]
        [InlineData("1920x1080", "1080p60")]
        [InlineData("1920x1080p", "1080p60")]
        [InlineData("1920x1080p60", "1080p60")]
        [InlineData("", "1080p60")]
        [InlineData(null, "1080p60")]
        [InlineData("Source", "1080p60")]
        [InlineData("chunked", "1080p60")]
        [InlineData("Best", "1080p60")]
        [InlineData("Worst", "720p30")]
        public static void FindsQuality_FromLiveM3U8(string? qualityString, string expectedPath)
        {
            var m3u8 = new M3U8(new M3U8.Metadata(), [
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1920, 1080), 60, "1080p60", "1080p60", "source"),
                    "1080p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1280, 720), 60, "720p60", "720p60", "transcode"),
                    "720p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1280, 720), 30, "720p30", "720p", "transcode"),
                    "720p30"),
            ]);

            var qualities = VideoQualities.FromM3U8(m3u8);
            var selectedQuality = qualities.GetQuality(qualityString);

            Assert.Equal(expectedPath, selectedQuality?.Item.Path);
        }

        [Theory]
        [InlineData("1080", "1080p60")]
        [InlineData("1080p", "1080p60")]
        [InlineData("1080p60", "1080p60")]
        [InlineData("1920x1080", "1080p60")]
        [InlineData("1920x1080p", "1080p60")]
        [InlineData("1920x1080p60", "1080p60")]
        [InlineData("720p60", "720p60")]
        [InlineData("1280x720p60", "720p60")]
        [InlineData("720", "720p30")]
        [InlineData("720p", "720p30")]
        [InlineData("720p30", "720p30")]
        [InlineData("1280x720", "720p30")]
        [InlineData("1280x720p", "720p30")]
        [InlineData("1280x720p30", "720p30")]
        [InlineData("audio", "audio_only")]
        [InlineData("Audio", "audio_only")]
        [InlineData("Audio Only", "audio_only")]
        [InlineData("", "1080p60")]
        [InlineData(null, "1080p60")]
        [InlineData("Source", "1080p60")]
        [InlineData("chunked", "1080p60")]
        [InlineData("Best", "1080p60")]
        [InlineData("Worst", "144p30")]
        [InlineData("Source Portrait", "1080p60-portrait")]
        [InlineData("Best Portrait", "1080p60-portrait")]
        [InlineData("Worst Portrait", "720p60-portrait")]
        public static void FindsQuality_FromOldM3U8(string? qualityString, string expectedPath)
        {
            var m3u8 = new M3U8(new M3U8.Metadata(), [
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1920, 1080), 58.644M, "1080p60", "1080p60", "source"),
                    "1080p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1280, 720), 58.644M, "720p60", "720p60", "transcode"),
                    "720p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1280, 720), 28.814M, "720p30", "720p", "transcode"),
                    "720p30"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.42C01E", "mp4a.40.2"], (852, 480), 30.159M, "480p30", "480p", "transcode"),
                    "480p30"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.42C01E", "mp4a.40.2"], (640, 360), 30.159M, "360p30", "360p", "transcode"),
                    "360p30"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.42C00C", "mp4a.40.2"], (256, 144), 30.159M, "144p30", "144p", "transcode"),
                    "144p30"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["mp4a.40.2"], (256, 144), 0, "audio_only", "Audio Only", "source"),
                    "audio_only"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1080, 1920), 58.644M, "1080p60-portrait", "1080p", "source"),
                    "1080p60-portrait"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (720, 1280), 58.644M, "720p60-portrait", "720p60", "source"),
                    "720p60-portrait"),
            ]);

            var qualities = VideoQualities.FromM3U8(m3u8);
            var selectedQuality = qualities.GetQuality(qualityString);

            Assert.Equal(expectedPath, selectedQuality?.Item.Path);
        }

        [Theory]
        [InlineData("1080", "1080p60")]
        [InlineData("1080p", "1080p60")]
        [InlineData("1080p60", "1080p60")]
        [InlineData("720p60", "720p60")]
        [InlineData("", "1080p60")]
        [InlineData(null, "1080p60")]
        [InlineData("Source", "1080p60")]
        [InlineData("chunked", "1080p60")]
        [InlineData("Best", "1080p60")]
        [InlineData("Worst", "720p60")]
        [InlineData("Source Portrait", "1080p60-portrait")]
        [InlineData("Best Portrait", "1080p60-portrait")]
        [InlineData("Worst Portrait", "720p60-portrait")]
        public static void FindsQuality_FromM3U8WithoutFramerate(string? qualityString, string? expectedPath)
        {
            var m3u8 = new M3U8(new M3U8.Metadata(), [
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1920, 1080), 0, "1080p60", "1080p", "source"),
                    "1080p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1280, 720), 58.644M, "720p60", "720p60", "transcode"),
                    "720p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1080, 1920), 0, "1080p60-portrait", "1080p", "source"),
                    "1080p60-portrait"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (720, 1280), 58.644M, "720p60-portrait", "720p60", "source"),
                    "720p60-portrait"),
            ]);

            var qualities = VideoQualities.FromM3U8(m3u8);
            var selectedQuality = qualities.GetQuality(qualityString);

            Assert.Equal(expectedPath, selectedQuality?.Item.Path);
        }

        [Fact]
        public static void ReturnsNull_WhenNoStreamsPresent()
        {
            var m3u8 = new M3U8(new M3U8.Metadata(), []);
            var qualities = VideoQualities.FromM3U8(m3u8);
            var selectedQuality = qualities.GetQuality("");

            Assert.Null(selectedQuality);
        }
    }
}