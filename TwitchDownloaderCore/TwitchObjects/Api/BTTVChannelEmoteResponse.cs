using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects.Api
{
    public class BTTVChannelEmoteResponse
    {
        public string id { get; set; }
        public List<object> bots { get; set; }
        public string avatar { get; set; }
        public List<BTTVEmote> channelEmotes { get; set; }
        public List<BTTVEmote> sharedEmotes { get; set; }
    }
    public class BTTVEmote
    {
        public string id { get; set; }
        public string code { get; set; }
        public string imageType { get; set; }
        public string userId { get; set; }
        public User user { get; set; }
    }

    public class User
    {
        public string id { get; set; }
        public string name { get; set; }
        public string displayName { get; set; }
        public string providerId { get; set; }
    }
}
