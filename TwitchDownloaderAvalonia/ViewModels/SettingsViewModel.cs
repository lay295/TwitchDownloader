using CommunityToolkit.Mvvm.ComponentModel;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
        private readonly AppStatus _status;

        public SettingsViewModel(SettingsService settings, AppStatus status)
        {
            _settings = settings;
            _status = status;
            ReduceMotion = settings.Current.ReduceMotion;
        }

        [ObservableProperty]
        public partial bool ReduceMotion { get; set; }

        public string Details => "Remaining settings (temp path, templates, updater) will be added later.";

        partial void OnReduceMotionChanged(bool value)
        {
            _settings.Current.ReduceMotion = value;
            _settings.Save();
            _status.ReduceMotion = value;
        }
    }
}
