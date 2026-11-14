using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // ReSharper disable StringLiteralTypo
    public class IdParseTests
    {
        // Bare numeric ids of every historic length (8 to 11 digits) are accepted as VOD ids.
        [Theory]
        [InlineData("41546181")] // Oldest VODs - 8
        [InlineData("982306410")] // Old VODs - 9
        [InlineData("6834869128")] // Current VODs - 10
        [InlineData("11987163407")] // Future VODs - 11
        public void CorrectlyParsesVodId(string id)
        {
            var match = IdParse.MatchVideoId(id);

            Assert.NotNull(match);
            Assert.Equal(id, match.Value);
        }

        // VOD URLs in all shapes (/videos/, /<channel>/video/, /<channel>/v/, trailing slash) yield the numeric id.
        [Theory]
        [InlineData("https://www.twitch.tv/videos/41546181", "41546181")] // Oldest VODs - 8
        [InlineData("https://www.twitch.tv/videos/982306410", "982306410")] // Old VODs - 9
        [InlineData("https://www.twitch.tv/videos/6834869128", "6834869128")] // Current VODs - 10
        [InlineData("https://www.twitch.tv/videos/11987163407", "11987163407")] // Future VODs - 11
        [InlineData("https://www.twitch.tv/kitboga/video/2865132173", "2865132173")] // Alternate highlight URL
        [InlineData("https://www.twitch.tv/kitboga/v/2865132173", "2865132173")] // Alternate highlight URL
        [InlineData("https://www.twitch.tv/videos/4894164023/", "4894164023")]
        public void CorrectlyParsesVodLink(string link, string expectedId)
        {
            var match = IdParse.MatchVideoId(link);

            Assert.NotNull(match);
            Assert.Equal(expectedId, match.Value);
        }

        // Bare clip slugs, old-style (words) and new-style (with -suffix), are accepted as clip ids.
        [Theory]
        [InlineData("SpineyPieTwitchRPGNurturing")]
        [InlineData("FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        public void CorrectlyParsesClipId(string id)
        {
            var match = IdParse.MatchClipId(id);

            Assert.NotNull(match);
            Assert.Equal(id, match.Value);
        }

        // Clip URLs (channel/clip/<slug> and clips.twitch.tv/<slug>) yield the slug, ignoring query strings and trailing slashes.
        [Theory]
        [InlineData("https://www.twitch.tv/streamer8/clip/SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing")]
        [InlineData("https://www.twitch.tv/streamer8/clip/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        [InlineData("https://www.twitch.tv/streamer8/clip/SpineyPieTwitchRPGNurturing?featured=false&filter=clips&range=all&sort=time", "SpineyPieTwitchRPGNurturing")]
        [InlineData("https://www.twitch.tv/streamer8/clip/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf?featured=false&filter=clips&range=all&sort=time", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        [InlineData("https://clips.twitch.tv/SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing")]
        [InlineData("https://clips.twitch.tv/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        [InlineData("https://clips.twitch.tv/SpineyPieTwitchRPGNurturing/", "SpineyPieTwitchRPGNurturing")]
        [InlineData("https://clips.twitch.tv/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf/", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        public void CorrectlyParsesClipLink(string link, string expectedId)
        {
            var match = IdParse.MatchClipId(link);

            Assert.NotNull(match);
            Assert.Equal(expectedId, match.Value);
        }

        // The combined matcher accepts both bare VOD ids and clip slugs (VOD ids are tried first).
        [Theory]
        [InlineData("41546181")] // Oldest VODs - 8
        [InlineData("982306410")] // Old VODs - 9
        [InlineData("6834869128")] // Current VODs - 10
        [InlineData("11987163407")] // Future VODs - 11
        [InlineData("SpineyPieTwitchRPGNurturing")]
        [InlineData("FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        public void CorrectlyParsesVodOrClipId(string id)
        {
            var match = IdParse.MatchVideoOrClipId(id);

            Assert.NotNull(match);
            Assert.Equal(id, match.Value);
        }

        // The combined matcher handles every VOD and clip URL shape, as used by the `info` verb.
        [Theory]
        [InlineData("https://www.twitch.tv/videos/41546181", "41546181")] // Oldest VODs - 8
        [InlineData("https://www.twitch.tv/videos/982306410", "982306410")] // Old VODs - 9
        [InlineData("https://www.twitch.tv/videos/6834869128", "6834869128")] // Current VODs - 10
        [InlineData("https://www.twitch.tv/videos/11987163407", "11987163407")] // Future VODs - 11
        [InlineData("https://www.twitch.tv/kitboga/video/2865132173", "2865132173")] // Alternate highlight URL
        [InlineData("https://www.twitch.tv/kitboga/v/2865132173", "2865132173")] // Alternate VOD URL
        [InlineData("https://www.twitch.tv/videos/4894164023/", "4894164023")]
        [InlineData("https://www.twitch.tv/streamer8/clip/SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing")]
        [InlineData("https://www.twitch.tv/streamer8/clip/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        [InlineData("https://www.twitch.tv/streamer8/clip/SpineyPieTwitchRPGNurturing?featured=false&filter=clips&range=all&sort=time", "SpineyPieTwitchRPGNurturing")]
        [InlineData("https://www.twitch.tv/streamer8/clip/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf?featured=false&filter=clips&range=all&sort=time", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        [InlineData("https://clips.twitch.tv/SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing")]
        [InlineData("https://clips.twitch.tv/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        [InlineData("https://clips.twitch.tv/SpineyPieTwitchRPGNurturing/", "SpineyPieTwitchRPGNurturing")]
        [InlineData("https://clips.twitch.tv/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf/", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        public void CorrectlyParsesVodOrClipLink(string link, string expectedId)
        {
            var match = IdParse.MatchVideoOrClipId(link);

            Assert.NotNull(match);
            Assert.Equal(expectedId, match.Value);
        }

        // Free text is not mistaken for a VOD id.
        [Fact]
        public void DoesNotParseGarbageVodId()
        {
            const string GARBAGE = "SORRY FOR THE TRAFFIC NaM";

            var match = IdParse.MatchVideoId(GARBAGE);

            Assert.Null(match);
        }

        // Free text with spaces is not mistaken for a clip slug.
        [Fact]
        public void DoesNotParseGarbageClipId()
        {
            const string GARBAGE = "SORRY FOR THE TRAFFIC NaM";

            var match = IdParse.MatchClipId(GARBAGE);

            Assert.Null(match);
        }

        // Free text is rejected by the combined matcher too.
        [Fact]
        public void DoesNotParseGarbageVodOrClipId()
        {
            const string GARBAGE = "SORRY FOR THE TRAFFIC NaM";

            var match = IdParse.MatchVideoOrClipId(GARBAGE);

            Assert.Null(match);
        }
    }
}