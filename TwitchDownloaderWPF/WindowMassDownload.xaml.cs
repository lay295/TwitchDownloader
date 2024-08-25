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
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for WindowMassDownload.xaml
    /// </summary>
    public partial class WindowMassDownload : Window
    {
        public DownloadType downloaderType { get; set; }
        public ObservableCollection<TaskData> videoList { get; set; } = new ObservableCollection<TaskData>();
        public readonly List<TaskData> selectedItems = new List<TaskData>();
        public readonly List<string> cursorList = new List<string>();
        public int cursorIndex = -1;
        public string currentChannel = "";
        public string period = "";
        public int videoCount = 50;

        public WindowMassDownload(DownloadType type)
        {
            downloaderType = type;
            InitializeComponent();
            itemList.ItemsSource = videoList;
            if (downloaderType == DownloadType.Video)
            {
                ComboSortByDate.Visibility = Visibility.Hidden;
                LabelSort.Visibility = Visibility.Hidden;
            }
            btnNext.IsEnabled = false;
            btnPrev.IsEnabled = false;
        }

        private async void btnChannel_Click(object sender, RoutedEventArgs e)
        {
            await ChangeCurrentChannel();
        }

        private Task ChangeCurrentChannel()
        {
            currentChannel = textChannel.Text;
            videoList.Clear();
            cursorList.Clear();
            cursorIndex = -1;
            return UpdateList();
        }

        private async Task UpdateList()
        {
            if (!IsInitialized)
                return;

            StatusImage.Visibility = Visibility.Visible;

            if (string.IsNullOrWhiteSpace(currentChannel))
            {
                // Pretend we are doing something so the status icon has time to show
                await Task.Delay(50);
                videoList.Clear();
                cursorList.Clear();
                cursorIndex = -1;
                StatusImage.Visibility = Visibility.Hidden;
                return;
            }

            if (downloaderType == DownloadType.Video)
            {
                string currentCursor = "";
                if (cursorList.Count > 0 && cursorIndex >= 0)
                {
                    currentCursor = cursorList[cursorIndex];
                }

                GqlVideoSearchResponse res;
                try
                {
                    res = await TwitchHelper.GetGqlVideos(currentChannel, currentCursor, videoCount);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Translations.Strings.UnknownErrorOccurred, MessageBoxButton.OK, MessageBoxImage.Error);

                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(this, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    return;
                }

                videoList.Clear();
                if (res.data.user != null)
                {
                    foreach (var video in res.data.user.videos.edges)
                    {
                        var thumbUrl = video.node.previewThumbnailURL;
                        if (!ThumbnailService.TryGetThumb(thumbUrl, out var thumbnail))
                        {
                            _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out thumbnail);
                        }

                        videoList.Add(new TaskData
                        {
                            Title = video.node.title,
                            Length = video.node.lengthSeconds,
                            Id = video.node.id,
                            Time = Settings.Default.UTCVideoTime ? video.node.createdAt : video.node.createdAt.ToLocalTime(),
                            Views = video.node.viewCount,
                            Streamer = currentChannel,
                            Game = video.node.game?.displayName ?? Translations.Strings.UnknownGame,
                            Thumbnail = thumbnail
                        });
                    }

                    btnNext.IsEnabled = res.data.user.videos.pageInfo.hasNextPage;
                    btnPrev.IsEnabled = res.data.user.videos.pageInfo.hasPreviousPage;
                    if (res.data.user.videos.pageInfo.hasNextPage)
                    {
                        string newCursor = res.data.user.videos.edges[0].cursor;
                        if (!cursorList.Contains(newCursor))
                            cursorList.Add(newCursor);
                    }
                }
            }
            else
            {
                string currentCursor = "";
                if (cursorList.Count > 0 && cursorIndex >= 0)
                {
                    currentCursor = cursorList[cursorIndex];
                }

                GqlClipSearchResponse res;
                try
                {
                    res = await TwitchHelper.GetGqlClips(currentChannel, period, currentCursor, videoCount);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Translations.Strings.UnknownErrorOccurred, MessageBoxButton.OK, MessageBoxImage.Error);

                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(this, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    return;
                }

                videoList.Clear();
                if (res.data.user != null)
                {
                    foreach (var clip in res.data.user.clips.edges)
                    {
                        var thumbUrl = clip.node.thumbnailURL;
                        if (!ThumbnailService.TryGetThumb(thumbUrl, out var thumbnail))
                        {
                            _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out thumbnail);
                        }

                        videoList.Add(new TaskData
                        {
                            Title = clip.node.title,
                            Length = clip.node.durationSeconds,
                            Id = clip.node.slug,
                            Time = Settings.Default.UTCVideoTime ? clip.node.createdAt : clip.node.createdAt.ToLocalTime(),
                            Views = clip.node.viewCount,
                            Streamer = currentChannel,
                            Game = clip.node.game?.displayName ?? Translations.Strings.UnknownGame,
                            Thumbnail = thumbnail
                        });
                    }

                    btnNext.IsEnabled = res.data.user.clips.pageInfo.hasNextPage;
                    btnPrev.IsEnabled = cursorIndex >= 0;
                    if (res.data.user.clips.pageInfo.hasNextPage)
                    {
                        string newCursor = res.data.user.clips.edges.First(x => x.cursor != null).cursor;
                        if (!cursorList.Contains(newCursor))
                            cursorList.Add(newCursor);
                    }
                }
            }

            StatusImage.Visibility = Visibility.Hidden;
        }

        private void Border_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border) return;
            if (border.DataContext is not TaskData taskData) return;

            if (selectedItems.Any(x => x.Id == taskData.Id))
            {
                border.Background = Brushes.Transparent;
                selectedItems.RemoveAll(x => x.Id == taskData.Id);
            }
            else
            {
                border.Background = Brushes.LightBlue;
                selectedItems.Add(taskData);
            }
            textCount.Text = selectedItems.Count.ToString();
        }

        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            btnNext.IsEnabled = false;
            btnPrev.IsEnabled = false;
            cursorIndex++;
            await UpdateList();
        }

        private async void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            btnNext.IsEnabled = false;
            btnPrev.IsEnabled = false;
            cursorIndex--;
            await UpdateList();
        }

        private void Border_Initialized(object sender, EventArgs e)
        {
            if (sender is not Border border) return;
            if (border.DataContext is not TaskData taskData) return;

            if (selectedItems.Any(x => x.Id == taskData.Id))
            {
                border.Background = Brushes.LightBlue;
            }
        }

        private void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            if (selectedItems.Count > 0)
            {
                var queue = new WindowQueueOptions(selectedItems)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                if (queue.ShowDialog().GetValueOrDefault())
                    this.Close();
            }
        }

        private async void ComboSortByDate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            period = ((ComboBoxItem)ComboSortByDate.SelectedItem).Tag.ToString();
            videoList.Clear();
            cursorList.Clear();
            cursorIndex = -1;
            await UpdateList();
        }

        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            //I'm sure there is a much better way to do this. Could not find a way to iterate over each itemcontrol border
            foreach (var video in videoList)
            {
                if (selectedItems.All(x => x.Id != video.Id))
                {
                    selectedItems.Add(video);
                }
            }

            // Remove and re-add all of the items to trigger Border_Initialized
            var oldData = videoList.ToArray();
            videoList.Clear();
            foreach (var item in oldData)
            {
                videoList.Add(item);
            }

            textCount.Text = selectedItems.Count.ToString();
        }

        private void Window_OnSourceInitialized(object sender, EventArgs e)
        {
            App.RequestTitleBarChange();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title = downloaderType == DownloadType.Video
                ? Translations.Strings.TitleVideoMassDownloader
                : Translations.Strings.TitleClipMassDownloader;
        }

        private async void TextChannel_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await ChangeCurrentChannel();
                e.Handled = true;
            }
        }

        private async void ComboVideoCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            videoCount = int.Parse((string)((ComboBoxItem)ComboVideoCount.SelectedValue).Content);
            videoList.Clear();
            cursorList.Clear();
            cursorIndex = -1;
            await UpdateList();
        }

        private void MenuItemCopyVideoID_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TaskData taskData }) return;

            var id = taskData.Id;
            Clipboard.SetText(id);

            e.Handled = true;
        }

        private void MenuItemCopyVideoUrl_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TaskData taskData }) return;

            var id = taskData.Id;
            var url = id.All(char.IsDigit)
                ? $"https://twitch.tv/videos/{id}"
                : $"https://clips.twitch.tv/{id}";

            Clipboard.SetText(url);

            e.Handled = true;
        }

        private void MenuItemOpenInBrowser_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TaskData taskData }) return;

            var id = taskData.Id;
            var url = id.All(char.IsDigit)
                ? $"https://twitch.tv/videos/{id}"
                : $"https://clips.twitch.tv/{id}";

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            e.Handled = true;
        }
    }
}
