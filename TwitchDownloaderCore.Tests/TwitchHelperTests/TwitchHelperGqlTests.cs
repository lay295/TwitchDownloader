using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using TwitchDownloaderCore.Tests.TestHelpers;
using static TwitchDownloaderCore.Tests.TwitchHelperTests.TwitchHelperTestUtil;

namespace TwitchDownloaderCore.Tests.TwitchHelperTests
{
    // Tests the Twitch GQL / usher request wrappers in TwitchHelper (video, token, playlist, clip, search and user queries).
    // Every download mode and the WPF/CLI info panels start with one of these calls, so a malformed hand-built query body,
    // a wrong Client-ID or a response-parsing regression breaks the app before anything is downloaded. All traffic is faked.
    [Collection(StaticHttpClientCollection.NAME)]
    public class TwitchHelperGqlTests
    {
        private const string SEARCH_CLIENT_ID = "kd1unb4b3q4t58fwlpcbzcbnm76a8fp";

        private static FakeHttpMessageHandler JsonHandler(string json) => new((_, _) => FakeHttpMessageHandler.Json(json));

        // Asserts the request is a GQL POST with the expected Client-ID and returns its (valid JSON) body.
        private static JsonElement AssertGqlPost(FakeHttpMessageHandler handler, string clientId = WEB_CLIENT_ID)
        {
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(GQL_URL, request.Uri.AbsoluteUri);
            Assert.Equal(clientId, request.Headers["Client-ID"]);
            return JsonDocument.Parse(request.Body!).RootElement;
        }

        private static string Query(JsonElement body) => body.GetProperty("query").GetString()!;

        private const string VIDEO_JSON = """
            {"data":{"video":{"title":"Test stream","thumbnailURLs":["https://static-cdn.jtvnw.net/cf_vods/d1m7jfoe9zdc1j/abc/thumb/thumb0-320x180.jpg"],
            "createdAt":"2024-05-01T18:00:00Z","lengthSeconds":3725,"owner":{"id":"12345","displayName":"Streamer","login":"streamer"},
            "viewCount":4321,"game":{"id":"509658","displayName":"Just Chatting","boxArtURL":"https://static-cdn.jtvnw.net/ttv-boxart/509658-{width}x{height}.jpg"},
            "description":"A description","status":"RECORDED"}},"extensions":{"durationMilliseconds":52,"requestID":"01J0000000000000000000000"}}
            """;

        // GetVideoInfo must embed the VOD id in the query and map every field the info panels and filename templates use.
        [Fact]
        public async Task GetVideoInfo_PostsVideoQueryAndParsesResponse()
        {
            var handler = JsonHandler(VIDEO_JSON);
            using var _ = UseHandler(handler);

            var response = await TwitchHelper.GetVideoInfo(2123456789);

            Assert.Contains("video(id:\"2123456789\")", Query(AssertGqlPost(handler)));
            var video = response.data.video;
            Assert.Equal("Test stream", video.title);
            Assert.Equal(new DateTime(2024, 5, 1, 18, 0, 0, DateTimeKind.Utc), video.createdAt.ToUniversalTime());
            Assert.Equal(3725, video.lengthSeconds);
            Assert.Equal(("12345", "Streamer", "streamer"), (video.owner.id, video.owner.displayName, video.owner.login));
            Assert.Equal(4321, video.viewCount);
            Assert.Equal("Just Chatting", video.game.displayName);
            Assert.Single(video.thumbnailURLs);
            Assert.Equal("RECORDED", video.status);
            Assert.Equal("01J0000000000000000000000", response.extensions.requestID);
        }

        // Twitch answers deleted/unknown VODs with "video": null; callers detect that rather than catching an exception.
        [Fact]
        public async Task GetVideoInfo_DeletedVideo_ReturnsNullVideo()
        {
            using var _ = UseHandler(JsonHandler("""{"data":{"video":null},"extensions":{}}"""));

            var response = await TwitchHelper.GetVideoInfo(1);

            Assert.Null(response.data.video);
        }

        // Non-success responses must surface as HttpRequestException (with the status) instead of being parsed as data.
        [Fact]
        public async Task GetVideoInfo_HttpError_ThrowsHttpRequestException()
        {
            using var _ = UseHandler(new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => TwitchHelper.GetVideoInfo(1));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        }

