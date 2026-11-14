using System.Text.Json;
using System.Text.RegularExpressions;
using TwitchDownloaderCLI.Tests.EndToEndTests;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.LiveTests
{
    // Live end-to-end tests: the real CLI talking to the real Twitch API/CDN. They catch what offline tests cannot (Twitch changing GQL
    // queries, playlist formats, CDN URLs or chat pagination) and run nightly via .github/workflows/e2e-live.yml, never on pull requests.
    // They are skipped unless TD_LIVE_TESTS=1. Assertions check structure (ids, owners, ranges, formats), never counts that drift
    // (views, comment totals).
    //
    // Targets (verified 2026-11-14) were picked to be long-lived: highlights and clips are not subject to VOD expiry.
    [Trait("Category", "Live")]
    public class LiveCliTests : IDisposable
    {
        /// <summary>jerma985 highlight from 2023, 6h52m, with chat replay.</summary>
        private const string VOD_ID = "1822879862";

        /// <summary>jerma985 clip from 2019, 12 s, 300k+ views. Its source VOD has expired, so it is only used for video/info.</summary>
        private const string VIDEO_CLIP = "SpunkyWildDillGivePLZ";

        /// <summary>A 42 s clip of the highlight above. Clip chat can only be downloaded while the clip's source video exists.</summary>
        private const string CHAT_CLIP = "DignifiedGrossMooseJebaited-H0I9LF4LpM_8sWox";

        private const string STREAMER_LOGIN = "jerma985";

        private readonly TempDirectory _temp = new();

        public LiveCliTests()
        {
            Assert.SkipUnless(Environment.GetEnvironmentVariable("TD_LIVE_TESTS") == "1", "Live Twitch tests are disabled; set TD_LIVE_TESTS=1 to run them");
        }

        public void Dispose() => _temp.Dispose();

        private CliResult RunLive(params string[] args)
        {
            var result = Cli.Run(_temp.Path, [..args, "--banner", "false"], offline: false, timeout: TimeSpan.FromMinutes(5));
            Assert.True(result.ExitCode == 0, result.ToString());
            return result;
        }

        private static JsonElement JsonLine(CliResult result, string prefix)
        {
            var line = result.StdOut.Split('\n').FirstOrDefault(l => l.StartsWith(prefix));
            Assert.True(line is not null, $"no line starting with {prefix}\n{result}");
            return JsonDocument.Parse(line).RootElement;
        }

        // `info` on a VOD (default table format) must print the video and stream tables with the owner. Guards the GQL video query,
        // the playback token and the master playlist parsing all at once.
        [Fact]
        public void InfoTableForVod()
        {
            var result = RunLive("info", "--id", VOD_ID);

            Assert.Contains("Video Info", result.StdOut);
            Assert.Contains("Jerma985", result.StdOut);
            Assert.Contains("Video Streams", result.StdOut);
        }

        // `info --format raw` on a VOD URL must print the raw GQL video JSON, then the chapters JSON, then the master playlist. Scripts parse
        // this, so its shape matters. Also checks that a full VOD URL is accepted in place of an id.
        [Fact]
        public void InfoRawForVodUrl()
        {
            var result = RunLive("info", "--id", $"https://www.twitch.tv/videos/{VOD_ID}", "--format", "Raw");

            var video = JsonLine(result, "{\"data\":{\"video\":{\"title\"").GetProperty("data").GetProperty("video");
            Assert.Equal(STREAMER_LOGIN, video.GetProperty("owner").GetProperty("login").GetString());
            Assert.True(video.GetProperty("lengthSeconds").GetInt32() > 3600);
            Assert.Contains("#EXTM3U", result.StdOut);
            Assert.Contains("#EXT-X-STREAM-INF", result.StdOut);
        }

        // `info --format m3u8` must print just a valid master playlist (it is re-serialized after parsing).
        [Fact]
        public void InfoM3u8ForVod()
        {
            var result = RunLive("info", "--id", VOD_ID, "--format", "M3U8", "--log-level", "Error");

            Assert.StartsWith("#EXTM3U", result.StdOut);
            Assert.Contains("#EXT-X-STREAM-INF", result.StdOut);
            Assert.Contains(".m3u8", result.StdOut);
        }

        // `info` on a clip (table) must print the clip and quality tables.
        [Fact]
        public void InfoTableForClip()
        {
            var result = RunLive("info", "--id", VIDEO_CLIP);

            Assert.Contains("Clip Info", result.StdOut);
            Assert.Contains("Jerma985", result.StdOut);
            Assert.Contains("Clip Qualities", result.StdOut);
        }

        // `info --format raw` on a clip URL must print the raw GQL clip JSON with the right slug and broadcaster.
        [Fact]
        public void InfoRawForClipUrl()
        {
            var result = RunLive("info", "--id", $"https://clips.twitch.tv/{VIDEO_CLIP}", "--format", "Raw");

            var clip = JsonLine(result, "{\"data\":{\"clip\":").GetProperty("data").GetProperty("clip");
            Assert.Equal(VIDEO_CLIP, clip.GetProperty("slug").GetString());
            Assert.Equal(STREAMER_LOGIN, clip.GetProperty("broadcaster").GetProperty("login").GetString());
        }

        // `clipdownload` must produce a playable H.264 + audio file of the clip's length at the requested quality. Guards the clip
        // access token/URL signing and the ffmpeg metadata step.
        [Fact]
        public void ClipDownloadProducesPlayableVideo()
        {
            TestData.RequireFfmpeg();
            var output = _temp.Combine("clip.mp4");

            RunLive("clipdownload", "--id", VIDEO_CLIP, "-q", "360", "-o", output, "--temp-path", _temp.Combine("cache"));

            var probe = Media.Inspect(output);
            Assert.Equal("h264", probe.VideoCodec);
            Assert.True(probe.HasAudio, "clip has no audio stream");
            Assert.InRange(probe.Height, 1, 720);
            Assert.InRange(probe.DurationSeconds, 10, 15);
        }

        // `videodownload -b/-e` must download only the requested 8 s of a long VOD (not the whole thing) and produce a playable file of
        // that length. Guards playlist parsing, segment selection, the parallel downloader and exact-trim finalization.
        [Fact]
        public void VideoDownloadOfTrimmedRange()
        {
            TestData.RequireFfmpeg();
            var output = _temp.Combine("vod.mp4");

            RunLive("videodownload", "--id", VOD_ID, "-q", "160p30", "-b", "1:00:00", "-e", "1:00:08", "-o", output, "--temp-path", _temp.Combine("cache"));

            var probe = Media.Inspect(output);
            Assert.NotNull(probe.VideoCodec);
            Assert.True(probe.HasAudio, "VOD download has no audio stream");
            Assert.InRange(probe.DurationSeconds, 7.5, 9);
        }

        // `chatdownload` of a 60 s VOD range must work for every output format, with and without embedded images. JSON must carry the
        // requested trim and sorted comments; TXT must be "[time] name: message" lines; HTML must contain comments (and inline images with -E).
        [Theory]
        [InlineData("json", false)]
        [InlineData("json", true)]
        [InlineData("txt", false)]
        [InlineData("html", false)]
        [InlineData("html", true)]
        public void ChatDownloadOfVodRange(string format, bool embed)
        {
            var output = _temp.Combine($"chat.{format}");
            string[] args = ["chatdownload", "--id", VOD_ID, "-b", "1:00:00", "-e", "1:01:00", "-o", output, "--temp-path", _temp.Combine("cache")];

            RunLive(embed ? [..args, "-E"] : args);

            switch (format)
            {
                case "json":
                    using (var doc = JsonDocument.Parse(File.ReadAllText(output)))
                    {
                        var root = doc.RootElement;
                        Assert.Equal(VOD_ID, root.GetProperty("video").GetProperty("id").GetString());
                        Assert.Equal(3600, root.GetProperty("video").GetProperty("start").GetDouble());
                        Assert.Equal(3660, root.GetProperty("video").GetProperty("end").GetDouble());
                        Assert.Equal(STREAMER_LOGIN, root.GetProperty("streamer").GetProperty("login").GetString());
                        AssertComments(root, double.MinValue, double.MaxValue);
                        Assert.Equal(embed, root.GetProperty("embeddedData").ValueKind == JsonValueKind.Object);
                        if (embed) Assert.True(root.GetProperty("embeddedData").GetProperty("twitchBadges").GetArrayLength() > 0);
                    }
                    break;
                case "txt":
                    var lines = File.ReadAllLines(output);
                    Assert.NotEmpty(lines);
                    Assert.All(lines, l => Assert.Matches(new Regex(@"^\[\d+:\d{2}:\d{2}\] [^:]+: "), l));
                    break;
                case "html":
                    var html = File.ReadAllText(output);
                    Assert.Contains("<pre class=\"comment-root\">", html);
                    Assert.Equal(embed, html.Contains("data:image/png;base64,"));
                    break;
            }
        }

        // `chatdownload` of a clip must resolve the clip to its source video and offset, and return only comments inside the clip's
        // window, with and without embedded images.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ChatDownloadOfClip(bool embed)
        {
            var output = _temp.Combine("clip.json");
            string[] args = ["chatdownload", "--id", CHAT_CLIP, "-o", output, "--temp-path", _temp.Combine("cache")];

            RunLive(embed ? [..args, "-E"] : args);

            using var doc = JsonDocument.Parse(File.ReadAllText(output));
            var root = doc.RootElement;
            var video = root.GetProperty("video");
            Assert.Equal(VOD_ID, video.GetProperty("id").GetString());
            Assert.Equal(STREAMER_LOGIN, root.GetProperty("streamer").GetProperty("login").GetString());
            Assert.Equal(JsonValueKind.Object, root.GetProperty("clipper").ValueKind);
            var start = video.GetProperty("start").GetDouble();
            var end = video.GetProperty("end").GetDouble();
            Assert.Equal(video.GetProperty("length").GetDouble(), end - start, 1);
            AssertComments(root, start, end + 1);
            if (embed) Assert.True(root.GetProperty("embeddedData").GetProperty("twitchBadges").GetArrayLength() > 0);
        }

        // The documented offline workflow: `chatdownload -E` then `chatrender --offline`. The render runs behind a dead proxy, so it
        // proves the downloaded JSON alone contains everything needed, and the video must span the clip's chat window.
        [Fact]
        public void ChatDownloadThenOfflineRender()
        {
            TestData.RequireFfmpeg();
            var chat = _temp.Combine("clip.json");
            var render = _temp.Combine("render.mp4");
            RunLive("chatdownload", "--id", CHAT_CLIP, "-E", "-o", chat, "--temp-path", _temp.Combine("cache"));

            var result = Cli.Run(_temp.Path, ["chatrender", "-i", chat, "-o", render, "--offline", "-w", "200", "-h", "300", "--framerate", "10", "--temp-path", _temp.Combine("cache")], offline: true, timeout: TimeSpan.FromMinutes(5));

            Assert.True(result.ExitCode == 0, result.ToString());
            using var doc = JsonDocument.Parse(File.ReadAllText(chat));
            var video = doc.RootElement.GetProperty("video");
            var probe = Media.Inspect(render);
            Assert.Equal((200, 300), (probe.Width, probe.Height));
            Assert.Equal(video.GetProperty("end").GetDouble() - Math.Floor(video.GetProperty("start").GetDouble()), probe.DurationSeconds, 1);
        }

        /// <summary>Comments exist, are sorted by offset, fall inside [min, max] and have an author and a body.</summary>
        private static void AssertComments(JsonElement root, double min, double max)
        {
            var comments = root.GetProperty("comments").EnumerateArray().ToArray();
            Assert.NotEmpty(comments);
            var offsets = comments.Select(c => c.GetProperty("content_offset_seconds").GetDouble()).ToArray();
            Assert.Equal(offsets.Order(), offsets);
            Assert.All(offsets, o => Assert.InRange(o, min, max));
            Assert.All(comments, c =>
            {
                Assert.False(string.IsNullOrEmpty(c.GetProperty("commenter").GetProperty("display_name").GetString()));
                Assert.NotNull(c.GetProperty("message").GetProperty("body").GetString());
            });
        }
    }
}
