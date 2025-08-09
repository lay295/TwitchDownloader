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

    // https://dev.twitch.tv/docs/chat/irc/#irc-command-reference
    // https://modern.ircdocs.horse/#numerics
    public enum IrcCommand
    {
        Unknown,
        ClearChat,
        ClearMsg,
        GlobalUserState,
        Notice,
        Join,
        Part,
        Ping,
        Pong,
        PrivMsg,
        Reconnect,
        RoomState,
        UserNotice,
        UserState,
        RplWelcome, // 001
        RplYourHost, // 002
        RplCreated, // 003
        RplMyInfo, // 004
        RplNameReply, // 353
        RplEndOfNames, // 366
        RplMotd, // 372
        RplMotdStart, // 375
        RplEndOfMod, // 376
    }
}