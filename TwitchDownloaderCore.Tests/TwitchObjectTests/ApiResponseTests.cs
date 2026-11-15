using System.Text.Json;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Api;

namespace TwitchDownloaderCore.Tests.TwitchObjectTests
{
    // Tests the response models under TwitchObjects/Api (BTTV, FFZ, 7TV, Twitch's unavailable-media playlist tag) plus
    // StvEmoteFlags and EmoteResponse(Item). They are plain DTOs, so the risk is a renamed property or [JsonPropertyName]
    // silently deserializing to null; each test feeds a trimmed copy of the real payload through the same serializer
    // settings production uses and checks the fields the downloaders read.
    public class ApiResponseTests
    {
        // HttpContent.ReadFromJsonAsync, used by TwitchHelper for these APIs, uses the Web defaults (case-insensitive).
        private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

        private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, WebOptions)!;

        /// <summary>
        /// BTTV channel response: channel and shared emote lists (TwitchHelper concatenates both) with id/code/imageType, and
        /// the uploader info on shared emotes.
        /// </summary>
        [Fact]
        public void Bttv_ChannelResponse_Deserializes()
        {
            var response = Parse<BTTVChannelEmoteResponse>("""
                {"id":"5f000000000000000000000a","bots":[],"avatar":"https://example.invalid/a.png",
                 "channelEmotes":[{"id":"000000000000000000000b01","code":"TestBttv","imageType":"gif","animated":true,"userId":"5f000000000000000000000a"}],
                 "sharedEmotes":[{"id":"000000000000000000000b02","code":"SharedBttv","imageType":"png","animated":false,
                                  "user":{"id":"u1","name":"otheruser","displayName":"OtherUser","providerId":"100000099"}}]}
                """);

            Assert.Equal("5f000000000000000000000a", response.id);
            Assert.Empty(response.bots);
            var channel = Assert.Single(response.channelEmotes);
            Assert.Equal(("000000000000000000000b01", "TestBttv", "gif", "5f000000000000000000000a"), (channel.id, channel.code, channel.imageType, channel.userId));
            var shared = Assert.Single(response.sharedEmotes);
            Assert.Equal(("u1", "otheruser", "OtherUser", "100000099"), (shared.user.id, shared.user.name, shared.user.displayName, shared.user.providerId));
        }

        /// <summary>
        /// FFZ emotes (via BTTV's cache API): numeric id, the "1x"/"2x"/"4x" image keys mapped through [JsonPropertyName]
        /// (C# names can't start with a digit), and the animated flag that picks the animated CDN URL.
        /// </summary>
        [Fact]
        public void Ffz_Emotes_Deserialize()
        {
            var emote = Assert.Single(Parse<List<FFZEmote>>("""
                [{"id":100001,"user":{"id":7,"name":"ffzuser","displayName":"FfzUser"},"code":"TestFfz",
                  "images":{"1x":"https://example.invalid/1","2x":"https://example.invalid/2","4x":"https://example.invalid/4"},
                  "imageType":"png","animated":true,"modifier":true}]
                """));

            Assert.Equal((100001, "TestFfz", "png", true, true), (emote.id, emote.code, emote.imageType, emote.animated, emote.modifier));
            Assert.Equal(("https://example.invalid/1", "https://example.invalid/2", "https://example.invalid/4"), (emote.images._1x, emote.images._2x, emote.images._4x));
            Assert.Equal((7, "ffzuser", "FfzUser"), (emote.user.id, emote.user.name, emote.user.displayName));
        }

