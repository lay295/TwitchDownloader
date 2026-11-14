using System.Text.Json;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects.Gql;
using static TwitchDownloaderCore.Tests.TwitchHelperTests.TwitchHelperTestUtil;

namespace TwitchDownloaderCore.Tests.TwitchHelperTests
{
    // Tests TwitchHelper's chapter helpers (GetVideoChapters, GetOrGenerateVideoChapters, GenerateClipChapter).
    // Chapters are written into video metadata and chat JSON; Twitch's chapter data has known quirks (missing for
    // single-game VODs, negative or zero durations) that these helpers normalize, so regressions produce broken MP4 chapter tables.
    [Collection(StaticHttpClientCollection.NAME)]
    public class TwitchHelperChapterTests
    {
        private static FakeHttpMessageHandler JsonHandler(string json) => new((_, _) => FakeHttpMessageHandler.Json(json));

        private static string Moment(int position, int duration, string game = "Just Chatting") =>
            "{\"node\":{\"moments\":{\"edges\":[]},\"id\":\"m" + position + "\",\"durationMilliseconds\":" + duration + ",\"positionMilliseconds\":" + position
            + ",\"type\":\"GAME_CHANGE\",\"description\":\"" + game + "\",\"subDescription\":\"\",\"thumbnailURL\":\"\",\"details\":{\"game\":{\"id\":\"509658\",\"displayName\":\""
            + game + "\",\"boxArtURL\":\"https://x/40x53.jpg\"}},\"video\":{\"id\":\"123\",\"lengthSeconds\":600}}}";

        private static string ChaptersJson(params string[] moments) =>
            "{\"data\":{\"video\":{\"id\":\"123\",\"moments\":{\"edges\":[" + string.Join(",", moments) + "]}}}}";

        // The chapter request is a persisted query keyed by video id; chapter type/game details must be parsed (type maps to _type).
        [Fact]
        public async Task GetVideoChapters_PostsPersistedQueryAndParsesMoments()
        {
            var handler = JsonHandler(ChaptersJson(Moment(0, 300000), Moment(300000, 300000, "Minecraft")));
            using var _ = UseHandler(handler);

            var response = await TwitchHelper.GetVideoChapters(123);

            var request = Assert.Single(handler.Requests);
            Assert.Equal(GQL_URL, request.Uri.AbsoluteUri);
            Assert.Equal(WEB_CLIENT_ID, request.Headers["Client-ID"]);
            var body = JsonDocument.Parse(request.Body!).RootElement;
            Assert.Equal("VideoPlayer_ChapterSelectButtonVideo", body.GetProperty("operationName").GetString());
            Assert.Equal("123", body.GetProperty("variables").GetProperty("videoID").GetString());

            var edges = response.data.video.moments.edges;
            Assert.Equal(2, edges.Count);
            Assert.Equal("GAME_CHANGE", edges[1].node._type);
            Assert.Equal("Minecraft", edges[1].node.details.game.displayName);
            Assert.Equal((300000, 300000), (edges[1].node.positionMilliseconds, edges[1].node.durationMilliseconds));
        }

        // Twitch sometimes reports negative chapter durations; they must be clamped to 0 instead of producing negative metadata.
        // The last chapter of a still-airing VOD has duration 0 and must be filled in up to the end of the video.
        [Fact]
        public async Task GetVideoChapters_ClampsNegativeDurationsAndFillsOpenEndedLastChapter()
        {
            using var _ = UseHandler(JsonHandler(ChaptersJson(Moment(0, -5000), Moment(120000, 0))));

            var response = await TwitchHelper.GetVideoChapters(123);

            var edges = response.data.video.moments.edges;
            Assert.Equal(0, edges[0].node.durationMilliseconds);
            Assert.Equal(600 * 1000 - 120000, edges[1].node.durationMilliseconds);
        }

        // A null video or null moments (seen for VODs without chapter data) must be normalized to an empty edge list so callers can iterate safely.
        [Theory]
        [InlineData("""{"data":{"video":null}}""")]
        [InlineData("""{"data":{"video":{"id":"123","moments":null}}}""")]
        public async Task GetVideoChapters_MissingVideoOrMoments_ReturnsEmptyEdges(string json)
        {
            using var _ = UseHandler(JsonHandler(json));

            var response = await TwitchHelper.GetVideoChapters(123);

            Assert.Equal("123", response.data.video.id);
            Assert.Empty(response.data.video.moments.edges);
        }

