using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed class ChatRenderViewModel(AppStatus status) : ViewModelBase
    {
        public AppStatus AppStatus { get; } = status;

        public string Details => "Chat render preview and encoding options will be added in a later milestone.";
    }
}
