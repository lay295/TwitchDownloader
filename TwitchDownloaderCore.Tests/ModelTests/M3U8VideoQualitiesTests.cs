using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    // Tests M3U8VideoQualities, the quality picker behind --quality / the GUI quality box for VODs and live streams: naming renditions
    // (1080p60, 720p, Audio Only...), resolving user strings and keywords, and best/worst selection.
    public static class M3U8VideoQualitiesTests
    {
        // User strings (exact names, 'best'/'source'/'worst' keywords, null for best) resolve to the right rendition of a current master playlist.
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
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1920, 1080), 60, "1080p60", "1080p60", ["landscape"], "source"),
                    "1080p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1280, 720), 60, "720p60", "720p60", ["landscape"], "transcode"),
                    "720p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1280, 720), 30, "720p30", "720p", ["landscape"], "transcode"),
                    "720p30"),
            ]);

            var qualities = VideoQualities.FromM3U8(m3u8);
            var selectedQuality = qualities.GetQuality(qualityString);

            Assert.Equal(expectedPath, selectedQuality?.Item.Path);
        }
        // Many spellings of one quality (height only, with 'p', with framerate, WIDTHxHEIGHT, with or without framerate) resolve to the same rendition, and the 30fps default applies when no framerate is given.

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
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1920, 1080), 58.644M, "1080p60", "1080p60", ["landscape"], "source"),
                    "1080p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1280, 720), 58.644M, "720p60", "720p60", ["landscape"], "transcode"),
                    "720p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1280, 720), 28.814M, "720p30", "720p", ["landscape"], "transcode"),
                    "720p30"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.42C01E", "mp4a.40.2"], (852, 480), 30.159M, "480p30", "480p", ["landscape"], "transcode"),
                    "480p30"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.42C01E", "mp4a.40.2"], (640, 360), 30.159M, "360p30", "360p", ["landscape"], "transcode"),
                    "360p30"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.42C00C", "mp4a.40.2"], (256, 144), 30.159M, "144p30", "144p", ["landscape"], "transcode"),
                    "144p30"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["mp4a.40.2"], (256, 144), 0, "audio_only", "Audio Only", ["landscape", "portrait"], "source"),
                    "audio_only"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1080, 1920), 58.644M, "1080p60-portrait", "1080p", ["portrait"], "source"),
                    "1080p60-portrait"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (720, 1280), 58.644M, "720p60-portrait", "720p60", ["portrait"], "source"),
                    "720p60-portrait"),
            ]);

            var qualities = VideoQualities.FromM3U8(m3u8);
            var selectedQuality = qualities.GetQuality(qualityString);

            Assert.Equal(expectedPath, selectedQuality?.Item.Path);
        }
        // When FRAME-RATE is missing, names fall back to resolution only and lookups by '1080p' still work (null when nothing matches).

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
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1920, 1080), 0, "1080p60", "1080p", ["landscape"], "source"),
                    "1080p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1280, 720), 58.644M, "720p60", "720p60", ["landscape"], "transcode"),
                    "720p60"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (1080, 1920), 0, "1080p60-portrait", "1080p", ["portrait"], "source"),
                    "1080p60-portrait"),
                new M3U8.Stream(
                    new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1.4D401F", "mp4a.40.2"], (720, 1280), 58.644M, "720p60-portrait", "720p60", ["portrait"], "source"),
                    "720p60-portrait"),
            ]);

            var qualities = VideoQualities.FromM3U8(m3u8);
            var selectedQuality = qualities.GetQuality(qualityString);

            Assert.Equal(expectedPath, selectedQuality?.Item.Path);
        }

        // Rendition names are built from whatever attributes exist (resolution, framerate, source flag, portrait orientation, audio only),
        // matching the labels the Twitch player shows.
        [Theory]
        // Landscape
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,STABLE-VARIANT-ID=\"1080p60\",IVS-NAME=\"1080p60\",IVS-GROUPS=\"landscape\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60/index-dvr.m3u8", "1080p60")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,STABLE-VARIANT-ID=\"1080p\",IVS-NAME=\"1080p\",IVS-GROUPS=\"landscape\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60/index-dvr.m3u8", "1080p60")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",FRAME-RATE=60.000,STABLE-VARIANT-ID=\"1080p\",IVS-NAME=\"1080p\",IVS-GROUPS=\"landscape\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60/index-dvr.m3u8", "1080p60")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,STABLE-VARIANT-ID=\"1080p\",IVS-NAME=\"1080p\",IVS-GROUPS=\"landscape\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60/index-dvr.m3u8", "1080p60")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,IVS-NAME=\"1080p60\",IVS-GROUPS=\"landscape\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60/index-dvr.m3u8", "1080p60")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,STABLE-VARIANT-ID=\"1080p60\",IVS-GROUPS=\"landscape\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60/index-dvr.m3u8", "1080p60")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,IVS-GROUPS=\"landscape\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60/index-dvr.m3u8", "1080p60")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60/index-dvr.m3u8", "1080p60")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/chunked/index-dvr.m3u8", "1080p")]
        // Portrait
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,STABLE-VARIANT-ID=\"1080p60-portrait\",IVS-NAME=\"1080p60-portrait\",IVS-GROUPS=\"portrait\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60-portrait/index-dvr.m3u8", "1080p60-Portrait")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",FRAME-RATE=60.000,STABLE-VARIANT-ID=\"1080p60-portrait\",IVS-NAME=\"1080p60-portrait\",IVS-GROUPS=\"portrait\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60-portrait/index-dvr.m3u8", "1080p60-Portrait")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",FRAME-RATE=60.000,STABLE-VARIANT-ID=\"1080p-portrait\",IVS-NAME=\"1080p-portrait\",IVS-GROUPS=\"portrait\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60-portrait/index-dvr.m3u8", "1080p60-Portrait")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,STABLE-VARIANT-ID=\"1080p60-portrait\",IVS-NAME=\"1080p60-portrait\",IVS-GROUPS=\"portrait\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60-portrait/index-dvr.m3u8", "1080p60-Portrait")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,IVS-NAME=\"1080p60-portrait\",IVS-GROUPS=\"portrait\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60-portrait/index-dvr.m3u8", "1080p60-Portrait")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,STABLE-VARIANT-ID=\"1080p60-portrait\",IVS-GROUPS=\"portrait\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60-portrait/index-dvr.m3u8", "1080p60-Portrait")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,STABLE-VARIANT-ID=\"1080p60-portrait\",IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60-portrait/index-dvr.m3u8", "1080p60-Portrait")]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=1920x1080,FRAME-RATE=60.000,IVS-VARIANT-SOURCE=\"source\"",
            "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/1080p60-portrait/index-dvr.m3u8", "1080p60-Portrait")]
        public static void BuildsAccurateName_WhenMissingProperties(string streamInfo, string path, string expectedName)
        {
            var m3u8 = new M3U8(new M3U8.Metadata(), [
                new M3U8.Stream(M3U8.Stream.ExtStreamInfo.Parse(streamInfo), path),
            ]);

            var qualities = VideoQualities.FromM3U8(m3u8);

            Assert.Equal(expectedName, qualities.First().Name);
        }
        // A master playlist without renditions yields null instead of throwing, so callers can report 'no qualities'.

        [Fact]
        public static void ReturnsNull_WhenNoStreamsPresent()
        {
            var m3u8 = new M3U8(new M3U8.Metadata(), []);
            var qualities = VideoQualities.FromM3U8(m3u8);
            var selectedQuality = qualities.GetQuality("");

            Assert.Null(selectedQuality);
        }

        // Builds a stream that has no resolution/framerate at all, so neither the generated name ("0p") nor the regex lookup can match it.
        private static M3U8.Stream BareStream(string stableVariantId, string ivsName, string pathSegment) =>
            new(new M3U8.Stream.ExtStreamInfo(0, 1, [], default, 0, stableVariantId, ivsName, ["landscape"], "transcode"),
                $"https://abc123.cloudfront.net/abc_streamer42_123/{pathSegment}/index-dvr.m3u8");

        /// <summary>
        /// When the name/keyword/regex lookups fail (e.g. Twitch omitted RESOLUTION and FRAME-RATE), the user's string is matched against the
        /// raw STABLE-VARIANT-ID, IVS-NAME and URL quality segment, so users can still pick qualities by Twitch's own labels.
        /// </summary>
        [Theory]
        [InlineData("hd", "hd")]
        [InlineData("HIGH DEFINITION", "hd")]
        [InlineData("hd_path", "hd")]
        [InlineData("sd", "sd")]
        [InlineData("nope", null)]
        public static void FallsBackToVariantIdIvsNameOrPathSegment(string qualityString, string? expectedVariant)
        {
            var m3u8 = new M3U8(new M3U8.Metadata(), [BareStream("hd", "High Definition", "hd_path"), BareStream("sd", "Standard", "sd_path")]);

            var qualities = VideoQualities.FromM3U8(m3u8);

            Assert.Equal(expectedVariant, qualities.GetQuality(qualityString)?.Item.StreamInfo.StableVariantId);
        }

        /// <summary>
        /// Streamers can broadcast several source renditions with the same resolution and framerate (e.g. H.264 + HEVC). Their names get
        /// "-1"/"-2" suffixes, and a plain "1080p60" request must still resolve (to the first) instead of returning nothing.
        /// </summary>
        [Fact]
        public static void PicksFirst_WhenSeveralStreamsShareResolutionAndFramerate()
        {
            var m3u8 = new M3U8(new M3U8.Metadata(), [
                new M3U8.Stream(new M3U8.Stream.ExtStreamInfo(0, 2, ["hev1"], (1920, 1080), 60, "", "", ["landscape"], "source"), "https://x.net/a/hevc/index.m3u8"),
                new M3U8.Stream(new M3U8.Stream.ExtStreamInfo(0, 1, ["avc1"], (1920, 1080), 60, "", "", ["landscape"], "source"), "https://x.net/a/avc/index.m3u8"),
            ]);

            var qualities = VideoQualities.FromM3U8(m3u8);

            Assert.Equal(["1080p60-1", "1080p60-2"], qualities.Select(x => x.ToString()));
            Assert.Equal("https://x.net/a/hevc/index.m3u8", qualities.GetQuality("1080p60")?.Path);
        }

        /// <summary>The non-generic enumerator must yield the same qualities as the generic one (used by WPF data binding).</summary>
        [Fact]
        public static void NonGenericEnumeratorYieldsAllQualities()
        {
            var m3u8 = new M3U8(new M3U8.Metadata(), [BareStream("hd", "", "hd"), BareStream("sd", "", "sd")]);

            var qualities = VideoQualities.FromM3U8(m3u8);
            var items = new List<object>();
            var enumerator = ((System.Collections.IEnumerable)qualities).GetEnumerator(); // LINQ's Cast would take the generic path
            while (enumerator.MoveNext())
            {
                items.Add(enumerator.Current);
            }

            Assert.Equal(qualities.Qualities, items);
        }

        /// <summary>A collection without a quality list returns null for best/worst instead of throwing.</summary>
        [Fact]
        public static void BestAndWorst_ReturnNull_WhenQualitiesNull()
        {
            var qualities = new M3U8VideoQualities(null!);

            Assert.Null(qualities.BestQuality());
            Assert.Null(qualities.WorstQuality());
        }
    }
}
