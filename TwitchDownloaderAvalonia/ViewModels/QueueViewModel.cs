using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class QueueViewModel(AppStatus status, QueueService queue) : ViewModelBase
    {
        public AppStatus AppStatus { get; } = status;
        public QueueService Queue { get; } = queue;

        [RelayCommand]
        private void CancelAll() => Queue.CancelAll();

        [RelayCommand]
        private void ClearFinished() => Queue.ClearFinished();
    }
}
