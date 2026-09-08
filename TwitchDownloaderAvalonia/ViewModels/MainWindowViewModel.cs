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
            ThumbnailService thumbnails,
            QueueService queue)
        {
            _ffmpeg = ffmpeg;
            Status = status;
            Vod = new VodDownloadViewModel(settings, status, ffmpeg, dialogs, fileDialogs, collision, thumbnails, queue);
            Clip = new ClipDownloadViewModel(settings, status, ffmpeg, dialogs, fileDialogs, collision, thumbnails, queue);
            ChatDownload = new ChatDownloadViewModel(settings, status, dialogs, fileDialogs, collision, thumbnails, queue);
            ChatUpdate = new ChatUpdateViewModel(settings, status, dialogs, fileDialogs, collision, thumbnails, queue);
            ChatRender = new ChatRenderViewModel(settings, status, ffmpeg, dialogs, fileDialogs, collision, thumbnails, queue);
            Search = new SearchViewModel(settings, status, dialogs, thumbnails, OpenSearchResultAsync, queue, ffmpeg, collision);
            Queue = new QueueViewModel(status, queue);
            SettingsPage = new SettingsViewModel(settings, status, fileDialogs, queue);
            About = new AboutViewModel();
            CurrentPage = Vod;
            AppVersion = $"v{typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3)}";
            WindowTitle = $"Twitch Downloader {AppVersion}";
        }

        public AppStatus Status { get; }
        public VodDownloadViewModel Vod { get; }
        public ClipDownloadViewModel Clip { get; }
        public ChatDownloadViewModel ChatDownload { get; }
        public ChatUpdateViewModel ChatUpdate { get; }
        public ChatRenderViewModel ChatRender { get; }
        public SearchViewModel Search { get; }
        public QueueViewModel Queue { get; }
        public SettingsViewModel SettingsPage { get; }
        public AboutViewModel About { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageTitle))]
        [NotifyPropertyChangedFor(nameof(PageSubtitle))]
        public partial ViewModelBase CurrentPage { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageTitle))]
        [NotifyPropertyChangedFor(nameof(PageSubtitle))]
        public partial AppPage SelectedPage { get; set; } = AppPage.Vod;

        [ObservableProperty]
        public partial string WindowTitle { get; set; }

        public string AppName => "Twitch Downloader";

        public string AppVersion { get; }

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

        public string PageSubtitle => SelectedPage switch
        {
            AppPage.Vod => "Download Twitch VODs with ease.",
            AppPage.Clip => "Download Twitch clips.",
            AppPage.ChatDownload => "Download VOD or clip chat.",
            AppPage.ChatUpdate => "Update existing chat files.",
            AppPage.ChatRender => "Render chat to video.",
            AppPage.Search => "Find VODs and clips, then open one or add a selection to the queue.",
            AppPage.Queue => "Every download and render lives here.",
            AppPage.Settings => "Preferences for this app.",
            AppPage.About => "About Twitch Downloader.",
            _ => "Download Twitch VODs with ease.",
        };

        [RelayCommand]
        private void Navigate(AppPage page) => SelectedPage = page;

        private async Task OpenSearchResultAsync(SearchResultItem item)
        {
            if (item.IsClip)
            {
                Clip.ClipUrl = item.Url;
                Navigate(AppPage.Clip);
                if (Clip.GetInfoCommand.CanExecute(null))
                    await Clip.GetInfoCommand.ExecuteAsync(null);

                return;
            }

            Vod.VideoUrl = item.Url;
            Navigate(AppPage.Vod);
            if (Vod.GetInfoCommand.CanExecute(null))
                await Vod.GetInfoCommand.ExecuteAsync(null);
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
