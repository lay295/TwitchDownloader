using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;

namespace TwitchDownloaderCore.VideoPlatforms.Kick
{
    public class Category
    {
        public int id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public string responsive { get; set; }
        public string banner { get; set; }
        public string parent_category { get; set; }
    }

    public class Channel
    {
        public int id { get; set; }
        public string username { get; set; }
        public string slug { get; set; }
        public string profile_picture { get; set; }
    }

    public class Clip
    {
        public string id { get; set; }
        public string livestream_id { get; set; }
        public string category_id { get; set; }
        public int channel_id { get; set; }
        public int user_id { get; set; }
        public string title { get; set; }
        public string clip_url { get; set; }
        public string thumbnail_url { get; set; }
        public string privacy { get; set; }
        public int likes { get; set; }
        public bool liked { get; set; }
        public int views { get; set; }
        public int duration { get; set; }
        public DateTime started_at { get; set; }
        public DateTime created_at { get; set; }
        public bool is_mature { get; set; }
        public string video_url { get; set; }
        public int view_count { get; set; }
        public int likes_count { get; set; }
        public Category category { get; set; }
        public Creator creator { get; set; }
        public Channel channel { get; set; }
    }

    public class Creator
    {
        public int id { get; set; }
        public string username { get; set; }
        public string slug { get; set; }
        public object profile_picture { get; set; }
    }

    public class KickClipResponse : IVideoInfo
    {
        public Clip clip { get; set; }

        public string message { get; set; }

        public string ThumbnailUrl => clip?.thumbnail_url;

        public DateTime CreatedAt => clip?.created_at ?? DateTime.MinValue;

        public string StreamerName => clip?.channel?.username;

        public string Title => clip?.title;

        public int Duration => clip?.duration ?? 0;

        public int ViewCount => clip?.views ?? 0;

        public string Game => clip?.category?.name;

        public string VideoUrl => clip?.video_url;

        public List<VideoQuality> VideoQualities { get; set; }
    }
}
