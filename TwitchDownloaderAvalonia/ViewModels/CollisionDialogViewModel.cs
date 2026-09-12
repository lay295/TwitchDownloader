namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class CollisionDialogViewModel(
        string fileName,
        string fullPath,
        Action<CollisionPromptResult> close)
        : ViewModelBase
    {
        public string HeaderText => Loc.Get("dialogs.collision_header", fileName);
        public string FullPath { get; } = fullPath;

        [ObservableProperty]
        public partial bool Remember { get; set; }

        protected override void OnCultureChanged(object? sender, EventArgs e) => Notify(nameof(HeaderText));

        [RelayCommand]
        private void Overwrite() => close(new CollisionPromptResult(CollisionChoice.Overwrite, Remember));

        [RelayCommand]
        private void Rename() => close(new CollisionPromptResult(CollisionChoice.Rename, Remember));

        [RelayCommand]
        private void Cancel() => close(new CollisionPromptResult(CollisionChoice.Cancel, Remember));
    }
}
