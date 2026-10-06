using System.Windows.Input;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class UrlBoxActionsViewModel(
        LocalizationService loc,
        IDialogService dialogs,
        Action<string> setText)
        : ViewModelBase(loc), IUrlBoxHost
    {
        ICommand IUrlBoxHost.PasteCommand => PasteCommand;
        ICommand IUrlBoxHost.ClearCommand => ClearCommand;
        [RelayCommand]
        private async Task PasteAsync()
        {
            var text = await dialogs.GetClipboardTextAsync();
            if (!string.IsNullOrEmpty(text))
                setText(text);
        }

        [RelayCommand]
        private void Clear() => setText(string.Empty);
    }
}
