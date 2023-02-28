using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class BadgeResponse
    {
        public Dictionary<string, BadgeSet> BadgeSets { get; set; }
    }

    public class BadgeSet
    {
        public Dictionary<string, ChatBadgeItem> Versions { get; set; }
    }

    public class ChatBadgeItem
    {
        public string ImageUrl { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
    }
}