        /// <summary>
        /// 7TV global emote set: per-emote data (flags as StvEmoteFlags, listed, host URL and hosted files with formats).
        /// TwitchHelper filters emotes by these exact fields (webp only, not private/disallowed, zero-width flag, listed).
        /// </summary>
        [Fact]
        public void Stv_GlobalEmoteSet_Deserializes()
        {
            var response = Parse<STVGlobalEmoteResponse>("""
                {"id":"global","name":"Global Emotes","flags":0,"tags":["t"],"immutable":true,"privileged":true,
                 "emotes":[{"id":"01TESTSTVEMOTE0000000000Z1","name":"TestZeroWidth","flags":0,"timestamp":1690000000000,"actor_id":null,"origin_id":null,
                   "data":{"id":"01TESTSTVEMOTE0000000000Z1","name":"TestZeroWidth","flags":256,"lifecycle":3,"state":["LISTED"],"listed":true,"animated":true,
                     "owner":{"id":"o1","username":"owner","display_name":"Owner","avatar_url":"//example.invalid/a.webp","style":{"color":-5635841,"paint_id":null,"badge_id":null},"role_ids":["r1"]},
                     "host":{"url":"//cdn.7tv.app/emote/01TESTSTVEMOTE0000000000Z1",
                             "files":[{"name":"1x.avif","static_name":"1x_static.avif","width":32,"height":32,"frame_count":4,"size":1000,"format":"AVIF"},
                                      {"name":"1x.webp","static_name":"1x_static.webp","width":32,"height":32,"frame_count":4,"size":2000,"format":"WEBP"}]},
                     "tags":["zw"]}}],
                 "emote_count":1,"capacity":1000,"origins":[{"id":"x","weight":1,"slices":[]}],"owner":null}
                """);

            Assert.Equal(("global", 1, 1000, true, true), (response.id, response.emote_count, response.capacity, response.immutable, response.privileged));
            Assert.Equal(1, Assert.Single(response.origins).weight);
            var emote = Assert.Single(response.emotes);
            Assert.Equal((1690000000000UL, "TestZeroWidth"), (emote.timestamp, emote.name));
            Assert.Equal(StvEmoteFlags.ZeroWidth, emote.data.flags);
            Assert.True(emote.data.listed);
            Assert.Equal(-5635841, emote.data.owner.style.color);
            Assert.Equal("//cdn.7tv.app/emote/01TESTSTVEMOTE0000000000Z1", emote.data.host.url);
            Assert.Equal(["AVIF", "WEBP"], emote.data.host.files.Select(f => f.format));
            Assert.Equal((32, 32, 4), (emote.data.host.files[1].width, emote.data.host.files[1].height, emote.data.host.files[1].frame_count));
        }

        /// <summary>
        /// 7TV user lookup: TwitchHelper reads emote_set_id from it to fetch the channel's emote set; the nested user,
        /// editors and connections must also parse (they include numeric timestamps that overflow int).
        /// </summary>
        [Fact]
        public void Stv_ChannelResponse_Deserializes()
        {
            var response = Parse<STVChannelEmoteResponse>("""
                {"id":"100000001","platform":"TWITCH","username":"teststreamer","display_name":"TestStreamer","linked_at":1690000000000,"emote_capacity":1000,"emote_set_id":"set1",
                 "user":{"id":"u7","username":"teststreamer","display_name":"TestStreamer","created_at":1690000000000,"avatar_url":"//example.invalid/u.webp","biography":"",
                   "style":{},"editors":[{"id":"e1","permissions":17,"visible":true,"added_at":1690000000000}],"roles":["r1"],
                   "connections":[{"id":"100000001","platform":"TWITCH","username":"teststreamer","display_name":"TestStreamer","linked_at":1690000000000,"emote_capacity":1000,"emote_set_id":"set1"}]}}
                """);

            Assert.Equal(("set1", "TWITCH", 1690000000000L), (response.emote_set_id, response.platform, response.linked_at));
            Assert.Equal(17, Assert.Single(response.user.editors).permissions);
            Assert.Equal("set1", Assert.Single(response.user.connections).emote_set_id);
            Assert.Null(response.user.style.color);
        }

        /// <summary>
        /// A channel's 7TV emote set (STVEmoteSet) uses the same emote shape as the global set; TwitchHelper appends its emotes
        /// to the global ones.
        /// </summary>
        [Fact]
        public void Stv_EmoteSet_Deserializes()
        {
            var set = Parse<STVEmoteSet>("""
                {"id":"set1","name":"TestStreamer's Emotes","tags":[],"immutable":false,"privileged":false,"emote_count":1,"capacity":1000,
                 "owner":{"id":"u7","username":"teststreamer","display_name":"TestStreamer"},
                 "emotes":[{"id":"01TESTSTVEMOTE0000000000S1","name":"TestStv","flags":0,"data":{"id":"01TESTSTVEMOTE0000000000S1","name":"TestStv","flags":0,"listed":false,
                   "host":{"url":"//cdn.7tv.app/emote/01TESTSTVEMOTE0000000000S1","files":[]}}}]}
                """);

            Assert.Equal(("set1", "teststreamer"), (set.id, set.owner.username));
            var emote = Assert.Single(set.emotes);
            Assert.False(emote.data.listed);
            Assert.Empty(emote.data.host.files);
        }

