namespace TwitchDownloaderAvalonia.Models
{
    public sealed record SearchFilterOption(string Name, string Value)
    {
        public override string ToString() => Name;
    }
}