        // A garbage (e.g. HTML error page) body must fail loudly as a JSON error, not return a half-filled object.
        [Fact]
        public async Task GetVideoInfo_MalformedJson_ThrowsJsonException()
        {
            using var _ = UseHandler(new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Json("<html>oops</html>")));

            await Assert.ThrowsAnyAsync<JsonException>(() => TwitchHelper.GetVideoInfo(1));
        }

        // The playback token request must ask for a VOD (not live) token for this id, and only send Authorization when an OAuth token was given
        // (an empty "OAuth " header makes Twitch reject the request).
        [Theory]
        [InlineData("abcdef123456", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(null, false)]
        public async Task GetVideoToken_SendsVodIdAndOptionalOAuthHeader(string? authToken, bool expectAuthHeader)
        {
            var handler = JsonHandler("""{"data":{"videoPlaybackAccessToken":{"value":"{\"vod_id\":123}","signature":"0123abcd","__typename":"PlaybackAccessToken"}}}""");
            using var _ = UseHandler(handler);

            var response = await TwitchHelper.GetVideoToken(123, authToken!);

            var body = AssertGqlPost(handler);
            Assert.Equal("PlaybackAccessToken_Template", body.GetProperty("operationName").GetString());
            var variables = body.GetProperty("variables");
            Assert.Equal("123", variables.GetProperty("vodID").GetString());
            Assert.True(variables.GetProperty("isVod").GetBoolean());
            Assert.False(variables.GetProperty("isLive").GetBoolean());

            var headers = Assert.Single(handler.Requests).Headers;
            Assert.Equal(expectAuthHeader, headers.ContainsKey("Authorization"));
            if (expectAuthHeader) Assert.Equal($"OAuth {authToken}", headers["Authorization"]);

            Assert.Equal("{\"vod_id\":123}", response.data.videoPlaybackAccessToken.value);
            Assert.Equal("0123abcd", response.data.videoPlaybackAccessToken.signature);
        }

        // The usher playlist request must carry sig/token and the codec/source flags (dropping av1/h265 or allow_source hides qualities).
        [Fact]
        public async Task GetVideoPlaylist_RequestsUsherPlaylistAndReturnsBody()
        {
            var handler = new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Text("#EXTM3U\n#EXT-X-TWITCH-INFO:ORIGIN=\"s3\""));
            using var _ = UseHandler(handler);

            var playlist = await TwitchHelper.GetVideoPlaylist(123, "TOKEN", "SIG");

            Assert.StartsWith("#EXTM3U", playlist);
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("usher.ttvnw.net", request.Uri.Host);
            Assert.Equal("/vod/v2/123.m3u8", request.Uri.AbsolutePath);
            Assert.Contains("sig=SIG", request.Uri.Query);
            Assert.Contains("token=TOKEN", request.Uri.Query);
            Assert.Contains("allow_source=true", request.Uri.Query);
            Assert.Contains("supported_codecs=av1,h265,h264", request.Uri.Query);
        }

        // Sub-only VODs return 403 with a recognizable error; that body must be returned (not thrown) so callers can explain it to the user.
        [Theory]
        [InlineData("""[{"error":"Unauthorized","error_code":"vod_manifest_restricted","type":"error"}]""")]
        [InlineData("""[{"error":"Unauthorized","error_code":"unauthorized_entitlements","type":"error"}]""")]
        public async Task GetVideoPlaylist_RestrictedForbidden_ReturnsErrorBody(string errorBody)
        {
            using var _ = UseHandler(new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Json(errorBody, HttpStatusCode.Forbidden)));

            var result = await TwitchHelper.GetVideoPlaylist(123, "TOKEN", "SIG");

