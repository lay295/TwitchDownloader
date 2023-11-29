using System.Text.Json;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests
{
    // ReSharper disable StringLiteralTypo
    public class HighlightIconsTests
    {
        private static Comment CreateCommentWithCommenterAndMessage(Commenter commenter, Message message)
        {
            return new Comment
            {
                _id = Guid.NewGuid().ToString(),
                created_at = DateTime.Today,
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
                    created_at = DateTime.Today,
                    updated_at = DateTime.Today,
                    logo = @"https://vod-secure.twitch.tv/_404/404_processing_320x180.png"
                },
                message);
        }

        [Theory]
        // SubscribedTier no custom message
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        // SubscribedTier custom message
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"}],\"user_color\":\"#1E90FF\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! Hello LUL LUL LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed at Tier 1. They've subscribed for 3 months, currently on a 3 month streak! Hello \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":100,\"end\":104},{\"_id\":\"425618\",\"begin\":104,\"end\":108},{\"_id\":\"425618\",\"begin\":108,\"end\":112}]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"premium\",\"version\":\"1\"}],\"user_color\":\"#FF69B4\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedPrime)]
        // SubscribedPrime no custom message
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"premium\",\"version\":\"1\"}],\"user_color\":\"#FF69B4\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedPrime)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 2 months! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 2 months! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"premium\",\"version\":\"1\"}],\"user_color\":\"#FF69B4\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedPrime)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 8 months, currently on a 8 month streak! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 8 months, currently on a 8 month streak! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"premium\",\"version\":\"1\"}],\"user_color\":\"#FF69B4\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedPrime)]
        // SubscribedPrime custom message
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. Hello LUL LUL LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. Hello \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":100,\"end\":104},{\"_id\":\"425618\",\"begin\":104,\"end\":108},{\"_id\":\"425618\",\"begin\":108,\"end\":112}]}",
            HighlightType.SubscribedPrime)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 3 months! Hello LUL LUL LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 3 months! Hello \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":100,\"end\":104},{\"_id\":\"425618\",\"begin\":104,\"end\":108},{\"_id\":\"425618\",\"begin\":108,\"end\":112}]}",
            HighlightType.SubscribedPrime)]
        [InlineData(
            "{\"body\":\"viewer8 subscribed with Prime. They've subscribed for 3 months, currently on a 3 month streak! Hello LUL LUL LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 subscribed with Prime. They've subscribed for 3 months, currently on a 3 month streak! Hello \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}},{\"text\":\" \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"3\"},{\"_id\":\"sub-gifter\",\"version\":\"5\"}],\"user_color\":\"#1E90FF\",\"emoticons\":[{\"_id\":\"425618\",\"begin\":100,\"end\":104},{\"_id\":\"425618\",\"begin\":104,\"end\":108},{\"_id\":\"425618\",\"begin\":108,\"end\":112}]}",
            HighlightType.SubscribedPrime)]
        // Converted
        [InlineData(
            "{\"body\":\"viewer8 converted from a Prime sub to a Tier 1 sub! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 converted from a Prime sub to a Tier 1 sub! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"},{\"_id\":\"bits\",\"version\":\"100\"}],\"user_color\":null,\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 converted from a Tier 1 sub to a Tier 2 sub! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 converted from a Tier 2 sub to a Tier 3 sub! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"},{\"_id\":\"bits\",\"version\":\"100\"}],\"user_color\":null,\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 converted from a Tier 1 sub to a Tier 3 sub! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 converted from a Tier 1 sub to a Tier 3 sub! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"},{\"_id\":\"bits\",\"version\":\"100\"}],\"user_color\":null,\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedTier)]
        [InlineData(
            "{\"body\":\"viewer8 converted from a Tier 1 sub to a Prime sub! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 converted from a Tier 1 sub to a Prime sub! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"},{\"_id\":\"bits\",\"version\":\"100\"}],\"user_color\":null,\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.SubscribedPrime)]
        // GiftedMany
        [InlineData(
            "{\"body\":\"viewer8 is gifting 5 Tier 1 Subs to streamer8's community! They've gifted a total of 349 in the channel! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 is gifting 5 Tier 1 Subs to streamer8's community! They've gifted a total of 349 in the channel! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"6\"},{\"_id\":\"bits\",\"version\":\"50000\"}],\"user_color\":\"#DAA520\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.GiftedMany)]
        // GiftedSingle
        [InlineData(
            "{\"body\":\"viewer8 gifted a Tier 1 sub to viewer9! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 gifted a Tier 1 sub to viewer9! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"6\"},{\"_id\":\"bits\",\"version\":\"50000\"}],\"user_color\":\"#DAA520\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.GiftedSingle)]
        // GiftedAnonymous
        // Special case, in separate method.
        // ContinuingGift
        [InlineData(
            "{\"body\":\"viewer8 is continuing the Gift Sub they got from an anonymous user! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 is continuing the Gift Sub they got from an anonymous user! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"}],\"user_color\":\"#8A2BE2\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.ContinuingGift)]
        // PayingForward
        [InlineData(
            "{\"body\":\"viewer8 is paying forward the Gift they got from an anonymous gifter to the community! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 is paying forward the Gift they got from an anonymous gifter to the community! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"0\"},{\"_id\":\"bits\",\"version\":\"100\"}],\"user_color\":null,\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.PayingForward)]
        // ChannelPointHighlight
        // Not yet supported
        // Raid
        [InlineData(
            "{\"body\":\"5180 raiders from viewer8 have joined! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"5180 raiders from viewer8 have joined! \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"moderator\",\"version\":\"1\"},{\"_id\":\"partner\",\"version\":\"1\"}],\"user_color\":\"#8A2BE2\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.Raid)]
        // BitBadgeTierNotification
        [InlineData(
            "{\"body\":\"bits badge tier notification \",\"bits_spent\":0,\"fragments\":[{\"text\":\"bits badge tier notification \",\"emoticon\":null}],\"user_badges\":[{\"_id\":\"subscriber\",\"version\":\"6\"},{\"_id\":\"bits\",\"version\":\"1000\"}],\"user_color\":\"#9E1A67\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.BitBadgeTierNotification)]
        // WatchStreak no custom message
        [InlineData(
            "{\"body\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! \",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#008000\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.WatchStreak)]
        // WatchStreak custom message
        [InlineData(
            "{\"body\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! me too LUL\",\"bits_spent\":0,\"fragments\":[{\"text\":\"viewer8 watched 3 consecutive streams this month and sparked a watch streak! me too \",\"emoticon\":null},{\"text\":\"LUL\",\"emoticon\":{\"emoticon_id\":\"425618\"}}],\"user_badges\":[],\"user_color\":\"#1E90FF\",\"user_notice_params\":null,\"emoticons\":[{\"_id\":\"425618\",\"begin\":84,\"end\":88}]}",
            HighlightType.WatchStreak)]
        // Regular messages
        [InlineData(
            "{\"body\":\"Hi\",\"bits_spent\":0,\"fragments\":[{\"text\":\"Hi\",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#5F9EA0\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.None)]
        [InlineData(
            "{\"body\":\"\",\"bits_spent\":0,\"fragments\":[{\"text\":\"\",\"emoticon\":null}],\"user_badges\":[],\"user_color\":\"#5F9EA0\",\"user_notice_params\":null,\"emoticons\":[]}",
            HighlightType.None)]
        public void CorrectlyIdentifiesHighlightTypes(string messageString, HighlightType expectedType)
        {
            var message = JsonSerializer.Deserialize<Message>(messageString)!;
            var comment = CreateCommentWithMessage("viewer8", "viewer8", message);

            var actualType = HighlightIcons.GetHighlightType(comment);

            Assert.Equal(expectedType, actualType);
        }

        [Fact]
        public void CorrectlyIdentifiesAnonymousGiftSub()
        {
            const string MESSAGE_STRING =
                "{\"body\":\"An anonymous user gifted a Tier 1 sub to viewer8!  \",\"bits_spent\":0,\"fragments\":[{\"text\":\"An anonymous user gifted a Tier 1 sub to viewer8!  \",\"emoticon\":null}],\"user_badges\":[],\"user_color\":null,\"user_notice_params\":null,\"emoticons\":[]}";
            const HighlightType EXPECTED_TYPE = HighlightType.GiftedAnonymous;

            var message = JsonSerializer.Deserialize<Message>(MESSAGE_STRING)!;
            var comment = CreateCommentWithCommenterAndMessage(new Commenter
            {
                display_name = "AnAnonymousGifter",
                _id = "274598607",
                name = "ananonymousgifter",
                bio = "?????????????????????????????",
                created_at = DateTime.Parse("2018-11-12T21:57:31.811529Z"),
                updated_at = DateTime.Parse("2022-04-18T21:57:27.392173Z"),
                logo = "https://static-cdn.jtvnw.net/jtv_user_pictures/ae7b05c6-c924-44ab-8203-475a2d3e488c-profile_image-300x300.png"
            }, message);

            var actualType = HighlightIcons.GetHighlightType(comment);

            Assert.Equal(EXPECTED_TYPE, actualType);
        }

        // TODO: SplitSubComment & SplitWatchStreakComment tests
    }
}