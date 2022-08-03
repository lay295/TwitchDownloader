using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class EmoteResponse
    {
        public List<EmoteResponseItem> BTTV { get; set; } = new();
        public List<EmoteResponseItem> FFZ { get; set; } = new();
        public List<EmoteResponseItem> STV { get; set; } = new();
    }
}
