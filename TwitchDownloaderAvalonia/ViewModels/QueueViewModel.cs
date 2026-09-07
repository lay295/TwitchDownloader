using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed class QueueViewModel(AppStatus status) : ViewModelBase
    {
        public AppStatus AppStatus { get; } = status;

        public string Details => "Queued downloads and renders will appear here. Use the status bar chip to return to this page.";
    }
}
