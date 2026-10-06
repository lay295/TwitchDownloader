using Microsoft.Extensions.Logging;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class AbandonedVideoCacheItem(
        LocalizationService loc,
        DirectoryInfo directory,
        ILogger logger,
        Func<AbandonedVideoCacheItem, Task>? copyPath = null)
        : ObservableObject
    {
        private readonly Func<AbandonedVideoCacheItem, Task> _copyPath = copyPath ?? (_ => Task.CompletedTask);

        public LocalizationService Loc { get; } = loc;

        public DirectoryInfo Directory { get; } = directory;

        public string Path => Directory.FullName;

        public string FolderName => Directory.Name;

        public int Age { get; } = (DateTime.UtcNow - directory.CreationTimeUtc).Days;

        public string AgeText => Loc.Get("cache.age_days", Age);

        [ObservableProperty]
        public partial bool ShouldDelete { get; set; }

        [ObservableProperty]
        public partial long SizeBytes { get; set; }

        [ObservableProperty]
        public partial string SizeText { get; set; } = string.Empty;

        public long CalculateSize()
        {
            var sizeBytes = Directory.EnumerateFiles().Sum(file => file.Length);
            SizeBytes = sizeBytes;
            SizeText = FormatSize(sizeBytes);
            if (sizeBytes == 0)
                ShouldDelete = true;

            return sizeBytes;
        }

        [RelayCommand]
        private void OpenFolder()
        {
            if (!System.IO.Directory.Exists(Path))
                return;

            try
            {
                Process.Start(new ProcessStartInfo(Path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to open cache folder {Path}", Path);
            }
        }

        [RelayCommand]
        private Task CopyPathAsync() => _copyPath(this);

        public void RefreshCulture()
        {
            OnPropertyChanged(nameof(Loc));
            OnPropertyChanged(nameof(AgeText));
        }

        internal static string FormatSize(long sizeBytes)
        {
            var sizeString = VideoSizeEstimator.StringifyByteCount(sizeBytes);
            return string.IsNullOrEmpty(sizeString) ? "0B" : sizeString;
        }
    }

    public sealed partial class AbandonedVideoCacheViewModel : ViewModelBase
    {
        private readonly Action<DirectoryInfo[]> _close;
        private readonly IDialogService _dialogs;
        private readonly ILogger _logger;

        public AbandonedVideoCacheViewModel(
            LocalizationService loc,
            IEnumerable<DirectoryInfo> directories,
            Action<DirectoryInfo[]> close,
            IDialogService dialogs,
            ILogger logger) : base(loc)
        {
            _close = close;
            _dialogs = dialogs;
            _logger = logger;
            Items =
            [
                .. directories.Select(directory => new AbandonedVideoCacheItem(Loc, directory, logger, CopyPathCore)),
            ];
        }

        public ObservableCollection<AbandonedVideoCacheItem> Items { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalSizeText))]
        public partial long TotalSizeBytes { get; set; }

        public string TotalSizeText
        {
            get
            {
                var size = AbandonedVideoCacheItem.FormatSize(TotalSizeBytes);
                return $"{Loc.Get("cache.total_size")} {size}";
            }
        }

        public DirectoryInfo[] GetItemsToDelete() =>
        [
            .. Items
                .Where(item => item.ShouldDelete)
                .Select(item => item.Directory),
        ];

        public DirectoryInfo[] Complete(bool accepted) => accepted ? GetItemsToDelete() : [];

        public void CalculateSizes()
        {
            long total = 0;
            foreach (var item in Items)
            {
                try
                {
                    total += item.CalculateSize();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to measure cache size for {Path}", item.Path);
                    item.SizeText = AbandonedVideoCacheItem.FormatSize(0);
                }

                TotalSizeBytes = total;
            }
        }

        public void StartSizeCalculation() => _ = Task.Run(CalculateSizes);

        protected override void OnCultureChanged(object? sender, EventArgs e)
        {
            Notify(nameof(TotalSizeText));
            foreach (var item in Items)
                item.RefreshCulture();
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in Items)
                item.ShouldDelete = true;
        }

        [RelayCommand]
        private void Accept() => _close(Complete(true));

        [RelayCommand]
        private void OpenFolder(AbandonedVideoCacheItem? item) => item?.OpenFolderCommand.Execute(null);

        [RelayCommand]
        private Task CopyPathAsync(AbandonedVideoCacheItem? item) => item is null ? Task.CompletedTask : CopyPathCore(item);

        private async Task CopyPathCore(AbandonedVideoCacheItem item)
        {
            try
            {
                await _dialogs.CopyTextAsync(item.Path);
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync(Loc.Get("search.copy_failed"), ex.ToString());
            }
        }
    }
}
