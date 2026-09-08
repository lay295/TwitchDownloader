using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
        private readonly AppStatus _status;
        private readonly FileDialogService _files;

        public SettingsViewModel(
            SettingsService settings,
            AppStatus status,
            FileDialogService files,
            QueueService queue)
        {
            _settings = settings;
            _status = status;
            _files = files;
            Queue = queue;
            ReduceMotion = settings.Current.ReduceMotion;
            QueueFolder = settings.Current.QueueFolder;
            SelectedQuality = !EnqueueOptionsViewModel.Qualities.Contains(settings.Current.PreferredQuality)
                ? EnqueueOptionsViewModel.Qualities[0]
                : settings.Current.PreferredQuality;
        }

        public QueueService Queue { get; }
        public IReadOnlyList<string> Qualities => EnqueueOptionsViewModel.Qualities;

        [ObservableProperty]
        public partial bool ReduceMotion { get; set; }

        [ObservableProperty]
        public partial string QueueFolder { get; set; }

        [ObservableProperty]
        public partial string SelectedQuality { get; set; }

        public string Details => "Temp path, filename templates, bandwidth, and collision behavior will be added later.";

        [RelayCommand]
        private async Task BrowseQueueFolderAsync()
        {
            var path = await _files.PickFolderAsync("Download folder", QueueFolder);
            if (!string.IsNullOrWhiteSpace(path))
                QueueFolder = path;
        }

        partial void OnReduceMotionChanged(bool value)
        {
            _settings.Current.ReduceMotion = value;
            _settings.Save();
            _status.ReduceMotion = value;
        }

        partial void OnQueueFolderChanged(string value)
        {
            _settings.Current.QueueFolder = value;
            _settings.Save();
        }

        partial void OnSelectedQualityChanged(string value)
        {
            _settings.Current.PreferredQuality = value;
            _settings.Save();
        }
    }
}
