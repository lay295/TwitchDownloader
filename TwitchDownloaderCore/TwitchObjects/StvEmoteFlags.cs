using System;

namespace TwitchDownloaderCore.TwitchObjects
{
    // https://github.com/SevenTV/Common/blob/4139fcc3eb8d79003573b26b552ef112ec85b8df/structures/v3/type.emote.go#L49
    [Flags]
    public enum StvEmoteFlags
    {
        Private                 = 1 << 0, // The emote is private and can only be accessed by its owner, editors and moderators
        Authentic               = 1 << 1, // The emote was verified to be an original creation by the uploader
        ZeroWidth               = 1 << 8, // The emote is recommended to be enabled as Zero-Width


        // Content Flags

        ContentSexual           = 1 << 16, // Sexually Suggesive
        ContentEpilepsy         = 1 << 17, // Rapid flashing
        ContentEdgy             = 1 << 18, // Edgy or distasteful, may be offensive to some users
        ContentTwitchDisallowed = 1 << 24, // Not allowed specifically on the Twitch platform
    };
}
