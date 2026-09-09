using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Models
{
    public sealed class SearchFilterOption(string nameKey, string value)
    {
        public string NameKey { get; } = nameKey;
        public string Value { get; } = value;
        public string Name => Loc.Get(NameKey);
        public override string ToString() => Name;
    }
}
