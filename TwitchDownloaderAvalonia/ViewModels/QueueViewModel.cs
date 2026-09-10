namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class QueueViewModel(
        AppStatus status,
        QueueService queue,
        DialogService dialogs,
        ThumbnailService thumbnails,
        QueueEnqueueService enqueue) : ViewModelBase
    {
        public AppStatus AppStatus { get; } = status;
        public QueueService Queue { get; } = queue;

        [RelayCommand]
        private void CancelAll() => Queue.CancelAll();

        [RelayCommand]
        private void ClearFinished() => Queue.ClearFinished();

        [RelayCommand]
        private Task AddUrlsAsync() => dialogs.ShowUrlListAsync(thumbnails, enqueue);
    }
}
