using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects.Api
{
    public class TwitchBadgeResponse
    {
        public Dictionary<string, TwitchBadgeSet> badge_sets { get; set; }
    }

    public class TwitchBadgeSet
    {
        public Dictionary<string, TwitchChatBadge> versions { get; set; }
    }

    public class TwitchChatBadge
    {
        public string image_url_1x { get; set; }
        public string image_url_2x { get; set; }
        public string image_url_4x { get; set; }
        public string description { get; set; }
        public string title { get; set; }
        public string click_action { get; set; }
        public string click_url { get; set; }
        public object last_updated { get; set; }
    }
}
