using System.Collections.Specialized;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public partial class SearchViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
        private readonly DialogService _dialogs;
        private readonly ThumbnailService _thumbnails;
        private readonly QueueEnqueueService _enqueue;
        private readonly Func<SearchResultItem, Task> _openItem;
        private readonly SearchRequestCoordinator _searchRequests = new();
        private readonly List<string> _cursors = [""];
        private readonly Dictionary<string, SearchResultItem> _selected = new(StringComparer.Ordinal);
        private User? _currentChannel;
        private CancellationTokenSource? _thumbnailCts;
        private int _cursorIndex;
        private bool _hasNextPage;
        private bool _suppressSearch;
        private bool _hasSearched;

        public SearchViewModel(
            SettingsService settings,
            AppStatus status,
            DialogService dialogs,
            ThumbnailService thumbnails,
            Func<SearchResultItem, Task> openItem,
            QueueEnqueueService enqueue)
        {
            _settings = settings;
            AppStatus = status;
            _dialogs = dialogs;
            _thumbnails = thumbnails;
            _openItem = openItem;
            _enqueue = enqueue;

            VideoTypes = CreateVideoTypes();
            ClipPeriods = CreateClipPeriods();

            _suppressSearch = true;
            SelectedVideoType = VideoTypes[0];
            SelectedClipPeriod = ClipPeriods[2];
            SelectedPageSize = 30;
            RefreshChannelSuggestions();
            _suppressSearch = false;

            Results.CollectionChanged += OnResultsChanged;
        }

        public AppStatus AppStatus { get; }
        public ObservableCollection<SearchResultItem> Results { get; } = [];
        public ObservableCollection<string> ChannelSuggestions { get; } = [];
        public IReadOnlyList<SearchFilterOption> VideoTypes { get; private set; }
        public IReadOnlyList<SearchFilterOption> ClipPeriods { get; private set; }
        public IReadOnlyList<int> PageSizes { get; } = [16, 30, 50, 100];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsVideos))]
        [NotifyPropertyChangedFor(nameof(IsClips))]
        public partial SearchKind Kind { get; set; } = SearchKind.Videos;

        [ObservableProperty]
        public partial string ChannelQuery { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string? SelectedSuggestion { get; set; }

        [ObservableProperty]
        public partial SearchFilterOption? SelectedVideoType { get; set; }

        [ObservableProperty]
        public partial SearchFilterOption? SelectedClipPeriod { get; set; }

        [ObservableProperty]
        public partial int SelectedPageSize { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
        [NotifyCanExecuteChangedFor(nameof(SelectAllCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenSelectedCommand))]
        [NotifyCanExecuteChangedFor(nameof(EnqueueSelectedCommand))]
        [NotifyPropertyChangedFor(nameof(CanEditFilters))]
        [NotifyPropertyChangedFor(nameof(ShowSearchSpinner))]
        [NotifyPropertyChangedFor(nameof(ShowEmptyIdleGif))]
        [NotifyPropertyChangedFor(nameof(EmptyText))]
        [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
        public partial bool IsSearching { get; set; }

        public bool IsVideos => Kind == SearchKind.Videos;
        public bool IsClips => Kind == SearchKind.Clips;
        public bool CanEditFilters => !IsSearching;
        public bool HasResults => Results.Count > 0;
        public bool ShowEmptyState => Results.Count == 0;
        public bool ShowSearchSpinner => IsSearching && AppStatus.ShowStatusImage;
        public bool ShowEmptyIdleGif => !IsSearching && AppStatus.ShowStatusImage;
        public int SelectedCount => _selected.Count;
        public string SelectedCountText => Loc.Get("search.selected_count", SelectedCount);
        public string AddToQueueText => SelectedCount > 1
            ? Loc.Get("search.add_n_to_queue", SelectedCount)
            : Loc.Get("search.add_to_queue");
        public bool CanSearch => !IsSearching;
        public bool CanNextPage => !IsSearching && _hasNextPage;
        public bool CanPreviousPage => !IsSearching && _cursorIndex > 0;
        public bool CanSelectAll => !IsSearching && Results.Count > 0;
        public bool CanOpenSelected => !IsSearching && _selected.Count == 1;
        public bool CanEnqueueSelected => !IsSearching && _selected.Count > 0;

        public string EmptyText
        {
            get
            {
                if (IsSearching)
                    return Loc.Get("search.searching");

                if (_hasSearched)
                    return Loc.Get("search.empty_results");

                return Loc.Get("search.empty_idle");
            }
        }

        protected override void OnCultureChanged(object? sender, EventArgs e)
        {
            var videoValue = SelectedVideoType?.Value;
            var clipValue = SelectedClipPeriod?.Value;
            var suppress = _suppressSearch;

            _suppressSearch = true;
            VideoTypes = CreateVideoTypes();
            ClipPeriods = CreateClipPeriods();
            OnPropertyChanged(nameof(VideoTypes));
            OnPropertyChanged(nameof(ClipPeriods));
            SelectedVideoType = VideoTypes.FirstOrDefault(option => option.Value == videoValue) ?? VideoTypes[0];
            SelectedClipPeriod = ClipPeriods.FirstOrDefault(option => option.Value == clipValue) ?? ClipPeriods[2];
            RefreshChannelSuggestions();
            _suppressSearch = suppress;
            Notify(nameof(AddToQueueText), nameof(SelectedCountText), nameof(EmptyText));
        }

        private static IReadOnlyList<SearchFilterOption> CreateVideoTypes() =>
        [
            new("search.video_all", ""),
            new("search.video_archive", "ARCHIVE"),
            new("search.video_highlight", "HIGHLIGHT"),
            new("search.video_upload", "UPLOAD"),
        ];

        private static IReadOnlyList<SearchFilterOption> CreateClipPeriods() =>
        [
            new("search.clip_day", "LAST_DAY"),
            new("search.clip_week", "LAST_WEEK"),
            new("search.clip_month", "LAST_MONTH"),
            new("search.clip_all", "ALL_TIME"),
        ];

        partial void OnKindChanged(SearchKind value) => OnSearchKindChanged(value);

        private void OnSearchKindChanged(SearchKind _)
        {
            if (_suppressSearch)
                return;

            _selected.Clear();
            NotifySelection();
            RestartSearch();
        }

        partial void OnSelectedSuggestionChanged(string? value)
        {
            if (_suppressSearch || string.IsNullOrEmpty(value))
                return;

            if (value == Loc.Get("search.clear_recent"))
            {
                Dispatcher.UIThread.Post(ClearRecentChannels, DispatcherPriority.Background);
                return;
            }

            if (string.Equals(value, _currentChannel?.login, StringComparison.OrdinalIgnoreCase))
                return;

            ChannelQuery = value;
            _ = SearchAsync();
        }

        partial void OnSelectedVideoTypeChanged(SearchFilterOption? value)
        {
            if (_suppressSearch || Kind != SearchKind.Videos)
                return;

            OnSearchFilterChanged(value);
        }

        partial void OnSelectedClipPeriodChanged(SearchFilterOption? value)
        {
            if (_suppressSearch || Kind != SearchKind.Clips)
                return;

            OnSearchFilterChanged(value);
        }

        private void OnSearchFilterChanged(SearchFilterOption? _) => RestartSearch();

        partial void OnSelectedPageSizeChanged(int value)
        {
            if (_suppressSearch || value <= 0)
                return;

            ResetPagination();
            _ = UpdateListAsync();
        }

        [RelayCommand(CanExecute = nameof(CanSearch))]
        private Task SearchAsync()
        {
            return RunSearchOperationAsync(async token =>
            {
                var textTrimmed = ChannelQuery.Trim();
                if (!textTrimmed.Equals(_currentChannel?.login, StringComparison.InvariantCultureIgnoreCase))
                {
                    _currentChannel = null;
                    if (!string.IsNullOrEmpty(textTrimmed) && !textTrimmed.Any(char.IsWhiteSpace))
                    {
                        try
                        {
                            token.ThrowIfCancellationRequested();
                            var idRes = await TwitchHelper.GetUserIds([textTrimmed.ToLowerInvariant()]);
                            token.ThrowIfCancellationRequested();
                            var ids = idRes.data?.users?
                                .Where(user => !string.IsNullOrEmpty(user?.id))
                                .Select(user => user.id)
                                .ToArray() ?? [];

                            if (ids.Length > 0)
                            {
                                var infoRes = await TwitchHelper.GetUserInfo(ids);
                                token.ThrowIfCancellationRequested();
                                _currentChannel = infoRes.data?.users?.FirstOrDefault();
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            if (_settings.Current.VerboseErrors)
                                await _dialogs.ShowErrorAsync(Loc.Get("dialogs.verbose_error"), ex.ToString());
                        }
                    }
                }

                token.ThrowIfCancellationRequested();
                _selected.Clear();
                NotifySelection();
                ResetPagination();
                await UpdateListCoreAsync(token);
            });
        }

        [RelayCommand(CanExecute = nameof(CanNextPage))]
        private Task NextPageAsync()
        {
            return RunSearchOperationAsync(async token =>
            {
                var previousIndex = _cursorIndex;
                var previousHasNext = _hasNextPage;
                if (_cursorIndex < _cursors.Count - 1)
                    _cursorIndex++;

                var loaded = await UpdateListCoreAsync(token);
                if (!loaded)
                {
                    _cursorIndex = previousIndex;
                    _hasNextPage = previousHasNext;
                }
            });
        }

        [RelayCommand(CanExecute = nameof(CanPreviousPage))]
        private Task PreviousPageAsync()
        {
            return RunSearchOperationAsync(async token =>
            {
                var previousIndex = _cursorIndex;
                var previousHasNext = _hasNextPage;
                if (_cursorIndex > 0)
                    _cursorIndex--;

                var loaded = await UpdateListCoreAsync(token);
                if (!loaded)
                {
                    _cursorIndex = previousIndex;
                    _hasNextPage = previousHasNext;
                }
            });
        }

        [RelayCommand(CanExecute = nameof(CanSelectAll))]
        private void SelectAll()
        {
            foreach (var item in Results)
                item.IsSelected = true;
        }

        [RelayCommand(CanExecute = nameof(CanOpenSelected))]
        private Task OpenSelectedAsync()
        {
            var item = _selected.Values.FirstOrDefault();
            if (item is null)
                return Task.CompletedTask;

            return _openItem(item);
        }

        [RelayCommand(CanExecute = nameof(CanEnqueueSelected))]
        private Task EnqueueSelectedAsync()
        {
            return EnqueueItemsAsync([.. _selected.Values]);
        }

        private Task EnqueueOneAsync(SearchResultItem item)
        {
            return EnqueueItemsAsync([item]);
        }

        private async Task EnqueueItemsAsync(IReadOnlyList<SearchResultItem> items)
        {
            if (items.Count == 0)
                return;

            var queued = await _enqueue.EnqueueAsync([.. items.Select(QueueableMedia.FromSearch)]);
            if (!queued)
                return;

            foreach (var item in items)
                item.IsSelected = false;
        }

        private Task UpdateListAsync()
        {
            return RunSearchOperationAsync(async token => { await UpdateListCoreAsync(token); });
        }

        private async Task RunSearchOperationAsync(Func<CancellationToken, Task> operation)
        {
            var token = _searchRequests.StartNew();
            IsSearching = true;
            try
            {
                await operation(token);
            }
            catch (OperationCanceledException) when (!_searchRequests.IsCurrent(token))
            {
            }
            finally
            {
                if (_searchRequests.IsCurrent(token))
                {
                    IsSearching = false;
                    NotifyPaging();
                }
            }
        }

        private async Task<bool> UpdateListCoreAsync(CancellationToken cancellationToken)
        {
            CancelThumbnails();
            var previousHasNext = _hasNextPage;
            _hasNextPage = false;
            NotifyPaging();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(_currentChannel?.login))
                {
                    await Task.Delay(50, cancellationToken);
                    _hasSearched = !string.IsNullOrWhiteSpace(ChannelQuery);
                    ResetPagination();
                    OnPropertyChanged(nameof(EmptyText));
                    return true;
                }

                var cursor = _cursors.Count > 0 && _cursorIndex >= 0 && _cursorIndex < _cursors.Count
                    ? _cursors[_cursorIndex]
                    : "";

                var pageSize = SelectedPageSize <= 0 ? 30 : SelectedPageSize;
                var loaded = Kind == SearchKind.Videos
                    ? await LoadVideosAsync(cursor, pageSize, cancellationToken)
                    : await LoadClipsAsync(cursor, pageSize, cancellationToken);

                if (!loaded)
                {
                    _hasNextPage = previousHasNext;
                    return false;
                }

                RememberCurrentChannel();
                _hasSearched = true;
                OnPropertyChanged(nameof(EmptyText));
                return true;
            }
            catch (OperationCanceledException)
            {
                _hasNextPage = previousHasNext;
                throw;
            }
        }

        private async Task<bool> LoadVideosAsync(string cursor, int pageSize, CancellationToken cancellationToken)
        {
            GqlVideoSearchResponse res;
            try
            {
                res = await TwitchHelper.GetGqlVideos(_currentChannel!.login, cursor, pageSize, SelectedVideoType?.Value ?? "");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync(Loc.Get("search.videos_failed"), Loc.Get("search.videos_failed_message", ex.Message));
                if (_settings.Current.VerboseErrors)
                    await _dialogs.ShowErrorAsync(Loc.Get("dialogs.verbose_error"), ex.ToString());

                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var created = new List<SearchResultItem>();
            var edges = res.data?.user?.videos?.edges;
            if (edges is not null)
            {
                created.Capacity = edges.Count;
                foreach (var video in edges)
                {
                    if (video.node is null)
                        continue;

                    created.Add(CreateItem(
                        video.node.id,
                        video.node.title,
                        video.node.createdAt,
                        video.node.lengthSeconds,
                        video.node.viewCount,
                        video.node.game?.displayName,
                        video.node.previewThumbnailURL,
                        isClip: false,
                        isRecording: string.Equals(video.node.status, "RECORDING", StringComparison.OrdinalIgnoreCase)));
                }
            }

            string? newCursor = null;
            if (res.data?.user?.videos?.pageInfo?.hasNextPage == true)
                newCursor = edges?.FirstOrDefault()?.cursor;

            var applied = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_searchRequests.IsCurrent(cancellationToken))
                    return;

                ClearResults();
                AddResults(created);
                _hasNextPage = newCursor is not null;
                if (newCursor is not null && !_cursors.Contains(newCursor))
                    _cursors.Add(newCursor);

                applied = true;
            });

            return applied;
        }

        private async Task<bool> LoadClipsAsync(string cursor, int pageSize, CancellationToken cancellationToken)
        {
            GqlClipSearchResponse res;
            try
            {
                res = await TwitchHelper.GetGqlClips(
                    _currentChannel!.login,
                    SelectedClipPeriod?.Value ?? "LAST_MONTH",
                    cursor,
                    pageSize);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync(Loc.Get("search.clips_failed"), Loc.Get("search.clips_failed_message", ex.Message));
                if (_settings.Current.VerboseErrors)
                    await _dialogs.ShowErrorAsync(Loc.Get("dialogs.verbose_error"), ex.ToString());

                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var created = new List<SearchResultItem>();
            var edges = res.data?.user?.clips?.edges;
            if (edges is not null)
            {
                created.Capacity = edges.Count;
                foreach (var clip in edges)
                {
                    if (clip.node is null)
                        continue;

                    created.Add(CreateItem(
                        clip.node.slug,
                        clip.node.title,
                        clip.node.createdAt,
                        clip.node.durationSeconds,
                        clip.node.viewCount,
                        clip.node.game?.displayName,
                        clip.node.thumbnailURL,
                        isClip: true,
                        clip.node.curator?.displayName,
                        clip.node.curator?.id));
                }
            }

            string? newCursor = null;
            if (res.data?.user?.clips?.pageInfo?.hasNextPage == true)
                newCursor = edges?.FirstOrDefault(edge => edge.cursor != null)?.cursor;

            var applied = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_searchRequests.IsCurrent(cancellationToken))
                    return;

                ClearResults();
                AddResults(created);
                _hasNextPage = newCursor is not null;
                if (newCursor is not null && !_cursors.Contains(newCursor))
                    _cursors.Add(newCursor);

                applied = true;
            });

            return applied;
        }

        private SearchResultItem CreateItem(
            string id,
            string title,
            DateTime createdAt,
            int length,
            int views,
            string? game,
            string? thumbnailUrl,
            bool isClip,
            string? clipperName = null,
            string? clipperId = null,
            bool isRecording = false)
        {
            var item = new SearchResultItem
            {
                Id = id,
                Title = title,
                Time = _settings.Current.UtcVideoTime ? createdAt : createdAt.ToLocalTime(),
                Length = length,
                Views = views,
                Game = game ?? Loc.Get("common.unknown_game"),
                ThumbnailUrl = thumbnailUrl ?? string.Empty,
                IsClip = isClip,
                IsRecording = isRecording,
                StreamerName = _currentChannel?.displayName ?? _currentChannel?.login ?? Loc.Get("common.unknown_user"),
                StreamerId = _currentChannel?.id ?? string.Empty,
                ClipperName = clipperName ?? string.Empty,
                ClipperId = clipperId ?? string.Empty,
                CopyId = CopyIdAsync,
                CopyUrl = CopyUrlAsync,
                OpenInBrowser = OpenInBrowserAsync,
                OpenInApp = _openItem,
                EnqueueOne = EnqueueOneAsync,
            };

            return item;
        }

        private void AddResults(List<SearchResultItem> items)
        {
            foreach (var item in items)
            {
                item.IsSelected = _selected.ContainsKey(item.Id);
                item.PropertyChanged += OnItemPropertyChanged;
                if (item.IsSelected)
                    _selected[item.Id] = item;

                Results.Add(item);
            }

            NotifySelection();
            _ = LoadThumbnailsAsync(items);
        }

        private async Task LoadThumbnailsAsync(List<SearchResultItem> items)
        {
            if (_thumbnailCts is { } previous)
            {
                await previous.CancelAsync();
                previous.Dispose();
            }

            _thumbnailCts = new CancellationTokenSource();
            var token = _thumbnailCts.Token;

            try
            {
                await Task.WhenAll(items.Select(item => LoadThumbnailAsync(item, token)));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task LoadThumbnailAsync(SearchResultItem item, CancellationToken token)
        {
            var bytes = await _thumbnails.TryGetAsync(item.ThumbnailUrl, token) ?? await _thumbnails.TryGetAsync(null, token);

            token.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() => item.ThumbnailBytes = bytes);
        }

        private async Task CopyIdAsync(SearchResultItem item)
        {
            try
            {
                await _dialogs.CopyTextAsync(item.Id);
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync(Loc.Get("search.copy_failed"), ex.ToString());
            }
        }

        private async Task CopyUrlAsync(SearchResultItem item)
        {
            try
            {
                await _dialogs.CopyTextAsync(item.Url);
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync(Loc.Get("search.copy_failed"), ex.ToString());
            }
        }

        private Task OpenInBrowserAsync(SearchResultItem item)
        {
            Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true });
            return Task.CompletedTask;
        }

        private void ClearRecentChannels()
        {
            _suppressSearch = true;
            SelectedSuggestion = null;
            ChannelQuery = string.Empty;
            _settings.Current.RecentChannels.Clear();
            _settings.Save();
            _currentChannel = null;
            RefreshChannelSuggestions();
            _selected.Clear();
            NotifySelection();
            ResetPagination();
            ClearResults();
            _suppressSearch = false;
        }

        private void RememberCurrentChannel()
        {
            var login = _currentChannel?.login;
            if (string.IsNullOrWhiteSpace(login))
                return;

            var recent = _settings.Current.RecentChannels;
            recent.RemoveAll(channel => string.Equals(channel, login, StringComparison.OrdinalIgnoreCase));
            recent.Insert(0, login);
            while (recent.Count > 15)
                recent.RemoveAt(recent.Count - 1);

            _settings.Save();
            RefreshChannelSuggestions();
        }

        private void RefreshChannelSuggestions()
        {
            var text = ChannelQuery;
            var suppress = _suppressSearch;
            _suppressSearch = true;
            ChannelSuggestions.Clear();
            foreach (var channel in _settings.Current.RecentChannels)
                ChannelSuggestions.Add(channel);

            if (ChannelSuggestions.Count > 0)
                ChannelSuggestions.Add(Loc.Get("search.clear_recent"));

            SelectedSuggestion = null;
            ChannelQuery = text;
            _suppressSearch = suppress;
        }

        private void RestartSearch()
        {
            ResetPagination();
            _ = UpdateListAsync();
        }

        private void ResetPagination()
        {
            _cursors.Clear();
            _cursors.Add("");
            _cursorIndex = 0;
            _hasNextPage = false;
            ClearResults();
        }

        private void ClearResults()
        {
            foreach (var item in Results)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
                item.Detach();
            }

            Results.Clear();
        }

        private void CancelThumbnails()
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
            _thumbnailCts = null;
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SearchResultItem.IsSelected) || sender is not SearchResultItem item)
                return;

            if (item.IsSelected)
                _selected[item.Id] = item;
            else
                _selected.Remove(item.Id);

            NotifySelection();
        }

        private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(EmptyText));
            SelectAllCommand.NotifyCanExecuteChanged();
        }

        private void NotifySelection()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedCountText));
            OnPropertyChanged(nameof(AddToQueueText));
            OpenSelectedCommand.NotifyCanExecuteChanged();
            EnqueueSelectedCommand.NotifyCanExecuteChanged();
        }

        private void NotifyPaging()
        {
            NextPageCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
        }
    }
}
