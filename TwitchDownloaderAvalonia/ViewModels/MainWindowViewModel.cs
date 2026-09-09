using System.Diagnostics;
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
            QueueService queue,
            UpdateCheckService updates)
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
            SettingsPage = new SettingsViewModel(settings, status, fileDialogs, dialogs, collision, queue);
            About = new AboutViewModel(updates, dialogs, ffmpeg);
            CurrentPage = Vod;
            AppVersion = Loc.Get("about.version", typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
            RefreshWindowTitle();
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
        public partial string WindowTitle { get; set; } = string.Empty;

        public string AppName => Loc.Get("common.app_name");

        public string AppVersion { get; }

        public string PageTitle => SelectedPage switch
        {
            AppPage.Vod => Loc.Get("nav.video"),
            AppPage.Clip => Loc.Get("nav.clip"),
            AppPage.ChatDownload => Loc.Get("nav.chat"),
            AppPage.ChatUpdate => Loc.Get("nav.chat_update"),
            AppPage.ChatRender => Loc.Get("nav.chat_render"),
            AppPage.Search => Loc.Get("nav.search"),
            AppPage.Queue => Loc.Get("nav.queue"),
            AppPage.Settings => Loc.Get("nav.settings"),
            AppPage.About => Loc.Get("nav.about"),
            _ => Loc.Get("nav.video"),
        };

        public string PageSubtitle => SelectedPage switch
        {
            AppPage.Vod => Loc.Get("vod.subtitle"),
            AppPage.Clip => Loc.Get("clip.subtitle"),
            AppPage.ChatDownload => Loc.Get("chat.subtitle"),
            AppPage.ChatUpdate => Loc.Get("update.subtitle"),
            AppPage.ChatRender => Loc.Get("render.subtitle"),
            AppPage.Search => Loc.Get("search.subtitle"),
            AppPage.Queue => Loc.Get("queue.subtitle"),
            AppPage.Settings => Loc.Get("settings.subtitle"),
            AppPage.About => About.Description,
            _ => Loc.Get("vod.subtitle"),
        };

        protected override void OnCultureChanged(object? sender, EventArgs e)
        {
            Notify(nameof(AppName), nameof(PageTitle), nameof(PageSubtitle));
            RefreshWindowTitle();
        }

        private void RefreshWindowTitle()
        {
            WindowTitle = Loc.Get("common.window_title", AppVersion);
        }

        [RelayCommand]
        private void Navigate(AppPage page) => SelectedPage = page;

        [RelayCommand]
        private void Donate()
        {
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
        }

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

            if (value == AppPage.About)
                _ = About.EnsureUpdateCheckAsync();
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
                Status.Set(AppStatusKind.Error, Loc.Get("status.ffmpeg_download_failed"), 0);
                Vod.AppendLog(Loc.Get("status.ffmpeg_download_failed_log", ex.Message));
                return;
            }

            WindowTitle = previousTitle;
        }
    }
}
