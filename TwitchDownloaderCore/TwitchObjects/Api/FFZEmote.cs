using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace TwitchDownloaderCore.TwitchObjects.Api
{

    public class FFZEmote
    {
        public int id { get; set; }
        public FFZUser user { get; set; }
        public string code { get; set; }
        public FFZImages images { get; set; }
        public string imageType { get; set; }
    }

    public class FFZImages
    {
        [JsonProperty("1x")]
        [JsonPropertyName("1x")]
        public string _1x { get; set; }

        [JsonProperty("2x")]
        [JsonPropertyName("2x")]
        public string _2x { get; set; }

        [JsonProperty("4x")]
        [JsonPropertyName("4x")]
        public string _4x { get; set; }
    }

    public class FFZUser
    {
        public int id { get; set; }
        public string name { get; set; }
        public string displayName { get; set; }
    }
}
