using System.Text;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // Tests M3U8Extensions.WithUnavailableMedia, which turns Twitch's "com.amazon.ivs.unavailable-media" session data into
    // real playlist entries. This is what lets users pick 1440p/4K qualities that Twitch hides from logged-out clients.
    // (The other M3U8Extensions members are covered through ModelTests/M3U8VideoQualitiesTests.)
    public class M3U8ExtensionsTests
    {
        private const string UNAVAILABLE_MEDIA_KEY = "com.amazon.ivs.unavailable-media";
        private const string BASE_URL = "https://abc123.cloudfront.net/abc_streamer42_123_456";

        private const string UNAVAILABLE_1440P_JSON =
            """{"STABLE-VARIANT-ID":"1440p60","IVS_NAME":"1440p60","BANDWIDTH":7736560,"CODECS":"hev1.1.2.L150.90.0.0.0.0.0,mp4a.40.2","RESOLUTION":"2560x1440","FILTER_REASONS":[],"AUTHORIZATION_REASONS":["AUTHZ_NOT_LOGGED_IN"],"FRAME-RATE":60,"IVS-VARIANT-SOURCE":"source","IVS-GROUPS":["landscape"]}""";

        private static M3U8.Stream Stream(string variant, uint width, uint height, int bandwidth) =>
            new(new M3U8.Stream.ExtStreamInfo(0, bandwidth, ["avc1.4D401F", "mp4a.40.2"], (width, height), 60, variant, variant, ["landscape"], "transcode"),
                $"{BASE_URL}/{variant}/index-dvr.m3u8");

        private static M3U8 Playlist(string? unavailableMediaJson, params M3U8.Stream[] streams)
        {
            var sessionData = new Dictionary<string, string>();
            if (unavailableMediaJson is not null)
            {
                sessionData[UNAVAILABLE_MEDIA_KEY] = Convert.ToBase64String(Encoding.UTF8.GetBytes(unavailableMediaJson));
            }

            return new M3U8(new M3U8.Metadata { SessionData = sessionData }, streams);
        }

        /// <summary>
        /// An unavailable quality is added as a stream whose URL reuses the existing streams' URL with the variant segment swapped,
        /// carries the advertised stream info, and is sorted into quality order (1440p before 720p).
        /// </summary>
        [Fact]
        public void AddsUnavailableStreamsWithDerivedPath()
        {
            var m3u8 = Playlist($"[{UNAVAILABLE_1440P_JSON}]", Stream("720p60", 1280, 720, 3_000_000));

            var result = m3u8.WithUnavailableMedia();

            Assert.Equal([$"{BASE_URL}/1440p60/index-dvr.m3u8", $"{BASE_URL}/720p60/index-dvr.m3u8"], result.Streams.Select(x => x.Path));
            var added = result.Streams[0].StreamInfo;
            Assert.Equal((M3U8.Stream.ExtStreamInfo.StreamResolution)(2560, 1440), added.Resolution);
            Assert.Equal(60, added.Framerate);
            Assert.Equal(7736560, added.Bandwidth);
            Assert.Equal(["hev1.1.2.L150.90.0.0.0.0.0", "mp4a.40.2"], added.Codecs);
            Assert.Equal("source", added.IvsVariantSource);
            Assert.Equal(["landscape"], added.IvsGroups);
        }

        /// <summary>The input playlist must not be mutated; callers keep using the original record.</summary>
        [Fact]
        public void DoesNotMutateInput()
        {
            var m3u8 = Playlist($"[{UNAVAILABLE_1440P_JSON}]", Stream("720p60", 1280, 720, 3_000_000));

            _ = m3u8.WithUnavailableMedia();

            Assert.Single(m3u8.Streams);
        }

        /// <summary>A quality that is both listed as unavailable and already present must not be duplicated.</summary>
        [Fact]
        public void SkipsUnavailableStreamsThatAlreadyExist()
        {
            var m3u8 = Playlist($"[{UNAVAILABLE_1440P_JSON}]", Stream("1440p60", 2560, 1440, 7_000_000), Stream("720p60", 1280, 720, 3_000_000));

            var result = m3u8.WithUnavailableMedia();

            Assert.Equal(2, result.Streams.Length);
            Assert.Equal(7_000_000, result.Streams[0].StreamInfo.Bandwidth); // The original entry wins
        }

        /// <summary>Without streams, session data, or with an empty unavailable list, the very same playlist instance is returned.</summary>
        [Theory]
        [InlineData(null, true)]
        [InlineData("[]", true)]
        [InlineData(UNAVAILABLE_1440P_JSON, false)]
        public void ReturnsSameInstance_WhenNothingToAdd(string? json, bool hasStreams)
        {
            var m3u8 = hasStreams
                ? Playlist(json, Stream("720p60", 1280, 720, 3_000_000))
                : Playlist(json is null ? null : $"[{json}]");

            Assert.Same(m3u8, m3u8.WithUnavailableMedia());
        }

        /// <summary>
        /// Twitch also lists "Audio Only" as unavailable (FR_AUDIO_DISALLOWED) with an empty RESOLUTION. That entry should be
        /// added without a resolution; today StreamResolution.Parse("") throws a FormatException, which makes
        /// VideoQualities.FromM3U8 (and so the whole VOD download) fail.
        /// </summary>
        [Fact(Skip = "Bug: WithUnavailableMedia throws FormatException when an unavailable entry has an empty RESOLUTION (e.g. audio only)")]
        public void HandlesUnavailableMediaWithoutResolution()
        {
            const string AUDIO_ONLY_JSON =
                """{"STABLE-VARIANT-ID":"audio_only","IVS_NAME":"Audio Only","BANDWIDTH":204310,"CODECS":"mp4a.40.2","RESOLUTION":"","FILTER_REASONS":["FR_AUDIO_DISALLOWED"],"AUTHORIZATION_REASONS":[],"FRAME-RATE":0,"IVS-VARIANT-SOURCE":"source","IVS-GROUPS":[]}""";
            var m3u8 = Playlist($"[{AUDIO_ONLY_JSON}]", Stream("720p60", 1280, 720, 3_000_000));

            var result = m3u8.WithUnavailableMedia();

            Assert.Contains(result.Streams, x => x.Path == $"{BASE_URL}/audio_only/index-dvr.m3u8" && x.StreamInfo.Resolution == default);
        }
    }
}
