using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly FfmpegService _ffmpeg;

        public MainWindowViewModel(
            SettingsService settings,
            FfmpegService ffmpeg,
            DialogService dialogs,
            FileDialogService fileDialogs,
            FileCollisionService collision,
            ThumbnailService thumbnails)
        {
            _ffmpeg = ffmpeg;
            Vod = new VodDownloadViewModel(settings, ffmpeg, dialogs, fileDialogs, collision, thumbnails);
            Clip = new PlaceholderViewModel("Clip Download", "Clip download will be added in a later milestone.");
            ChatDownload = new PlaceholderViewModel("Chat Download", "Chat download will be added in a later milestone.");
            ChatUpdate = new PlaceholderViewModel("Chat Updater", "Chat updating will be added in a later milestone.");
            ChatRender = new PlaceholderViewModel("Chat Render", "Chat rendering will be added in a later milestone.");
            Queue = new PlaceholderViewModel("Task Queue", "The download queue will be added in a later milestone.");
            CurrentPage = Vod;
            WindowTitle = $"Twitch Downloader v{typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3)}";
        }

        public VodDownloadViewModel Vod { get; }
        public PlaceholderViewModel Clip { get; }
        public PlaceholderViewModel ChatDownload { get; }
        public PlaceholderViewModel ChatUpdate { get; }
        public PlaceholderViewModel ChatRender { get; }
        public PlaceholderViewModel Queue { get; }

        [ObservableProperty]
        public partial ViewModelBase CurrentPage { get; set; }

        [ObservableProperty]
        public partial AppPage SelectedPage { get; set; } = AppPage.Vod;

        [ObservableProperty]
        public partial string WindowTitle { get; set; } = "Twitch Downloader";

        [RelayCommand]
        private void Navigate(AppPage page)
        {
            SelectedPage = page;
        }

        partial void OnSelectedPageChanged(AppPage value)
        {
            CurrentPage = value switch
            {
                AppPage.Vod => Vod,
                AppPage.Clip => Clip,
                AppPage.ChatDownload => ChatDownload,
                AppPage.ChatUpdate => ChatUpdate,
                AppPage.ChatRender => ChatRender,
                AppPage.Queue => Queue,
                _ => Vod,
            };
        }

        public async Task InitializeAsync()
        {
            if (!_ffmpeg.NeedsRefresh() && _ffmpeg.IsAvailable())
                return;

            var previousTitle = WindowTitle;
            var progress = new AvaloniaTaskProgress(
                LogLevel.Info | LogLevel.Error,
                _ => { },
                status => WindowTitle = $"{previousTitle} - {status}");

            try
            {
                await _ffmpeg.EnsureAvailableAsync(progress);
            }
            catch (Exception ex)
            {
                WindowTitle = previousTitle;
                Vod.AppendLog("ERROR: Unable to download FFmpeg: " + ex.Message);
                return;
            }

            WindowTitle = previousTitle;
        }
    }
}
