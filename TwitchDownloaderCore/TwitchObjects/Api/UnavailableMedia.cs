using System.Text.Json.Serialization;

namespace TwitchDownloaderCore.TwitchObjects.Api
{
    public class UnavailableMedia
    {
        [JsonPropertyName("NAME")]
        public string Name { get; set; }

        [JsonPropertyName("BANDWIDTH")]
        public int Bandwidth { get; set; }

        [JsonPropertyName("CODECS")]
        public string Codecs { get; set; }

        [JsonPropertyName("RESOLUTION")]
        public string Resolution { get; set; }

        [JsonPropertyName("FILTER_REASONS")]
        public string[] FilterReasons { get; set; }

        [JsonPropertyName("AUTHORIZATION_REASONS")]
        public string[] AuthorizationReasons { get; set; }

        [JsonPropertyName("GROUP-ID")]
        public string GroupId { get; set; }

        [JsonPropertyName("FRAME-RATE")]
        public decimal FrameRate { get; set; }
    }
}