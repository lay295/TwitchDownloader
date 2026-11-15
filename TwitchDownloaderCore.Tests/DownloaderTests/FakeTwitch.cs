using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using SkiaSharp;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore.Tests.DownloaderTests
{
    /// <summary>
    /// An offline stand-in for every endpoint the downloaders talk to: Twitch GQL (video/clip info, access tokens, chapters,
    /// comments, users, badges, cheermotes), the usher master playlist, the VOD CDN (media playlists + segments), clip CDN,
    /// emote/badge image CDNs and BTTV. Payloads are built from the production GQL DTOs so they always match what the
    /// deserializers expect. Tests tweak the public fields (e.g. set <see cref="Video"/> to null for a deleted VOD) and can
    /// hook <see cref="Intercept"/> to inject failures; anything not handled answers 404.
    /// </summary>
    internal sealed class FakeTwitch
    {
        public const long VIDEO_ID = 1234567890;
        public const string CLIP_ID = "FakeClipSlug";
        public const int STREAMER_ID = 11111;
        public const string VOD_BASE = "https://vod.test/0123abcd_fakestreamer_1234567890_1700000000/";
        public static readonly DateTime VideoCreatedAt = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public VideoInfo? Video = new()
        {
            title = "Fake VOD",
            thumbnailURLs = [],
            createdAt = VideoCreatedAt,
            lengthSeconds = 60,
            owner = new VideoOwner { id = STREAMER_ID.ToString(), displayName = "FakeStreamer", login = "fakestreamer" },
            viewCount = 42,
            game = new Game { id = "509670", displayName = "Science & Technology", boxArtURL = "https://box.test/{width}x{height}.jpg" },
            // Twitch duplicates newlines in descriptions; the downloaders are expected to clean this up
            description = "Line one  \nLine two\n\n",
            status = "RECORDED"
        };

        /// <summary>Chapters returned by VideoPlayer_ChapterSelectButtonVideo. Empty = single-chapter VOD (one is generated).</summary>
        public List<VideoMomentEdge> Chapters = [];

        public VideoPlaybackAccessToken? Token = new() { value = "{\"vod_id\":1234567890}", signature = "fakesig" };

        /// <summary>Overrides the usher response (status + body), e.g. a sub-only 403.</summary>
        public (HttpStatusCode status, string body)? UsherOverride;

        /// <summary>Static files served by absolute URL (query string ignored): media playlists, segments, images, clips.</summary>
        public readonly ConcurrentDictionary<string, byte[]> Files = new();

        public readonly List<CommentNode> Comments = [];
        private const int COMMENT_PAGE_SIZE = 2;

        public ShareClipRenderStatusClip? Clip = DefaultClip();

        public readonly List<User> Users = [];

        /// <summary>BTTV global emotes as (id, code). Null = BTTV answers 404.</summary>
        public List<(string id, string code)>? BttvGlobalEmotes;

        /// <summary>Global chat badges as (setID, version, imageURL).</summary>
        public List<(string setId, string version, string url)> GlobalBadges = [];

        /// <summary>Cheermote groups as (id, prefix, tiers).</summary>
        public List<(string id, string prefix, int[] tiers)> Cheermotes = [];

        /// <summary>Called first for every request; return null to fall through to the default routing.</summary>
        public Func<HttpRequestMessage, string?, HttpResponseMessage?>? Intercept;

        public FakeHttpMessageHandler Handler { get; }

        public FakeTwitch()
        {
            Handler = new FakeHttpMessageHandler(Respond);
        }

        public IEnumerable<FakeHttpMessageHandler.RecordedRequest> RequestsTo(string urlFragment) =>
            Handler.Requests.Where(r => r.Uri.AbsoluteUri.Contains(urlFragment, StringComparison.Ordinal));

        public IEnumerable<FakeHttpMessageHandler.RecordedRequest> GqlRequests(string bodyFragment) =>
            Handler.Requests.Where(r => r.Body?.Contains(bodyFragment, StringComparison.Ordinal) == true);

        private HttpResponseMessage Respond(HttpRequestMessage request, string? body)
        {
            if (Intercept?.Invoke(request, body) is { } intercepted)
            {
                return intercepted;
            }

            var uri = request.RequestUri!;
            if (uri.Host == "gql.twitch.tv")
            {
                return RespondGql(body ?? "");
            }

            if (uri.Host == "usher.ttvnw.net")
            {
                return UsherOverride is var (status, text)
                    ? FakeHttpMessageHandler.Text(text, status)
                    : FakeHttpMessageHandler.Text(MasterPlaylist());
            }

            if (uri.AbsoluteUri == "https://api.betterttv.net/3/cached/emotes/global" && BttvGlobalEmotes is not null)
            {
                return Json(BttvGlobalEmotes.Select(e => new { e.id, e.code, imageType = "png", animated = false }));
            }

            return Files.TryGetValue(uri.GetLeftPart(UriPartial.Path), out var bytes)
                ? FakeHttpMessageHandler.Bytes(bytes)
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private HttpResponseMessage RespondGql(string body)
        {
            if (body.Contains("VideoCommentsByOffsetOrCursor")) return CommentPage(body);
            if (body.Contains("PlaybackAccessToken_Template")) return Json(new GqlVideoTokenResponse { data = new GqlVideoData { videoPlaybackAccessToken = Token } });
            if (body.Contains("VideoPlayer_ChapterSelectButtonVideo"))
                return Json(new GqlVideoChapterResponse { data = new ChapterData { video = new ChapterVideo { id = VIDEO_ID.ToString(), moments = new VideoMomentConnection { edges = Chapters } } } });
            if (body.Contains("ShareClipRenderStatus")) return Json(new GqlShareClipRenderStatusResponse { data = new ShareClipRenderStatusData { clip = Clip } });
            if (body.Contains("query{video(id:")) return Json(new GqlVideoResponse { data = new VideoData { video = Video } });
            if (body.Contains("query{users(ids:")) return Json(new GqlUserInfoResponse { data = new UserInfoData { users = Users.Where(u => body.Contains(u.id)).ToList() } });
            if (body.Contains("query{badges{"))
                return Json(new GqlGlobalBadgeResponse { data = new TwitchGlobalChatBadgeData { badges = GlobalBadges.Select(b => new TwitchChatBadge { name = b.setId, version = b.version, image_url_2x = b.url, title = b.setId, description = b.setId }).ToList() } });
            if (body.Contains("broadcastBadges")) return Json(new GqlSubBadgeResponse { data = new TwitchUserChatBadgeData { user = new TwitchChatBadgeUser { badges = [] } } });
            if (body.Contains("cheerConfig"))
            {
                var group = new CheerGroup
                {
                    templateURL = "https://cheer.test/actions/PREFIX/BACKGROUND/ANIMATION/TIER/SCALE.EXTENSION",
                    nodes = Cheermotes.Select(c => new CheerNode { id = c.id, prefix = c.prefix, tiers = c.tiers.Select(t => new Tier { bits = t }).ToList() }).ToList()
                };
                return Json(new GqlCheerResponse { data = new CheerData { cheerConfig = new CheerConfig { groups = [group] } } });
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        /// <summary>Serves comments like Twitch: by content offset on the first request, then by the cursor of the last edge.</summary>
        private HttpResponseMessage CommentPage(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var variables = doc.RootElement.GetProperty("variables");

            var ordered = Comments.OrderBy(c => c.contentOffsetSeconds).ToList();
            int startIndex;
            if (variables.TryGetProperty("cursor", out var cursor))
            {
                startIndex = int.Parse(cursor.GetString()!) + 1;
            }
            else
            {
                var offset = variables.GetProperty("contentOffsetSeconds").GetInt32();
                startIndex = ordered.FindIndex(c => c.contentOffsetSeconds >= offset);
                if (startIndex == -1) startIndex = ordered.Count;
            }

            var page = ordered.Skip(startIndex).Take(COMMENT_PAGE_SIZE).ToList();
            var edges = page.Select((node, i) => new CommentEdge { cursor = (startIndex + i).ToString(), node = node }).ToList();

            return Json(new GqlCommentResponse
            {
                data = new CommentData
                {
                    video = new CommentVideo
                    {
                        id = VIDEO_ID.ToString(),
                        creator = new Creator { id = STREAMER_ID.ToString(), channel = new CommentChannel { id = STREAMER_ID.ToString() } },
                        comments = new CommentComments
                        {
                            edges = edges,
                            pageInfo = new CommentPageInfo { hasNextPage = startIndex + page.Count < ordered.Count }
                        }
                    }
                }
            });
        }

        public static HttpResponseMessage Json<T>(T value) => FakeHttpMessageHandler.Json(JsonSerializer.Serialize(value));

        public static CommentNode Comment(string id, int offsetSeconds, string userId, string text, string? emoteId = null, string? badge = null)
        {
            var fragments = new List<CommentFragment> { new() { text = text } };
            if (emoteId is not null)
            {
                fragments.Add(new CommentFragment { text = " " });
                fragments.Add(new CommentFragment { text = "Kappa", emote = new CommentEmote { emoteID = emoteId, from = text.Length + 1 } });
            }

            return new CommentNode
            {
                id = id,
                commenter = new CommentCommenter { id = userId, login = $"user{userId}", displayName = $" User{userId} " },
                contentOffsetSeconds = offsetSeconds,
                createdAt = VideoCreatedAt.AddSeconds(offsetSeconds),
                message = new CommentMessage
                {
                    fragments = fragments,
                    userBadges = badge is null ? [new CommentUserBadge()] : [new CommentUserBadge { setID = badge, version = "1" }],
                    userColor = "#FF0000"
                }
            };
        }

        public static ShareClipRenderStatusClip DefaultClip() => new()
        {
            id = "987654321",
            slug = CLIP_ID,
            title = "Fake Clip",
            viewCount = 7,
            createdAt = VideoCreatedAt.AddMinutes(5),
            durationSeconds = 30,
            videoOffsetSeconds = 100,
            video = new ShareClipRenderStatusVideo { id = VIDEO_ID.ToString() },
            broadcaster = new ShareClipRenderStatusBroadcaster { id = STREAMER_ID.ToString(), login = "fakestreamer", displayName = "FakeStreamer" },
            curator = new ShareClipRenderStatusCurator { id = "22222", login = "fakeclipper", displayName = "FakeClipper" },
            game = new ShareClipRenderStatusGame { id = "509670", displayName = "Science & Technology", boxArtURL = "" },
            playbackAccessToken = new ShareClipRenderStatusPlaybackAccessToken { signature = "clipsig", value = "{\"clip\":\"a b\"}" },
            assets =
            [
                new ShareClipRenderStatusAsset
                {
                    id = "asset", aspectRatio = 1.7777m, type = "SOURCE", thumbnailURL = "",
                    videoQualities =
                    [
                        new ShareClipRenderStatusVideoQuality { quality = "720", frameRate = 30, width = 1280, height = 720, sourceURL = "https://clips.test/720.mp4" },
                        new ShareClipRenderStatusVideoQuality { quality = "1080", frameRate = 60, width = 1920, height = 1080, sourceURL = "https://clips.test/1080.mp4" },
                    ]
                }
            ]
        };

        /// <summary>A usher master playlist offering 1080p60 (chunked/source), 720p30 and audio only.</summary>
        public static string MasterPlaylist(string sourceCodecs = "avc1.64002A,mp4a.40.2") =>
            "#EXTM3U\n" +
            "#EXT-X-TWITCH-INFO:ORIGIN=\"s3\",B=\"false\",REGION=\"NA\",USER-IP=\"127.0.0.1\",SERVING-ID=\"fake\",CLUSTER=\"cloudfront_vod\",USER-COUNTRY=\"US\",MANIFEST-CLUSTER=\"cloudfront_vod\"\n" +
            $"#EXT-X-STREAM-INF:BANDWIDTH=8000,CODECS=\"{sourceCodecs}\",RESOLUTION=1920x1080,FRAME-RATE=60.000,STABLE-VARIANT-ID=\"1080p60\",IVS-NAME=\"1080p60\",IVS-GROUPS=\"landscape\",IVS-VARIANT-SOURCE=\"source\"\n" +
            $"{VOD_BASE}chunked/index-dvr.m3u8\n" +
            "#EXT-X-STREAM-INF:BANDWIDTH=4000,CODECS=\"avc1.4D401F,mp4a.40.2\",RESOLUTION=1280x720,FRAME-RATE=30.000,STABLE-VARIANT-ID=\"720p30\",IVS-NAME=\"720p\",IVS-GROUPS=\"landscape\",IVS-VARIANT-SOURCE=\"transcode\"\n" +
            $"{VOD_BASE}720p30/index-dvr.m3u8\n" +
            "#EXT-X-STREAM-INF:BANDWIDTH=1000,CODECS=\"mp4a.40.2\",STABLE-VARIANT-ID=\"audio_only\",IVS-NAME=\"Audio Only\",IVS-GROUPS=\"landscape\",IVS-VARIANT-SOURCE=\"source\"\n" +
            $"{VOD_BASE}audio_only/index-dvr.m3u8\n";

        /// <summary>A VOD media playlist of 1s parts. <paramref name="airDate"/> controls whether muted parts are worth un-muting (VODs under 24h old).</summary>
        public static string MediaPlaylist(IEnumerable<string> parts, DateTimeOffset? airDate = null, string? mapUri = null)
        {
            var sb = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:1\n");
            sb.Append("#ID3-EQUIV-TDTG:").Append((airDate ?? new DateTimeOffset(VideoCreatedAt)).ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("#EXT-X-PLAYLIST-TYPE:EVENT\n#EXT-X-MEDIA-SEQUENCE:0\n");
            if (mapUri is not null) sb.Append("#EXT-X-MAP:URI=\"").Append(mapUri).Append("\"\n");
            foreach (var part in parts)
            {
                sb.Append("#EXTINF:1.000,\n").Append(part).Append('\n');
            }

            return sb.Append("#EXT-X-ENDLIST\n").ToString();
        }

        /// <summary>Serves the three committed 1s MPEG-TS segments (0.ts..2.ts) as the given quality of the fake VOD.</summary>
        public void AddTsVod(string quality = "chunked")
        {
            Files[$"{VOD_BASE}{quality}/index-dvr.m3u8"] = Encoding.UTF8.GetBytes(MediaPlaylist(["0.ts", "1.ts", "2.ts"]));
            for (var i = 0; i < 3; i++)
            {
                Files[$"{VOD_BASE}{quality}/{i}.ts"] = TsSegment(i);
            }
        }

        public static byte[] TsSegment(int index) => File.ReadAllBytes(TestData.Path("Downloaders", "vod_ts", $"{index}.ts"));

        public static byte[] Png(SKColor color, int size = 4)
        {
            using var bitmap = new SKBitmap(size, size);
            bitmap.Erase(color);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }

    /// <summary>
    /// Reads a media file with ffmpeg (ffprobe is not guaranteed to be installed): returns the container metadata
    /// (ffmetadata format, from stdout) and ffmpeg's stream/duration report (stderr).
    /// </summary>
    internal static class MediaProbe
    {
        public static (string metadata, string report) Probe(string file)
        {
            var psi = new ProcessStartInfo(TestData.RequireFfmpeg()) { RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in new[] { "-hide_banner", "-i", file, "-f", "ffmetadata", "-" }) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi)!;
            var stderr = process.StandardError.ReadToEndAsync();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (stdout, stderr.Result);
        }

        public static double DurationSeconds(string report)
        {
            var match = System.Text.RegularExpressions.Regex.Match(report, @"Duration: (\d+):(\d+):(\d+\.\d+)");
            Assert.True(match.Success, $"No duration in ffmpeg report:\n{report}");
            return int.Parse(match.Groups[1].Value) * 3600 + int.Parse(match.Groups[2].Value) * 60
                   + double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
