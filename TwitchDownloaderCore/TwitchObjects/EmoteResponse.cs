using System.Collections.Generic;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class EmoteResponse
    {
        public List<EmoteResponseItem> BTTV { get; set; }
        public List<EmoteResponseItem> FFZ { get; set; }
        public List<EmoteResponseItem> STV { get; set; }
    }
}
