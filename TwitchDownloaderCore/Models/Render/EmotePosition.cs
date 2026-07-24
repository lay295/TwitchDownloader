using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Models.Render
{
    public readonly record struct EmotePosition(Point DrawPoint, TwitchEmote Emote);
}