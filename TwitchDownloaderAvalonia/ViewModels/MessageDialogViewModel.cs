using CommunityToolkit.Mvvm.Input;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class MessageDialogViewModel(string title, string message, Action close) : ViewModelBase
    {
        public string Title { get; } = title;
        public string Message { get; } = message;

        [RelayCommand]
        private void Ok() => close();
    }
}
