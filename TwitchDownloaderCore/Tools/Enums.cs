using System.Text.Json.Serialization;

namespace TwitchDownloaderCore.Tools
{
    public enum VideoType
    {
        Unknown,
        Video,
        Clip
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum VideoPlatform
    {
        Unknown,
        Twitch,
        Kick,
        Youtube
    }

    public enum ChatCompression
    {
        None,
        Gzip
    }

    public enum ChatFormat
    {
        Json,
        Text,
        Html
    }

    public enum TimestampFormat
    {
        Utc,
        Relative,
        None,
        UtcFull
    }
}