using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class EnqueueOptionsViewModel : ViewModelBase
    {
        public static IReadOnlyList<string> Qualities { get; } =
        [
            "Source",
            "Source Portrait",
            "1440p",
            "1080p",
            "720p",
            "480p",
            "360p",
            "160p",
            "144p",
            "Worst",
            "Worst Portrait",
            "Audio Only",
        ];

        private readonly SettingsService _settings;
        private readonly FileDialogService _files;
        private readonly Action<EnqueueOptions?> _close;

        public EnqueueOptionsViewModel(
            SettingsService settings,
            FileDialogService files,
            bool includeAudioOnly,
            Action<EnqueueOptions?> close)
        {
            _settings = settings;
            _files = files;
            _close = close;
            Folder = settings.Current.QueueFolder;
            AvailableQualities = !includeAudioOnly
                ? [.. Qualities.Where(quality => quality != "Audio Only")]
                : Qualities;

            var preferred = settings.Current.PreferredQuality;
            SelectedQuality = AvailableQualities.Contains(preferred)
                ? preferred
                : AvailableQualities[0];
        }

        public IReadOnlyList<string> AvailableQualities { get; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddCommand))]
        public partial string Folder { get; set; }

        [ObservableProperty]
        public partial string SelectedQuality { get; set; }

        [ObservableProperty]
        public partial bool DownloadChat { get; set; }

        public bool CanAdd => !string.IsNullOrWhiteSpace(Folder);

        [RelayCommand]
        private async Task BrowseAsync()
        {
            var path = await _files.PickFolderAsync("Download folder", Folder);
            if (!string.IsNullOrWhiteSpace(path))
                Folder = path;
        }

        [RelayCommand(CanExecute = nameof(CanAdd))]
        private void Add()
        {
            if (string.IsNullOrWhiteSpace(Folder))
                return;

            _settings.Current.QueueFolder = Folder.Trim();
            _settings.Current.PreferredQuality = SelectedQuality;
            _settings.Save();
            _close(new EnqueueOptions
            {
                Folder = Folder.Trim(),
                Quality = SelectedQuality,
                DownloadChat = DownloadChat,
            });
        }

        [RelayCommand]
        private void Cancel() => _close(null);
    }
}