        // Single-game VODs have no chapter edges; a single chapter spanning the whole video must be generated from the video info,
        // with the box art template sized to 40x53 so it can be downloaded directly.
        [Fact]
        public async Task GetOrGenerateVideoChapters_NoChapters_GeneratesWholeVideoChapter()
        {
            using var _ = UseHandler(JsonHandler(ChaptersJson()));
            var videoInfo = new VideoInfo { lengthSeconds = 3600, game = new Game { id = "509658", displayName = "Just Chatting", boxArtURL = "https://x/509658-{width}x{height}.jpg" } };

            var response = await TwitchHelper.GetOrGenerateVideoChapters(123, videoInfo, new RecordingTaskProgress());

            var node = Assert.Single(response.data.video.moments.edges).node;
            Assert.Equal("GAME_CHANGE", node._type);
            Assert.Equal((0, 3600 * 1000), (node.positionMilliseconds, node.durationMilliseconds));
            Assert.Equal("Just Chatting", node.description);
            Assert.Equal(("509658", "Just Chatting"), (node.details.game.id, node.details.game.displayName));
            Assert.Equal("https://x/509658-40x53.jpg", node.details.game.boxArtURL);
        }

        // Existing chapters must be passed through untouched (no extra generated chapter).
        [Fact]
        public async Task GetOrGenerateVideoChapters_WithChapters_ReturnsThemUnchanged()
        {
            using var _ = UseHandler(JsonHandler(ChaptersJson(Moment(0, 1000), Moment(1000, 2000))));

            var response = await TwitchHelper.GetOrGenerateVideoChapters(123, new VideoInfo { lengthSeconds = 3 }, new RecordingTaskProgress());

            Assert.Equal(["m0", "m1000"], response.data.video.moments.edges.Select(e => e.node.id));
        }

        // A response without "data" makes GetVideoChapters throw NullReferenceException; that must be logged and replaced by a generated chapter
        // (with "Unknown" game placeholders when the video has no game) so a chapter glitch never aborts a VOD download.
        [Fact]
        public async Task GetOrGenerateVideoChapters_NullData_LogsErrorAndGeneratesUnknownGameChapter()
        {
            using var _ = UseHandler(JsonHandler("""{"errors":[{"message":"service timeout"}]}"""));
            var progress = new RecordingTaskProgress();

            var response = await TwitchHelper.GetOrGenerateVideoChapters(123, new VideoInfo { lengthSeconds = 10, game = null }, progress);

            Assert.Contains(progress.Errors, e => e.StartsWith("Null object reference while fetching chapters"));
            Assert.Equal("123", response.data.video.id);
            var node = Assert.Single(response.data.video.moments.edges).node;
            Assert.Equal(10000, node.durationMilliseconds);
            Assert.Equal(("-1", "Unknown", "Unknown", ""), (node.details.game.id, node.details.game.displayName, node.description, node.details.game.boxArtURL));
        }

        // Clips always get exactly one chapter covering the whole clip, using the clip's game (or the Unknown placeholders).
        [Fact]
        public void GenerateClipChapter_UsesClipDurationAndGame()
        {
            var withGame = TwitchHelper.GenerateClipChapter(new ShareClipRenderStatusClip
            {
                durationSeconds = 25,
                game = new ShareClipRenderStatusGame { id = "32982", displayName = "Grand Theft Auto V", boxArtURL = "https://x/32982-{width}x{height}.jpg" }
            });
            var withoutGame = TwitchHelper.GenerateClipChapter(new ShareClipRenderStatusClip { durationSeconds = 7 });

            Assert.Equal((0, 25000), (withGame.node.positionMilliseconds, withGame.node.durationMilliseconds));
            Assert.Equal(("32982", "Grand Theft Auto V", "https://x/32982-40x53.jpg"), (withGame.node.details.game.id, withGame.node.details.game.displayName, withGame.node.details.game.boxArtURL));
            Assert.Equal(7000, withoutGame.node.durationMilliseconds);
            Assert.Equal(("-1", "Unknown"), (withoutGame.node.details.game.id, withoutGame.node.details.game.displayName));
        }
    }
}
