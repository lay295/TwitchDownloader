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

namespace TwitchDownloaderWPF;

public partial class WindowOldVideoCacheManager : Window {
    private long _totalSize;

    public WindowOldVideoCacheManager(IEnumerable<DirectoryInfo> directories) {
        foreach (var directoryInfo in directories)
            this.GridItems.Add(new(directoryInfo));

        Task.Run(
            () => {
                // Do this on a separate thread because there may be a lot of dirs/files to check
                foreach (var gridItem in this.GridItems)
                    this._totalSize += gridItem.CalculateSize();
            }
        );

        this.InitializeComponent();
    }

    public ObservableCollection<GridItem> GridItems { get; } = new();

    private void OnSourceInitialized(object sender, EventArgs e) { App.RequestTitleBarChange(); }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        // Do this the dumb way because bindings are annoying without view models
        var sizeString = VideoSizeEstimator.StringifyByteCount(this._totalSize);
        this.TextTotalSize.Text = string.IsNullOrEmpty(sizeString) ? "0B" : sizeString;
        this.DataGrid.ItemsSource = this.GridItems;
    }

    private void OnClosing(object sender, CancelEventArgs e) {
        if (!this.BtnAccept.IsEnabled)
            return;

        // Make sure no items are deleted if the user closes the window instead of clicking accept
        foreach (var gridItem in this.GridItems)
            gridItem.ShouldDelete = false;
    }

    private void BtnAccept_OnClick(object sender, RoutedEventArgs e) {
        this.BtnAccept.IsEnabled = false;
        this.Close();
    }

    private void BtnSelectAll_OnClick(object sender, RoutedEventArgs e) {
        foreach (var gridItem in this.GridItems)
            gridItem.ShouldDelete = true;
    }

    private void MenuItemOpenFolder_OnClick(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem { DataContext: GridItem gridItem })
            return;

        if (!Directory.Exists(gridItem.Path))
            return;

        Process.Start(new ProcessStartInfo(gridItem.Path) { UseShellExecute = true });
    }

    private void MenuItemCopyPath_OnClick(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem { DataContext: GridItem gridItem })
            return;

        Clipboard.SetText(gridItem.Path);
    }

    public DirectoryInfo[] GetItemsToDelete() => this
        .GridItems
        .Where(x => x.ShouldDelete)
        .Select(x => x.Directory)
        .ToArray();

    public sealed class GridItem : INotifyPropertyChanged {

        public readonly DirectoryInfo Directory;

        private bool _shouldDelete;

        private string _size = "";

        public GridItem(DirectoryInfo directoryInfo) {
            this.Directory = directoryInfo;
            this.ShouldDelete = false;
            this.Age = (DateTime.UtcNow - directoryInfo.CreationTimeUtc).Days;
        }

        public bool ShouldDelete {
            get => this._shouldDelete;
            set => this.SetField(ref this._shouldDelete, value);
        }

        public int Age { get; }

        public string Path => this.Directory.FullName;

        public string Size {
            get => this._size;
            private set => this.SetField(ref this._size, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;

            field = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }

        public long CalculateSize() {
            var sizeBytes = this.Directory.EnumerateFiles().Sum(file => file.Length);
            var sizeString = VideoSizeEstimator.StringifyByteCount(sizeBytes);
            this.Size = string.IsNullOrEmpty(sizeString) ? "0B" : sizeString;

            if (sizeBytes == 0)
                this.ShouldDelete = true;

            return sizeBytes;
        }
    }
}
