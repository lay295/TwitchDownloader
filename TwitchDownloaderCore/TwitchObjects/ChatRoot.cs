using Newtonsoft.Json;
using System;
using System.Collections.Generic;

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
    [JsonProperty("msg-id")]
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

public class VideoTime
{
    public double start { get; set; }
    public double end { get; set; }
}

public class ThirdPartyEmoteData
{
    public string id { get; set; }
    public int imageScale { get; set; }
    public byte[] data { get; set; }
    public string name { get; set; }
}

public class FirstPartyEmoteData
{
    public string id { get; set; }
    public int imageScale { get; set; }
    public byte[] data { get; set; }
}

public class Emotes
{
    public List<ThirdPartyEmoteData> thirdParty { get; set; }
    public List<FirstPartyEmoteData> firstParty { get; set; }
}

public class CommentResponse
{
    public List<Comment> comments { get; set; }
    public string _next { get; set; }
}

public class ChatRoot
{
    public Streamer streamer { get; set; }
    public List<Comment> comments { get; set; }
    public VideoTime video { get; set; }
    public Emotes emotes { get; set; }
}