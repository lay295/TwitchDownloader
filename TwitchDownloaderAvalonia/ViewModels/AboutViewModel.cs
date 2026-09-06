namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed class AboutViewModel : ViewModelBase
    {
        public string VersionText { get; } =
            $"Twitch Downloader v{typeof(AboutViewModel).Assembly.GetName().Version?.ToString(3)}";

        public string Details =>
            "Download and render Twitch VODs, clips, and chats.\n\n" +
            "This Avalonia UI is a work in progress.";
    }
}
