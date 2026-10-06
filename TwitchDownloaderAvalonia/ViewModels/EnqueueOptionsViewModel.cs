namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class EnqueueOptionsViewModel : ViewModelBase
    {
        public static IReadOnlyList<string> Qualities { get; } =
        [
            QualityNames.SOURCE,
            QualityNames.SOURCE_PORTRAIT,
            "1440p",
            "1080p",
            "720p",
            "480p",
            "360p",
            "160p",
            "144p",
            QualityNames.WORST,
            QualityNames.WORST_PORTRAIT,
            QualityNames.AUDIO_ONLY,
        ];

        private readonly SettingsService _settings;
        private readonly IFileDialogService _files;
        private readonly Action<EnqueueOptions?> _close;
        private bool _suppressQuality;
        private bool _suppressRender;

        public EnqueueOptionsViewModel(
            LocalizationService loc,
            SettingsService settings,
            IFileDialogService files,
            bool hasVods,
            bool hasRecordingVods,
            Action<EnqueueOptions?> close) : base(loc)
        {
            _settings = settings;
            _files = files;
            _close = close;
            HasVods = hasVods;
            HasRecordingVods = hasRecordingVods;
            Folder = settings.Current.Queue.Folder;
            AvailableQualities = !hasVods
                ? [.. Qualities.Where(quality => quality != QualityNames.AUDIO_ONLY)]
                : Qualities;

            RebuildQualityOptions();
            var preferred = settings.Current.Queue.PreferredQuality;
            SelectedQuality = !AvailableQualities.Contains(preferred)
                ? AvailableQualities[0]
                : preferred;

            _suppressQuality = true;
            SelectedQualityOption = QualityOptions.FirstOrDefault(option => option.Value == SelectedQuality) ?? QualityOptions[0];
            _suppressQuality = false;

            var queue = settings.Current.Queue;
            DownloadVideo = queue.EnqueueDownloadVideo || !queue.EnqueueDownloadChat;
            DownloadChat = queue.EnqueueDownloadChat;
            DelayVideo = queue.EnqueueDelayVideo;
            DelayChat = queue.EnqueueDelayChat;
            RenderChat = queue is { EnqueueDownloadChat: true, EnqueueRenderChat: true };

            ChatFormat = settings.Current.Chat.DownloadFormat;
            ChatCompression = settings.Current.Chat.JsonCompression;
            EmbedImages = settings.Current.Chat.EmbedEmotes;
            Bttv = settings.Current.Chat.BttvEmotes;
            Ffz = settings.Current.Chat.FfzEmotes;
            Stv = settings.Current.Chat.StvEmotes;
        }

        public bool HasVods { get; }
        public bool HasRecordingVods { get; }
        public IReadOnlyList<string> AvailableQualities { get; }
        public IReadOnlyList<LabeledOption> QualityOptions { get; private set; } = [];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddCommand))]
        [NotifyPropertyChangedFor(nameof(CanAdd))]
        public partial string Folder { get; set; }

        [ObservableProperty]
        public partial string SelectedQuality { get; set; }

        [ObservableProperty]
        public partial LabeledOption? SelectedQualityOption { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddCommand))]
        [NotifyPropertyChangedFor(nameof(CanAdd))]
        [NotifyPropertyChangedFor(nameof(IsQualityEnabled))]
        [NotifyPropertyChangedFor(nameof(IsDelayVideoEnabled))]
        public partial bool DownloadVideo { get; set; }

        [ObservableProperty]
        public partial bool DelayVideo { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddCommand))]
        [NotifyPropertyChangedFor(nameof(CanAdd))]
        [NotifyPropertyChangedFor(nameof(IsChatOptionsEnabled))]
        [NotifyPropertyChangedFor(nameof(ShowCompression))]
        [NotifyPropertyChangedFor(nameof(IsEmbedEnabled))]
        [NotifyPropertyChangedFor(nameof(IsThirdPartyEmbedEnabled))]
        [NotifyPropertyChangedFor(nameof(IsDelayChatEnabled))]
        [NotifyPropertyChangedFor(nameof(IsRenderEnabled))]
        public partial bool DownloadChat { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCompression))]
        [NotifyPropertyChangedFor(nameof(IsEmbedEnabled))]
        [NotifyPropertyChangedFor(nameof(IsThirdPartyEmbedEnabled))]
        [NotifyPropertyChangedFor(nameof(IsRenderEnabled))]
        public partial ChatFormat ChatFormat { get; set; }

        [ObservableProperty]
        public partial ChatCompression ChatCompression { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsThirdPartyEmbedEnabled))]
        public partial bool EmbedImages { get; set; }

        [ObservableProperty]
        public partial bool Bttv { get; set; }

        [ObservableProperty]
        public partial bool Ffz { get; set; }

        [ObservableProperty]
        public partial bool Stv { get; set; }

        [ObservableProperty]
        public partial bool DelayChat { get; set; }

        [ObservableProperty]
        public partial bool RenderChat { get; set; }

        public bool CanAdd => !string.IsNullOrWhiteSpace(Folder) && (DownloadVideo || DownloadChat);
        public bool IsQualityEnabled => DownloadVideo;
        public bool ShowDelayControls => HasRecordingVods;
        public bool IsDelayVideoEnabled => DownloadVideo && HasRecordingVods;
        public bool IsChatOptionsEnabled => DownloadChat;
        public bool ShowCompression => DownloadChat && ChatFormat == ChatFormat.Json;
        public bool IsEmbedEnabled => DownloadChat && ChatFormat != ChatFormat.Text;
        public bool IsThirdPartyEmbedEnabled => IsEmbedEnabled && EmbedImages;
        public bool IsDelayChatEnabled => DownloadChat && HasRecordingVods;
        public bool IsRenderEnabled => DownloadChat && ChatFormat == ChatFormat.Json;

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
            if (!CanAdd)
                return;

            _settings.Current.Queue.Folder = Folder.Trim();
            _settings.Current.Queue.PreferredQuality = SelectedQuality;
            _settings.Current.Queue.EnqueueDownloadVideo = DownloadVideo;
            _settings.Current.Queue.EnqueueDownloadChat = DownloadChat;
            _settings.Current.Queue.EnqueueRenderChat = RenderChat;
            _settings.Current.Queue.EnqueueDelayVideo = DelayVideo;
            _settings.Current.Queue.EnqueueDelayChat = DelayChat;
            _settings.Save();

            _close(new EnqueueOptions
            {
                Folder = Folder.Trim(),
                Quality = SelectedQuality,
                DownloadVideo = DownloadVideo,
                DelayVideo = HasRecordingVods && DownloadVideo && DelayVideo,
                DownloadChat = DownloadChat,
                ChatFormat = ChatFormat,
                ChatCompression = ChatCompression,
                EmbedImages = EmbedImages,
                Bttv = Bttv,
                Ffz = Ffz,
                Stv = Stv,
                DelayChat = HasRecordingVods && DownloadChat && DelayChat,
                RenderChat = IsRenderEnabled && RenderChat,
            });
        }

        [RelayCommand]
        private void Cancel() => _close(null);

        private void RebuildQualityOptions()
        {
            QualityOptions = [.. AvailableQualities.Select(value => new LabeledOption(value, QualityLabels.Get(Loc, value)))];
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

        partial void OnDownloadChatChanged(bool value)
        {
            if (!value)
                ClearRender();
        }

        partial void OnChatFormatChanged(ChatFormat value)
        {
            if (value != ChatFormat.Json)
                ClearRender();
        }

        private void ClearRender()
        {
            if (_suppressRender || !RenderChat)
                return;

            _suppressRender = true;
            RenderChat = false;
            _suppressRender = false;
        }
    }
}
