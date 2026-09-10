namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class MessageDialogViewModel(
        string title,
        string message,
        Action<bool> close,
        bool showCancel = false) : ViewModelBase
    {
        public string Title { get; } = title;
        public string Message { get; } = message;
        public bool ShowCancel { get; } = showCancel;

        [RelayCommand]
        private void Ok() => close(true);

        [RelayCommand]
        private void Cancel() => close(false);
    }
}
