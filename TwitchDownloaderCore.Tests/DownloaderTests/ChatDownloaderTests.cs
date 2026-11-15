using System.Net;
using System.Text.Json;
using SkiaSharp;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.DownloaderTests
{
    // Tests ChatDownloader: GQL video/clip info -> paged, cursor-driven comment download (optionally split across parallel
    // sections) -> conversion to the chat JSON model -> optional image embedding and commenter backfill -> JSON/HTML/TXT
    // output (optionally gzipped). Chat files are the input to the renderer and updater, so their content must be exact.
    [Collection(StaticHttpClientCollection.NAME)]
    public class ChatDownloaderTests
    {
        private static readonly byte[] EmotePng = FakeTwitch.Png(SKColors.Red);
        private static readonly byte[] BttvPng = FakeTwitch.Png(SKColors.Green);
        private static readonly byte[] BadgePng = FakeTwitch.Png(SKColors.Blue);
        private static readonly byte[] CheerPng = FakeTwitch.Png(SKColors.Purple);

        private sealed class Fakes : IDisposable
        {
            public readonly FakeTwitch Twitch = new();
            private readonly HttpClientOverride _helper;
            private readonly HttpClientOverride _chat;

            public Fakes()
            {
                Twitch.Comments.AddRange([
                    FakeTwitch.Comment("c1", 5, "1", "hello", emoteId: "25", badge: "subscriber"),
                    FakeTwitch.Comment("c2", 12, "2", "Cheer100 nice"),
                    FakeTwitch.Comment("c3", 20, "1", "bttvFake lol"),
                    FakeTwitch.Comment("c4", 33, "3", "late message"),
                    DeletedCommenter(FakeTwitch.Comment("c5", 45, "4", "from a deleted account")),
                    FakeTwitch.Comment("c6", 58, "2", "bye"),
                ]);
                Twitch.Users.AddRange([
                    new() { id = "1", login = "user1", displayName = "User1", description = "bio one", profileImageURL = "https://avatar.test/1.png" },
                    new() { id = "2", login = "user2", displayName = "User2", description = "bio two", profileImageURL = "https://avatar.test/2.png" },
                ]);
                _helper = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, Twitch.Handler);
                _chat = new HttpClientOverride(c => ChatDownloader.HttpClient = c, () => ChatDownloader.HttpClient, Twitch.Handler);
            }

            private static TwitchObjects.Gql.CommentNode DeletedCommenter(TwitchObjects.Gql.CommentNode node)
            {
                node.commenter = null;
                return node;
            }

            public void Dispose()
            {
                _chat.Dispose();
                _helper.Dispose();
            }
        }

        private static ChatDownloadOptions Options(TempDirectory temp, ChatFormat format = ChatFormat.Json, string id = "1234567890") => new()
        {
            Id = id,
            DownloadFormat = format,
            Filename = temp.Combine("out", "chat" + format switch { ChatFormat.Html => ".html", ChatFormat.Text => ".txt", _ => ".json" }),
            TempFolder = temp.Combine("temp"),
            TimeFormat = TimestampFormat.Relative,
        };

        private static async Task<ChatRoot> Download(ChatDownloadOptions options, RecordingTaskProgress? progress = null)
        {
            await new ChatDownloader(options, progress ?? new RecordingTaskProgress()).DownloadAsync(CancellationToken.None);
            return await ChatJson.DeserializeAsync(options.Filename, cancellationToken: TestContext.Current.CancellationToken);
        }

        private static IEnumerable<string> CommentRequestBodies(FakeTwitch twitch) => twitch.GqlRequests("VideoCommentsByOffsetOrCursor").Select(r => r.Body!);

        /// <summary>
        /// A full VOD chat download to JSON: all pages are followed via the last edge's cursor, comments from deleted
        /// accounts (null commenter) are dropped, display names are trimmed, emotes/badges/colors/bits are converted, the
        /// video metadata (cleaned description, generated single chapter) and streamer are filled in, and commenters are
        /// backfilled with bio/avatar from the user GQL query.
        /// </summary>
        [Fact]
        public async Task VideoChat_Json_EndToEnd()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();

            var chat = await Download(Options(temp));

            Assert.Equal(["c1", "c2", "c3", "c4", "c6"], chat.comments.Select(c => c._id));
            Assert.Equal(new ChatRootVersion(1, 4, 0), chat.FileInfo.Version);
            Assert.Equal(("FakeStreamer", "fakestreamer", FakeTwitch.STREAMER_ID), (chat.streamer.name, chat.streamer.login, chat.streamer.id));
            Assert.Equal(FakeTwitch.VIDEO_ID.ToString(), chat.video.id);
            Assert.Equal("Fake VOD", chat.video.title);
            Assert.Equal("Line one\nLine two", chat.video.description);
            Assert.Equal((0d, 60d, 60d), (chat.video.start, chat.video.end, chat.video.length));
            Assert.Equal("Science & Technology", chat.video.game);
            var chapter = Assert.Single(chat.video.chapters);
            Assert.Equal((0, 60_000, "Science & Technology"), (chapter.startMilliseconds, chapter.lengthMilliseconds, chapter.gameDisplayName));

            var first = chat.comments[0];
            Assert.Equal(("User1", "user1", "1"), (first.commenter.display_name, first.commenter.name, first.commenter._id));
            Assert.Equal(("bio one", "https://avatar.test/1.png"), (first.commenter.bio, first.commenter.logo));
            Assert.Equal(("video", FakeTwitch.VIDEO_ID.ToString(), FakeTwitch.STREAMER_ID.ToString()), (first.content_type, first.content_id, first.channel_id));
            Assert.Equal("hello Kappa", first.message.body);
            Assert.Equal("25", first.message.fragments[2].emoticon.emoticon_id);
            Assert.Equal(("25", 6), (first.message.emoticons.Single()._id, first.message.emoticons.Single().begin));
            Assert.Equal(("subscriber", "1"), (first.message.user_badges.Single()._id, first.message.user_badges.Single().version));
            Assert.Equal("#FF0000", first.message.user_color);
            Assert.Equal(100, chat.comments[1].message.bits_spent);
            Assert.Empty(chat.comments[2].message.user_badges); // An empty badge (no setID/version) is skipped
            Assert.Null(chat.comments[3].commenter.bio); // User 3 is unknown to the user query

            var bodies = CommentRequestBodies(fakes.Twitch).ToArray();
            Assert.Equal(3, bodies.Length); // 5 comments + 1 deleted, 2 per page
            Assert.Contains("\"contentOffsetSeconds\":0", bodies[0]);
            Assert.Contains("\"cursor\":\"1\"", bodies[1]);
            Assert.Contains("\"cursor\":\"3\"", bodies[2]);
        }

        /// <summary>
        /// Trimming downloads from the trim start (first request uses that content offset) and keeps only comments inside
        /// [start, end]; the video start/end in the file reflect the trim so the renderer starts at the right time.
        /// </summary>
        [Fact]
        public async Task Trim_KeepsOnlyCommentsInRange()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            var options = Options(temp);
            options.TrimBeginning = true;
            options.TrimBeginningTime = 10;
            options.TrimEnding = true;
            options.TrimEndingTime = 40;

            var chat = await Download(options);

            Assert.Equal(["c2", "c3", "c4"], chat.comments.Select(c => c._id));
            Assert.Equal((10d, 40d), (chat.video.start, chat.video.end));
            Assert.Contains("\"contentOffsetSeconds\":10", CommentRequestBodies(fakes.Twitch).First());
        }

        /// <summary>
        /// With several download threads the VOD is split into equal sections fetched in parallel (each starting at its own
        /// content offset); overlapping pages are de-duplicated by comment id and the result is sorted by offset.
        /// </summary>
        [Fact]
        public async Task ParallelSections_AreMergedWithoutDuplicates()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            var options = Options(temp);
            options.DownloadThreads = 3;

            var chat = await Download(options);

            Assert.Equal(["c1", "c2", "c3", "c4", "c6"], chat.comments.Select(c => c._id));
            var offsets = CommentRequestBodies(fakes.Twitch)
                .Select(b => JsonDocument.Parse(b).RootElement.GetProperty("variables"))
                .Where(v => v.TryGetProperty("contentOffsetSeconds", out _))
                .Select(v => v.GetProperty("contentOffsetSeconds").GetInt32())
                .Order();
            Assert.Equal([0, 21, 42], offsets); // 61s (exclusive end) in ceil(61/3) = 21s sections
        }

        /// <summary>
        /// Plain text output writes one "[h:mm:ss] Name: message" line per comment and skips the commenter backfill (which
        /// only matters for JSON), saving a GQL round trip per 100 users.
        /// </summary>
        [Fact]
        public async Task TextFormat_WritesRelativeTimestampLines()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            var options = Options(temp, ChatFormat.Text);

            await new ChatDownloader(options, new RecordingTaskProgress()).DownloadAsync(CancellationToken.None);

            var lines = await File.ReadAllLinesAsync(options.Filename, TestContext.Current.CancellationToken);
            Assert.Equal(["[0:00:05] User1: hello Kappa", "[0:00:12] User2: Cheer100 nice", "[0:00:20] User1: bttvFake lol", "[0:00:33] User3: late message", "[0:00:58] User2: bye"], lines);
            Assert.Empty(fakes.Twitch.GqlRequests("query{users(ids:"));
        }

        /// <summary>
        /// HTML output (without embedded images) contains one comment block per comment with a timestamped link back to the
        /// VOD and the file name as the page title.
        /// </summary>
        [Fact]
        public async Task HtmlFormat_WritesCommentMarkup()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            var options = Options(temp, ChatFormat.Html);

            await new ChatDownloader(options, new RecordingTaskProgress()).DownloadAsync(CancellationToken.None);

            var html = await File.ReadAllTextAsync(options.Filename, TestContext.Current.CancellationToken);
            Assert.Equal(5, html.Split("<pre class=\"comment-root\">").Length - 1);
            Assert.Contains($"https://twitch.tv/videos/{FakeTwitch.VIDEO_ID}/?t=0h00m33s", html);
            Assert.Contains("late message", html);
            Assert.Contains("<title>\nchat\n</title>", html.ReplaceLineEndings("\n")); // The page title is the file name
        }

        /// <summary>Gzip compression produces a real gzip stream (finished on dispose) that ChatJson can read back.</summary>
        [Fact]
        public async Task GzipCompression_ProducesReadableGzip()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp);
            options.Filename += ".gz";
            options.Compression = ChatCompression.Gzip;

            var chat = await Download(options);

            var bytes = await File.ReadAllBytesAsync(options.Filename, TestContext.Current.CancellationToken);
            Assert.Equal([0x1F, 0x8B], bytes[..2]);
            Assert.Equal(5, chat.comments.Count);
        }

        /// <summary>
        /// EmbedData stores every image kind the renderer needs offline: first-party emotes used in messages, matching BTTV
        /// emotes, the badges commenters wear and the cheermote tiers of used cheer prefixes, each with its image bytes.
        /// </summary>
        [Fact]
        public async Task EmbedData_EmbedsEmotesBadgesAndCheermotes()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            var twitch = fakes.Twitch;
            twitch.BttvGlobalEmotes = [("bttv1", "bttvFake"), ("bttv2", "unusedEmote")];
            twitch.Files["https://cdn.betterttv.net/emote/bttv1/2x"] = BttvPng;
            twitch.Files["https://static-cdn.jtvnw.net/emoticons/v2/25/default/dark/2.0"] = EmotePng;
            twitch.GlobalBadges = [("subscriber", "1", "https://static-cdn.jtvnw.net/badges/v1/sub-badge-id/2"), ("moderator", "1", "https://static-cdn.jtvnw.net/badges/v1/mod-badge-id/2")];
            twitch.Files["https://static-cdn.jtvnw.net/badges/v1/sub-badge-id/2"] = BadgePng;
            twitch.Cheermotes = [("cheer-id", "Cheer", [1, 100])];
            twitch.Files["https://cheer.test/actions/cheer/dark/animated/1/2.gif"] = CheerPng;
            twitch.Files["https://cheer.test/actions/cheer/dark/animated/100/2.gif"] = CheerPng;
            var options = Options(temp);
            options.EmbedData = true;
            options.BttvEmotes = true;

            var chat = await Download(options);

            var firstParty = Assert.Single(chat.embeddedData.firstParty);
            Assert.Equal("25", firstParty.id);
            Assert.Equal(EmotePng, firstParty.data);
            var thirdParty = Assert.Single(chat.embeddedData.thirdParty);
            Assert.Equal(("bttvFake", "bttv1"), (thirdParty.name, thirdParty.id));
            Assert.Equal(BttvPng, thirdParty.data);
            var badge = Assert.Single(chat.embeddedData.twitchBadges);
            Assert.Equal("subscriber", badge.name);
            Assert.Equal(BadgePng, badge.versions["1"].bytes);
            var bits = Assert.Single(chat.embeddedData.twitchBits);
            Assert.Equal("Cheer", bits.prefix);
            Assert.Equal([1, 100], bits.tierList.Keys.Order());
            Assert.Empty(twitch.RequestsTo("bttv2"));
        }

        /// <summary>
        /// Clip chats are downloaded from the clip's source VOD: the window is [videoOffsetSeconds, + duration], the file
        /// records the VOD id, the clipper and a generated clip chapter, and comments outside the clip are excluded.
        /// </summary>
        [Fact]
        public async Task ClipChat_UsesSourceVodWindow()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Comments.Clear();
            fakes.Twitch.Comments.AddRange([
                FakeTwitch.Comment("before", 90, "1", "too early"),
                FakeTwitch.Comment("in1", 105, "1", "first"),
                FakeTwitch.Comment("in2", 120, "2", "second"),
                FakeTwitch.Comment("after", 140, "2", "too late"),
            ]);

            var options = Options(temp, id: FakeTwitch.CLIP_ID);
            var chat = await Download(options);

            Assert.Equal(["in1", "in2"], chat.comments.Select(c => c._id));
            Assert.Equal(FakeTwitch.VIDEO_ID.ToString(), chat.video.id);
            Assert.Equal((100d, 130d, 30d), (chat.video.start, chat.video.end, chat.video.length));
            Assert.Equal("Fake Clip", chat.video.title);
            // ChatJson.DeserializeAsync does not read "clipper" back, so check the raw file
            using var raw = JsonDocument.Parse(await File.ReadAllBytesAsync(options.Filename, TestContext.Current.CancellationToken));
            var clipper = raw.RootElement.GetProperty("clipper");
            Assert.Equal(("FakeClipper", "fakeclipper", 22222), (clipper.GetProperty("name").GetString(), clipper.GetProperty("login").GetString(), clipper.GetProperty("id").GetInt32()));
            Assert.Equal(30_000, Assert.Single(chat.video.chapters).lengthMilliseconds);
            Assert.Contains("\"contentOffsetSeconds\":100", CommentRequestBodies(fakes.Twitch).First());
        }

        /// <summary>
        /// A transient HTTP error and a page with a null comment list (Twitch does this under load) are both retried with
        /// back-off and logged, and the download still completes with every comment.
        /// </summary>
        [Fact]
        public async Task TransientCommentErrors_AreRetried()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            var commentCalls = 0;
            fakes.Twitch.Intercept = (_, body) =>
            {
                if (body?.Contains("VideoCommentsByOffsetOrCursor") != true) return null;
                return Interlocked.Increment(ref commentCalls) switch
                {
                    1 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                    2 => FakeHttpMessageHandler.Json("{\"data\":{\"video\":{\"id\":\"1234567890\",\"comments\":null}}}"),
                    _ => null
                };
            };
            var progress = new RecordingTaskProgress();

            var chat = await Download(Options(temp), progress);

            Assert.Equal(5, chat.comments.Count);
            Assert.Contains(progress.Verbose, x => x.StartsWith("Exception 'Response status code does not indicate success: 503"));
            Assert.Contains(progress.Verbose, x => x.StartsWith("Received null comment list"));
        }

        /// <summary>
        /// Some old VODs have every comment offset by a fixed amount relative to its creation time; when the first comment is
        /// more than 5s out of sync, all offsets are shifted back (and logged) so chat lines up with the video again.
        /// </summary>
        [Fact]
        public async Task DesyncedCommentOffsets_AreRealigned()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Video!.lengthSeconds = 720;
            fakes.Twitch.Comments.Clear();
            var a = FakeTwitch.Comment("a", 700, "1", "first");
            a.createdAt = FakeTwitch.VideoCreatedAt.AddSeconds(100);
            var b = FakeTwitch.Comment("b", 710, "2", "second");
            b.createdAt = FakeTwitch.VideoCreatedAt.AddSeconds(110);
            fakes.Twitch.Comments.AddRange([a, b]);
            var progress = new RecordingTaskProgress();

            var chat = await Download(Options(temp), progress);

            Assert.Equal([100d, 110d], chat.comments.Select(c => c.content_offset_seconds));
            Assert.Contains(progress.Info, x => x == "Video comments are offset by ~600 seconds. Adjusting offsets...");
        }

        /// <summary>
        /// Since April 2025 Twitch sends comment createdAt in local time with a UTC suffix; a createdAt more than 5 minutes
        /// before the video's creation is reinterpreted as local time and converted to UTC.
        /// </summary>
        [Fact]
        public async Task CreatedAtBeforeVideo_IsTreatedAsLocalTime()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Comments.Clear();
            var comment = FakeTwitch.Comment("a", 2, "1", "hi");
            comment.createdAt = FakeTwitch.VideoCreatedAt.AddHours(-1);
            fakes.Twitch.Comments.Add(comment);

            var chat = await Download(Options(temp));

            Assert.Equal(DateTime.SpecifyKind(comment.createdAt, DateTimeKind.Local).ToUniversalTime(), chat.comments.Single().created_at);
        }

        /// <summary>
        /// When Twitch reports a video length of 0 (still processing), the last comment's offset is used as the length and
        /// end so the renderer doesn't produce a 0-second video.
        /// </summary>
        [Fact]
        public async Task ZeroLengthVideo_UsesLastCommentAsLength()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Video!.lengthSeconds = 0;

            var chat = await Download(Options(temp));

            Assert.Equal(5, chat.comments.Count);
            Assert.Equal((58d, 58d), (chat.video.length, chat.video.end));
        }

        /// <summary>A VOD without any chat still produces a valid (empty) chat file with the video metadata.</summary>
        [Fact]
        public async Task VodWithoutChat_WritesEmptyCommentList()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Comments.Clear();

            var chat = await Download(Options(temp));

            Assert.Empty(chat.comments);
            Assert.Equal(("Fake VOD", 60d), (chat.video.title, chat.video.length));
        }

        /// <summary>A failing user backfill is best effort: it is logged and the chat is still written.</summary>
        [Fact]
        public async Task BackfillFailure_IsNotFatal()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Intercept = (_, body) => body?.Contains("query{users(ids:") == true ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : null;
            var progress = new RecordingTaskProgress();

            var chat = await Download(Options(temp), progress);

            Assert.Equal(5, chat.comments.Count);
            Assert.All(chat.comments, c => Assert.Null(c.commenter.bio));
            Assert.Contains(progress.Info, x => x == "Failed to backfill some commenter info");
        }

        /// <summary>A deleted/expired VOD fails with a clear message and the empty output file is removed.</summary>
        [Fact]
        public async Task DeletedVod_Throws_AndRemovesOutput()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Video = null;
            var options = Options(temp);

            var ex = await Assert.ThrowsAsync<NullReferenceException>(() => new ChatDownloader(options, new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.Equal("Invalid VOD, deleted/expired VOD possibly?", ex.Message);
            Assert.False(File.Exists(options.Filename));
        }

        /// <summary>A clip whose source VOD is gone has no chat to download; this is reported instead of crashing.</summary>
        [Fact]
        public async Task ClipWithoutVod_Throws()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Clip!.video = null;

            var ex = await Assert.ThrowsAsync<NullReferenceException>(() => new ChatDownloader(Options(temp, id: FakeTwitch.CLIP_ID), new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.Equal("Invalid VOD for clip, deleted/expired VOD possibly?", ex.Message);
        }

        /// <summary>An empty id is rejected before any file is created or request is sent.</summary>
        [Fact]
        public async Task EmptyId_IsRejectedUpFront()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            var options = Options(temp, id: " ");

            await Assert.ThrowsAsync<NullReferenceException>(() => new ChatDownloader(options, new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.False(File.Exists(options.Filename));
            Assert.Empty(fakes.Twitch.Handler.Requests);
        }

        /// <summary>Cancellation stops the comment download and removes the empty output file.</summary>
        [Fact]
        public async Task Cancelled_Throws_AndRemovesOutput()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            var options = Options(temp);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ChatDownloader(options, new RecordingTaskProgress()).DownloadAsync(new CancellationToken(true)));

            Assert.False(File.Exists(options.Filename));
            Assert.Empty(CommentRequestBodies(fakes.Twitch));
        }
    }
}
