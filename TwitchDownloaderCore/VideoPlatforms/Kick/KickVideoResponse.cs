using System;
using System.Collections.Generic;
using System.Linq;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;

namespace TwitchDownloaderCore.VideoPlatforms.Kick
{
    public class KickCategory
    {
        public int id { get; set; }
        public int category_id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public List<string> tags { get; set; }
        public string description { get; set; }
        public string deleted_at { get; set; }
        public int viewers { get; set; }
        public KickCategory2 category { get; set; }
    }

    public class KickCategory2
    {
        public int id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public string icon { get; set; }
    }

    public class KickChannel
    {
        public int id { get; set; }
        public int user_id { get; set; }
        public string slug { get; set; }
        public bool is_banned { get; set; }
        public string playback_url { get; set; }
        public string name_updated_at { get; set; }
        public bool vod_enabled { get; set; }
        public bool subscription_enabled { get; set; }
        public int followersCount { get; set; }
        public KickUser user { get; set; }
        public bool can_host { get; set; }
        public KickVerified verified { get; set; }
    }

    public class KickLivestream
    {
        public int id { get; set; }
        public string slug { get; set; }
        public int channel_id { get; set; }
        public string created_at { get; set; }
        public string session_title { get; set; }
        public bool is_live { get; set; }
        public string risk_level_id { get; set; }
        public DateTime start_time { get; set; }
        public string source { get; set; }
        public string twitch_channel { get; set; }
        public int duration { get; set; }
        public string language { get; set; }
        public bool is_mature { get; set; }
        public int viewer_count { get; set; }
        public string thumbnail { get; set; }
        public KickChannel channel { get; set; }
        public List<KickCategory> categories { get; set; }
    }

    public class KickVideoResponse : IVideoInfo
    {
        public int id { get; set; }
        public int live_stream_id { get; set; }
        public string slug { get; set; }
        public string thumb { get; set; }
        public object s3 { get; set; }
        public string trading_platform_id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string uuid { get; set; }
        public int views { get; set; }
        public DateTime? deleted_at { get; set; }
        public string source { get; set; }
        public KickLivestream livestream { get; set; }

        public string Id => id.ToString();
        public string ThumbnailUrl => livestream?.thumbnail;
        public DateTime CreatedAt => livestream.start_time;
        public string StreamerName => livestream?.channel?.slug;
        public string Title => livestream?.session_title;
        public int Duration => livestream?.duration == null ? 0 : livestream.duration / 1000;
        public int ViewCount => livestream?.viewer_count ?? 0;
        public string Game => livestream?.categories?.FirstOrDefault()?.name;
    }

    public class KickUser
    {
        public string profilepic { get; set; }
        public string bio { get; set; }
        public string twitter { get; set; }
        public string facebook { get; set; }
        public string instagram { get; set; }
        public string youtube { get; set; }
        public string discord { get; set; }
        public string tiktok { get; set; }
        public string username { get; set; }
    }

    public class KickVerified
    {
        public int id { get; set; }
        public int channel_id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
    }
}
