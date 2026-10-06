namespace TwitchDownloaderAvalonia.Models
{
    public sealed record FilenameParameter
    {
        private readonly LocalizationService _loc;

        public FilenameParameter(LocalizationService loc, string token, string tooltipKey)
        {
            _loc = loc;
            Token = token;
            TooltipKey = tooltipKey;
        }

        public string Token { get; }
        public string TooltipKey { get; }
        public string Tooltip => _loc.Get(TooltipKey);
    }
}
