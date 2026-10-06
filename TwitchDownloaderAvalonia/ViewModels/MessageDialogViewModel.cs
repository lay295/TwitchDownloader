namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class MessageDialogViewModel(
        LocalizationService loc,
        string title,
        string message,
        Action<bool> close,
        bool showCancel = false) : ViewModelBase(loc)
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
