#pragma warning disable IDE1006
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class VideoMomentEdgeVideo
    {
        public string id { get; set; }
        public int lengthSeconds { get; set; }
    }

    public class Game
    {
        public string id { get; set; }
        public string displayName { get; set; }
        public string boxArtURL { get; set; }
    }

    public class GameChangeMomentDetails
    {
        public Game game { get; set; }
    }

    public class VideoMoment
    {
        public VideoMomentConnection moments { get; set; } // seemingly always blank. Probably needs Oauth in the request to be populated
        public string id { get; set; }
        public int durationMilliseconds { get; set; }
        public int positionMilliseconds { get; set; }
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string _type { get; set; }
        public string description { get; set; }
        public string subDescription { get; set; }
        public string thumbnailURL { get; set; }
        public GameChangeMomentDetails details { get; set; }
        public VideoMomentEdgeVideo video { get; set; }
    }

    public class VideoMomentEdge
    {
        public VideoMoment node { get; set; }
    }

    public class VideoMomentConnection
    {
        public List<VideoMomentEdge> edges { get; set; }
    }

    public class ChapterVideo
    {
        public string id { get; set; }
        public VideoMomentConnection moments { get; set; }
    }

    public class ChapterData
    {
        public ChapterVideo video { get; set; }
    }

    public class GqlVideoChapterResponse
    {
        public ChapterData data { get; set; }
        public Extensions extensions { get; set; }
    }
}