            Assert.Equal(errorBody, result);
        }

        // Any other 403 is a real failure and must throw rather than be treated as a playlist.
        [Fact]
        public async Task GetVideoPlaylist_OtherForbidden_Throws()
        {
            using var _ = UseHandler(new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Json("""[{"error_code":"something_else"}]""", HttpStatusCode.Forbidden)));

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => TwitchHelper.GetVideoPlaylist(123, "TOKEN", "SIG"));

            Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        }

        // When TLS to usher fails (AuthenticationException anywhere in the inner-exception chain, e.g. old OS TLS stacks),
        // the request must be retried once through the TwitchDownloader proxy with the same path and query.
        [Fact]
        public async Task GetVideoPlaylist_TlsFailure_RetriesThroughProxy()
        {
            var handler = new FakeHttpMessageHandler((req, _) => req.RequestUri!.Host == "usher.ttvnw.net"
                ? throw new HttpRequestException("The SSL connection could not be established, see inner exception.", new IOException("wrapper", new AuthenticationException("TLS handshake failed")))
                : FakeHttpMessageHandler.Text("#EXTM3U proxied"));
            using var _ = UseHandler(handler);

            var playlist = await TwitchHelper.GetVideoPlaylist(123, "TOKEN", "SIG");

            Assert.Equal("#EXTM3U proxied", playlist);
            var requests = handler.Requests.ToArray();
            Assert.Equal(2, requests.Length);
            Assert.Equal("twitch-downloader-proxy.twitcharchives.workers.dev", requests[1].Uri.Host);
            Assert.Equal("/v2/123.m3u8", requests[1].Uri.AbsolutePath);
            Assert.Equal(requests[0].Uri.Query, requests[1].Uri.Query);
        }

        // Non-TLS network errors must propagate unchanged and must NOT be silently retried through the proxy.
        [Fact]
        public async Task GetVideoPlaylist_NonTlsFailure_RethrowsWithoutProxyRetry()
        {
            var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("No such host is known."));
            using var _ = UseHandler(handler);

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => TwitchHelper.GetVideoPlaylist(123, "TOKEN", "SIG"));

            Assert.Equal("No such host is known.", ex.Message);
            Assert.Single(handler.Requests);
        }

        // GetClipInfo must query by slug and cope with clips whose source VOD was deleted (video/videoOffsetSeconds null).
        [Fact]
        public async Task GetClipInfo_PostsSlugQueryAndParsesResponse()
        {
            var handler = JsonHandler("""
                {"data":{"clip":{"title":"Nice clip","thumbnailURL":"https://clips-media-assets2.twitch.tv/x-preview-480x272.jpg","createdAt":"2024-02-03T04:05:06Z",
                "curator":{"id":"999","displayName":"Clipper","login":"clipper"},"durationSeconds":30,"broadcaster":{"id":"12345","displayName":"Streamer","login":"streamer"},
                "videoOffsetSeconds":null,"video":null,"viewCount":77,"game":{"id":"32982","displayName":"Grand Theft Auto V","boxArtURL":"https://x/{width}x{height}.jpg"}}}}
                """);
            using var _ = UseHandler(handler);

            var response = await TwitchHelper.GetClipInfo("SpineyPieTwitchRPGNurturing");

            Assert.Contains("clip(slug:\"SpineyPieTwitchRPGNurturing\")", Query(AssertGqlPost(handler)));
            var clip = response.data.clip;
            Assert.Equal("Nice clip", clip.title);
            Assert.Equal("clipper", clip.curator.login);
            Assert.Equal("streamer", clip.broadcaster.login);
            Assert.Equal(30, clip.durationSeconds);
            Assert.Null(clip.videoOffsetSeconds);
            Assert.Null(clip.video);
            Assert.Equal("32982", clip.game.id);
        }

        // Clip qualities must come back sorted best-first (resolution, then framerate) because the downloader picks index 0 by default.
        [Fact]
        public async Task GetClipLinks_UsesPersistedQueryAndSortsQualitiesBestFirst()
        {
            var handler = JsonHandler("""
                {"data":{"clip":{"id":"1","playbackAccessToken":{"signature":"sig","value":"tok"},"videoQualities":[
                {"frameRate":30,"quality":"360","sourceURL":"https://x/360.mp4"},
                {"frameRate":60,"quality":"1080","sourceURL":"https://x/1080p60.mp4"},
                {"frameRate":30,"quality":"720","sourceURL":"https://x/720.mp4"},
                {"frameRate":30,"quality":"1080","sourceURL":"https://x/1080p30.mp4"}]}}}
                """);
            using var _ = UseHandler(handler);

            var response = await TwitchHelper.GetClipLinks("SomeSlug");

            var body = AssertGqlPost(handler);
            Assert.Equal("VideoAccessToken_Clip", body.GetProperty("operationName").GetString());
            Assert.Equal("SomeSlug", body.GetProperty("variables").GetProperty("slug").GetString());
            Assert.True(body.GetProperty("extensions").GetProperty("persistedQuery").TryGetProperty("sha256Hash", out var _));
            Assert.Equal(["1080p60", "1080p30", "720", "360"], response.data.clip.videoQualities.Select(q => Path.GetFileNameWithoutExtension(q.sourceURL)));
            Assert.Equal("sig", response.data.clip.playbackAccessToken.signature);
        }

        // Clips with no playable qualities (null/empty) must not crash the sort.
        [Theory]
        [InlineData("null")]
        [InlineData("[]")]
        public async Task GetClipLinks_NoQualities_DoesNotThrow(string qualities)
        {
            using var _ = UseHandler(JsonHandler("""{"data":{"clip":{"id":"1","playbackAccessToken":null,"videoQualities":""" + qualities + "}}}"));

            var response = await TwitchHelper.GetClipLinks("SomeSlug");

            Assert.True(response.data.clip.videoQualities is null or { Length: 0 });
        }

        // Each clip asset's qualities must be sorted best-first, the same contract as GetClipLinks, for the newer render-status API.
        [Fact]
        public async Task GetShareClipRenderStatus_SortsEveryAssetsQualities()
        {
            var handler = JsonHandler("""
                {"data":{"clip":{"id":"1","slug":"SomeSlug","title":"t","durationSeconds":12,"assets":[
                {"id":"a1","type":"SOURCE","videoQualities":[{"quality":"480","frameRate":30,"sourceURL":"480"},{"quality":"1080","frameRate":60,"sourceURL":"1080"}]},
                {"id":"a2","type":"RECOMPOSED","videoQualities":[{"quality":"720","frameRate":30,"sourceURL":"720"},{"quality":"720","frameRate":60,"sourceURL":"720p60"}]}],
                "game":{"id":"1","displayName":"G","boxArtURL":"b"}}}}
                """);
            using var _ = UseHandler(handler);

            var response = await TwitchHelper.GetShareClipRenderStatus("SomeSlug");

            var body = AssertGqlPost(handler);
            Assert.Equal("ShareClipRenderStatus", body.GetProperty("operationName").GetString());
            Assert.Equal("SomeSlug", body.GetProperty("variables").GetProperty("slug").GetString());
            Assert.Equal(["1080", "480"], response.data.clip.assets[0].videoQualities.Select(q => q.sourceURL));
            Assert.Equal(["720p60", "720"], response.data.clip.assets[1].videoQualities.Select(q => q.sourceURL));
            Assert.Equal(12, response.data.clip.durationSeconds);
        }

        // A clip still rendering has no assets yet; that must not throw.
        [Fact]
        public async Task GetShareClipRenderStatus_NullAssets_DoesNotThrow()
        {
            using var _ = UseHandler(JsonHandler("""{"data":{"clip":{"id":"1","assets":null}}}"""));

            var response = await TwitchHelper.GetShareClipRenderStatus("SomeSlug");

            Assert.Null(response.data.clip.assets);
        }

        // The VOD search query is hand-concatenated: the optional cursor and type arguments must only appear when given,
        // and it must use the search Client-ID. Paging info drives the "next page" button.
        [Theory]
        [InlineData("", "", 50, "videos(first: 50)")]
        [InlineData("CURSOR1", "", 10, "videos(first: 10,after:\"CURSOR1\")")]
        [InlineData("", "ARCHIVE", 25, "videos(first: 25,type:ARCHIVE)")]
        [InlineData("CURSOR1", "HIGHLIGHT", 5, "videos(first: 5,after:\"CURSOR1\",type:HIGHLIGHT)")]
        public async Task GetGqlVideos_BuildsPagedQueryAndParsesEdges(string cursor, string type, int limit, string expectedArgs)
        {
            var handler = JsonHandler("""
                {"data":{"user":{"videos":{"edges":[{"node":{"title":"Vod 1","id":"111","lengthSeconds":60,"previewThumbnailURL":"p","createdAt":"2024-01-01T00:00:00Z",
                "viewCount":5,"game":{"id":"1","displayName":"G"}},"cursor":"c1"}],"pageInfo":{"hasNextPage":true,"hasPreviousPage":false},"totalCount":42}}}}
                """);
            using var _ = UseHandler(handler);

            var response = await TwitchHelper.GetGqlVideos("streamer", cursor, limit, type);

            var query = Query(AssertGqlPost(handler, SEARCH_CLIENT_ID));
            Assert.Contains("user(login:\"streamer\")", query);
            Assert.Contains(expectedArgs, query);
            var videos = response.data.user.videos;
            Assert.Equal(42, videos.totalCount);
            Assert.True(videos.pageInfo.hasNextPage);
            Assert.Equal(("111", "c1"), (videos.edges[0].node.id, videos.edges[0].cursor));
        }

        // Same contract for the clip search: optional cursor, period criteria, search Client-ID, and curator/game parsing.
        [Theory]
        [InlineData("LAST_WEEK", "", 50, "clips(first: 50, criteria: { period: LAST_WEEK })")]
        [InlineData("ALL_TIME", "CURSOR2", 20, "clips(first: 20, after: \"CURSOR2\", criteria: { period: ALL_TIME })")]
        public async Task GetGqlClips_BuildsPagedQueryAndParsesEdges(string period, string cursor, int limit, string expectedArgs)
        {
            var handler = JsonHandler("""
                {"data":{"user":{"clips":{"edges":[{"cursor":"c9","node":{"id":"5","slug":"SlugA","title":"Clip A","createdAt":"2024-01-01T00:00:00Z",
                "curator":{"id":"7","displayName":"Curator"},"durationSeconds":15,"thumbnailURL":"t","viewCount":3,"game":null}}],"pageInfo":{"hasNextPage":false,"hasPreviousPage":true}}}}}
                """);
            using var _ = UseHandler(handler);

            var response = await TwitchHelper.GetGqlClips("streamer", period, cursor, limit);

            var query = Query(AssertGqlPost(handler, SEARCH_CLIENT_ID));
            Assert.Contains("user(login:\"streamer\")", query);
            Assert.Contains(expectedArgs, query);
            var edge = Assert.Single(response.data.user.clips.edges);
            Assert.Equal(("SlugA", "Curator", "c9"), (edge.node.slug, edge.node.curator.displayName, edge.cursor));
            Assert.Null(edge.node.game);
            Assert.True(response.data.user.clips.pageInfo.hasPreviousPage);
        }

        // Login -> id lookup must quote every login in the list (used to resolve channel names for chat downloads/renders).
        [Fact]
        public async Task GetUserIds_QuotesEveryLoginAndParsesIds()
        {
            var handler = JsonHandler("""{"data":{"users":[{"id":"11"},null]}}""");
            using var _ = UseHandler(handler);

            var response = await TwitchHelper.GetUserIds(["alpha", "beta"]);

            Assert.Contains("users(logins:[\"alpha\",\"beta\"])", Query(AssertGqlPost(handler)));
            Assert.Equal("11", response.data.users[0].id);
            Assert.Null(response.data.users[1]); // Unknown logins come back as null entries
        }

        // Id -> profile lookup must quote every id and parse the profile fields used for chat avatars/metadata.
        [Fact]
        public async Task GetUserInfo_QuotesEveryIdAndParsesProfiles()
        {
            var handler = JsonHandler("""
                {"data":{"users":[{"id":"11","displayName":"Alpha","login":"alpha","createdAt":"2015-06-07T08:09:10Z","updatedAt":"2024-01-01T00:00:00Z",
                "description":"bio","profileImageURL":"https://static-cdn.jtvnw.net/jtv_user_pictures/alpha-profile_image-70x70.png"}]}}
                """);
            using var _ = UseHandler(handler);

            var response = await TwitchHelper.GetUserInfo(["11", "22"]);

            Assert.Contains("users(ids:[\"11\",\"22\"])", Query(AssertGqlPost(handler)));
            var user = Assert.Single(response.data.users);
            Assert.Equal(("Alpha", "alpha", "bio"), (user.displayName, user.login, user.description));
            Assert.EndsWith("70x70.png", user.profileImageURL);
            Assert.Equal(new DateTime(2015, 6, 7, 8, 9, 10, DateTimeKind.Utc), user.createdAt.ToUniversalTime());
        }
    }
}
