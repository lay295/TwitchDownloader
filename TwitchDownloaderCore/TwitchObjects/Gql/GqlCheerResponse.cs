using System;
using System.Collections.Generic;
using System.Text;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class Tier
    {
        public int bits { get; set; }
    }

    public class CheerNode
    {
        public string id { get; set; }
        public string prefix { get; set; }
        public List<Tier> tiers { get; set; }
    }

    public class CheerGroup
    {
        public List<CheerNode> nodes { get; set; }
        public string templateURL { get; set; }
    }

    public class CheerConfig
    {
        public List<CheerGroup> groups { get; set; }
    }

    public class Cheer
    {
        public List<CheerGroup> cheerGroups { get; set; }
    }

    public class CheerUser
    {
        public Cheer cheer { get; set; }
    }

    public class CheerData
    {
        public CheerConfig cheerConfig { get; set; }
        public CheerUser user { get; set; }
    }

    public class GqlCheerResponse
    {
        public CheerData data { get; set; }
        public Extensions extensions { get; set; }
    }
}
