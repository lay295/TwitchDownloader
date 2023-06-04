using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class GqlGlobalBadgeResponse
    {
        public TwitchGlobalChatBadgeData data { get; set; }
    }

    public class GqlSubBadgeResponse
    {
        public TwitchUserChatBadgeData data { get; set; }
    }
    public class TwitchUserChatBadgeData
    {
        public TwitchChatBadgeUser user { get; set; }
    }
    public class TwitchChatBadgeUser
    {
        [JsonPropertyName("broadcastBadges")]
        public List<TwitchChatBadge> badges { get; set; }
    }

    public class TwitchGlobalChatBadgeData
    {
        public List<TwitchChatBadge> badges { get; set; }
    }

    public class TwitchBadgeSet
    {
        public Dictionary<string, TwitchChatBadge> versions { get; set; }
    }

    public class TwitchChatBadge
    {
        [JsonPropertyName("imageURL")]
        public string image_url_2x { get; set; }
        public string description { get; set; }
        public string title { get; set; }
        [JsonPropertyName("setID")]
        public string name { get; set; }
        public string version { get; set; }
    }
}
