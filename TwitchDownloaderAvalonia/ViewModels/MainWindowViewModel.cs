using TwitchDownloaderAvalonia.Update.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly FfmpegService _ffmpeg;
        private readonly UpdateLauncher _updateLauncher;
        private readonly IDialogService _dialogs;

        public MainWindowViewModel(
            LocalizationService loc,
            ThemeService themes,
            SettingsService settings,
            AppStatus status,
            FfmpegService ffmpeg,
            IDialogService dialogs,
            IFileDialogService fileDialogs,
            FileCollisionService collision,
            AbandonedVideoCacheService cacheCleaner,
            ThumbnailService thumbnails,
            QueueService queue,
            UpdateCheckService updates,
            QueueEnqueueService enqueue,
            UpdateLauncher updateLauncher,
            UpdatePreferencesStore updatePreferences) : base(loc)
        {
            _ffmpeg = ffmpeg;
            _updateLauncher = updateLauncher;
            _dialogs = dialogs;
            Status = status;

            Vod = new VodDownloadViewModel(loc, settings, status, ffmpeg, dialogs, fileDialogs, collision, cacheCleaner, thumbnails, queue);
            Clip = new ClipDownloadViewModel(loc, settings, status, ffmpeg, dialogs, fileDialogs, collision, thumbnails, queue);
            ChatDownload = new ChatDownloadViewModel(loc, settings, status, dialogs, fileDialogs, collision, thumbnails, queue);
            ChatUpdate = new ChatUpdateViewModel(loc, settings, status, dialogs, fileDialogs, collision, thumbnails, queue);
            ChatRender = new ChatRenderViewModel(loc, settings, status, ffmpeg, dialogs, fileDialogs, collision, thumbnails, queue);
            Search = new SearchViewModel(loc, settings, status, dialogs, thumbnails, OpenSearchResultAsync, enqueue);
            Queue = new QueueViewModel(loc, status, queue, dialogs, thumbnails, enqueue);
            SettingsPage = new SettingsViewModel(loc, themes, settings, status, fileDialogs, dialogs, collision, queue, updatePreferences);
            About = new AboutViewModel(loc, updates, dialogs, ffmpeg, updateLauncher);

            CurrentPage = Vod;
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

        public string AppVersion => Loc.Get("about.version", typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

        public bool HasUnfinishedWork => Queue.Queue.HasUnfinishedWork;

        public Task<bool> ConfirmCloseAsync()
        {
            if (!HasUnfinishedWork)
                return Task.FromResult(true);

            return _dialogs.ShowConfirmAsync(Loc.Get("dialogs.close_busy_title"), Loc.Get("dialogs.close_busy_message"));
        }

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
            Notify(nameof(AppName), nameof(AppVersion), nameof(PageTitle), nameof(PageSubtitle));
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

            if (value == AppPage.Settings)
                SettingsPage.RefreshThemeOptions();

            if (value == AppPage.About)
                _ = About.EnsureUpdateCheckAsync();
        }

        public async Task InitializeAsync()
        {
            if (_ffmpeg.NeedsRefresh() || !_ffmpeg.IsAvailable())
            {
                var previousTitle = WindowTitle;
                var previousKind = Status.Kind;
                var previousMessage = Status.Message;
                var progress = new AvaloniaTaskProgress(
                    Loc,
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

            await About.EnsureUpdateCheckAsync();
            if (About.HasUpdate)
                await _updateLauncher.LaunchAsync(About.RemoteVersion);
        }
    }
}
