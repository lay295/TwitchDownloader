using TwitchDownloaderCore.Properties;

namespace TwitchDownloaderCore.Chat
{
    public enum EmojiVendor
    {
        None,
        TwitterTwemoji,
        GoogleNotoColor
    }

    public static class EmojiVendorExtensions
    {
        private const string NOT_SUPPORTED_MESSAGE = "The requested emoji vendor is not implemented";

        public static string EmojiFolder(this EmojiVendor vendor)
        {
            return vendor switch
            {
                EmojiVendor.TwitterTwemoji => "twemoji",
                EmojiVendor.GoogleNotoColor => "noto-color",
                _ => throw new NotSupportedException(NOT_SUPPORTED_MESSAGE)
            };
        }

        public static MemoryStream MemoryStream(this EmojiVendor vendor)
        {
            return vendor switch
            {
                EmojiVendor.TwitterTwemoji => new MemoryStream(Resources.twemoji),
                EmojiVendor.GoogleNotoColor => new MemoryStream(Resources.noto_emoji),
                _ => throw new NotSupportedException(NOT_SUPPORTED_MESSAGE)
            };
        }

        public static int EmojiCount(this EmojiVendor vendor)
        {
            return vendor switch
            {
                EmojiVendor.TwitterTwemoji => 3680,
                EmojiVendor.GoogleNotoColor => 3689,
                _ => throw new NotSupportedException(NOT_SUPPORTED_MESSAGE)
            };
        }

        public static string AssetPath(this EmojiVendor vendor)
        {
            return vendor switch
            {
                EmojiVendor.TwitterTwemoji => Path.Combine("assets", "72x72"),
                EmojiVendor.GoogleNotoColor => Path.Combine("png", "72"),
                _ => throw new NotSupportedException(NOT_SUPPORTED_MESSAGE)
            };
        }

        public static char UnicodeSequenceSeparator(this EmojiVendor vendor)
        {
            return vendor switch
            {
                EmojiVendor.TwitterTwemoji => '-',
                EmojiVendor.GoogleNotoColor => '_',
                _ => throw new NotSupportedException(NOT_SUPPORTED_MESSAGE)
            };
        }
    }
}