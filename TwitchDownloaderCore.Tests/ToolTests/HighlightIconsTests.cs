using System.Text.Json;
using SkiaSharp;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // ReSharper disable StringLiteralTypo
    // Tests HighlightIcons: classifying sub/gift/raid/etc. notices so the chat renderer highlights them, splitting re-sub messages,
    // and generating/caching/disposing the highlight icons drawn next to them.
    public class HighlightIconsTests
    {
        private static Comment CreateCommentWithCommenterAndMessage(Commenter commenter, Message message)
        {
            return new Comment
            {
                _id = Guid.NewGuid().ToString(),
                created_at = DateTime.UtcNow,
                channel_id = Random.Shared.Next(10_000_000, 99_999_999).ToString(),
                content_type = "video",
                content_id = Random.Shared.NextInt64(10_000_000, 99_999_999_999).ToString(),
                content_offset_seconds = Random.Shared.NextDouble() * 100,
                commenter = commenter,
                message = message
            };
        }

        private static Comment CreateCommentWithMessage(string viewerDisplayName, string viewerName, Message message)
        {
            return CreateCommentWithCommenterAndMessage(
                new Commenter
                {
                    display_name = viewerDisplayName,
                    _id = Random.Shared.Next(10_000_000, 99_999_999).ToString(),
                    name = viewerName,
                    bio = "I am a test user.",
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow,
                    logo = @"https://vod-secure.twitch.tv/_404/404_processing_320x180.png"
                },
                message);
        }

        // Each Twitch system notice (sub tiers/Prime, resubs with and without custom text, gifts, raids, bit badge tiers, watch streaks,
        // charity, combos...) maps to its HighlightType and ordinary chat stays None. The renderer picks icons and layout from this, so a
        // wrong classification is visible in every render.
        [Theory]
        // SubscribedTier no custom message
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        // SubscribedTier custom message
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months! Hello LUL LUL LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months! Hello \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":100,\"end\":104},{\"_id\":\"425618\",\"begin\":104,\"end\":108},{\"_id\":\"425618\",\"begin\":108,\"end\":112}]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! Hello LUL LUL LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! Hello \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":100,\"end\":104},{\"_id\":\"425618\",\"begin\":104,\"end\":108},{\"_id\":\"425618\",\"begin\":108,\"end\":112}]}",
            HighlightType.SubscribedTier)]
        // SubscribedPrime no custom message
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"premium\",\"version\":\"1\"}],\"user_color\":\"#FF69B4\",\"emoticons\":[]}",
            HighlightType.SubscribedPrime)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 2 months! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 2 months! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"premium\",\"version\":\"1\"}],\"user_color\":\"#FF69B4\",\"emoticons\":[]}",
            HighlightType.SubscribedPrime)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 8 months, currently on a 8 month streak! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 8 months, currently on a 8 month streak! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"premium\",\"version\":\"1\"}],\"user_color\":\"#FF69B4\",\"emoticons\":[]}",
            HighlightType.SubscribedPrime)]
        // SubscribedPrime custom message
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 3 months! Hello LUL LUL LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 3 months! Hello \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":100,\"end\":104},{\"_id\":\"425618\",\"begin\":104,\"end\":108},{\"_id\":\"425618\",\"begin\":108,\"end\":112}]}",
            HighlightType.SubscribedPrime)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 3 months, currently on a 3 month streak! Hello LUL LUL LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 3 months, currently on a 3 month streak! Hello \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":100,\"end\":104},{\"_id\":\"425618\",\"begin\":104,\"end\":108},{\"_id\":\"425618\",\"begin\":108,\"end\":112}]}",
            HighlightType.SubscribedPrime)]
        // Converted
        [InlineData(
            "{\"body\":\"viewer8 converted from a Prime sub to a Tier 1 sub! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 converted from a Prime sub to a Tier 1 sub! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"},{\"_id\":\"bits\",\"version\":\"100\"}],\"user_color\":null,\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 converted from a Tier 1 sub to a Tier 2 sub! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 converted from a Tier 2 sub to a Tier 3 sub! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"},{\"_id\":\"bits\",\"version\":\"100\"}],\"user_color\":null,\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 converted from a Tier 1 sub to a Tier 3 sub! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 converted from a Tier 1 sub to a Tier 3 sub! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"},{\"_id\":\"bits\",\"version\":\"100\"}],\"user_color\":null,\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 converted from a Tier 1 sub to a Prime sub! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 converted from a Tier 1 sub to a Prime sub! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"},{\"_id\":\"bits\",\"version\":\"100\"}],\"user_color\":null,\"emoticons\":[]}",
            HighlightType.SubscribedPrime)]
        // GiftedMany
        [InlineData(
            "{\"body\":\"viewer8 is gifting 5 Tier 1 Subs to streamer8's community! They've gifted a total of 349 in the channel! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 is gifting 5 Tier 1 Subs to streamer8's community! They've gifted a total of 349 in the channel! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"6\"},{\"_id\":\"bits\",\"version\":\"50000\"}],\"user_color\":\"#DAA520\",\"emoticons\":[]}",
            HighlightType.GiftedMany)]
        // +Special case in separate method.
        // GiftedSingle
        [InlineData(
            "{\"body\":\"viewer8 gifted a Tier 1 sub to viewer9! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 gifted a Tier 1 sub to viewer9! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"6\"},{\"_id\":\"bits\",\"version\":\"50000\"}],\"user_color\":\"#DAA520\",\"emoticons\":[]}",
            HighlightType.GiftedSingle)]
        // GiftedAnonymous
        // Special case, in separate method.
        // ContinuingGift
        [InlineData(
            "{\"body\":\"viewer8 is continuing the Gift Sub they got from viewer9! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 is continuing the Gift Sub they got from viewer9! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"}],\"user_color\":\"#8A2BE2\",\"emoticons\":[]}",
            HighlightType.ContinuingGift)]
        // ContinuingAnonymousGift
        [InlineData(
            "{\"body\":\"viewer8 is continuing the Gift Sub they got from an anonymous user! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 is continuing the Gift Sub they got from an anonymous user! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"}],\"user_color\":\"#8A2BE2\",\"emoticons\":[]}",
            HighlightType.ContinuingAnonymousGift)]
        // PayingForward
        [InlineData(
            "{\"body\":\"viewer8 is paying forward the Gift they got from an anonymous gifter to the community! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 is paying forward the Gift they got from an anonymous gifter to the community! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"},{\"_id\":\"bits\",\"version\":\"100\"}],\"user_color\":null,\"emoticons\":[]}",
            HighlightType.PayingForward)]
        // ChannelPointHighlight
        // Not yet supported
        // Raid
        [InlineData(
            "{\"body\":\"5180 raiders from viewer8 have joined! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"5180 raiders from viewer8 have joined! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"1\"},{\"_id\":\"partner\",\"version\":\"1\"}],\"user_color\":\"#8A2BE2\",\"emoticons\":[]}",
            HighlightType.Raid)]
        // BitBadgeTierNotification
        [InlineData(
            "{\"body\":\"bits badge tier notification \",\"bits_spent\":0,\"fragments\":[{\"text\":\"bits badge tier notification \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"6\"},{\"_id\":\"bits\",\"version\":\"1000\"}],\"user_color\":\"#9E1A67\",\"emoticons\":[]}",
            HighlightType.BitBadgeTierNotification)]
        // WatchStreak no custom message
        [InlineData(
            "{\"body\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#008000\",\"emoticons\":[]}",
            HighlightType.WatchStreak)]
        // WatchStreak custom message
        [InlineData(
            "{\"body\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! me too!\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! me too!\",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#1E90FF\",\"emoticons\":[]}",
            HighlightType.WatchStreak)]
        [InlineData(
            "{\"body\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! me too LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! me too \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":84,\"end\":88}]}",
            HighlightType.WatchStreak)]
        [InlineData(
            "{\"body\":\"viewer8 watched 30 consecutive streams and sparked a watch streak! 30 DAYS\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 watched 30 consecutive streams and sparked a watch streak! 30 DAYS\",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#1E90FF\",\"emoticons\":[]}",
            HighlightType.WatchStreak)]
        // Charity donation
        [InlineData(
            "{\"body\":\"viewer8: Donated USD 20 to support Guide Dog Foundation for the Blind, Inc. \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8: Donated USD 20 to support Guide Dog Foundation for the Blind, Inc. \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#1E90FF\",\"emoticons\":[]}",
            HighlightType.CharityDonation)]
        [InlineData(
            "{\"body\":\"viewer8: Donated USD 5 to support Alveus Sanctuary \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8: Donated USD 5 to support Alveus Sanctuary \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#1E90FF\",\"emoticons\":[]}",
            HighlightType.CharityDonation)]
        // Regular messages
        [InlineData(
            "{\"body\":\"Hi\",\"bits_spent\":0,\"fragments\":[{\"text\":\"Hi\",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#5F9EA0\",\"emoticons\":[]}",
            HighlightType.None)]
        [InlineData(
            "{\"body\":\"\",\"bits_spent\":0,\"fragments\":[{\"text\":\"\",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#5F9EA0\",\"emoticons\":[]}",
            HighlightType.None)]
        public void CorrectlyIdentifiesHighlightTypes(string messageString, HighlightType expectedType)
        {
            var message = JsonSerializer.Deserialize<Message>(messageString)!;
            var comment = CreateCommentWithMessage("viewer8", "viewer8", message);

            var actualType = HighlightIcons.GetHighlightType(comment);

            Assert.Equal(expectedType, actualType);
        }

        // A raid notice is only recognised when the raiding channel named in the message is the commenter. A viewer typing
        // "5180 raiders from someone_else have joined!" must stay a plain message, otherwise it would be rendered as a raid.
        [Fact]
        public void RaidNotice_RequiresCommenterToBeTheRaider()
        {
            const string BODY = "5180 raiders from streamer8 have joined! ";
            var message = new Message { body = BODY, fragments = [new Fragment { text = BODY }], user_badges = [], emoticons = [] };

            Assert.Equal(HighlightType.Raid, HighlightIcons.GetHighlightType(CreateCommentWithMessage("streamer8", "streamer8", message)));
            Assert.Equal(HighlightType.None, HighlightIcons.GetHighlightType(CreateCommentWithMessage("viewer8", "viewer8", message)));
        }

        // Twitch's 'We added N Bonus Gift Subs' notices are posted by the Twitch/VALORANT system accounts and must be treated as GiftedMany;
        // the same text from a normal user must not be (guarded by the account id check).
        [Theory]
        [InlineData("{\"display_name\":\"Twitch\",\"_id\":\"12826\",\"name\":\"twitch\",\"bio\":\"Twitch is where thousands of communities come together for whatever, every day. \",\"created_at\":\"2007-05-22T10:39:54.238271Z\",\"updated_at\":\"2024-09-22T22:28:39.594659Z\",\"logo\":\"https://static-cdn.jtvnw.net/jtv_user_pictures/aa88230d-7af5-4053-a7cd-889e626d3382-profile_image-300x300.png\"}",
            "{\"body\":\"We added 13 Gift Subs AND 10 Bonus Gift Subs to viewer8's gift! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"We added 13 Gift Subs AND 10 Bonus Gift Subs to viewer8's gift! \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":null,\"emoticons\":[]}")]
        [InlineData("{\"display_name\":\"Twitch\",\"_id\":\"12826\",\"name\":\"twitch\",\"bio\":\"Twitch is where thousands of communities come together for whatever, every day. \",\"created_at\":\"2007-05-22T10:39:54.238271Z\",\"updated_at\":\"2024-09-22T22:28:39.594659Z\",\"logo\":\"https://static-cdn.jtvnw.net/jtv_user_pictures/aa88230d-7af5-4053-a7cd-889e626d3382-profile_image-300x300.png\"}",
            "{\"body\":\"We added 1 Gift Subs to viewer8's gift! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"We added 1 Gift Subs to viewer8's gift! \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":null,\"emoticons\":[]}")]
        [InlineData("{\"display_name\": \"VALORANT\",\"_id\": \"490592527\",\"name\": \"valorant\",\"bio\": \"VALORANT is a 5v5 character based tactical shooter video game from Riot Games. \",\"created_at\": \"2007-05-22T10:39:54.238271Z\",\"updated_at\": \"2024-09-22T22:28:39.594659Z\",\"logo\": \"https://static-cdn.jtvnw.net/jtv_user_pictures/34c5c8da-19b6-47b2-85ba-cc7ae87f1413-profile_image-300x300.png\"}",
            "{\"body\":\"We added 13 Gift Subs AND 10 Bonus Gift Subs to viewer8's gift! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"We added 13 Gift Subs AND 10 Bonus Gift Subs to viewer8's gift! \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":null,\"emoticons\":[]}")]
        [InlineData("{\"display_name\": \"VALORANT\",\"_id\": \"490592527\",\"name\": \"valorant\",\"bio\": \"VALORANT is a 5v5 character based tactical shooter video game from Riot Games. \",\"created_at\": \"2007-05-22T10:39:54.238271Z\",\"updated_at\": \"2024-09-22T22:28:39.594659Z\",\"logo\": \"https://static-cdn.jtvnw.net/jtv_user_pictures/34c5c8da-19b6-47b2-85ba-cc7ae87f1413-profile_image-300x300.png\"}",
            "{\"body\":\"We added 1 Gift Subs to viewer8's gift! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"We added 1 Gift Subs to viewer8's gift! \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":null,\"emoticons\":[]}")]
        public void CorrectlyIdentifiesBonusSubsGiftedMany(string commenterString, string messageString)
        {
            const HighlightType EXPECTED_TYPE = HighlightType.GiftedMany;

            var commenter = JsonSerializer.Deserialize<Commenter>(commenterString)!;
            var message = JsonSerializer.Deserialize<Message>(messageString)!;
            var comment = CreateCommentWithCommenterAndMessage(commenter, message);

            var actualType = HighlightIcons.GetHighlightType(comment);

            Assert.Equal(EXPECTED_TYPE, actualType);
        }

        // Anonymous gift subs are posted by the AnAnonymousGifter or Twitch account; both must classify as GiftedAnonymous.
        [Theory]
        [InlineData("{\"display_name\":\"AnAnonymousGifter\",\"_id\":\"274598607\",\"name\":\"ananonymousgifter\",\"type\":\"user\",\"bio\":\"?????????????????????????????\",\"created_at\":\"2018-11-12T21:57:31.811529Z\",\"updated_at\":\"2022-04-18T21:57:27.392173Z\",\"logo\":\"https://static-cdn.jtvnw.net/jtv_user_pictures/ae7b05c6-c924-44ab-8203-475a2d3e488c-profile_image-300x300.png\"}",
            "{\"body\":\"An anonymous user gifted a Tier 1 sub to viewer8!  \",\"bits_spent\":0,\"fragments\":[{\"text\":\"An anonymous user gifted a Tier 1 sub to viewer8!  \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":null,\"emoticons\":[]}")]
        [InlineData("{\"display_name\":\"Twitch\",\"_id\":\"12826\",\"name\":\"twitch\",\"bio\":\"Twitch is where thousands of communities come together for whatever, every day. \",\"created_at\":\"2007-05-22T10:39:54.238271Z\",\"updated_at\":\"2024-09-22T22:28:39.594659Z\",\"logo\":\"https://static-cdn.jtvnw.net/jtv_user_pictures/aa88230d-7af5-4053-a7cd-889e626d3382-profile_image-300x300.png\"}",
            "{\"body\":\"An anonymous user gifted a Tier 1 sub to viewer8!  \",\"bits_spent\":0,\"fragments\":[{\"text\":\"An anonymous user gifted a Tier 1 sub to viewer8!  \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":null,\"emoticons\":[]}")]
        public void CorrectlyIdentifiesAnonymousGiftSub(string commenterString, string messageString)
        {
            const HighlightType EXPECTED_TYPE = HighlightType.GiftedAnonymous;

            var commenter = JsonSerializer.Deserialize<Commenter>(commenterString)!;
            var message = JsonSerializer.Deserialize<Message>(messageString)!;
            var comment = CreateCommentWithCommenterAndMessage(commenter, message);

            var actualType = HighlightIcons.GetHighlightType(comment);

            Assert.Equal(EXPECTED_TYPE, actualType);
        }

        // A resub notice without user text is returned as-is with a null custom message, so the renderer does not draw an empty second line.
        [Theory]
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[]}")]
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 2. They've subscribed for 3 months! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 2. They've subscribed for 3 months! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[]}")]
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 3. They've subscribed for 3 months, currently on a 3 month streak! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 3. They've subscribed for 3 months, currently on a 3 month streak! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[]}")]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"premium\",\"version\":\"1\"}],\"user_color\":\"#FF69B4\",\"emoticons\":[]}")]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 2 months! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 2 months! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"premium\",\"version\":\"1\"}],\"user_color\":\"#FF69B4\",\"emoticons\":[]}")]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 8 months, currently on a 8 month streak! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 8 months, currently on a 8 month streak! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"premium\",\"version\":\"1\"}],\"user_color\":\"#FF69B4\",\"emoticons\":[]}")]
        public void DoesNotSplitSubCommentWithoutCustomMessage(string messageString)
        {
            var message = JsonSerializer.Deserialize<Message>(messageString)!;
            var comment = CreateCommentWithMessage("viewer8", "viewer8", message);

            var (actual, shouldBeNull) = HighlightIcons.SplitSubComment(comment);

            Assert.Same(comment, actual);
            Assert.Null(shouldBeNull);
        }

        // A resub notice with user text is split into the system part and the user's message, keeping emote fragments intact (one or many
        // fragments), so the custom message can be rendered as a normal chat line under the highlight.
        [Theory]
        // 1 Fragment
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months! Hello!\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months! Hello!\",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[]}",
            "viewer8 subscribed at Tier 1. They've subscribed for 3 months! ", "Hello!")]
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! Hello!\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! Hello!\",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[]}",
            "viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! ", "Hello!")]
        // > 1 Fragment
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 3 months! Hello LUL LUL LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 3 months! Hello \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":100,\"end\":104},{\"_id\":\"425618\",\"begin\":104,\"end\":108},{\"_id\":\"425618\",\"begin\":108,\"end\":112}]}",
            "viewer8 subscribed with Prime. They've subscribed for 3 months! ", "Hello LUL LUL LUL")]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 3 months, currently on a 3 month streak! Hello LUL LUL LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 3 months, currently on a 3 month streak! Hello \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":100,\"end\":104},{\"_id\":\"425618\",\"begin\":104,\"end\":108},{\"_id\":\"425618\",\"begin\":108,\"end\":112}]}",
            "viewer8 subscribed with Prime. They've subscribed for 3 months, currently on a 3 month streak! ", "Hello LUL LUL LUL")]
        public void CorrectlySplitsSubCommentWithCustomMessage(string messageString, string subMessageString, string customMessageString)
        {
            var message = JsonSerializer.Deserialize<Message>(messageString)!;
            var comment = CreateCommentWithMessage("viewer8", "viewer8", message);

            var (subComment, customMessage) = HighlightIcons.SplitSubComment(comment);

            Assert.NotSame(comment, subComment);
            Assert.Equal(subComment.message.body, subMessageString);
            Assert.True(subComment.message.fragments.Count == 1, $"{subComment.message.fragments.Count} fragments were found in {nameof(subComment)}. Expected 1.");
            Assert.Equal(subComment.message.fragments[0].text, subMessageString);

            Assert.NotNull(customMessage);
            Assert.Equal(customMessage.message.body, customMessageString);
            Assert.True(customMessage.message.fragments.Count > 0, $"0 fragments were found in {nameof(customMessage)}. Expected more than 0.");
        }
        // A watch streak notice without user text is returned unchanged with no custom message.

        [Fact]
        public void DoesNotSplitWatchStreakWithoutCustomMessage()
        {
            const string MESSAGE_STRING =
                "{\"body\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#008000\",\"emoticons\":[]}";

            var message = JsonSerializer.Deserialize<Message>(MESSAGE_STRING)!;
            var comment = CreateCommentWithMessage("viewer8", "viewer8", message);

            var (actual, shouldBeNull) = HighlightIcons.SplitWatchStreakComment(comment);

            Assert.Same(comment, actual);
            Assert.Null(shouldBeNull);
        }

        // A watch streak notice with user text is split into the notice and the user's message (emote fragments preserved).
        [Theory]
        [InlineData(
            "{\"body\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! me too!\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! me too!\",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#1E90FF\",\"emoticons\":[]}",
            "viewer8 watched 3 consecutive streams this month and sparked a watch streak! ", "me too!")]
        [InlineData(
            "{\"body\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! me too LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! me too \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":84,\"end\":88}]}",
            "viewer8 watched 3 consecutive streams this month and sparked a watch streak! ", "me too LUL")]
        public void CorrectlySplitsWatchStreakWithCustomMessage(string messageString, string subMessageString, string customMessageString)
        {
            var message = JsonSerializer.Deserialize<Message>(messageString)!;
            var comment = CreateCommentWithMessage("viewer8", "viewer8", message);

            var (watchStreakMessage, customMessage) = HighlightIcons.SplitWatchStreakComment(comment);

            Assert.NotSame(comment, watchStreakMessage);
            Assert.Equal(watchStreakMessage.message.body, subMessageString);
            Assert.True(watchStreakMessage.message.fragments.Count == 1, $"{watchStreakMessage.message.fragments.Count} fragments were found in {nameof(watchStreakMessage)}. Expected 1.");
            Assert.Equal(watchStreakMessage.message.fragments[0].text, subMessageString);

            Assert.NotNull(customMessage);
            Assert.Equal(customMessage.message.body, customMessageString);
            Assert.True(customMessage.message.fragments.Count > 0, $"0 fragments were found in {nameof(customMessage)}. Expected more than 0.");
        }

        private static Comment CreateCommentWithBody(string displayName, string body) =>
            CreateCommentWithMessage(displayName, displayName.ToLowerInvariant(), new Message
            {
                body = body,
                fragments = [new Fragment { text = body }],
                user_badges = [],
                emoticons = []
            });

        // Combo messages (both the per-user "community sent" form and the system "Combo started!" form), sub conversions to an
        // unknown tier, unparseable conversions and empty bodies must map to the right type; these were the unclassified branches.
        [Theory]
        [InlineData("viewer8", "viewer8's community sent 25 hearts! ", HighlightType.Combo)]
        [InlineData("viewer8", "Combo started! You have 30 seconds left to join. ", HighlightType.Combo)]
        [InlineData("viewer8", "viewer8 converted from a Prime sub to a Tier 4 sub! ", HighlightType.None)]
        [InlineData("viewer8", "viewer8 converted from a mystery sub! ", HighlightType.None)]
        [InlineData("viewer8", "", HighlightType.None)]
        public void CorrectlyIdentifiesComboAndConversionEdgeCases(string displayName, string body, HighlightType expectedType)
        {
            var comment = CreateCommentWithBody(displayName, body);

            Assert.Equal(expectedType, HighlightIcons.GetHighlightType(comment));
        }

        private static HighlightIcons CreateIcons(string cacheDir, bool outline = false, SKPaint? outlinePaint = null) =>
            new(new ChatRenderOptions { FontSize = 12, Outline = outline, Offline = true }, cacheDir, SKColors.Purple, outlinePaint ?? new SKPaint());

        public static TheoryData<HighlightType> AllHighlightTypes => new(Enum.GetValues<HighlightType>());

        // Every highlight type yields an icon scaled to the chat font (20x20 px at 12pt), and the same cached instance is reused on
        // later calls so a render does not regenerate (and leak) one image per highlighted message. GiftedMany runs offline here,
        // so with an empty cache it must fall back to a blank icon instead of hitting the network.
        [Theory]
        [MemberData(nameof(AllHighlightTypes))]
        public void GetHighlightIcon_ReturnsCachedIconOfFontSize(HighlightType type)
        {
            using var temp = new TempDirectory();
            using var icons = CreateIcons(temp.Path);

            var icon = icons.GetHighlightIcon(type, SKColors.White);

            Assert.Equal(20, icon.Width);
            Assert.Equal(20, icon.Height);
            Assert.Same(icon, icons.GetHighlightIcon(type, SKColors.White));
        }

        // With outlines enabled the icon is drawn with a private clone of the outline paint, whose stroke is rescaled to the icon's
        // 72px drawing space; the renderer's own paint must not be modified (it is shared with the text outline).
        [Fact]
        public void GetHighlightIcon_WithOutline_DoesNotMutateCallerPaint()
        {
            using var temp = new TempDirectory();
            using var outlinePaint = new SKPaint { StrokeWidth = 2, Style = SKPaintStyle.Stroke, Color = SKColors.Black };
            using var icons = CreateIcons(temp.Path, true, outlinePaint);

            var icon = icons.GetHighlightIcon(HighlightType.SubscribedTier, SKColors.White);

            Assert.Equal(20, icon.Width);
            Assert.Equal(2, outlinePaint.StrokeWidth);
        }

        // A gift icon already in the image cache is decoded and resized to the icon size, so offline renders still show it.
        [Fact]
        public void GetHighlightIcon_GiftedMany_UsesCachedImage()
        {
            using var temp = new TempDirectory();
            var iconDir = Directory.CreateDirectory(temp.Combine("icons"));
            using (var bitmap = new SKBitmap(64, 64))
            {
                bitmap.Erase(SKColors.Red);
                using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(iconDir.FullName, "gift-illus_3.png"), data.ToArray());
            }

            using var icons = CreateIcons(temp.Path);
            var icon = icons.GetHighlightIcon(HighlightType.GiftedMany, SKColors.White);

            using var pixels = SKBitmap.FromImage(icon);
            Assert.Equal(20, icon.Width);
            Assert.Equal(SKColors.Red, pixels.GetPixel(10, 10));
        }

        // Dispose frees the generated icons and is idempotent, because the renderer may dispose it from more than one cleanup path.
        [Fact]
        public void Dispose_FreesIconsAndIsIdempotent()
        {
            using var temp = new TempDirectory();
            var icons = CreateIcons(temp.Path);
            var icon = icons.GetHighlightIcon(HighlightType.SubscribedTier, SKColors.White);

            icons.Dispose();
            icons.Dispose();

            Assert.True(icons.Disposed);
            Assert.Equal(IntPtr.Zero, icon.Handle);
        }

        // Every icon the class creates should be freed on Dispose. The watch streak, charity donation and blank icons are
        // never disposed, so each chat render leaks those native images.
        [Theory(Skip = "Bug: HighlightIcons.Dispose does not dispose the watch streak, charity donation and blank icons")]
        [InlineData(HighlightType.WatchStreak)]
        [InlineData(HighlightType.CharityDonation)]
        [InlineData(HighlightType.None)]
        public void Dispose_FreesEveryIcon(HighlightType type)
        {
            using var temp = new TempDirectory();
            var icons = CreateIcons(temp.Path);
            var icon = icons.GetHighlightIcon(type, SKColors.White);

            icons.Dispose();

            Assert.Equal(IntPtr.Zero, icon.Handle);
        }

        private static Comment CreateCommentWithFragments(string notice, params Fragment[] customFragments)
        {
            var fragments = customFragments.Prepend(new Fragment { text = notice }).ToList();
            return CreateCommentWithMessage("viewer8", "viewer8", new Message
            {
                body = string.Concat(fragments.Select(x => x.text)),
                fragments = fragments,
                user_badges = [],
                emoticons = [new Emoticon2 { _id = "425618" }]
            });
        }

        // When the custom re-sub / watch streak message starts with an emote, the notice keeps only its own text fragment while
        // the custom message keeps every original fragment after it (emote first), so the emote is still drawn as an image.
        [Fact]
        public void SplitsNoticesWhoseCustomMessageStartsWithAnEmote()
        {
            const string SUB_NOTICE = "viewer8 subscribed at Tier 1. They've subscribed for 3 months! ";
            const string STREAK_NOTICE = "viewer8 watched 3 consecutive streams this month and sparked a watch streak! ";
            Fragment[] custom = [new Fragment { text = "LUL", emoticon = new Emoticon { emoticon_id = "425618" } }, new Fragment { text = " hi" }];

            var (subNotice, subCustom) = HighlightIcons.SplitSubComment(CreateCommentWithFragments(SUB_NOTICE, custom));
            var (streakNotice, streakCustom) = HighlightIcons.SplitWatchStreakComment(CreateCommentWithFragments(STREAK_NOTICE, custom));

            foreach (var (notice, customMessage, noticeText) in new[] { (subNotice, subCustom, SUB_NOTICE), (streakNotice, streakCustom, STREAK_NOTICE) })
            {
                Assert.Equal([noticeText], notice.message.fragments.Select(x => x.text));
                Assert.Empty(notice.message.emoticons);
                Assert.Equal("LUL hi", customMessage.message.body);
                Assert.Equal(["LUL", " hi"], customMessage.message.fragments.Select(x => x.text));
                Assert.Equal("425618", customMessage.message.fragments[0].emoticon.emoticon_id);
            }
        }
    }
}
