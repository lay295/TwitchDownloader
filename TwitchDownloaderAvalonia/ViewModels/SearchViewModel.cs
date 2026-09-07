using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderCore;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public partial class SearchViewModel : ViewModelBase
    {
        private const string CLEAR_RECENT_CHANNELS_LABEL = "Clear recent channels";

        private readonly SettingsService _settings;
        private readonly DialogService _dialogs;
        private readonly ThumbnailService _thumbnails;
        private readonly Func<SearchResultItem, Task> _openItem;
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
            Func<SearchResultItem, Task> openItem)
        {
            _settings = settings;
            AppStatus = status;
            _dialogs = dialogs;
            _thumbnails = thumbnails;
            _openItem = openItem;

            VideoTypes =
            [
                new SearchFilterOption("All videos", ""),
                new SearchFilterOption("Past broadcasts", "ARCHIVE"),
                new SearchFilterOption("Highlights", "HIGHLIGHT"),
                new SearchFilterOption("Uploads", "UPLOAD"),
            ];
            ClipPeriods =
            [
                new SearchFilterOption("Top 24 hours", "LAST_DAY"),
                new SearchFilterOption("Top 7 days", "LAST_WEEK"),
                new SearchFilterOption("Top 30 days", "LAST_MONTH"),
                new SearchFilterOption("Top all time", "ALL_TIME"),
            ];

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
        public IReadOnlyList<SearchFilterOption> VideoTypes { get; }
        public IReadOnlyList<SearchFilterOption> ClipPeriods { get; }
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
        public string SelectedCountText => $"Selected {SelectedCount}";
        public bool CanSearch => !IsSearching;
        public bool CanNextPage => !IsSearching && _hasNextPage;
        public bool CanPreviousPage => !IsSearching && _cursorIndex > 0;
        public bool CanSelectAll => !IsSearching && Results.Count > 0;
        public bool CanOpenSelected => !IsSearching && _selected.Count == 1;

        public string EmptyText
        {
            get
            {
                if (IsSearching)
                    return "Searching…";

                if (_hasSearched)
                    return "No VODs or clips found for this channel.";

                return "Enter a channel to list VODs or clips. Open a result on the Video or Clip page.";
            }
        }

        partial void OnKindChanged(SearchKind value)
        {
            if (_suppressSearch)
                return;

            _selected.Clear();
            NotifySelection();
            ResetPagination();
            _ = UpdateListAsync();
        }

        partial void OnSelectedSuggestionChanged(string? value)
        {
            if (_suppressSearch || string.IsNullOrEmpty(value))
                return;

            if (value == CLEAR_RECENT_CHANNELS_LABEL)
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

            ResetPagination();
            _ = UpdateListAsync();
        }

        partial void OnSelectedClipPeriodChanged(SearchFilterOption? value)
        {
            if (_suppressSearch || Kind != SearchKind.Clips)
                return;

            ResetPagination();
            _ = UpdateListAsync();
        }

        partial void OnSelectedPageSizeChanged(int value)
        {
            if (_suppressSearch || value <= 0)
                return;

            ResetPagination();
            _ = UpdateListAsync();
        }

        [RelayCommand(CanExecute = nameof(CanSearch))]
        private async Task SearchAsync()
        {
            IsSearching = true;

            var textTrimmed = ChannelQuery.Trim();
            if (!textTrimmed.Equals(_currentChannel?.login, StringComparison.InvariantCultureIgnoreCase))
            {
                _currentChannel = null;
                if (!string.IsNullOrEmpty(textTrimmed) && !textTrimmed.Any(char.IsWhiteSpace))
                {
                    try
                    {
                        var idRes = await TwitchHelper.GetUserIds([textTrimmed.ToLowerInvariant()]);
                        var ids = idRes.data?.users?
                            .Where(user => !string.IsNullOrEmpty(user?.id))
                            .Select(user => user.id)
                            .ToArray() ?? [];

                        if (ids.Length > 0)
                        {
                            var infoRes = await TwitchHelper.GetUserInfo(ids);
                            _currentChannel = infoRes.data?.users?.FirstOrDefault();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_settings.Current.VerboseErrors)
                            await _dialogs.ShowErrorAsync("Verbose error", ex.ToString());
                    }
                }
            }

            _selected.Clear();
            NotifySelection();
            ResetPagination();
            await UpdateListAsync();
        }

        [RelayCommand(CanExecute = nameof(CanNextPage))]
        private async Task NextPageAsync()
        {
            if (_cursorIndex < _cursors.Count - 1)
                _cursorIndex++;

            await UpdateListAsync();
        }

        [RelayCommand(CanExecute = nameof(CanPreviousPage))]
        private async Task PreviousPageAsync()
        {
            if (_cursorIndex > 0)
                _cursorIndex--;

            await UpdateListAsync();
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
            return item is null ? Task.CompletedTask : _openItem(item);
        }

        private async Task UpdateListAsync()
        {
            CancelThumbnails();
            IsSearching = true;
            _hasNextPage = false;
            NotifyPaging();

            try
            {
                if (string.IsNullOrWhiteSpace(_currentChannel?.login))
                {
                    await Task.Delay(50);
                    _hasSearched = !string.IsNullOrWhiteSpace(ChannelQuery);
                    ResetPagination();
                    OnPropertyChanged(nameof(EmptyText));
                    return;
                }

                var cursor = _cursors.Count > 0 && _cursorIndex >= 0 && _cursorIndex < _cursors.Count
                    ? _cursors[_cursorIndex]
                    : "";

                var pageSize = SelectedPageSize <= 0 ? 30 : SelectedPageSize;

                if (Kind == SearchKind.Videos)
                    await LoadVideosAsync(cursor, pageSize);
                else
                    await LoadClipsAsync(cursor, pageSize);

                RememberCurrentChannel();
                _hasSearched = true;
                OnPropertyChanged(nameof(EmptyText));
            }
            finally
            {
                IsSearching = false;
                NotifyPaging();
            }
        }

        private async Task LoadVideosAsync(string cursor, int pageSize)
        {
            GqlVideoSearchResponse res;
            try
            {
                res = await TwitchHelper.GetGqlVideos(_currentChannel!.login, cursor, pageSize, SelectedVideoType?.Value ?? "");
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync("Unable to get channel videos", $"Unable to get channel videos: {ex.Message}");
                if (_settings.Current.VerboseErrors)
                    await _dialogs.ShowErrorAsync("Verbose error", ex.ToString());

                return;
            }

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
                        isClip: false));
                }
            }

            var hasNext = false;
            string? newCursor = null;
            if (res.data?.user?.videos?.pageInfo?.hasNextPage == true)
                newCursor = edges?.FirstOrDefault()?.cursor;

            if (newCursor is not null)
            {
                hasNext = true;
                if (!_cursors.Contains(newCursor))
                    _cursors.Add(newCursor);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ClearResults();
                AddResults(created);
                _hasNextPage = hasNext;
            });
        }

        private async Task LoadClipsAsync(string cursor, int pageSize)
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
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync("Unable to get channel clips", $"Unable to get channel clips: {ex.Message}");
                if (_settings.Current.VerboseErrors)
                    await _dialogs.ShowErrorAsync("Verbose error", ex.ToString());

                return;
            }

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
                        isClip: true));
                }
            }

            var hasNext = false;
            string? newCursor = null;
            if (res.data?.user?.clips?.pageInfo?.hasNextPage == true)
                newCursor = edges?.FirstOrDefault(edge => edge.cursor != null)?.cursor;

            if (newCursor is not null)
            {
                hasNext = true;
                if (!_cursors.Contains(newCursor))
                    _cursors.Add(newCursor);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ClearResults();
                AddResults(created);
                _hasNextPage = hasNext;
            });
        }

        private SearchResultItem CreateItem(
            string id,
            string title,
            DateTime createdAt,
            int length,
            int views,
            string? game,
            string? thumbnailUrl,
            bool isClip)
        {
            var item = new SearchResultItem
            {
                Id = id,
                Title = title,
                Time = _settings.Current.UtcVideoTime ? createdAt : createdAt.ToLocalTime(),
                Length = length,
                Views = views,
                Game = game ?? "Unknown Game",
                ThumbnailUrl = thumbnailUrl ?? string.Empty,
                IsClip = isClip,
                CopyId = CopyIdAsync,
                CopyUrl = CopyUrlAsync,
                OpenInBrowser = OpenInBrowserAsync,
                OpenInApp = _openItem,
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
            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
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
            var bytes = await _thumbnails.TryGetAsync(item.ThumbnailUrl, token);
            if (bytes is null)
                bytes = await _thumbnails.TryGetAsync(null, token);

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
                await _dialogs.ShowErrorAsync("Failed to copy to clipboard", ex.ToString());
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
                await _dialogs.ShowErrorAsync("Failed to copy to clipboard", ex.ToString());
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
                ChannelSuggestions.Add(CLEAR_RECENT_CHANNELS_LABEL);

            SelectedSuggestion = null;
            ChannelQuery = text;
            _suppressSearch = suppress;
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
                item.PropertyChanged -= OnItemPropertyChanged;

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
            OpenSelectedCommand.NotifyCanExecuteChanged();
        }

        private void NotifyPaging()
        {
            NextPageCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
        }
    }
}
