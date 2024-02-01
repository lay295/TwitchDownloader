using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderWPF;

public partial class WindowOldVideoCacheManager : Window
{
    public ObservableCollection<GridItem> GridItems { get; } = new();

    public WindowOldVideoCacheManager(DirectoryInfo[] directories)
    {
        foreach (var directoryInfo in directories)
        {
            GridItems.Add(new GridItem(directoryInfo));
        }

        Task.Run(() =>
        {
            // Do this on a separate thread because there may be a lot of IO to do
            var totalSize = 0L;
            foreach (var gridItem in GridItems)
            {
                totalSize += gridItem.CalculateSize();
            }

            if (TextTotalSize != null)
            {
                // Do this the dumb way because bindings are annoying without view models
                var sizeString = VideoSizeEstimator.StringifyByteCount(totalSize);
                Dispatcher.Invoke(() => TextTotalSize.Text = string.IsNullOrEmpty(sizeString) ? "0B" : sizeString);
            }
        });

        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Title = Translations.Strings.TitleWindowOldVideoCacheManager;
        App.RequestTitleBarChange();

        // For some stupid reason, this does not work unless I manually set it, even though its a binding
        DataGrid.ItemsSource = GridItems;
    }

    private void OnClosing(object sender, CancelEventArgs e)
    {
        if (!BtnAccept.IsEnabled)
            return;

        // Make sure no items are deleted if the user closes the window instead of clicking accept
        foreach (var gridItem in GridItems)
        {
            gridItem.ShouldDelete = false;
        }
    }

    private void BtnAccept_OnClick(object sender, RoutedEventArgs e)
    {
        BtnAccept.IsEnabled = false;
        Close();
    }

    private void BtnSelectAll_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var gridItem in GridItems)
        {
            gridItem.ShouldDelete = true;
        }
    }

    public DirectoryInfo[] GetItemsToDelete() => GridItems
        .Where(x => x.ShouldDelete)
        .Select(x => x.Directory)
        .ToArray();

    public sealed class GridItem
    {
        public GridItem(DirectoryInfo directoryInfo)
        {
            Directory = directoryInfo;
            ShouldDelete = false;
            Age = string.Format(Translations.Strings.FileAgeInDays, (DateTime.UtcNow - Directory.CreationTimeUtc).Days);
        }

        public long CalculateSize()
        {
            var sizeBytes = Directory.EnumerateFiles().Sum(file => file.Length);
            var sizeString = VideoSizeEstimator.StringifyByteCount(sizeBytes);
            Size = string.IsNullOrEmpty(sizeString) ? "0B" : sizeString;

            return sizeBytes;
        }

        public readonly DirectoryInfo Directory;
        public bool ShouldDelete { get; set; }
        public string Age { get; }
        public string Path => Directory.FullName;
        public string Size { get; private set; } = "";
    }
}