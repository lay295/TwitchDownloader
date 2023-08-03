using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class Streamer
    {
        public string name { get; set; }
        public int id { get; set; }
    }

    [DebuggerDisplay("{display_name}")]
    public class Commenter
    {
        public string display_name { get; set; }
        public string _id { get; set; }
        public string name { get; set; }
        public string bio { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string logo { get; set; }

        public Commenter Clone()
        {
            return new Commenter()
            {
                display_name = display_name,
                _id = _id,
                name = name,
                bio = bio,
                created_at = created_at,
                updated_at = updated_at,
                logo = logo
            };
        }
    }

    [DebuggerDisplay("{emoticon_id}")]
    public class Emoticon
    {
        public string emoticon_id { get; set; }
    }

    [DebuggerDisplay("{text}")]
    public class Fragment
    {
        public string text { get; set; }
        public Emoticon emoticon { get; set; }

        public Fragment Clone()
        {
            return new Fragment()
            {
                text = text,
                emoticon = emoticon
            };
        }
    }

    [DebuggerDisplay("{_id}")]
    public class UserBadge
    {
        public string _id { get; set; }
        public string version { get; set; }

        public UserBadge Clone()
        {
            return new UserBadge()
            {
                _id = _id,
                version = version
            };
        }
    }

    [DebuggerDisplay("{_id}")]
    public class Emoticon2
    {
        public string _id { get; set; }
        public int begin { get; set; }
        public int end { get; set; }

        public Emoticon2 Clone()
        {
            return new Emoticon2()
            {
                _id = _id,
                begin = begin,
                end = end
            };
        }
    }

    [DebuggerDisplay("{body}")]
    public class Message
    {
        public string body { get; set; }
        public int bits_spent { get; set; }
        public List<Fragment> fragments { get; set; }
        public List<UserBadge> user_badges { get; set; }
        public string user_color { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public UserNoticeParams user_notice_params { get; set; }
        public List<Emoticon2> emoticons { get; set; }

        public Message Clone()
        {
            var newMessage = new Message()
            {
                body = body,
                bits_spent = bits_spent,
                fragments = new List<Fragment>(fragments.Capacity),
                user_badges = new List<UserBadge>(user_badges?.Capacity ?? 0),
                user_color = user_color,
                user_notice_params = user_notice_params?.Clone(),
                emoticons = new List<Emoticon2>(emoticons?.Capacity ?? 0)
            };
            for (int i = 0; i < fragments.Count; i++)
            {
                newMessage.fragments.Add(fragments[i].Clone());
            }
            for (int i = 0; i < user_badges?.Count; i++)
            {
                newMessage.user_badges.Add(user_badges[i].Clone());
            }
            for (int i = 0; i < emoticons?.Count; i++)
            {
                newMessage.emoticons.Add(emoticons[i].Clone());
            }
            return newMessage;
        }
    }

    [DebuggerDisplay("{msg_id}")]
    public class UserNoticeParams
    {
        public string msg_id { get; set; }

        public UserNoticeParams Clone()
        {
            return new UserNoticeParams()
            {
                msg_id = msg_id
            };
        }
    }

    [DebuggerDisplay("{commenter} {message}")]
    public class Comment
    {
        public string _id { get; set; }
        public DateTime created_at { get; set; }
        public string channel_id { get; set; }
        public string content_type { get; set; }
        public string content_id { get; set; }
        public double content_offset_seconds { get; set; }
        public Commenter commenter { get; set; }
        public Message message { get; set; }

        public Comment Clone()
        {
            return new Comment()
            {
                _id = _id,
                created_at = created_at,
                channel_id = channel_id,
                content_type = content_type,
                content_id = content_id,
                content_offset_seconds = content_offset_seconds,
                commenter = commenter.Clone(),
                message = message.Clone()
            };
        }
    }

    public class VideoChapter
    {
        public string id { get; set; }
        public int startMilliseconds { get; set; }
        public int lengthMilliseconds { get; set; }
        [JsonPropertyName("type")]
        public string _type { get; set; }
        public string description { get; set; }
        public string subDescription { get; set; }
        public string thumbnailUrl { get; set; }
        public string gameId { get; set; }
        public string gameDisplayName { get; set; }
        public string gameBoxArtUrl { get; set; }
    }

    public class Video
    {
        public string title { get; set; }
        public string id { get; set; }
        public DateTime created_at { get; set; }
        public double start { get; set; }
        public double end { get; set; }
        public double length { get; set; } = -1;
        public int viewCount { get; set; }
        public string game { get; set; }
        public List<VideoChapter> chapters { get; set; } = new();

#region DeprecatedProperties
        /// <summary>Deprecated. Used only by chats from before 8d521f7a78222bec187b56c3c747909d240add21.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string duration { get; set; } = null;
        /// <summary>Deprecated. Used only by chats from before 8d521f7a78222bec187b56c3c747909d240add21.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string user_id { get; set; } = null;
        /// <summary>Deprecated. Used only by chats from before 8d521f7a78222bec187b56c3c747909d240add21.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string user_name { get; set; } = null;
#endregion
    }

    [DebuggerDisplay("{name}")]
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

    [DebuggerDisplay("{name}")]
    public class EmbedChatBadge
    {
        public string name { get; set; }
        public Dictionary<string, ChatBadgeData> versions { get; set; }
    }

    [DebuggerDisplay("{name}")]
    public class LegacyEmbedChatBadge
    {
        public string name { get; set; }
        public Dictionary<string, byte[]> versions { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string> urls { get; set; }
    }

    [DebuggerDisplay("{prefix}")]
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
    }

    public class LegacyEmbeddedData
    {
        public List<EmbedEmoteData> thirdParty { get; set; } = new();
        public List<EmbedEmoteData> firstParty { get; set; } = new();
        public List<LegacyEmbedChatBadge> twitchBadges { get; set; } = new();
        public List<EmbedCheerEmote> twitchBits { get; set; } = new();
    }

    public class ChatRoot
    {
        public ChatRootInfo FileInfo { get; set; } = new();
        public Streamer streamer { get; set; }
        public Video video { get; set; }
        public List<Comment> comments { get; set; }
        public EmbeddedData embeddedData { get; set; }
    }
}