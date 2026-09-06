namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed class PlaceholderViewModel(string title, string details) : ViewModelBase
    {
        public string Title { get; } = title;
        public string Details { get; } = details;
    }
}
