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
        public bool animated { get; set; }
        public bool modifier { get; set; }
    }

    public class FFZImages
    {
        [JsonPropertyName("1x")]
        public string _1x { get; set; }

        [JsonPropertyName("2x")]
        public string _2x { get; set; }

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
