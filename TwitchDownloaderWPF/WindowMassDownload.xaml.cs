using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TwitchDownloaderCore;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Translations;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF;

/// <summary>
///     Interaction logic for WindowMassDownload.xaml
/// </summary>
public partial class WindowMassDownload : Window {
    public readonly List<string> cursorList = new();
    public readonly List<TaskData> selectedItems = new();
    public string currentChannel = "";
    public int cursorIndex = -1;
    public string period = "";
    public int videoCount = 50;

    public WindowMassDownload(DownloadType type) {
        this.downloaderType = type;
        this.InitializeComponent();
        this.itemList.ItemsSource = this.videoList;
        if (this.downloaderType == DownloadType.Video) {
            this.ComboSortByDate.Visibility = Visibility.Hidden;
            this.LabelSort.Visibility = Visibility.Hidden;
        }

        this.btnNext.IsEnabled = false;
        this.btnPrev.IsEnabled = false;
    }

    public DownloadType downloaderType { get; set; }
    public ObservableCollection<TaskData> videoList { get; set; } = new();

    private async void btnChannel_Click(object sender, RoutedEventArgs e) { await this.ChangeCurrentChannel(); }

    private Task ChangeCurrentChannel() {
        this.currentChannel = this.textChannel.Text;
        this.videoList.Clear();
        this.cursorList.Clear();
        this.cursorIndex = -1;
        return this.UpdateList();
    }

