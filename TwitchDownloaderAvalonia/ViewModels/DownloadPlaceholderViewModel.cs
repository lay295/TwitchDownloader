namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed class DownloadPlaceholderViewModel(
        string title,
        string urlPlaceholder,
        string primaryAction,
        string details)
        : ViewModelBase
    {
        public string Title { get; } = title;
        public string UrlPlaceholder { get; } = urlPlaceholder;
        public string PrimaryAction { get; } = primaryAction;
        public string Details { get; } = details;
    }
}
