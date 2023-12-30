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

    // TODO: Add Bzip2 and possibly 7Zip support
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