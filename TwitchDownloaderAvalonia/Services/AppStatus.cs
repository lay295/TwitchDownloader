namespace TwitchDownloaderAvalonia.Services
{
    public sealed partial class AppStatus : ObservableObject
    {
        public AppStatus(SettingsService settings)
        {
            ReduceMotion = settings.Current.ReduceMotion;
            Message = Loc.Get("status.idle");
            LocalizationService.Current.CultureChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(QueueChipText));
                if (Kind == AppStatusKind.Idle && QueueCount == 0)
                    Message = Loc.Get("status.idle");
            };
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowStatusImage))]
        [NotifyPropertyChangedFor(nameof(ShowStage))]
        [NotifyPropertyChangedFor(nameof(IsAnimatedStatus))]
        [NotifyPropertyChangedFor(nameof(ShowMascotGif))]
        [NotifyPropertyChangedFor(nameof(ShowErrorStatus))]
        [NotifyPropertyChangedFor(nameof(AnimatedSource))]
        public partial bool ReduceMotion { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnimatedStatus))]
        [NotifyPropertyChangedFor(nameof(ShowMascotGif))]
        [NotifyPropertyChangedFor(nameof(ShowIdleWait))]
        [NotifyPropertyChangedFor(nameof(ShowStage))]
        [NotifyPropertyChangedFor(nameof(ShowErrorStatus))]
        [NotifyPropertyChangedFor(nameof(AnimatedSource))]
        [NotifyPropertyChangedFor(nameof(ShowProgress))]
        public partial AppStatusKind Kind { get; set; } = AppStatusKind.Idle;

        [ObservableProperty]
        public partial string Message { get; set; }

        [ObservableProperty]
        public partial double Progress { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(QueueChipText))]
        public partial int QueueCount { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPreview))]
        [NotifyPropertyChangedFor(nameof(ShowStage))]
        [NotifyPropertyChangedFor(nameof(ShowMascotGif))]
        [NotifyPropertyChangedFor(nameof(ShowIdleWait))]
        [NotifyPropertyChangedFor(nameof(ShowErrorStatus))]
        [NotifyPropertyChangedFor(nameof(AnimatedSource))]
        public partial byte[]? PreviewBytes { get; set; }

        public bool ShowStatusImage => !ReduceMotion;

        public bool HasPreview => PreviewBytes is { Length: > 0 };

        public bool ShowIdleWait => !HasPreview && Kind == AppStatusKind.Idle;

        public bool ShowStage => HasPreview || ShowStatusImage || ShowIdleWait;

        public bool IsAnimatedStatus => ShowStatusImage && !HasPreview && Kind is AppStatusKind.Running or AppStatusKind.Canceling;

        public bool ShowMascotGif => IsAnimatedStatus;

        public bool ShowErrorStatus => ShowStatusImage && !HasPreview && Kind == AppStatusKind.Error;

        public bool ShowProgress => Kind is AppStatusKind.Running or AppStatusKind.Canceling;

        public string? AnimatedSource => Kind switch
        {
            AppStatusKind.Running when ShowMascotGif => "avares://TwitchDownloaderAvalonia/Assets/Status/ppOverheat.gif",
            AppStatusKind.Canceling when ShowMascotGif => "avares://TwitchDownloaderAvalonia/Assets/Status/ppStretch.gif",
            _ => null,
        };

        public void Set(AppStatusKind kind, string message, double? progress = null, byte[]? preview = null)
        {
            Kind = kind;
            Message = message;
            if (progress.HasValue)
                Progress = progress.Value;

            if (!ReferenceEquals(PreviewBytes, preview))
                PreviewBytes = preview;
        }

        public string QueueChipText => Loc.Get("status.queue_chip", QueueCount);
    }
}
