namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class QueueViewModel(
        LocalizationService loc,
        AppStatus status,
        QueueService queue,
        IDialogService dialogs,
        ThumbnailService thumbnails,
        QueueEnqueueService enqueue) : ViewModelBase(loc)
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
