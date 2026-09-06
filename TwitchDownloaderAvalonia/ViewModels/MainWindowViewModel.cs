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
            AppStatus status,
            FfmpegService ffmpeg,
            DialogService dialogs,
            FileDialogService fileDialogs,
            FileCollisionService collision,
            ThumbnailService thumbnails)
        {
            _ffmpeg = ffmpeg;
            Status = status;
            Vod = new VodDownloadViewModel(settings, status, ffmpeg, dialogs, fileDialogs, collision, thumbnails);
            Clip = new DownloadPlaceholderViewModel(
                "Clip",
                "https://www.twitch.tv/user/clip/...",
                "Download",
                "Clip download uses the same Get Info → quality → Download flow as Video.");
            ChatDownload = new DownloadPlaceholderViewModel(
                "Chat",
                "https://www.twitch.tv/videos/...",
                "Download",
                "Chat download will reuse this layout: URL, Get Info, options, Advanced, log.");
            ChatUpdate = new DownloadPlaceholderViewModel(
                "Chat Update",
                "Path to an existing chat JSON / ZIP",
                "Update",
                "Chat Update stays in the sidebar. Embed missing emotes and restamp chats here later.");
            ChatRender = new ChatRenderViewModel();
            Search = new SearchViewModel();
            Queue = new QueueViewModel();
            SettingsPage = new SettingsViewModel(settings, status);
            About = new AboutViewModel();
            CurrentPage = Vod;
            WindowTitle = $"Twitch Downloader v{typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3)}";
        }

        public AppStatus Status { get; }
        public VodDownloadViewModel Vod { get; }
        public DownloadPlaceholderViewModel Clip { get; }
        public DownloadPlaceholderViewModel ChatDownload { get; }
        public DownloadPlaceholderViewModel ChatUpdate { get; }
        public ChatRenderViewModel ChatRender { get; }
        public SearchViewModel Search { get; }
        public QueueViewModel Queue { get; }
        public SettingsViewModel SettingsPage { get; }
        public AboutViewModel About { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageTitle))]
        public partial ViewModelBase CurrentPage { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageTitle))]
        public partial AppPage SelectedPage { get; set; } = AppPage.Vod;

        [ObservableProperty]
        public partial string WindowTitle { get; set; }

        public string PageTitle => SelectedPage switch
        {
            AppPage.Vod => "Video",
            AppPage.Clip => "Clip",
            AppPage.ChatDownload => "Chat",
            AppPage.ChatUpdate => "Chat Update",
            AppPage.ChatRender => "Chat Render",
            AppPage.Search => "Search",
            AppPage.Queue => "Queue",
            AppPage.Settings => "Settings",
            AppPage.About => "About",
            _ => "Video",
        };

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
                AppPage.Search => Search,
                AppPage.Queue => Queue,
                AppPage.Settings => SettingsPage,
                AppPage.About => About,
                _ => Vod,
            };
        }

        public async Task InitializeAsync()
        {
            if (!_ffmpeg.NeedsRefresh() && _ffmpeg.IsAvailable())
                return;

            var previousTitle = WindowTitle;
            var previousKind = Status.Kind;
            var previousMessage = Status.Message;
            var progress = new AvaloniaTaskProgress(
                LogLevel.Info | LogLevel.Error,
                percent => Status.Progress = percent,
                status =>
                {
                    WindowTitle = $"{previousTitle} - {status}";
                    Status.Set(AppStatusKind.Running, status);
                });

            try
            {
                await _ffmpeg.EnsureAvailableAsync(progress);
                Status.Set(previousKind, previousMessage, 0);
            }
            catch (Exception ex)
            {
                WindowTitle = previousTitle;
                Status.Set(AppStatusKind.Error, "FFmpeg download failed", 0);
                Vod.AppendLog("ERROR: Unable to download FFmpeg: " + ex.Message);
                return;
            }

            WindowTitle = previousTitle;
        }
    }
}
