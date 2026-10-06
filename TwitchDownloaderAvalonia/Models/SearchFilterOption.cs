namespace TwitchDownloaderAvalonia.Models
{
    public sealed class SearchFilterOption(LocalizationService loc, string nameKey, string value)
    {
        public string NameKey { get; } = nameKey;
        public string Value { get; } = value;
        public string Name => loc.Get(NameKey);
        public override string ToString() => Name;
    }
}
