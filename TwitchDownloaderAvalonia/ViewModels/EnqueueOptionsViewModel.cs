using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Converters;
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
        private bool _suppressQuality;

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

            RebuildQualityOptions();
            var preferred = settings.Current.PreferredQuality;
            SelectedQuality = !AvailableQualities.Contains(preferred)
                ? AvailableQualities[0]
                : preferred;

            _suppressQuality = true;
            SelectedQualityOption = QualityOptions.FirstOrDefault(option => option.Value == SelectedQuality) ?? QualityOptions[0];
            _suppressQuality = false;
        }

        public IReadOnlyList<string> AvailableQualities { get; }
        public IReadOnlyList<LabeledOption> QualityOptions { get; private set; } = [];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddCommand))]
        public partial string Folder { get; set; }

        [ObservableProperty]
        public partial string SelectedQuality { get; set; }

        [ObservableProperty]
        public partial LabeledOption? SelectedQualityOption { get; set; }

        [ObservableProperty]
        public partial bool DownloadChat { get; set; }

        public bool CanAdd => !string.IsNullOrWhiteSpace(Folder);

        protected override void OnCultureChanged(object? sender, EventArgs e)
        {
            var quality = SelectedQualityOption?.Value ?? SelectedQuality;
            _suppressQuality = true;
            RebuildQualityOptions();
            SelectedQualityOption = QualityOptions.FirstOrDefault(option => option.Value == quality) ?? QualityOptions[0];
            _suppressQuality = false;
        }

        [RelayCommand]
        private async Task BrowseAsync()
        {
            var path = await _files.PickFolderAsync(Loc.Get("settings.download_folder"), Folder);
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

        private void RebuildQualityOptions()
        {
            QualityOptions = [.. AvailableQualities.Select(value => new LabeledOption(value, QualityLabels.Get(value)))];
            OnPropertyChanged(nameof(QualityOptions));
        }

        partial void OnSelectedQualityOptionChanged(LabeledOption? value)
        {
            if (_suppressQuality || value is null)
                return;

            SelectedQuality = value.Value;
        }

        partial void OnSelectedQualityChanged(string value)
        {
            if (_suppressQuality)
                return;

            var match = QualityOptions.FirstOrDefault(option => option.Value == value);
            if (match is not null && !ReferenceEquals(match, SelectedQualityOption))
            {
                _suppressQuality = true;
                SelectedQualityOption = match;
                _suppressQuality = false;
            }
        }
    }
}
