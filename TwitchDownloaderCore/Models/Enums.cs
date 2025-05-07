namespace TwitchDownloaderCore.Models
{
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

    public enum VideoTrimMode
    {
        Safe,
        Exact
    }
}