using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects.Api
{
    public class STVConnection
    {
        public string id { get; set; }
        public string platform { get; set; }
        public string username { get; set; }
        public string display_name { get; set; }
        public long linked_at { get; set; }
        public int emote_capacity { get; set; }
        public object emote_set_id { get; set; }
        public STVEmoteSet emote_set { get; set; }
    }

    public class STVEditor
    {
        public string id { get; set; }
        public int permissions { get; set; }
        public bool visible { get; set; }
        public object added_at { get; set; }
    }

    public class STVEmoteSet
    {
        public string id { get; set; }
        public string name { get; set; }
        public List<string> tags { get; set; }
        public bool immutable { get; set; }
        public bool privileged { get; set; }
        public List<STVEmote> emotes { get; set; }
        public int emote_count { get; set; }
        public int capacity { get; set; }
        public STVOwner owner { get; set; }
    }

    public class STVChannelEmoteResponse
    {
        public string id { get; set; }
        public string platform { get; set; }
        public string username { get; set; }
        public string display_name { get; set; }
        public long linked_at { get; set; }
        public int emote_capacity { get; set; }
        public object emote_set_id { get; set; }
        public STVEmoteSet emote_set { get; set; }
        public STVUser user { get; set; }
    }

    public class STVUser
    {
        public string id { get; set; }
        public string username { get; set; }
        public string display_name { get; set; }
        public long created_at { get; set; }
        public string avatar_url { get; set; }
        public string biography { get; set; }
        public STVStyle style { get; set; }
        public List<STVEditor> editors { get; set; }
        public List<string> roles { get; set; }
        public List<STVConnection> connections { get; set; }
    }
}
