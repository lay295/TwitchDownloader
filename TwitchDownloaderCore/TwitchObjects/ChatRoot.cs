using System;
using System.Collections.Generic;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class Streamer
    {
        public string name { get; set; }
        public int id { get; set; }
    }

    public class Commenter
    {
        public string display_name { get; set; }
        public string _id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string bio { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string logo { get; set; }
    }

    public class Emoticon
    {
        public string emoticon_id { get; set; }
        public string emoticon_set_id { get; set; }
    }

    public class Fragment
    {
        public string text { get; set; }
        public Emoticon emoticon { get; set; }
    }

    public class UserBadge
    {
        public string _id { get; set; }
        public string version { get; set; }
    }

    public class Emoticon2
    {
        public string _id { get; set; }
        public int begin { get; set; }
        public int end { get; set; }
    }

    public class Message
    {
        public string body { get; set; }
        public int bits_spent { get; set; }
        public List<Fragment> fragments { get; set; }
        public bool is_action { get; set; }
        public List<UserBadge> user_badges { get; set; }
        public string user_color { get; set; }
        public UserNoticeParams user_notice_params { get; set; }
        public List<Emoticon2> emoticons { get; set; }
    }

    public class UserNoticeParams
    {
        public string msg_id { get; set; }
    }

    public class Comment
    {
        public string _id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string channel_id { get; set; }
        public string content_type { get; set; }
        public string content_id { get; set; }
        public double content_offset_seconds { get; set; }
        public Commenter commenter { get; set; }
        public string source { get; set; }
        public string state { get; set; }
        public Message message { get; set; }
        public bool more_replies { get; set; }
    }

    public class Video
    {
        public string title { get; set; }
        public string id { get; set; }
        public DateTime created_at { get; set; }
        public double start { get; set; }
        public double end { get; set; }
        public double length { get; set; } = -1;
    }

    public class EmbedEmoteData
    {
        public string id { get; set; }
        public int imageScale { get; set; }
        public byte[] data { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }

    public class EmbedChatBadge
    {
        public string name { get; set; }
        public Dictionary<string, byte[]> versions { get; set; }
    }

    public class EmbedCheerEmote
    {
        public string prefix { get; set; }
        public Dictionary<int, EmbedEmoteData> tierList { get; set; }
    }

    public class EmbeddedData
    {
        public List<EmbedEmoteData> thirdParty { get; set; } = new();
        public List<EmbedEmoteData> firstParty { get; set; } = new();
        public List<EmbedChatBadge> twitchBadges { get; set; } = new();
        public List<EmbedCheerEmote> twitchBits { get; set; } = new();

        ~EmbeddedData()
        {
            thirdParty = null;
            firstParty = null;
            twitchBadges = null;
            twitchBits = null;
        }
    }

    public class CommentResponse
    {
        public List<Comment> comments { get; set; }
        public string _next { get; set; }
    }

    public class ChatRoot
    {
        public ChatRootInfo FileInfo { get; set; } = new ChatRootInfo();
        public Streamer streamer { get; set; }
        public Video video { get; set; }
        public List<Comment> comments { get; set; }
        public EmbeddedData embeddedData { get; set; }

        ~ChatRoot()
        {
            FileInfo = null;
            streamer = null;
            video = null;
            comments = null;
            embeddedData = null;
        }
    }
}