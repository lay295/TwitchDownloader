namespace TwitchDownloaderAvalonia.Models
{
    public sealed class NamedRenderPreset
    {
        public string Name { get; set; } = string.Empty;
        public ChatRenderSettings Settings { get; set; } = new();

        public NamedRenderPreset Clone() => new()
        {
            Name = Name,
            Settings = SettingsCopy.Clone(Settings),
        };
    }
}