    private async Task UpdateList() {
        if (!this.IsInitialized)
            return;

        this.StatusImage.Visibility = Visibility.Visible;

        if (string.IsNullOrWhiteSpace(this.currentChannel)) {
            // Pretend we are doing something so the status icon has time to show
            await Task.Delay(50);
            this.videoList.Clear();
            this.cursorList.Clear();
            this.cursorIndex = -1;
            this.StatusImage.Visibility = Visibility.Hidden;
            return;
        }

        if (this.downloaderType == DownloadType.Video) {
            var currentCursor = "";
            if (this.cursorList.Count > 0 && this.cursorIndex >= 0)
                currentCursor = this.cursorList[this.cursorIndex];
            var res = await TwitchHelper.GetGqlVideos(this.currentChannel, currentCursor, this.videoCount);
            this.videoList.Clear();
            if (res.data.user != null) {
                foreach (var video in res.data.user.videos.edges) {
                    var thumbUrl = video.node.previewThumbnailURL;
                    if (!ThumbnailService.TryGetThumb(thumbUrl, out var thumbnail))
                        _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out thumbnail);

                    this.videoList.Add(
                        new() {
                            Title = video.node.title,
                            Length = video.node.lengthSeconds,
                            Id = video.node.id,
                            Time = Settings.Default.UTCVideoTime
                                ? video.node.createdAt
                                : video.node.createdAt.ToLocalTime(),
                            Views = video.node.viewCount,
                            Streamer = this.currentChannel,
                            Game = video.node.game?.displayName ?? Strings.UnknownGame,
                            Thumbnail = thumbnail
                        }
                    );
                }

                this.btnNext.IsEnabled = res.data.user.videos.pageInfo.hasNextPage;
                this.btnPrev.IsEnabled = res.data.user.videos.pageInfo.hasPreviousPage;
                if (res.data.user.videos.pageInfo.hasNextPage) {
                    var newCursor = res.data.user.videos.edges[0].cursor;
                    if (!this.cursorList.Contains(newCursor))
                        this.cursorList.Add(newCursor);
                }
            }
        } else {
            var currentCursor = "";
            if (this.cursorList.Count > 0 && this.cursorIndex >= 0)
                currentCursor = this.cursorList[this.cursorIndex];
            var res = await TwitchHelper.GetGqlClips(this.currentChannel, this.period, currentCursor, this.videoCount);
            this.videoList.Clear();
            if (res.data.user != null) {
                foreach (var clip in res.data.user.clips.edges) {
                    var thumbUrl = clip.node.thumbnailURL;
                    if (!ThumbnailService.TryGetThumb(thumbUrl, out var thumbnail))
                        _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out thumbnail);

                    this.videoList.Add(
                        new() {
                            Title = clip.node.title,
                            Length = clip.node.durationSeconds,
                            Id = clip.node.slug,
                            Time = Settings.Default.UTCVideoTime
                                ? clip.node.createdAt
                                : clip.node.createdAt.ToLocalTime(),
                            Views = clip.node.viewCount,
                            Streamer = this.currentChannel,
                            Game = clip.node.game?.displayName ?? Strings.UnknownGame,
                            Thumbnail = thumbnail
                        }
                    );
                }

                this.btnNext.IsEnabled = res.data.user.clips.pageInfo.hasNextPage;
                this.btnPrev.IsEnabled = this.cursorIndex >= 0;
                if (res.data.user.clips.pageInfo.hasNextPage) {
                    var newCursor = res.data.user.clips.edges.First(x => x.cursor != null).cursor;
                    if (!this.cursorList.Contains(newCursor))
                        this.cursorList.Add(newCursor);
                }
            }
        }

        this.StatusImage.Visibility = Visibility.Hidden;
    }

    private void Border_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (sender is not Border border) return;
        if (border.DataContext is not TaskData taskData) return;

        if (this.selectedItems.Any(x => x.Id == taskData.Id)) {
            border.Background = Brushes.Transparent;
            this.selectedItems.RemoveAll(x => x.Id == taskData.Id);
        } else {
            border.Background = Brushes.LightBlue;
            this.selectedItems.Add(taskData);
        }

        this.textCount.Text = this.selectedItems.Count.ToString();
    }

    private async void btnNext_Click(object sender, RoutedEventArgs e) {
        this.btnNext.IsEnabled = false;
        this.btnPrev.IsEnabled = false;
        this.cursorIndex++;
        await this.UpdateList();
    }

    private async void btnPrev_Click(object sender, RoutedEventArgs e) {
        this.btnNext.IsEnabled = false;
        this.btnPrev.IsEnabled = false;
        this.cursorIndex--;
        await this.UpdateList();
    }

    private void Border_Initialized(object sender, EventArgs e) {
        if (sender is not Border border) return;
        if (border.DataContext is not TaskData taskData) return;

        if (this.selectedItems.Any(x => x.Id == taskData.Id))
            border.Background = Brushes.LightBlue;
    }

    private void btnQueue_Click(object sender, RoutedEventArgs e) {
        if (this.selectedItems.Count > 0) {
            var queue = new WindowQueueOptions(this.selectedItems) {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (queue.ShowDialog().GetValueOrDefault())
                this.Close();
        }
    }

    private async void ComboSortByDate_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        this.period = ((ComboBoxItem)this.ComboSortByDate.SelectedItem).Tag.ToString();
        this.videoList.Clear();
        this.cursorList.Clear();
        this.cursorIndex = -1;
        await this.UpdateList();
    }

    private void btnSelectAll_Click(object sender, RoutedEventArgs e) {
        //I'm sure there is a much better way to do this. Could not find a way to iterate over each itemcontrol border
        foreach (var video in this.videoList)
            if (this.selectedItems.All(x => x.Id != video.Id))
                this.selectedItems.Add(video);

        // Remove and re-add all of the items to trigger Border_Initialized
        var oldData = this.videoList.ToArray();
        this.videoList.Clear();
        foreach (var item in oldData)
            this.videoList.Add(item);

        this.textCount.Text = this.selectedItems.Count.ToString();
    }

    private void Window_OnSourceInitialized(object sender, EventArgs e) { App.RequestTitleBarChange(); }

    private void Window_Loaded(object sender, RoutedEventArgs e) {
        this.Title = this.downloaderType == DownloadType.Video
            ? Strings.TitleVideoMassDownloader
            : Strings.TitleClipMassDownloader;
    }

    private async void TextChannel_OnKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.Enter) {
            await this.ChangeCurrentChannel();
            e.Handled = true;
        }
    }

    private async void ComboVideoCount_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        this.videoCount = int.Parse((string)((ComboBoxItem)this.ComboVideoCount.SelectedValue).Content);
        this.videoList.Clear();
        this.cursorList.Clear();
        this.cursorIndex = -1;
        await this.UpdateList();
    }

    private void MenuItemCopyVideoID_OnClick(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem { DataContext: TaskData taskData }) return;

        var id = taskData.Id;
        Clipboard.SetText(id);

        e.Handled = true;
    }

    private void MenuItemCopyVideoUrl_OnClick(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem { DataContext: TaskData taskData }) return;

        var id = taskData.Id;
        var url = id.All(char.IsDigit)
            ? $"https://twitch.tv/videos/{id}"
            : $"https://clips.twitch.tv/{id}";

        Clipboard.SetText(url);

        e.Handled = true;
    }

    private void MenuItemOpenInBrowser_OnClick(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem { DataContext: TaskData taskData }) return;

        var id = taskData.Id;
        var url = id.All(char.IsDigit)
            ? $"https://twitch.tv/videos/{id}"
            : $"https://clips.twitch.tv/{id}";

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        e.Handled = true;
    }
}
