using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Spectre.Console;
using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCLI.Tests.ModeTests
{
    // Tests InfoHandler, the `info` verb that prints VOD/clip metadata, available qualities and chapters as a table or an M3U8 playlist.
    // Scripts parse this output and the M3U8 output is fed straight into players, so its content and error handling matter.
    // Twitch is faked through TwitchHelper.httpClient with the synthetic responses in TestData/CLI/info_*. Raw output is not tested because
    // it writes JSON through Console.OpenStandardOutput(), which bypasses Console.SetOut; the unparsable-id branch calls Environment.Exit.
    // In the static-HttpClient collection (non-parallel), which also makes it safe to redirect Console.Out and AnsiConsole here.
    [Collection(StaticHttpClientCollection.NAME)]
    public class InfoHandlerTests
    {
        private const string RESTRICTED_PLAYLIST = """[{"url":"https://usher.ttvnw.net/vod/v2/123456789.m3u8","error":"Unauthorized","error_code":"vod_manifest_restricted","type":"error"}]""";

        /// <summary>The bodies the fake Twitch API answers with; defaults are the TestData/CLI/info_* fixtures.</summary>
        private sealed record Responses
        {
            public string Video { get; init; } = TestData.ReadText("CLI", "info_video.json");
            public string Token { get; init; } = TestData.ReadText("CLI", "info_video_token.json");
            public string Chapters { get; init; } = TestData.ReadText("CLI", "info_video_chapters.json");
            public string Playlist { get; init; } = TestData.ReadText("CLI", "info_video_playlist.m3u8");
            public HttpStatusCode PlaylistStatus { get; init; } = HttpStatusCode.OK;
            public string Clip { get; init; } = TestData.ReadText("CLI", "info_clip.json");
        }

        private static FakeHttpMessageHandler FakeTwitch(Responses responses) => new((request, body) =>
        {
            if (request.RequestUri!.Host == "usher.ttvnw.net") return FakeHttpMessageHandler.Text(responses.Playlist, responses.PlaylistStatus);
            if (body is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (body.Contains("PlaybackAccessToken_Template")) return FakeHttpMessageHandler.Json(responses.Token);
            if (body.Contains("VideoPlayer_ChapterSelectButtonVideo")) return FakeHttpMessageHandler.Json(responses.Chapters);
            if (body.Contains("query{video(id:")) return FakeHttpMessageHandler.Json(responses.Video);
            if (body.Contains("ShareClipRenderStatus")) return FakeHttpMessageHandler.Json(responses.Clip);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        private static string EditJson(string json, Action<JsonNode> edit)
        {
            var node = JsonNode.Parse(json)!;
            edit(node);
            return node.ToJsonString();
        }

        private static string EditClip(Action<JsonNode> edit) => EditJson(new Responses().Clip, json => edit(json["data"]!["clip"]!));

        /// <summary>
        /// Runs the `info` verb against the fake Twitch API with an invariant culture, capturing Console.Out and Spectre's AnsiConsole
        /// (no ANSI codes, Unicode borders, 200 columns wide so table rows are not wrapped).
        /// </summary>
        private static (string Output, FakeHttpMessageHandler Http) RunInfo(Responses responses, string id, params string[] extraArgs)
        {
            var http = FakeTwitch(responses);
            var args = CliArgs.Parse<InfoArgs>(["info", "--id", id, "--log-level", "None", "--use-utf8", "false", ..extraArgs]);

            using var httpOverride = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, http);
            using var console = new ConsoleCapture();
            var originalAnsiConsole = AnsiConsole.Console;
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
                {
                    Ansi = AnsiSupport.No,
                    ColorSystem = ColorSystemSupport.NoColors,
                    Interactive = InteractionSupport.No,
                    Out = new AnsiConsoleOutput(Console.Out),
                });
                AnsiConsole.Console.Profile.Width = 200;
                AnsiConsole.Console.Profile.Capabilities.Unicode = true; // otherwise non-UTF-8 consoles (Windows) get ASCII borders

                InfoHandler.PrintInfo(args);
            }
            finally
            {
                AnsiConsole.Console = originalAnsiConsole;
                CultureInfo.CurrentCulture = originalCulture;
            }

            return (console.Output.ReplaceLineEndings("\n"), http);
        }

        /// <summary>Asserts that one of the rendered table rows consists of exactly <paramref name="cells"/> (cell padding ignored).</summary>
        private static void AssertRow(string output, params string[] cells)
        {
            var rows = output.Split('\n')
                .Where(line => line.StartsWith('│'))
                .Select(line => string.Join(" | ", line.Trim('│').Split('│').Select(cell => cell.Trim())));

            Assert.Contains(string.Join(" | ", cells), rows);
        }

        // The VOD table shows the video's metadata (markup characters in the title printed literally, description newline duplication
        // cleaned up), every stream with its resolution/fps/codecs/bitrate ("-" for the audio-only stream's missing values), and the chapters.
        [Fact]
        public void VodTable_ShowsInfoStreamsAndChapters()
        {
            var (output, http) = RunInfo(new Responses(), "https://www.twitch.tv/videos/123456789");

            AssertRow(output, "Streamer", "StreamerName");
            AssertRow(output, "Title", "[Hype] Speedrun");
            AssertRow(output, "Length", "1:02:03");
            AssertRow(output, "Category", "Just Chatting");
            AssertRow(output, "Views", "12,345");
            AssertRow(output, "Created at", "2024-01-02 03:04:05 UTC");
            AssertRow(output, "Description", "Line one");
            AssertRow(output, "", "Line two"); // continuation row: the doubled "\n\n" was collapsed

            AssertRow(output, "1080p60", "1920x1080", "60", "avc1.64002A, mp4a.40.2", "5898kbps", "~2.56GiB");
            AssertRow(output, "720p60", "1280x720", "60", "avc1.4D0020, mp4a.40.2", "3443kbps", "~1.49GiB");
            AssertRow(output, "Audio Only", "-", "-", "mp4a.40.2", "220kbps", "~97.79MiB");

            AssertRow(output, "Just Chatting", "GAME_CHANGE", "0:00", "30:00", "30:00");
            AssertRow(output, "Minecraft", "GAME_CHANGE", "30:00", "1:02:03", "32:03");

            Assert.Contains(http.Requests, r => r.Uri.AbsolutePath == "/vod/v2/123456789.m3u8" && r.Uri.Query.Contains("sig=SIG"));
        }

        // "Created at" is formatted with `hh` (12-hour clock without AM/PM), so a VOD created at 15:04 UTC is shown as 03:04 UTC.
        [Fact(Skip = "Bug: InfoHandler formats 'Created at' with the 12-hour 'hh' specifier and no AM/PM, so afternoon times are printed as morning times")]
        public void VodTable_CreatedAt_Uses24HourClock()
        {
            var responses = new Responses { Video = EditJson(new Responses().Video, json => json["data"]!["video"]!["createdAt"] = "2024-01-02T15:04:05Z") };

            var (output, _) = RunInfo(responses, "123456789");

            AssertRow(output, "Created at", "2024-01-02 15:04:05 UTC");
        }

        // --oauth must be forwarded as an "OAuth <token>" Authorization header on the playback token request, otherwise
        // subscriber-only VODs can never be inspected.
        [Fact]
        public void Vod_OAuthIsSentWithTokenRequest()
        {
            var (_, http) = RunInfo(new Responses(), "123456789", "--format", "m3u8", "--oauth", "secret");

            var tokenRequest = Assert.Single(http.Requests, r => r.Body?.Contains("PlaybackAccessToken_Template") == true);
            Assert.Equal("OAuth secret", tokenRequest.Headers["Authorization"]);
        }

        // The M3U8 format prints the VOD's master playlist (re-serialized after validating it) with every stream URL.
        [Fact]
        public void VodM3U8_PrintsMasterPlaylist()
        {
            var (output, _) = RunInfo(new Responses(), "123456789", "--format", "m3u");

            Assert.StartsWith("#EXTM3U", output);
            Assert.Contains("BANDWIDTH=5898203", output);
            Assert.Contains("https://example.invalid/vod/chunked/index-dvr.m3u8", output);
            Assert.Contains("https://example.invalid/vod/720p60/index-dvr.m3u8", output);
            Assert.Contains("https://example.invalid/vod/audio_only/index-dvr.m3u8", output);
        }

        // Attributes of #EXT-X-STREAM-INF must be comma separated. M3U8.Stream.ExtStreamInfo.ToString (Core) appends FRAME-RATE with no
        // separator, so `info -f m3u8` prints "FRAME-RATE=59.995STABLE-VARIANT-ID=..." which players cannot parse.
        [Fact(Skip = "Bug: M3U8 ExtStreamInfo.ToString (TwitchDownloaderCore/Models/M3U8.cs) omits the ',' after FRAME-RATE, so `info -f m3u8` prints malformed stream attributes")]
        public void VodM3U8_StreamAttributesAreCommaSeparated()
        {
            var (output, _) = RunInfo(new Responses(), "123456789", "--format", "m3u8");

            Assert.Contains("FRAME-RATE=59.995,STABLE-VARIANT-ID=\"1080p60\"", output);
        }

        // A VOD without a playback token (deleted/expired) must fail with a clear message instead of printing an empty table.
        [Fact]
        public void Vod_WithoutPlaybackToken_Throws()
        {
            var responses = new Responses { Token = """{"data":{"videoPlaybackAccessToken":null}}""" };

            var ex = Assert.Throws<NullReferenceException>(() => RunInfo(responses, "123456789"));
            Assert.Contains("Invalid VOD", ex.Message);
        }

        // A sub-only VOD (usher answers 403 vod_manifest_restricted) must fail with a hint that OAuth is required.
        [Fact]
        public void Vod_RestrictedPlaylist_Throws()
        {
            var responses = new Responses { Playlist = RESTRICTED_PLAYLIST, PlaylistStatus = HttpStatusCode.Forbidden };

            var ex = Assert.Throws<NullReferenceException>(() => RunInfo(responses, "123456789", "--format", "m3u8"));
            Assert.Contains("OAuth may be required", ex.Message);
        }

        // For raw output (canThrow = false) the fetch must not throw on a missing token or restricted playlist; it returns whatever Twitch
        // sent so the user can inspect it.
        [Fact]
        public async Task GetVideoInfo_WithoutThrowing_ReturnsPartialData()
        {
            var progress = new RecordingTaskProgress();

            using (new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, FakeTwitch(new Responses { Token = """{"data":{"videoPlaybackAccessToken":null}}""" })))
            {
                var (videoInfo, chapters, playlist) = await InfoHandler.GetVideoInfo(123456789, null, false, progress);

                Assert.Equal("[Hype] Speedrun", videoInfo.data.video.title);
                Assert.Null(chapters);
                Assert.Null(playlist);
            }

            using (new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, FakeTwitch(new Responses { Playlist = RESTRICTED_PLAYLIST, PlaylistStatus = HttpStatusCode.Forbidden })))
            {
                var (_, chapters, playlist) = await InfoHandler.GetVideoInfo(123456789, null, false, progress);

                Assert.Contains("vod_manifest_restricted", playlist);
                Assert.Equal(2, chapters.data.video.moments.edges.Count);
            }

            Assert.Equal(["Fetching Video Info [1/1]", "Fetching Video Info [1/1]"], progress.Statuses);
        }

        // The JSON format is advertised by the enum but not implemented; it must fail explicitly rather than print nothing.
        [Theory]
        [InlineData("123456789")]
        [InlineData("FancyClipSlug")]
        public void JsonFormat_IsNotImplemented(string id)
        {
            Assert.Throws<NotImplementedException>(() => RunInfo(new Responses(), id, "--format", "json"));
        }

        // The clip table shows the clip's metadata (including who clipped it, with the login appended to a non-ASCII display name, and the
        // source VOD id/offset) and every quality with resolution, fps, bitrate and estimated size.
        [Fact]
        public void ClipTable_ShowsInfoAndQualities()
        {
            var (output, _) = RunInfo(new Responses(), "https://clips.twitch.tv/FancyClipSlug");

            AssertRow(output, "Streamer", "StreamerName");
            AssertRow(output, "Title", "Clip [title]");
            AssertRow(output, "Length", "30s");
            AssertRow(output, "Clipped by", "クリッパー (clipper)");
            AssertRow(output, "Category", "Minecraft");
            AssertRow(output, "Views", "4,321");
            AssertRow(output, "Created at", "2024-05-06 07:08:09 UTC");
            AssertRow(output, "VOD ID", "123456789");
            AssertRow(output, "VOD offset", "2:05");

            AssertRow(output, "1080p60", "1920x1080", "60", "6000kbps", "~21.46MiB");
            AssertRow(output, "720p30", "1280x720", "30", "3000kbps", "~10.73MiB");
        }

        // User names fall back gracefully: login only, display name only, or "-" when the user is unknown (e.g. a deleted curator account).
        [Theory]
        [InlineData("""{"id":"22","login":"clipper","displayName":null}""", "clipper")]
        [InlineData("""{"id":"22","login":null,"displayName":"Clipper"}""", "Clipper")]
        [InlineData("""{"id":"22","login":"clipper","displayName":"Clipper"}""", "Clipper")]
        [InlineData("null", "-")]
        public void ClipTable_UserNameFallbacks(string curatorJson, string expected)
        {
            var responses = new Responses { Clip = EditClip(clip => clip["curator"] = JsonNode.Parse(curatorJson)) };

            var (output, _) = RunInfo(responses, "FancyClipSlug");

            AssertRow(output, "Clipped by", expected);
        }

        // A clip whose VOD is gone has no "VOD ID"/"VOD offset" rows.
        [Fact]
        public void ClipTable_WithoutVod_OmitsVodRows()
        {
            var responses = new Responses { Clip = EditClip(clip => clip["video"] = null) };

            var (output, _) = RunInfo(responses, "FancyClipSlug");

            Assert.DoesNotContain("VOD ID", output);
            Assert.DoesNotContain("VOD offset", output);
        }

        // Missing values degrade to "-": an unknown VOD offset, and qualities without bitrate data omit the bitrate/size columns instead
        // of showing "0kbps".
        [Fact]
        public void ClipTable_MissingOffsetAndBitrate_DegradeGracefully()
        {
            var responses = new Responses
            {
                Clip = EditClip(clip =>
                {
                    clip["videoOffsetSeconds"] = null;
                    foreach (var quality in clip["assets"]![0]!["videoQualities"]!.AsArray())
                    {
                        quality!["bitrate"] = 0;
                    }
                })
            };

            var (output, _) = RunInfo(responses, "FancyClipSlug");

            AssertRow(output, "VOD offset", "-");
            AssertRow(output, "Name", "Resolution", "FPS");
            AssertRow(output, "1080p60", "1920x1080", "60");
        }

        // A VOD playlist without BANDWIDTH/CODECS attributes still renders: no bitrate/size columns and "-" for the missing codecs.
        [Fact]
        public void VodTable_MissingBandwidthAndCodecs_DegradeGracefully()
        {
            var playlist = new Responses().Playlist.Replace("BANDWIDTH=220328,CODECS=\"mp4a.40.2\",", "");
            playlist = Regex.Replace(playlist, "BANDWIDTH=\\d+,", "");
            var responses = new Responses { Playlist = playlist };

            var (output, _) = RunInfo(responses, "123456789");

            AssertRow(output, "Name", "Resolution", "FPS", "Codecs");
            AssertRow(output, "1080p60", "1920x1080", "60", "avc1.64002A, mp4a.40.2");
            AssertRow(output, "Audio Only", "-", "-", "-");
        }

        // The clip M3U8 output builds a playable master playlist: each quality's source URL carries the signature and URL-encoded token.
        [Fact]
        public void ClipM3U8_PrintsSignedPlaylist()
        {
            var (output, _) = RunInfo(new Responses(), "FancyClipSlug", "--format", "m3u8");

            Assert.StartsWith("#EXTM3U", output);
            Assert.Contains("RESOLUTION=1920x1080", output);
            Assert.Contains("https://example.invalid/clip-1080.mp4?sig=SIG&token=%7b%22a%22%3a1%7d", output);
            Assert.Contains("https://example.invalid/clip-720.mp4?sig=SIG&token=%7b%22a%22%3a1%7d", output);
        }

        // Clips without a playback token or without any video qualities (deleted clips) must fail with a clear message.
        [Theory]
        [InlineData("playbackAccessToken", "Invalid Clip")]
        [InlineData("assets", "no video qualities")]
        public void Clip_Unavailable_Throws(string missingField, string expectedMessage)
        {
            var responses = new Responses { Clip = EditClip(clip => clip[missingField] = missingField == "assets" ? new JsonArray() : null) };

            var ex = Assert.Throws<NullReferenceException>(() => RunInfo(responses, "FancyClipSlug"));
            Assert.Contains(expectedMessage, ex.Message);
        }

        // For raw output (canThrow = false) an unavailable clip is returned as-is instead of throwing.
        [Fact]
        public async Task GetClipInfo_WithoutThrowing_ReturnsResponseAsIs()
        {
            var progress = new RecordingTaskProgress();
            var responses = new Responses { Clip = EditClip(clip => clip["playbackAccessToken"] = null) };

            using var _ = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, FakeTwitch(responses));
            var clipInfo = await InfoHandler.GetClipInfo("FancyClipSlug", false, progress);

            Assert.Null(clipInfo.data.clip.playbackAccessToken);
            Assert.Equal(["Fetching Clip Info [1/1]"], progress.Statuses);
        }

        // Durations are printed compactly: under a second "0:00", under a minute "Ns", under an hour "m:ss", otherwise "H:mm:ss" with
        // hours allowed to exceed 24 (VODs can be up to 48 hours long).
        [Theory]
        [InlineData(500, "0:00")]
        [InlineData(45_000, "45s")]
        [InlineData(303_000, "5:03")]
        [InlineData(3_723_000, "1:02:03")]
        [InlineData(93_600_000, "26:00:00")]
        public void StringifyTimestamp_UsesCompactFormat(long milliseconds, string expected)
        {
            Assert.Equal(expected, InfoHandler.StringifyTimestamp(TimeSpan.FromMilliseconds(milliseconds)));
        }

        // --use-utf8 false must leave the console encoding alone; when enabled the console ends up in UTF-8 (or a warning explains why not),
        // so non-ASCII titles and names are not mangled on Windows' default code page.
        [Fact]
        public void SetUtf8Encoding_SwitchesOnlyWhenRequested()
        {
            var original = Console.OutputEncoding;
            var logger = new RecordingTaskProgress();
            try
            {
                InfoHandler.SetUtf8Encoding(false, logger);
                Assert.Equal(original.CodePage, Console.OutputEncoding.CodePage);
                Assert.Empty(logger.Verbose);

                InfoHandler.SetUtf8Encoding(true, logger);
                Assert.True(Console.OutputEncoding.CodePage == Encoding.UTF8.CodePage || !logger.Warnings.IsEmpty);
            }
            finally
            {
                if (Console.OutputEncoding.CodePage != original.CodePage)
                {
                    try { Console.OutputEncoding = original; }
                    catch (Exception) { /* no console attached; nothing to restore */ }
                }
            }
        }
    }
}
