using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Models
{
    public sealed record FilenameParameter(string Token, string TooltipKey)
    {
        public string Tooltip => Loc.Get(TooltipKey);
    }
}
