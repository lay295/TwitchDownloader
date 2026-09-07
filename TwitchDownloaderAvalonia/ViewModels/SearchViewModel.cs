using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed class SearchViewModel(AppStatus status) : ViewModelBase
    {
        public AppStatus AppStatus { get; } = status;

        public string Details => "Channel and VOD search will enqueue downloads once the queue is implemented.";
    }
}
