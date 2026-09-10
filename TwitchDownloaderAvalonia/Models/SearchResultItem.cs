namespace TwitchDownloaderAvalonia.Models
{
    public sealed partial class SearchResultItem : ObservableObject
    {
        public SearchResultItem()
        {
            LocalizationService.Current.CultureChanged += OnCultureChanged;
        }

        public required string Id { get; init; }
        public required string Title { get; init; }
        public required DateTime Time { get; init; }
        public required int Length { get; init; }
        public required int Views { get; init; }
        public required string Game { get; init; }
        public required string ThumbnailUrl { get; init; }
        public required bool IsClip { get; init; }
        public required string StreamerName { get; init; }
        public required string StreamerId { get; init; }
        public string ClipperName { get; init; } = string.Empty;
        public string ClipperId { get; init; } = string.Empty;
        public required Func<SearchResultItem, Task> CopyId { get; init; }
        public required Func<SearchResultItem, Task> CopyUrl { get; init; }
        public required Func<SearchResultItem, Task> OpenInBrowser { get; init; }
        public required Func<SearchResultItem, Task> OpenInApp { get; init; }
        public required Func<SearchResultItem, Task> EnqueueOne { get; init; }

        public string Url => IsClip
            ? $"https://clips.twitch.tv/{Id}"
            : $"https://twitch.tv/videos/{Id}";

        public string HeaderText => $"[{Time:d}] - {Title}";

        public string LengthFormatted
        {
            get
            {
                var time = TimeSpan.FromSeconds(Length);
                if ((int)time.TotalHours > 0)
                    return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";

                if ((int)time.TotalMinutes > 0)
                    return $"{time.Minutes:D2}:{time.Seconds:D2}";

                return $"{time.Seconds:D1}s";
            }
        }

        public string DurationText => Loc.Get("search.duration", LengthFormatted);
        public string ViewsText => Loc.Get("search.views", Views);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasThumbnail))]
        public partial byte[]? ThumbnailBytes { get; set; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        public bool HasThumbnail => ThumbnailBytes is { Length: > 0 };

        [RelayCommand]
        private void ToggleSelect() => IsSelected = !IsSelected;

        [RelayCommand]
        private Task CopyIdToClipboard() => CopyId(this);

        [RelayCommand]
        private Task CopyUrlToClipboard() => CopyUrl(this);

        [RelayCommand]
        private Task OpenBrowser() => OpenInBrowser(this);

        [RelayCommand]
        private Task Open() => OpenInApp(this);

        [RelayCommand]
        private Task AddToQueue() => EnqueueOne(this);

        public void Detach()
        {
            LocalizationService.Current.CultureChanged -= OnCultureChanged;
        }

        private void OnCultureChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(ViewsText));
            OnPropertyChanged(nameof(HeaderText));
        }
    }
}
