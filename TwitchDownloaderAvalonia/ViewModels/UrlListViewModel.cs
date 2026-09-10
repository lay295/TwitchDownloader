namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class UrlListViewModel(
        SettingsService settings,
        DialogService dialogs,
        ThumbnailService thumbnails,
        QueueEnqueueService enqueue,
        Action<bool> close) : ViewModelBase
    {
        private bool _closed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAdd))]
        [NotifyCanExecuteChangedFor(nameof(AddCommand))]
        public partial string UrlText { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAdd))]
        [NotifyCanExecuteChangedFor(nameof(AddCommand))]
        public partial bool IsBusy { get; set; }

        public bool CanAdd => !IsBusy && !string.IsNullOrWhiteSpace(UrlText);

        [RelayCommand(CanExecute = nameof(CanAdd))]
        private async Task AddAsync()
        {
            var parsed = UrlListParser.Parse(UrlText);
            if (parsed.Invalid.Count > 0)
            {
                await dialogs.ShowErrorAsync(
                    Loc.Get("queue.parse_failed"),
                    Loc.Get("queue.parse_failed_message") + Environment.NewLine + string.Join(Environment.NewLine, parsed.Invalid));

                return;
            }

            if (parsed.Entries.Count == 0)
                return;

            IsBusy = true;
            try
            {
                var (items, errors) = await ResolveAsync(parsed.Entries);
                if (_closed)
                    return;

                if (errors.Count > 0)
                {
                    await dialogs.ShowErrorAsync(
                        Loc.Get("queue.info_failed"),
                        Loc.Get("queue.info_failed_message") + Environment.NewLine + string.Join(Environment.NewLine, errors));

                    return;
                }

                var queued = await enqueue.EnqueueAsync(items);
                if (queued && !_closed)
                    close(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            NotifyClosed();
            close(false);
        }

        public void NotifyClosed() => _closed = true;

        private async Task<(List<QueueableMedia> Items, List<string> Errors)> ResolveAsync(IReadOnlyList<UrlListEntry> entries)
        {
            var resolved = new QueueableMedia?[entries.Count];
            var tasks = entries.Select(async (entry, index) =>
            {
                resolved[index] = entry.IsClip
                    ? await ResolveClipAsync(entry)
                    : await ResolveVideoAsync(entry);
            });

            await Task.WhenAll(tasks);

            var items = new List<QueueableMedia>(entries.Count);
            var errors = new List<string>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (resolved[i] is { } item)
                    items.Add(item);
                else
                    errors.Add(entries[i].Source);
            }

            return (items, errors);
        }

        private async Task<QueueableMedia?> ResolveVideoAsync(UrlListEntry entry)
        {
            if (!long.TryParse(entry.Id, out var videoId))
                return null;

            try
            {
                var response = await TwitchHelper.GetVideoInfo(videoId);
                var video = response.data?.video;
                if (video is null)
                    return null;

                var thumbnail = await thumbnails.TryGetAsync(video.thumbnailURLs?.FirstOrDefault());
                return new QueueableMedia
                {
                    Id = entry.Id,
                    Title = video.title,
                    Time = settings.Current.UtcVideoTime ? video.createdAt : video.createdAt.ToLocalTime(),
                    Length = video.lengthSeconds,
                    Views = video.viewCount,
                    Game = video.game?.displayName ?? Loc.Get("common.unknown_game"),
                    IsClip = false,
                    StreamerName = video.owner?.displayName ?? Loc.Get("common.unknown_user"),
                    StreamerId = video.owner?.id ?? string.Empty,
                    ThumbnailBytes = thumbnail,
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<QueueableMedia?> ResolveClipAsync(UrlListEntry entry)
        {
            try
            {
                var response = await TwitchHelper.GetClipInfo(entry.Id);
                var clip = response.data?.clip;
                if (clip is null)
                    return null;

                var thumbnail = await thumbnails.TryGetAsync(clip.thumbnailURL);
                return new QueueableMedia
                {
                    Id = entry.Id,
                    Title = clip.title,
                    Time = settings.Current.UtcVideoTime ? clip.createdAt : clip.createdAt.ToLocalTime(),
                    Length = clip.durationSeconds,
                    Views = clip.viewCount,
                    Game = clip.game?.displayName ?? Loc.Get("common.unknown_game"),
                    IsClip = true,
                    StreamerName = clip.broadcaster?.displayName ?? Loc.Get("common.unknown_user"),
                    StreamerId = clip.broadcaster?.id ?? string.Empty,
                    ClipperName = clip.curator?.displayName ?? Loc.Get("common.unknown_user"),
                    ClipperId = clip.curator?.id ?? string.Empty,
                    ThumbnailBytes = thumbnail,
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
