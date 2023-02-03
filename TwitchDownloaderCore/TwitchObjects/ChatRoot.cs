using Newtonsoft.Json;
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

    [DebuggerDisplay("display_name: {display_name}")]
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

        public Commenter Clone()
        {
            return new Commenter()
            {
                display_name = display_name,
                _id = _id,
                name = name,
                type = type,
                bio = bio,
                created_at = created_at,
                updated_at = updated_at,
                logo = logo
            };
        }
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

        public Fragment Clone()
        {
            return new Fragment()
            {
                text = text,
                emoticon = emoticon
            };
        }
    }

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

    [DebuggerDisplay("body: {body}")]
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

        public Message Clone()
        {
            var newMessage = new Message()
            {
                body = body,
                bits_spent = bits_spent,
                fragments = new List<Fragment>(fragments.Capacity),
                is_action = is_action,
                user_badges = new List<UserBadge>(user_badges.Capacity),
                user_color = user_color,
                user_notice_params = user_notice_params?.Clone(),
                emoticons = new List<Emoticon2>(emoticons.Capacity)
            };
            for (int i = 0; i < fragments.Count; i++)
            {
                newMessage.fragments.Add(fragments[i].Clone());
            }
            for (int i = 0; i < user_badges.Count; i++)
            {
                newMessage.user_badges.Add(user_badges[i].Clone());
            }
            for (int i = 0; i < emoticons.Count; i++)
            {
                newMessage.emoticons.Add(emoticons[i].Clone());
            }
            return newMessage;
        }
    }

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

        public Comment Clone()
        {
            return new Comment()
            {
                _id = _id,
                created_at = created_at,
                updated_at = updated_at,
                channel_id = channel_id,
                content_type = content_type,
                content_id = content_id,
                content_offset_seconds = content_offset_seconds,
                commenter = commenter.Clone(),
                source = source,
                state = state,
                message = message.Clone(),
                more_replies = more_replies
            };
        }
    }

    public class VideoChapter
    {
        public string id { get; set; }
        public int startMilliseconds { get; set; }
        public int lengthMilliseconds { get; set; }
        [JsonProperty("type")]
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
        public List<VideoChapter> chapters { get; set; } = new();
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
    }

    public class CommentResponse
    {
        public List<Comment> comments { get; set; }
        public string _next { get; set; }
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