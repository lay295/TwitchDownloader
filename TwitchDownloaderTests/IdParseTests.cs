using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderTests
{
    // ReSharper disable StringLiteralTypo
    public class IdParseTests
    {
        [Theory]
        [InlineData("9d02cf06-52e8-4023-abd5-16b21d143867", VideoPlatform.Kick)]
        [InlineData("79200ecf-c46d-4d74-b3ad-d06a7e0e5d8d", VideoPlatform.Kick)]
        [InlineData("41546181", VideoPlatform.Twitch)] // Oldest VODs - 8
        [InlineData("982306410", VideoPlatform.Twitch)] // Old VODs - 9
        [InlineData("6834869128", VideoPlatform.Twitch)] // Current VODs - 10
        [InlineData("11987163407", VideoPlatform.Twitch)] // Future VODs - 11
        public void CorrectlyParsesVodId(string id, VideoPlatform expectedPlatform)
        {
            var success = IdParse.TryParseVod(id, out var videoPlatform, out var videoId);

            Assert.True(success);
            Assert.Equal(expectedPlatform, videoPlatform);
            Assert.Equal(id, videoId);
        }

        [Theory]
        [InlineData("https://kick.com/video/9d02cf06-52e8-4023-abd5-16b21d143867", "9d02cf06-52e8-4023-abd5-16b21d143867", VideoPlatform.Kick)]
        [InlineData("https://kick.com/video/79200ecf-c46d-4d74-b3ad-d06a7e0e5d8d", "79200ecf-c46d-4d74-b3ad-d06a7e0e5d8d", VideoPlatform.Kick)]
        [InlineData("https://www.twitch.tv/videos/41546181", "41546181", VideoPlatform.Twitch)] // Oldest VODs - 8
        [InlineData("https://www.twitch.tv/videos/982306410", "982306410", VideoPlatform.Twitch)] // Old VODs - 9
        [InlineData("https://www.twitch.tv/videos/6834869128", "6834869128", VideoPlatform.Twitch)] // Current VODs - 10
        [InlineData("https://www.twitch.tv/videos/11987163407", "11987163407", VideoPlatform.Twitch)] // Future VODs - 11
        [InlineData("https://www.twitch.tv/kitboga/video/2865132173", "2865132173", VideoPlatform.Twitch)] // Alternate highlight URL
        public void CorrectlyParsesVodLink(string link, string expectedId, VideoPlatform expectedPlatform)
        {
            var success = IdParse.TryParseVod(link, out var videoPlatform, out var videoId);

            Assert.True(success);
            Assert.Equal(expectedPlatform, videoPlatform);
            Assert.Equal(expectedId, videoId);
        }

        [Theory]
        [InlineData("clip_F786F81SF785610534215S23D0", VideoPlatform.Kick)]
        [InlineData("SpineyPieTwitchRPGNurturing", VideoPlatform.Twitch)]
        [InlineData("FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", VideoPlatform.Twitch)]
        public void CorrectlyParsesClipId(string id, VideoPlatform expectedPlatform)
        {
            var success = IdParse.TryParseClip(id, out var videoPlatform, out var videoId);

            Assert.True(success);
            Assert.Equal(expectedPlatform, videoPlatform);
            Assert.Equal(id, videoId);
        }

        [Theory]
        [InlineData("https://kick.com/streamer8?clip=clip_F786F81SF785610534215S23D0", "clip_F786F81SF785610534215S23D0", VideoPlatform.Kick)]
        [InlineData("https://www.twitch.tv/streamer8/clip/SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing", VideoPlatform.Twitch)]
        [InlineData("https://www.twitch.tv/streamer8/clip/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", VideoPlatform.Twitch)]
        [InlineData("https://www.twitch.tv/streamer8/clip/SpineyPieTwitchRPGNurturing?featured=false&filter=clips&range=all&sort=time", "SpineyPieTwitchRPGNurturing", VideoPlatform.Twitch)]
        [InlineData("https://www.twitch.tv/streamer8/clip/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf?featured=false&filter=clips&range=all&sort=time", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", VideoPlatform.Twitch)]
        [InlineData("https://clips.twitch.tv/SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing", VideoPlatform.Twitch)]
        [InlineData("https://clips.twitch.tv/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", VideoPlatform.Twitch)]
        public void CorrectlyParsesClipLink(string link, string expectedId, VideoPlatform expectedPlatform)
        {
            var success = IdParse.TryParseClip(link, out var videoPlatform, out var videoId);

            Assert.True(success);
            Assert.Equal(expectedPlatform, videoPlatform);
            Assert.Equal(expectedId, videoId);
        }

        [Theory]
        [InlineData("9d02cf06-52e8-4023-abd5-16b21d143867", VideoType.Video, VideoPlatform.Kick)]
        [InlineData("79200ecf-c46d-4d74-b3ad-d06a7e0e5d8d", VideoType.Video, VideoPlatform.Kick)]
        [InlineData("41546181", VideoType.Video, VideoPlatform.Twitch)] // Oldest VODs - 8
        [InlineData("982306410", VideoType.Video, VideoPlatform.Twitch)] // Old VODs - 9
        [InlineData("6834869128", VideoType.Video, VideoPlatform.Twitch)] // Current VODs - 10
        [InlineData("11987163407", VideoType.Video, VideoPlatform.Twitch)] // Future VODs - 11
        [InlineData("clip_F786F81SF785610534215S23D0", VideoType.Clip, VideoPlatform.Kick)]
        [InlineData("SpineyPieTwitchRPGNurturing", VideoType.Clip, VideoPlatform.Twitch)]
        [InlineData("FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", VideoType.Clip, VideoPlatform.Twitch)]
        public void CorrectlyParsesVodOrClipId(string id, VideoType expectedType, VideoPlatform expectedPlatform)
        {
            var success = IdParse.TryParseVideoOrClipId(id, out var videoPlatform, out var videoType, out var videoId);

            Assert.True(success);
            Assert.Equal(expectedType, videoType);
            Assert.Equal(expectedPlatform, videoPlatform);
            Assert.Equal(id, videoId);
        }

        [Theory]
        [InlineData("https://kick.com/video/9d02cf06-52e8-4023-abd5-16b21d143867", "9d02cf06-52e8-4023-abd5-16b21d143867", VideoType.Video, VideoPlatform.Kick)]
        [InlineData("https://kick.com/video/79200ecf-c46d-4d74-b3ad-d06a7e0e5d8d", "79200ecf-c46d-4d74-b3ad-d06a7e0e5d8d", VideoType.Video, VideoPlatform.Kick)]
        [InlineData("https://www.twitch.tv/videos/41546181", "41546181", VideoType.Video, VideoPlatform.Twitch)] // Oldest VODs - 8
        [InlineData("https://www.twitch.tv/videos/982306410", "982306410", VideoType.Video, VideoPlatform.Twitch)] // Old VODs - 9
        [InlineData("https://www.twitch.tv/videos/6834869128", "6834869128", VideoType.Video, VideoPlatform.Twitch)] // Current VODs - 10
        [InlineData("https://www.twitch.tv/videos/11987163407", "11987163407", VideoType.Video, VideoPlatform.Twitch)] // Future VODs - 11
        [InlineData("https://www.twitch.tv/kitboga/video/2865132173", "2865132173", VideoType.Video, VideoPlatform.Twitch)] // Alternate highlight URL
        [InlineData("https://kick.com/streamer8?clip=clip_F786F81SF785610534215S23D0", "clip_F786F81SF785610534215S23D0", VideoType.Clip, VideoPlatform.Kick)]
        [InlineData("https://www.twitch.tv/streamer8/clip/SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing", VideoType.Clip, VideoPlatform.Twitch)]
        [InlineData("https://www.twitch.tv/streamer8/clip/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", VideoType.Clip, VideoPlatform.Twitch)]
        [InlineData("https://www.twitch.tv/streamer8/clip/SpineyPieTwitchRPGNurturing?featured=false&filter=clips&range=all&sort=time", "SpineyPieTwitchRPGNurturing", VideoType.Clip, VideoPlatform.Twitch)]
        [InlineData("https://www.twitch.tv/streamer8/clip/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf?featured=false&filter=clips&range=all&sort=time", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", VideoType.Clip, VideoPlatform.Twitch)]
        [InlineData("https://clips.twitch.tv/SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing", VideoType.Clip, VideoPlatform.Twitch)]
        [InlineData("https://clips.twitch.tv/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", VideoType.Clip, VideoPlatform.Twitch)]
        public void CorrectlyParsesVodOrClipLink(string link, string expectedId, VideoType expectedType, VideoPlatform expectedPlatform)
        {
            var success = IdParse.TryParseVideoOrClipId(link, out var videoPlatform, out var videoType, out var videoId);

            Assert.True(success);
            Assert.Equal(expectedType, videoType);
            Assert.Equal(expectedPlatform, videoPlatform);
            Assert.Equal(expectedId, videoId);
        }

        [Fact]
        public void DoesNotParseGarbageVodId()
        {
            const string GARBAGE = "SORRY FOR THE TRAFFIC NaM";
            const VideoPlatform EXPECTED_PLATFORM = VideoPlatform.Unknown;

            var success = IdParse.TryParseVod(GARBAGE, out var videoPlatform, out var videoId);

            Assert.False(success);
            Assert.Equal(EXPECTED_PLATFORM, videoPlatform);
            Assert.NotEqual(GARBAGE, videoId);
        }

        [Fact]
        public void DoesNotParseGarbageClipId()
        {
            const string GARBAGE = "SORRY FOR THE TRAFFIC NaM";
            const VideoPlatform EXPECTED_PLATFORM = VideoPlatform.Unknown;

            var success = IdParse.TryParseClip(GARBAGE, out var videoPlatform, out var videoId);

            Assert.False(success);
            Assert.Equal(EXPECTED_PLATFORM, videoPlatform);
            Assert.NotEqual(GARBAGE, videoId);
        }

        [Fact]
        public void DoesNotParseGarbageVodOrClipId()
        {
            const string GARBAGE = "SORRY FOR THE TRAFFIC NaM";
            const VideoType EXPECTED_TYPE = VideoType.Unknown;
            const VideoPlatform EXPECTED_PLATFORM = VideoPlatform.Unknown;

            var success = IdParse.TryParseVideoOrClipId(GARBAGE, out var videoPlatform, out var videoType, out var videoId);

            Assert.False(success);
            Assert.Equal(EXPECTED_TYPE, videoType);
            Assert.Equal(EXPECTED_PLATFORM, videoPlatform);
            Assert.NotEqual(GARBAGE, videoId);
        }
    }
}