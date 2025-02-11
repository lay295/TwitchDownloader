using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderWPF.Services;

namespace TwitchDownloaderWPF
{
    public partial class WindowOldVideoCacheManager : Window
    {
        public ObservableCollection<GridItem> GridItems { get; } = new();
        private long _totalSize;

        public WindowOldVideoCacheManager(IEnumerable<DirectoryInfo> directories)
        {
            foreach (var directoryInfo in directories)
            {
                GridItems.Add(new GridItem(directoryInfo));
            }

            Task.Run(() =>
            {
                // Do this on a separate thread because there may be a lot of dirs/files to check
                foreach (var gridItem in GridItems)
                {
                    _totalSize += gridItem.CalculateSize();
                }
            });

            InitializeComponent();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            App.RequestTitleBarChange();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Do this the dumb way because bindings are annoying without view models
            var sizeString = VideoSizeEstimator.StringifyByteCount(_totalSize);
            TextTotalSize.Text = string.IsNullOrEmpty(sizeString) ? "0B" : sizeString;
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

        private void MenuItemOpenFolder_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: GridItem gridItem })
            {
                return;
            }

            if (!Directory.Exists(gridItem.Path))
            {
                return;
            }

            Process.Start(new ProcessStartInfo(gridItem.Path) { UseShellExecute = true });
        }

        private void MenuItemCopyPath_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: GridItem gridItem })
            {
                return;
            }

            if (!ClipboardService.TrySetText(gridItem.Path, out var exception))
            {
                MessageBox.Show(this, exception.ToString(), Translations.Strings.FailedToCopyToClipboard, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public DirectoryInfo[] GetItemsToDelete() => GridItems
            .Where(x => x.ShouldDelete)
            .Select(x => x.Directory)
            .ToArray();

        public sealed class GridItem : INotifyPropertyChanged
        {
            public GridItem(DirectoryInfo directoryInfo)
            {
                Directory = directoryInfo;
                ShouldDelete = false;
                Age = (DateTime.UtcNow - directoryInfo.CreationTimeUtc).Days;
            }

            public readonly DirectoryInfo Directory;

            private bool _shouldDelete;

            public bool ShouldDelete
            {
                get => _shouldDelete;
                set => SetField(ref _shouldDelete, value);
            }

            public int Age { get; }

            public string Path => Directory.FullName;

            private string _size = "";

            public string Size
            {
                get => _size;
                private set => SetField(ref _size, value);
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }

            public long CalculateSize()
            {
                var sizeBytes = Directory.EnumerateFiles().Sum(file => file.Length);
                var sizeString = VideoSizeEstimator.StringifyByteCount(sizeBytes);
                Size = string.IsNullOrEmpty(sizeString) ? "0B" : sizeString;

                if (sizeBytes == 0)
                {
                    ShouldDelete = true;
                }

                return sizeBytes;
            }
        }
    }
}