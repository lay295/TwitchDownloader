namespace TwitchDownloaderAvalonia.Models
{
    public sealed class AppSettings
    {
        public UiSettings Ui { get; set; } = new();
        public GeneralSettings General { get; set; } = new();
        public VodSettings Vod { get; set; } = new();
        public ClipSettings Clip { get; set; } = new();
        public ChatSettings Chat { get; set; } = new();
        public QueueSettings Queue { get; set; } = new();
        public SearchSettings Search { get; set; } = new();
        public ChatRenderSettings Render { get; set; } = new();
        public List<NamedRenderPreset> RenderPresets { get; set; } = [];

        public void CopyFrom(AppSettings other)
        {
            Ui = SettingsCopy.Clone(other.Ui);
            General = SettingsCopy.Clone(other.General);
            Vod = SettingsCopy.Clone(other.Vod);
            Clip = SettingsCopy.Clone(other.Clip);
            Chat = SettingsCopy.Clone(other.Chat);
            Queue = SettingsCopy.Clone(other.Queue);
            Search = SettingsCopy.Clone(other.Search);
            Render = SettingsCopy.Clone(other.Render);
            RenderPresets = [.. other.RenderPresets.Select(preset => preset.Clone())];
        }
    }
}
