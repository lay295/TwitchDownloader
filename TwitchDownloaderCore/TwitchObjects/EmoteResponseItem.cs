using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class EmoteResponseItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string ImageType { get; set; }
        public string ImageUrl { get; set; }
        public bool IsZeroWidth { get; set; }
    }
}
