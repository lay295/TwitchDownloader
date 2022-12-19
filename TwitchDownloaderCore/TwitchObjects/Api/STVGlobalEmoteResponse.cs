using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects.Api
{
    public class STVData
    {
        public string id { get; set; }
        public string name { get; set; }
        public StvEmoteFlags flags { get; set; }
        public int lifecycle { get; set; }
        public bool listed { get; set; }
        public bool animated { get; set; }
        public STVOwner owner { get; set; }
        public STVHost host { get; set; }
        public List<string> tags { get; set; }
    }

    public class STVEmote
    {
        public string id { get; set; }
        public string name { get; set; }
        //These flags, not currently respected
        //https://github.com/SevenTV/Common/blob/4139fcc3eb8d79003573b26b552ef112ec85b8df/structures/v3/type.emoteset.go#L66-L72
        public int flags { get; set; }
        public object timestamp { get; set; }
        public string actor_id { get; set; }
        public STVData data { get; set; }
        public string origin_id { get; set; }
    }

    public class STVFile
    {
        public string name { get; set; }
        public string static_name { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int frame_count { get; set; }
        public int size { get; set; }
        public string format { get; set; }
    }

    public class STVHost
    {
        public string url { get; set; }
        public List<STVFile> files { get; set; }
    }

    public class Origin
    {
        public string id { get; set; }
        public int weight { get; set; }
        public List<object> slices { get; set; }
    }

    public class STVOwner
    {
        public string id { get; set; }
        public string username { get; set; }
        public string display_name { get; set; }
        public string avatar_url { get; set; }
        public STVStyle style { get; set; }
        public List<string> roles { get; set; }
    }

    public class STVGlobalEmoteResponse
    {
        public string id { get; set; }
        public string name { get; set; }
        public List<string> tags { get; set; }
        public bool immutable { get; set; }
        public bool privileged { get; set; }
        public List<STVEmote> emotes { get; set; }
        public int emote_count { get; set; }
        public int capacity { get; set; }
        public List<Origin> origins { get; set; }
        public STVOwner owner { get; set; }
    }

    public class STVStyle
    {
        public int? color { get; set; }
    }
}