        /// <summary>
        /// The bit values of StvEmoteFlags must match 7TV's API (see the link in the enum). TwitchHelper skips Private and
        /// ContentTwitchDisallowed emotes and marks ZeroWidth ones, so a wrong bit would show banned emotes or break stacking.
        /// </summary>
        [Theory]
        [InlineData(StvEmoteFlags.Private, 1)]
        [InlineData(StvEmoteFlags.Authentic, 2)]
        [InlineData(StvEmoteFlags.ZeroWidth, 256)]
        [InlineData(StvEmoteFlags.ContentSexual, 65536)]
        [InlineData(StvEmoteFlags.ContentEpilepsy, 131072)]
        [InlineData(StvEmoteFlags.ContentEdgy, 262144)]
        [InlineData(StvEmoteFlags.ContentTwitchDisallowed, 16777216)]
        public void StvEmoteFlags_MatchSevenTvBitValues(StvEmoteFlags flag, int value)
        {
            Assert.Equal(value, (int)flag);
        }

        /// <summary>Combined flags from the API (e.g. zero-width + epilepsy = 131328) decode to both flags.</summary>
        [Fact]
        public void StvEmoteFlags_CombinedValueDeserializes()
        {
            var data = Parse<STVData>("""{"flags":131328}""");

            Assert.Equal(StvEmoteFlags.ZeroWidth | StvEmoteFlags.ContentEpilepsy, data.flags);
        }

        /// <summary>
        /// Twitch's "unavailable media" playlist tag is base64 JSON with SCREAMING-CASE keys mapped via [JsonPropertyName].
        /// M3U8Extensions deserializes it with the default (case-sensitive) options, so the names must match exactly.
        /// </summary>
        [Fact]
        public void UnavailableMedia_DeserializesWithDefaultOptions()
        {
            var media = Assert.Single(JsonSerializer.Deserialize<UnavailableMedia[]>("""
                [{"STABLE-VARIANT-ID":"1080p60","IVS_NAME":"1080p60","BANDWIDTH":8000000,"CODECS":"avc1.64002A,mp4a.40.2","RESOLUTION":"1920x1080",
                  "FILTER_REASONS":[],"AUTHORIZATION_REASONS":["channel_restricted"],"FRAME-RATE":59.94,"IVS-VARIANT-SOURCE":"source","IVS-GROUPS":["chunked"]}]
                """)!);

            Assert.Equal(("1080p60", "1080p60", 8000000, "avc1.64002A,mp4a.40.2", "1920x1080"), (media.StableVariantId, media.IvsName, media.Bandwidth, media.Codecs, media.Resolution));
            Assert.Equal(59.94m, media.FrameRate);
            Assert.Equal("source", media.VariantSource);
            Assert.Empty(media.FilterReasons);
            Assert.Equal(["channel_restricted"], media.AuthorizationReasons);
            Assert.Equal(["chunked"], media.IvsGroups);
        }

        /// <summary>
        /// EmoteResponse starts with empty (not null) provider lists: ChatHtml and others AddRange all three without null checks
        /// even when a provider was disabled or failed.
        /// </summary>
        [Fact]
        public void EmoteResponse_ListsDefaultToEmpty()
        {
            var response = new EmoteResponse();

            Assert.Empty(response.BTTV);
            Assert.Empty(response.FFZ);
            Assert.Empty(response.STV);
        }

        /// <summary>
        /// EmoteResponseItem is persisted (gzip JSON) as the offline fallback emote list. Its PascalCase property names are the
        /// on-disk format, so a rename would make previously cached lists load with null codes/URLs.
        /// </summary>
        [Fact]
        public void EmoteResponseItem_RoundTripsWithStableNames()
        {
            var item = new EmoteResponseItem { Id = "id1", Code = "TestStv", ImageType = "webp", ImageUrl = "https://cdn.7tv.app/emote/x/[scale]x.webp", IsZeroWidth = true };

            var json = JsonSerializer.Serialize(item);
            var back = JsonSerializer.Deserialize<EmoteResponseItem>(json)!;

            Assert.Equal("""{"Id":"id1","Code":"TestStv","ImageType":"webp","ImageUrl":"https://cdn.7tv.app/emote/x/[scale]x.webp","IsZeroWidth":true}""", json);
            Assert.Equal((item.Id, item.Code, item.ImageType, item.ImageUrl, item.IsZeroWidth), (back.Id, back.Code, back.ImageType, back.ImageUrl, back.IsZeroWidth));
        }
    }
}
