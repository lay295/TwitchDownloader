using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.VideoPlatforms.Kick
{
    public class KickChatData
    {
        public List<KickMessage> messages { get; set; }
        public string cursor { get; set; }
        public object pinned_message { get; set; }
    }

    public class KickIdentity
    {
        public string color { get; set; }
        public List<KickBadge> badges { get; set; }
    }

    public class KickBadge
    {
        public string type { get; set; }
        public string text { get; set; }
        public int count { get; set; }
        public bool active { get; set; }
    }

    public class KickMessage
    {
        public string id { get; set; }
        public int chat_id { get; set; }
        public int user_id { get; set; }
        public string content { get; set; }
        public string type { get; set; }
        public object metadata { get; set; }
        public DateTime created_at { get; set; }
        public KickSender sender { get; set; }
    }

    public class KickChatResponse
    {
        public KickChatResponseStatus status { get; set; }
        public KickChatData data { get; set; }
    }

    public class KickSender
    {
        public int id { get; set; }
        public string slug { get; set; }
        public string username { get; set; }
        public KickIdentity identity { get; set; }
    }

    public class KickChatResponseStatus
    {
        public bool error { get; set; }
        public int code { get; set; }
        public string message { get; set; }
    }
}
