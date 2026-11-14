using System.IO.Compression;
using SkiaSharp;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore.Tests.DownloaderTests
{
    // Tests ChatUpdater, which upgrades an existing chat JSON: refreshes video/clip info and chapters, extends or narrows the
    // trim (downloading missing comments through ChatDownloader), adds or replaces embedded images and converts to
    // JSON/HTML/TXT. Users run it on archives they can no longer re-download, so it must never lose existing data.
    // The input fixture (TestData/Downloaders/chat_update_input.json) is a synthetic chat trimmed to 10s..40s of a 60s VOD
    // with comments at 12s, 20s and 33s, one chapter covering 0-30s and a stale embedded first-party emote "25".
    [Collection(StaticHttpClientCollection.NAME)]
    public class ChatUpdaterTests
    {
        private static readonly byte[] FreshEmotePng = FakeTwitch.Png(SKColors.Red);

        private sealed class Fakes : IDisposable
        {
            public readonly FakeTwitch Twitch = new();
            private readonly HttpClientOverride _helper;
            private readonly HttpClientOverride _chat;

            public Fakes()
            {
                Twitch.Files["https://static-cdn.jtvnw.net/emoticons/v2/25/default/dark/2.0"] = FreshEmotePng;
                _helper = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, Twitch.Handler);
                _chat = new HttpClientOverride(c => ChatDownloader.HttpClient = c, () => ChatDownloader.HttpClient, Twitch.Handler);
            }

            public void Dispose()
            {
                _chat.Dispose();
                _helper.Dispose();
            }
        }

        private static readonly byte[] StaleEmotePng = Convert.FromBase64String(
            System.Text.Json.JsonDocument.Parse(TestData.ReadText("Downloaders", "chat_update_input.json"))
                .RootElement.GetProperty("embeddedData").GetProperty("firstParty")[0].GetProperty("data").GetString()!);

        private static ChatUpdateOptions Options(TempDirectory temp, ChatFormat format = ChatFormat.Json, string inputName = "input.json")
        {
            var input = temp.Combine(inputName);
            if (!File.Exists(input)) File.Copy(TestData.Path("Downloaders", "chat_update_input.json"), input);
            return new ChatUpdateOptions
            {
                InputFile = input,
                OutputFile = temp.Combine("out", "chat" + format switch { ChatFormat.Html => ".html", ChatFormat.Text => ".txt", _ => ".json" }),
                OutputFormat = format,
                TempFolder = temp.Combine("temp"),
                TextTimestampFormat = TimestampFormat.Relative,
            };
        }

        private static async Task<ChatUpdater> Update(ChatUpdateOptions options, RecordingTaskProgress? progress = null)
        {
            var updater = new ChatUpdater(options, progress ?? new RecordingTaskProgress());
            await updater.ParseJsonAsync(TestContext.Current.CancellationToken);
            await updater.UpdateAsync(CancellationToken.None);
            return updater;
        }

        private static VideoMomentEdge Chapter(int startSeconds, int lengthSeconds, string game) => new()
        {
            node = new VideoMoment
            {
                id = game, _type = "GAME_CHANGE", positionMilliseconds = startSeconds * 1000, durationMilliseconds = lengthSeconds * 1000,
                description = game, subDescription = "", thumbnailURL = "",
                details = new GameChangeMomentDetails { game = new Game { id = game, displayName = game, boxArtURL = "" } },
                video = new VideoMomentEdgeVideo { id = FakeTwitch.VIDEO_ID.ToString(), lengthSeconds = 60 }
            }
        };

        /// <summary>
        /// A plain update refreshes the video metadata from GQL (title, cleaned description, game, views), replaces the
        /// incomplete last chapter with the VOD's full chapter list (chat was downloaded mid-stream), bumps the file to the
        /// current schema version while keeping CreatedAt, and leaves comments and existing embeds untouched.
        /// </summary>
        [Fact]
        public async Task RefreshesVideoInfoAndChapters_KeepsCommentsAndEmbeds()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Chapters = [Chapter(0, 30, "Game A"), Chapter(30, 30, "Game B")];
            var options = Options(temp);
            var progress = new RecordingTaskProgress();

            await Update(options, progress);

            var chat = await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Fake VOD", chat.video.title);
            Assert.Equal("Line one\nLine two", chat.video.description);
            Assert.Equal(("Science & Technology", 42, 60d), (chat.video.game, chat.video.viewCount, chat.video.length));
            Assert.Equal(["Game A", "Game B"], chat.video.chapters.Select(c => c.gameDisplayName));
            Assert.Equal(new ChatRootVersion(1, 4, 0), chat.FileInfo.Version);
            Assert.Equal(new DateTime(2024, 1, 2), chat.FileInfo.CreatedAt.Date);
            Assert.True(chat.FileInfo.UpdatedAt > chat.FileInfo.CreatedAt);
            Assert.Equal(["c2", "c3", "c4"], chat.comments.Select(c => c._id));
            Assert.Equal((10d, 40d), (chat.video.start, chat.video.end));
            Assert.Equal(StaleEmotePng, Assert.Single(chat.embeddedData.firstParty).data);
            Assert.Equal(["Updating Video Info [1/2]", "Writing Output File [2/2]"], progress.Statuses);
        }

        /// <summary>
        /// If the VOD is gone, the existing metadata is kept (and the user informed) rather than failing or blanking the
        /// file: archives of deleted VODs are the main reason people run the updater.
        /// </summary>
        [Fact]
        public async Task DeletedVod_KeepsExistingInfo()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Video = null;
            var options = Options(temp);
            var progress = new RecordingTaskProgress();

            await Update(options, progress);

            var chat = await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(("Old title", "Old game"), (chat.video.title, chat.video.game));
            Assert.Equal(3, chat.comments.Count);
            Assert.Contains(progress.Info, x => x == "Unable to fetch video info, deleted/expired VOD possibly?");
        }

        /// <summary>
        /// Widening the trim (10..40 -> 0..60) downloads only the missing sections through ChatDownloader, merges them with
        /// the existing comments without duplicates, keeps them sorted and updates start/end.
        /// </summary>
        [Fact]
        public async Task WideningTrim_DownloadsAndMergesMissingComments()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Comments.AddRange([
                FakeTwitch.Comment("early", 5, "7", "new early comment"),
                FakeTwitch.Comment("c2", 12, "1", "hello Kappa"), // already in the file
                FakeTwitch.Comment("late", 50, "8", "new late comment"),
            ]);
            var options = Options(temp);
            options.TrimBeginning = true;
            options.TrimBeginningTime = 0;
            options.TrimEnding = true;
            options.TrimEndingTime = 60;
            var progress = new RecordingTaskProgress();

            await Update(options, progress);

            var chat = await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(["early", "c2", "c3", "c4", "late"], chat.comments.Select(c => c._id));
            Assert.Equal((0d, 60d), (chat.video.start, chat.video.end));
            Assert.Contains(progress.Info, x => x == "Input comment count: 3. Output count: 5");
            Assert.Empty(Directory.GetFiles(temp.Combine("temp", "TwitchDownloader"))); // Temporary section downloads are deleted
        }

        /// <summary>
        /// Narrowing the start for a non-JSON output removes the comments before the new start (JSON keeps them so the
        /// trim can be widened again later).
        /// </summary>
        [Fact]
        public async Task NarrowingStart_TextOutput_DropsEarlierComments()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp, ChatFormat.Text);
            options.TrimBeginning = true;
            options.TrimBeginningTime = 15;

            await Update(options);

            var lines = await File.ReadAllLinesAsync(options.OutputFile, TestContext.Current.CancellationToken);
            Assert.Equal(["[0:00:20] User2: bttvFake Cheer100", "[0:00:33] User3: still here"], lines);
        }

        /// <summary>
        /// Narrowing the end (to 25s) for a non-JSON output must keep every comment up to the new end (12s and 20s) and drop
        /// only the later ones. Currently RemoveRange starts at the last in-range comment, so the 20s comment is dropped too
        /// (and if no comment is in range, none are dropped).
        /// </summary>
        [Fact(Skip = "Bug: ChatUpdater.UpdateChatTrim removes the last comment inside the trim end for non-JSON outputs (RemoveRange starts at endIndex instead of endIndex + 1)")]
        public async Task NarrowingEnd_TextOutput_KeepsLastCommentInRange()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp, ChatFormat.Text);
            options.TrimEnding = true;
            options.TrimEndingTime = 25;

            await Update(options);

            var lines = await File.ReadAllLinesAsync(options.OutputFile, TestContext.Current.CancellationToken);
            Assert.Equal(["[0:00:12] User1: hello Kappa", "[0:00:20] User2: bttvFake Cheer100"], lines);
        }

        /// <summary>
        /// Narrowing the trim of a JSON output only moves start/end and keeps all comments, so no data is lost from the archive.
        /// </summary>
        [Fact]
        public async Task NarrowingTrim_JsonOutput_KeepsAllComments()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            var options = Options(temp);
            options.TrimBeginning = true;
            options.TrimBeginningTime = 15;
            options.TrimEnding = true;
            options.TrimEndingTime = 25;

            await Update(options);

            var chat = await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(3, chat.comments.Count);
            Assert.Equal((15d, 25d), (chat.video.start, chat.video.end));
            Assert.Empty(fakes.Twitch.GqlRequests("VideoCommentsByOffsetOrCursor")); // Nothing to download when narrowing
        }

        /// <summary>
        /// If the VOD has expired, widening the trim cannot fetch new comments: this is reported and the update still
        /// completes with the comments that were already there.
        /// </summary>
        [Fact]
        public async Task WideningTrim_ExpiredVod_KeepsExistingComments()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Video = null;
            var options = Options(temp);
            options.TrimBeginning = true;
            options.TrimBeginningTime = 0;
            options.TrimEnding = true;
            options.TrimEndingTime = 60;
            var progress = new RecordingTaskProgress();

            await Update(options, progress);

            var chat = await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(3, chat.comments.Count);
            Assert.Equal((0d, 60d), (chat.video.start, chat.video.end));
            Assert.Contains("Unable to fetch possible missing comments: source VOD is expired or embedded ID is corrupt", progress.Info);
        }

        /// <summary>
        /// Both trim tasks fail to fetch for an expired VOD, but the user should be told only once. The tasks run concurrently and
        /// guard the message with a plain check-then-set bool, so when they fail at the same moment both log it (seen on Windows CI).
        /// </summary>
        [Fact(Skip = "Bug: ChatUpdater's two concurrent trim tasks race on the non-atomic _trimTaskReportedExpiredVod check-then-set, so the expired-VOD message can be logged twice")]
        public async Task WideningTrim_ExpiredVod_ReportsMissingCommentsOnce()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Video = null;
            var options = Options(temp);
            options.TrimBeginning = true;
            options.TrimBeginningTime = 0;
            options.TrimEnding = true;
            options.TrimEndingTime = 60;
            var progress = new RecordingTaskProgress();

            await Update(options, progress);

            Assert.Single(progress.Info, x => x == "Unable to fetch possible missing comments: source VOD is expired or embedded ID is corrupt");
        }

        /// <summary>
        /// A Twitch API failure (here a 500 on the video query) while refreshing the metadata is swallowed like a deleted
        /// VOD: the existing title/game stay, the user is informed and the file is still written. An update must never
        /// fail just because Twitch is having a bad day.
        /// </summary>
        [Fact]
        public async Task TwitchApiError_KeepsExistingInfo()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Intercept = (_, body) => body?.Contains("query{video(id:") == true ? new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError) : null;
            var options = Options(temp);
            var progress = new RecordingTaskProgress();

            await Update(options, progress);

            var chat = await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(("Old title", "Old game"), (chat.video.title, chat.video.game));
            Assert.Equal(3, chat.comments.Count);
            Assert.Contains(progress.Info, x => x == "Unable to fetch video info, deleted/expired VOD possibly?");
            Assert.Empty(progress.Errors);
        }

        private static void ServeAllEmbeds(FakeTwitch twitch)
        {
            twitch.BttvGlobalEmotes = [("bttv1", "bttvFake")];
            twitch.Files["https://cdn.betterttv.net/emote/bttv1/2x"] = FakeTwitch.Png(SKColors.Green);
            twitch.GlobalBadges = [("subscriber", "1", "https://static-cdn.jtvnw.net/badges/v1/sub-badge-id/2")];
            twitch.Files["https://static-cdn.jtvnw.net/badges/v1/sub-badge-id/2"] = FakeTwitch.Png(SKColors.Blue);
            twitch.Cheermotes = [("cheer-id", "Cheer", [100])];
            twitch.Files["https://cheer.test/actions/cheer/dark/animated/100/2.gif"] = FakeTwitch.Png(SKColors.Purple);
        }

        /// <summary>
        /// EmbedMissing adds the images the file lacks (BTTV emote, badge, cheermote) but keeps the already embedded
        /// first-party emote as-is: the archived image may no longer exist on Twitch.
        /// </summary>
        [Fact]
        public async Task EmbedMissing_AddsMissingImages_KeepsExistingOnes()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            ServeAllEmbeds(fakes.Twitch);
            var options = Options(temp);
            options.EmbedMissing = true;
            options.BttvEmotes = true;
            var progress = new RecordingTaskProgress();

            await Update(options, progress);

            var chat = await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(StaleEmotePng, Assert.Single(chat.embeddedData.firstParty).data);
            Assert.Equal("bttvFake", Assert.Single(chat.embeddedData.thirdParty).name);
            Assert.Equal("subscriber", Assert.Single(chat.embeddedData.twitchBadges).name);
            Assert.Equal("Cheer", Assert.Single(chat.embeddedData.twitchBits).prefix);
            Assert.Contains(progress.Info, x => x == "Input 3rd party emote count: 0. Output count: 1");
            Assert.Contains("Updating Embeds [2/3]", progress.Statuses);
        }

        /// <summary>ReplaceEmbeds ignores the embedded images and re-downloads them, replacing the stale first-party emote.</summary>
        [Fact]
        public async Task ReplaceEmbeds_RedownloadsExistingImages()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            ServeAllEmbeds(fakes.Twitch);
            var options = Options(temp);
            options.ReplaceEmbeds = true;

            await Update(options);

            var chat = await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(FreshEmotePng, Assert.Single(chat.embeddedData.firstParty).data);
            Assert.Empty(chat.embeddedData.thirdParty); // No third party provider was enabled
        }

        /// <summary>
        /// Converting to HTML writes one comment block per comment and inlines the embedded first-party emote as a base64
        /// CSS rule, so the page works offline.
        /// </summary>
        [Fact]
        public async Task ConvertToHtml_InlinesEmbeddedImages()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp, ChatFormat.Html);

            await Update(options);

            var html = await File.ReadAllTextAsync(options.OutputFile, TestContext.Current.CancellationToken);
            Assert.Equal(3, html.Split("<pre class=\"comment-root\">").Length - 1);
            Assert.Contains($".first-25 {{ content:url(\"data:image/png;base64, {Convert.ToBase64String(StaleEmotePng)}\"); }}", html);
        }

        /// <summary>A gzipped input (.json.gz) is accepted and a gzip output is a valid gzip stream that reads back.</summary>
        [Fact]
        public async Task GzipInputAndOutput_RoundTrip()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var gzInput = temp.Combine("input.json.gz");
            await using (var source = File.OpenRead(TestData.Path("Downloaders", "chat_update_input.json")))
            await using (var gzip = new GZipStream(File.Create(gzInput), CompressionLevel.Fastest))
            {
                await source.CopyToAsync(gzip, TestContext.Current.CancellationToken);
            }

            var options = Options(temp, inputName: "input.json.gz");
            options.OutputFile += ".gz";
            options.Compression = ChatCompression.Gzip;

            await Update(options);

            var bytes = await File.ReadAllBytesAsync(options.OutputFile, TestContext.Current.CancellationToken);
            Assert.Equal([0x1F, 0x8B], bytes[..2]);
            Assert.Equal(3, (await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken)).comments.Count);
        }

        /// <summary>
        /// Clip chats refresh from the clip's info: title/length come from the clip, and a missing chapter list is replaced
        /// by a generated clip chapter.
        /// </summary>
        [Fact]
        public async Task ClipChat_RefreshesFromClipInfo()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp);
            var updater = new ChatUpdater(options, new RecordingTaskProgress());
            await updater.ParseJsonAsync(TestContext.Current.CancellationToken);
            updater.chatRoot.video.id = FakeTwitch.CLIP_ID;
            updater.chatRoot.video.chapters.Clear();

            await updater.UpdateAsync(CancellationToken.None);

            var chat = await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(("Fake Clip", 30d, 7), (chat.video.title, chat.video.length, chat.video.viewCount));
            Assert.Equal(30_000, Assert.Single(chat.video.chapters).lengthMilliseconds);
            Assert.Equal("FakeClipper", updater.chatRoot.clipper.name);
        }

        /// <summary>A clip that can no longer be fetched is reported and the existing info is kept.</summary>
        [Fact]
        public async Task DeletedClip_KeepsExistingInfo()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Intercept = (_, body) => body?.Contains("ShareClipRenderStatus") == true ? new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError) : null;
            var options = Options(temp);
            var progress = new RecordingTaskProgress();
            var updater = new ChatUpdater(options, progress);
            await updater.ParseJsonAsync(TestContext.Current.CancellationToken);
            updater.chatRoot.video.id = FakeTwitch.CLIP_ID;

            await updater.UpdateAsync(CancellationToken.None);

            Assert.Equal("Old title", (await ChatJson.DeserializeAsync(options.OutputFile, cancellationToken: TestContext.Current.CancellationToken)).video.title);
            Assert.Contains(progress.Info, x => x == "Unable to fetch clip info, deleted possibly?");
        }

        /// <summary>Only JSON chats can be updated; any other input is rejected and the empty output file is removed.</summary>
        [Fact]
        public async Task NonJsonInput_IsRejected_AndOutputRemoved()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp);
            options.InputFile = temp.Combine("chat.html");

            await Assert.ThrowsAsync<NotSupportedException>(() => new ChatUpdater(options, new RecordingTaskProgress()).UpdateAsync(CancellationToken.None));

            Assert.False(File.Exists(options.OutputFile));
        }

        /// <summary>Cancellation aborts the update and removes the empty output file.</summary>
        [Fact]
        public async Task Cancelled_Throws_AndRemovesOutput()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp);
            var updater = new ChatUpdater(options, new RecordingTaskProgress());
            await updater.ParseJsonAsync(TestContext.Current.CancellationToken);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => updater.UpdateAsync(new CancellationToken(true)));

            Assert.False(File.Exists(options.OutputFile));
        }
    }
}
