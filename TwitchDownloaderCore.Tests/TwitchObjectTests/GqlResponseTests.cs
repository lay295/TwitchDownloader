using System.Text.Json;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore.Tests.TwitchObjectTests
{
    // Tests the Twitch GQL response models under TwitchObjects/Gql. They are plain DTOs whose property names must match
    // Twitch's GraphQL field names (a few are remapped with [JsonPropertyName]); a typo or rename deserializes silently to
    // null and only shows up as a NullReferenceException deep in a download. Each test parses a trimmed, synthetic copy of
    // the real response shape with the Web defaults that HttpContent.ReadFromJsonAsync uses in TwitchHelper/ChatDownloader.
    public class GqlResponseTests
    {
        private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

        private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, WebOptions)!;

        private static readonly DateTime CreatedAt = new(2024, 1, 1, 0, 1, 5, 123, DateTimeKind.Utc);

        /// <summary>
        /// The chat comment page that ChatDownloader converts into chat JSON: creator/channel ids, commenter, offset,
        /// fragments with emote ids and offsets, user badges (setID/version) and paging info.
        /// </summary>
        [Fact]
        public void CommentResponse_Deserializes()
        {
            var response = Parse<GqlCommentResponse>("""
                {"data":{"video":{"id":"1000000001","creator":{"id":"100000001","channel":{"id":"100000001"}},
                  "comments":{"edges":[{"cursor":"cursor1","node":{"id":"c1","commenter":{"id":"100000011","login":"viewer_one","displayName":"Viewer_One"},
                    "contentOffsetSeconds":65,"createdAt":"2024-01-01T00:01:05.123Z",
                    "message":{"fragments":[{"emote":{"id":"e1","emoteID":"25","from":0},"text":"Kappa"},{"emote":null,"text":" hi"}],
                               "userBadges":[{"id":"b1","setID":"subscriber","version":"3"}],"userColor":"#FF0000"}}}],
                   "pageInfo":{"hasNextPage":true,"hasPreviousPage":false}}}},
                 "extensions":{"durationMilliseconds":42,"requestID":"req1"}}
                """);

            var video = response.data.video;
            Assert.Equal(("1000000001", "100000001", "100000001"), (video.id, video.creator.id, video.creator.channel.id));
            Assert.True(video.comments.pageInfo.hasNextPage);
            Assert.False(video.comments.pageInfo.hasPreviousPage);
            var edge = Assert.Single(video.comments.edges);
            Assert.Equal("cursor1", edge.cursor);
            var node = edge.node;
            Assert.Equal(("c1", 65, CreatedAt), (node.id, node.contentOffsetSeconds, node.createdAt.ToUniversalTime()));
            Assert.Equal(("100000011", "viewer_one", "Viewer_One"), (node.commenter.id, node.commenter.login, node.commenter.displayName));
            Assert.Equal(("e1", "25", 0), (node.message.fragments[0].emote.id, node.message.fragments[0].emote.emoteID, node.message.fragments[0].emote.from));
            Assert.Null(node.message.fragments[1].emote);
            Assert.Equal(("b1", "subscriber", "3"), (node.message.userBadges[0].id, node.message.userBadges[0].setID, node.message.userBadges[0].version));
            Assert.Equal("#FF0000", node.message.userColor);
            Assert.Equal((42, "req1"), (response.extensions.durationMilliseconds, response.extensions.requestID));
        }

        /// <summary>
        /// Global and channel badge responses. imageURL, setID and broadcastBadges are remapped to image_url_2x, name and
        /// badges via [JsonPropertyName]; TwitchHelper groups badges by name and downloads image_url_2x.
        /// </summary>
        [Fact]
        public void BadgeResponses_DeserializeRemappedNames()
        {
            var global = Parse<GqlGlobalBadgeResponse>("""
                {"data":{"badges":[{"imageURL":"https://example.invalid/vip.png","description":"VIP badge","title":"VIP","setID":"vip","version":"1"}]}}
                """);
            var channel = Parse<GqlSubBadgeResponse>("""
                {"data":{"user":{"broadcastBadges":[{"imageURL":"https://example.invalid/sub3.png","description":"3 months","title":"3-Month Subscriber","setID":"subscriber","version":"3"}]}}}
                """);

            var vip = Assert.Single(global.data.badges);
            Assert.Equal(("https://example.invalid/vip.png", "VIP badge", "VIP", "vip", "1"), (vip.image_url_2x, vip.description, vip.title, vip.name, vip.version));
            var sub = Assert.Single(channel.data.user.badges);
            Assert.Equal(("subscriber", "3", "https://example.invalid/sub3.png"), (sub.name, sub.version, sub.image_url_2x));

            var set = new TwitchBadgeSet { versions = new() { ["1"] = vip } };
            Assert.Same(vip, set.versions["1"]);
        }

        /// <summary>
        /// Cheermote config: global groups and the channel's custom cheer groups, each with prefixes, tier thresholds and the
        /// URL template TwitchHelper fills in (PREFIX/BACKGROUND/ANIMATION/TIER/SCALE.EXTENSION).
        /// </summary>
        [Fact]
        public void CheerResponse_Deserializes()
        {
            var response = Parse<GqlCheerResponse>("""
                {"data":{"cheerConfig":{"groups":[{"nodes":[{"id":"n1","prefix":"Cheer","tiers":[{"bits":1},{"bits":100},{"bits":1000}]}],
                                                   "templateURL":"https://example.invalid/actions/PREFIX/BACKGROUND/ANIMATION/TIER/SCALE.EXTENSION"}]},
                         "user":{"cheer":{"cheerGroups":[{"nodes":[{"id":"n2","prefix":"TestCheer","tiers":[{"bits":1}]}],"templateURL":"https://example.invalid/PREFIX"}]}}}}
                """);

            var group = Assert.Single(response.data.cheerConfig.groups);
            Assert.Equal("https://example.invalid/actions/PREFIX/BACKGROUND/ANIMATION/TIER/SCALE.EXTENSION", group.templateURL);
            var node = Assert.Single(group.nodes);
            Assert.Equal(("n1", "Cheer"), (node.id, node.prefix));
            Assert.Equal([1, 100, 1000], node.tiers.Select(t => t.bits));
            Assert.Equal("TestCheer", response.data.user.cheer.cheerGroups[0].nodes[0].prefix);
        }

        /// <summary>
        /// Video chapters ("moments"): the GQL field "type" is remapped to _type, positions/durations are milliseconds and the
        /// game details are nested; this feeds the chapter list stored in chat JSON and video metadata.
        /// </summary>
        [Fact]
        public void VideoChapterResponse_DeserializesRemappedType()
        {
            var response = Parse<GqlVideoChapterResponse>("""
                {"data":{"video":{"id":"1000000001","moments":{"edges":[{"node":{"moments":{"edges":[]},"id":"m1","durationMilliseconds":60000,"positionMilliseconds":5000,
                  "type":"GAME_CHANGE","description":"Software and Game Development","subDescription":"","thumbnailURL":"https://example.invalid/t.jpg",
                  "details":{"game":{"id":"1469308723","displayName":"Software and Game Development","boxArtURL":"https://example.invalid/box.jpg"}},
                  "video":{"id":"1000000001","lengthSeconds":120}}}]}}}}
                """);

            var moment = Assert.Single(response.data.video.moments.edges).node;
            Assert.Equal(("m1", 60000, 5000, "GAME_CHANGE"), (moment.id, moment.durationMilliseconds, moment.positionMilliseconds, moment._type));
            Assert.Equal(("1469308723", "Software and Game Development"), (moment.details.game.id, moment.details.game.displayName));
            Assert.Equal(120, moment.video.lengthSeconds);
            Assert.Empty(moment.moments.edges);
            Assert.Equal("https://example.invalid/t.jpg", moment.thumbnailURL);
        }

        /// <summary>VOD metadata used for chat/video downloads and filename templates (title, owner, length, game, status...).</summary>
        [Fact]
        public void VideoResponse_Deserializes()
        {
            var info = Parse<GqlVideoResponse>("""
                {"data":{"video":{"title":"Sample","thumbnailURLs":["https://example.invalid/thumb.jpg"],"createdAt":"2024-01-01T00:01:05.123Z","lengthSeconds":120,
                  "owner":{"id":"100000001","displayName":"TestStreamer","login":"teststreamer"},"viewCount":42,
                  "game":{"id":"1469308723","displayName":"Software and Game Development","boxArtURL":"https://example.invalid/box.jpg"},
                  "description":"desc","status":"RECORDED"}}}
                """).data.video;

            Assert.Equal(("Sample", 120, 42, "desc", "RECORDED", CreatedAt), (info.title, info.lengthSeconds, info.viewCount, info.description, info.status, info.createdAt.ToUniversalTime()));
            Assert.Equal(("100000001", "TestStreamer", "teststreamer"), (info.owner.id, info.owner.displayName, info.owner.login));
            Assert.Equal("https://example.invalid/thumb.jpg", Assert.Single(info.thumbnailURLs));
            Assert.Equal("https://example.invalid/box.jpg", info.game.boxArtURL);
        }

        /// <summary>
        /// Clip metadata: curator and broadcaster, duration, and the nullable videoOffsetSeconds (null when the source VOD is
        /// gone), which decides whether clip chat can be downloaded.
        /// </summary>
        [Fact]
        public void ClipResponse_Deserializes()
        {
            var clip = Parse<GqlClipResponse>("""
                {"data":{"clip":{"title":"Clip","thumbnailURL":"https://example.invalid/c.jpg","createdAt":"2024-01-01T00:01:05.123Z",
                  "curator":{"id":"100000011","displayName":"Viewer_One","login":"viewer_one"},"durationSeconds":30,
                  "broadcaster":{"id":"100000001","displayName":"TestStreamer","login":"teststreamer"},"videoOffsetSeconds":null,
                  "video":null,"viewCount":7,"game":{"id":"1","displayName":"Game"}}}}
                """).data.clip;

            Assert.Equal(("Clip", 30, 7, CreatedAt), (clip.title, clip.durationSeconds, clip.viewCount, clip.createdAt.ToUniversalTime()));
            Assert.Equal(("100000011", "viewer_one"), (clip.curator.id, clip.curator.login));
            Assert.Equal(("100000001", "TestStreamer"), (clip.broadcaster.id, clip.broadcaster.displayName));
            Assert.Null(clip.videoOffsetSeconds);
            Assert.Null(clip.video);
            Assert.Equal("Game", clip.game.displayName);

            var withVod = Parse<GqlClipResponse>("""{"data":{"clip":{"videoOffsetSeconds":13552,"video":{"id":"1000000001"}}}}""").data.clip;
            Assert.Equal((13552, "1000000001"), (withVod.videoOffsetSeconds, withVod.video.id));
        }

        /// <summary>Clip playback token and qualities (fractional frame rates, source URLs) used to pick and sign the clip download.</summary>
        [Fact]
        public void ClipTokenResponse_Deserializes()
        {
            var clip = Parse<GqlClipTokenResponse>("""
                {"data":{"clip":{"id":"clip1","playbackAccessToken":{"signature":"sig","value":"{\"token\":1}"},
                  "videoQualities":[{"frameRate":59.94,"quality":"1080","sourceURL":"https://example.invalid/1080.mp4"},{"frameRate":30,"quality":"360","sourceURL":"https://example.invalid/360.mp4"}]}}}
                """).data.clip;

            Assert.Equal(("clip1", "sig", "{\"token\":1}"), (clip.id, clip.playbackAccessToken.signature, clip.playbackAccessToken.value));
            Assert.Equal([59.94m, 30m], clip.videoQualities.Select(q => q.frameRate));
            Assert.Equal(("1080", "https://example.invalid/1080.mp4"), (clip.videoQualities[0].quality, clip.videoQualities[0].sourceURL));
        }

        /// <summary>
        /// The ShareClipRenderStatus response (newer clip API) with assets, per-asset video qualities, portrait crop metadata
        /// and broadcaster details. ClipDownloader prefers these qualities, so the nested names must keep matching.
        /// </summary>
        [Fact]
        public void ShareClipRenderStatusResponse_Deserializes()
        {
            var clip = Parse<GqlShareClipRenderStatusResponse>("""
                {"data":{"clip":{"id":"clip1","slug":"TestSlug","url":"https://example.invalid/clip","embedURL":"https://example.invalid/embed","title":"Clip","viewCount":7,
                  "language":"EN","isFeatured":false,"thumbnailURL":"https://example.invalid/c.jpg","createdAt":"2024-01-01T00:01:05.123Z","isPublished":true,"durationSeconds":30,
                  "videoOffsetSeconds":65,"isViewerEditRestricted":false,"suggestedCropping":null,
                  "assets":[{"id":"a1","aspectRatio":1.7777,"type":"SOURCE","createdAt":"2024-01-01T00:01:05.123Z","creationState":"CREATED",
                    "curator":{"id":"100000011","login":"viewer_one","displayName":"Viewer_One","profileImageURL":"https://example.invalid/p.png"},
                    "thumbnailURL":"https://example.invalid/a1.jpg",
                    "videoQualities":[{"bitrate":6000000,"codecs":"avc1.4D402A","width":1920,"height":1080,"frameRate":60,"videoCodec":"H264","quality":"1080","sourceURL":"https://example.invalid/a1.mp4"}],
                    "portraitMetadata":{"portraitClipLayout":"FULL","fullTemplateMetadata":{"mainFrame":{"topLeft":{"xPercentage":0.25,"yPercentage":0},"bottomRight":{"xPercentage":0.75,"yPercentage":1}}},"stackedTemplateMetadata":null}}],
                  "curator":{"id":"100000011","login":"viewer_one","displayName":"Viewer_One"},
                  "game":{"id":"1","name":"game","boxArtURL":"https://example.invalid/box.jpg","displayName":"Game","slug":"game"},
                  "broadcast":{"id":"b1","title":"Stream"},
                  "broadcaster":{"id":"100000001","login":"teststreamer","displayName":"TestStreamer","primaryColorHex":"9146FF","isPartner":true,"profileImageURL":"https://example.invalid/s.png",
                    "followers":{"totalCount":1234},"stream":{"id":"s1","viewersCount":99},"lastBroadcast":{"id":"lb1","startedAt":"2024-01-01T00:00:00Z"},"self":null},
                  "playbackAccessToken":{"signature":"sig","value":"tok"},
                  "video":{"id":"1000000001","broadcastType":"ARCHIVE","title":"Stream"}}},
                 "extensions":{"durationMilliseconds":1,"requestID":"r"}}
                """).data.clip;

            Assert.Equal(("clip1", "TestSlug", 30, 65, true), (clip.id, clip.slug, clip.durationSeconds, clip.videoOffsetSeconds, clip.isPublished));
            var asset = Assert.Single(clip.assets);
            Assert.Equal(("SOURCE", 1.7777m, "CREATED"), (asset.type, asset.aspectRatio, asset.creationState));
            var quality = Assert.Single(asset.videoQualities);
            Assert.Equal((6000000, 1920, 1080, 60m, "H264", "1080", "https://example.invalid/a1.mp4"),
                (quality.bitrate, quality.width, quality.height, quality.frameRate, quality.videoCodec, quality.quality, quality.sourceURL));
            Assert.Equal("FULL", asset.portraitMetadata.portraitClipLayout);
            Assert.Equal(0.75m, asset.portraitMetadata.fullTemplateMetadata.mainFrame.bottomRight.xPercentage);
            Assert.Null(asset.portraitMetadata.stackedTemplateMetadata);
            Assert.Equal(("teststreamer", 1234L, 99L, "lb1"), (clip.broadcaster.login, clip.broadcaster.followers.totalCount, clip.broadcaster.stream.viewersCount, clip.broadcaster.lastBroadcast.id));
            Assert.Equal(("sig", "tok"), (clip.playbackAccessToken.signature, clip.playbackAccessToken.value));
            Assert.Equal(("ARCHIVE", "Game", "Stream"), (clip.video.broadcastType, clip.game.displayName, clip.broadcast.title));
        }

        /// <summary>User lookups: ChatJson uses GetUserInfo to recover streamer logins, the CLI uses user ids for channel lookups.</summary>
        [Fact]
        public void UserResponses_Deserialize()
        {
            var info = Assert.Single(Parse<GqlUserInfoResponse>("""
                {"data":{"users":[{"id":"100000001","displayName":"TestStreamer","login":"teststreamer","createdAt":"2024-01-01T00:01:05.123Z",
                  "updatedAt":"2024-01-01T00:01:05.123Z","description":"bio","profileImageURL":"https://example.invalid/p.png"}]}}
                """).data.users);
            var id = Assert.Single(Parse<GqlUserIdResponse>("""{"data":{"users":[{"id":"100000001"}]}}""").data.users);

            Assert.Equal(("100000001", "TestStreamer", "teststreamer", "bio", "https://example.invalid/p.png"), (info.id, info.displayName, info.login, info.description, info.profileImageURL));
            Assert.Equal(CreatedAt, info.createdAt.ToUniversalTime());
            Assert.Equal("100000001", id.id);
        }

        /// <summary>Channel VOD and clip listings used by the GUI search pages, including cursors and paging.</summary>
        [Fact]
        public void SearchResponses_Deserialize()
        {
            var videos = Parse<GqlVideoSearchResponse>("""
                {"data":{"user":{"videos":{"edges":[{"cursor":"v1","node":{"title":"Sample","id":"1000000001","lengthSeconds":120,"previewThumbnailURL":"https://example.invalid/t.jpg",
                  "createdAt":"2024-01-01T00:01:05.123Z","viewCount":42,"game":{"id":"1","displayName":"Game"}}}],"pageInfo":{"hasNextPage":false,"hasPreviousPage":true},"totalCount":1}}}}
                """).data.user.videos;
            var clips = Parse<GqlClipSearchResponse>("""
                {"data":{"user":{"clips":{"edges":[{"cursor":"c1","node":{"id":"clip1","slug":"TestSlug","title":"Clip","createdAt":"2024-01-01T00:01:05.123Z",
                  "curator":{"id":"100000011","displayName":"Viewer_One"},"durationSeconds":30,"thumbnailURL":"https://example.invalid/c.jpg","viewCount":7,
                  "game":{"id":"1","displayName":"Game"}}}],"pageInfo":{"hasNextPage":true,"hasPreviousPage":false}}}}}
                """).data.user.clips;

            Assert.Equal(1, videos.totalCount);
            Assert.True(videos.pageInfo.hasPreviousPage);
            var video = Assert.Single(videos.edges);
            Assert.Equal(("v1", "1000000001", 120, 42, "Game"), (video.cursor, video.node.id, video.node.lengthSeconds, video.node.viewCount, video.node.game.displayName));
            Assert.Equal("https://example.invalid/t.jpg", video.node.previewThumbnailURL);

            Assert.True(clips.pageInfo.hasNextPage);
            var clip = Assert.Single(clips.edges);
            Assert.Equal(("c1", "TestSlug", 30, "Viewer_One", "Game"), (clip.cursor, clip.node.slug, clip.node.durationSeconds, clip.node.curator.displayName, clip.node.game.displayName));
        }

        /// <summary>The VOD playback access token used to request the m3u8 playlist.</summary>
        [Fact]
        public void VideoTokenResponse_Deserializes()
        {
            var token = Parse<GqlVideoTokenResponse>("""{"data":{"videoPlaybackAccessToken":{"value":"tok","signature":"sig"}}}""").data.videoPlaybackAccessToken;

            Assert.Equal(("tok", "sig"), (token.value, token.signature));
        }
    }
}
