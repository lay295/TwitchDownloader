using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.TwitchObjectTests
{
    // Tests the ChatRoot object model (Comment, Message, ... ) used for every chat file. Comment.Clone is used by the chat
    // renderer/updater to modify comments without touching the loaded chat, and the defaults are relied on by code that
    // fills a fresh ChatRoot, so both need to be right.
    public class ChatRootTests
    {
        private static Comment CreateComment() => new()
        {
            _id = "c1",
            created_at = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            channel_id = "100000001",
            content_type = "video",
            content_id = "1000000001",
            content_offset_seconds = 12.5,
            commenter = new Commenter { display_name = "Viewer", _id = "1", name = "viewer", bio = "bio", logo = "logo" },
            message = new Message
            {
                body = "Kappa hi",
                bits_spent = 5,
                fragments = [new Fragment { text = "Kappa", emoticon = new Emoticon { emoticon_id = "25" } }, new Fragment { text = " hi" }],
                user_badges = [new UserBadge { _id = "subscriber", version = "3" }],
                user_color = "#FF0000",
                user_notice_params = new UserNoticeParams { msg_id = "highlighted-message" },
                emoticons = [new Emoticon2 { _id = "25", begin = 0, end = 6 }]
            }
        };

        /// <summary>
        /// Clone is a deep copy: every value is equal, but mutating the clone's commenter, fragments, badges, emoticons or
        /// user_notice_params must not change the original comment (the renderer edits clones when splitting messages).
        /// </summary>
        [Fact]
        public void CommentClone_IsDeepCopy()
        {
            var original = CreateComment();

            var clone = original.Clone();
            clone.commenter.display_name = "changed";
            clone.message.fragments[1].text = "changed";
            clone.message.user_badges[0].version = "0";
            clone.message.emoticons[0].end = 99;
            clone.message.user_notice_params.msg_id = "changed";

            Assert.Equal((original._id, original.created_at, original.channel_id, original.content_type, original.content_id, original.content_offset_seconds),
                (clone._id, clone.created_at, clone.channel_id, clone.content_type, clone.content_id, clone.content_offset_seconds));
            Assert.Equal(("Kappa hi", 5, "#FF0000"), (clone.message.body, clone.message.bits_spent, clone.message.user_color));
            Assert.Equal("25", clone.message.fragments[0].emoticon.emoticon_id);
            Assert.Equal("Viewer", original.commenter.display_name);
            Assert.Equal(" hi", original.message.fragments[1].text);
            Assert.Equal("3", original.message.user_badges[0].version);
            Assert.Equal(6, original.message.emoticons[0].end);
            Assert.Equal("highlighted-message", original.message.user_notice_params.msg_id);
        }

        /// <summary>
        /// Old chat files can have null user_badges, emoticons and user_notice_params. Cloning such a message must not throw
        /// (regression guards for lay295#620 and the null user_badges fix in c94aab9) and yields empty lists.
        /// </summary>
        [Fact]
        public void MessageClone_ToleratesNullLists()
        {
            var message = new Message { body = "hi", fragments = [new Fragment { text = "hi" }] };

            var clone = message.Clone();

            Assert.Empty(clone.user_badges);
            Assert.Empty(clone.emoticons);
            Assert.Null(clone.user_notice_params);
            Assert.Equal("hi", Assert.Single(clone.fragments).text);
        }

        /// <summary>
        /// Defaults that code building a ChatRoot by hand relies on: FileInfo present, video length -1 ("unknown") with a
        /// non-null chapter list, and non-null embedded data lists.
        /// </summary>
        [Fact]
        public void Defaults_AreNonNull()
        {
            var root = new ChatRoot();
            var video = new Video();
            var embedded = new EmbeddedData();

            Assert.NotNull(root.FileInfo);
            Assert.Equal(-1, video.length);
            Assert.Empty(video.chapters);
            Assert.Empty(embedded.firstParty);
            Assert.Empty(embedded.thirdParty);
            Assert.Empty(embedded.twitchBadges);
            Assert.Empty(embedded.twitchBits);
        }
    }
}
