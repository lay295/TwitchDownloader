using CommunityToolkit.Mvvm.ComponentModel;
using TwitchDownloaderAvalonia.Models;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed partial class AppStatus : ObservableObject
    {
        public AppStatus(SettingsService settings)
        {
            ReduceMotion = settings.Current.ReduceMotion;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowStatusImage))]
        [NotifyPropertyChangedFor(nameof(IsAnimatedStatus))]
        [NotifyPropertyChangedFor(nameof(ShowErrorStatus))]
        public partial bool ReduceMotion { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnimatedStatus))]
        [NotifyPropertyChangedFor(nameof(ShowErrorStatus))]
        [NotifyPropertyChangedFor(nameof(AnimatedSource))]
        [NotifyPropertyChangedFor(nameof(ShowProgress))]
        public partial AppStatusKind Kind { get; set; } = AppStatusKind.Idle;

        [ObservableProperty]
        public partial string Message { get; set; } = "Idle";

        [ObservableProperty]
        public partial double Progress { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(QueueChipText))]
        public partial int QueueCount { get; set; }

        public bool ShowStatusImage => !ReduceMotion;

        public bool IsAnimatedStatus => ShowStatusImage && Kind != AppStatusKind.Error;

        public bool ShowErrorStatus => ShowStatusImage && Kind == AppStatusKind.Error;

        public bool ShowProgress => Kind is AppStatusKind.Running or AppStatusKind.Canceling;

        public string? AnimatedSource => Kind switch
        {
            AppStatusKind.Running => "avares://TwitchDownloaderAvalonia/Assets/Status/ppOverheat.gif",
            AppStatusKind.Canceling => "avares://TwitchDownloaderAvalonia/Assets/Status/ppStretch.gif",
            AppStatusKind.Error => null,
            _ => "avares://TwitchDownloaderAvalonia/Assets/Status/ppHop.gif",
        };

        public string QueueChipText => $"Queue · {QueueCount}";

        public void Set(AppStatusKind kind, string message, double? progress = null)
        {
            Kind = kind;
            Message = message;
            if (progress.HasValue)
                Progress = progress.Value;
        }
    }
}
