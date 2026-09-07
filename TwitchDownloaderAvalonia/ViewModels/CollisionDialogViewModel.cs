using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class CollisionDialogViewModel(
        string fileName,
        string fullPath,
        Action<CollisionPromptResult> close)
        : ViewModelBase
    {
        public string HeaderText { get; } = $"{fileName} already exists.";
        public string FullPath { get; } = fullPath;

        [ObservableProperty]
        public partial bool Remember { get; set; }

        [RelayCommand]
        private void Overwrite() => close(new CollisionPromptResult(CollisionChoice.Overwrite, Remember));

        [RelayCommand]
        private void Rename() => close(new CollisionPromptResult(CollisionChoice.Rename, Remember));

        [RelayCommand]
        private void Cancel() => close(new CollisionPromptResult(CollisionChoice.Cancel, Remember));
    }
}
