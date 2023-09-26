using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public List<TaskData> selectedItems = new List<TaskData>();
        public List<string> cursorList = new List<string>();
        public int cursorIndex = -1;
        public string currentChannel = "";
        public string period = "";

        public WindowMassDownload(DownloadType Type)
        {
            downloaderType = Type;
            InitializeComponent();
            itemList.ItemsSource = videoList;
            if (downloaderType == DownloadType.Video)
            {
                comboSort.Visibility = Visibility.Hidden;
                labelSort.Visibility = Visibility.Hidden;
            }
            btnNext.IsEnabled = false;
            btnPrev.IsEnabled = false;
        }

        private async void btnChannel_Click(object sender, RoutedEventArgs e)
        {
            await ChangeCurrentChannel();
        }

        private async Task ChangeCurrentChannel()
        {
            currentChannel = textChannel.Text;
            videoList.Clear();
            cursorList.Clear();
            cursorIndex = -1;
            await UpdateList();
        }

        private async Task UpdateList()
        {
            if (downloaderType == DownloadType.Video)
            {
                string currentCursor = "";
                if (cursorList.Count > 0 && cursorIndex >= 0)
                {
                    currentCursor = cursorList[cursorIndex];
                }
                GqlVideoSearchResponse res = await TwitchHelper.GetGqlVideos(currentChannel, currentCursor, 100);
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
                            Game = video.node.game?.displayName ?? "Unknown",
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
                GqlClipSearchResponse res = await TwitchHelper.GetGqlClips(currentChannel, period, currentCursor, 50);
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
                            Game = clip.node.game?.displayName ?? "Unknown",
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
        }

        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Border border = sender as Border;
            if (selectedItems.Any(x => x.Id == ((TaskData)border.DataContext).Id))
            {
                border.Background = Brushes.Transparent;
                selectedItems.RemoveAll(x => x.Id == ((TaskData)border.DataContext).Id);
            }
            else
            {
                border.Background = Brushes.LightBlue;
                selectedItems.Add((TaskData)border.DataContext);
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
            Border border = (Border)sender;
            if (border.DataContext != null)
            {
                if (selectedItems.Any(x => x.Id == ((TaskData)border.DataContext).Id))
                {
                    border.Background = Brushes.LightBlue;
                }
            }
        }

        private void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            if (selectedItems.Count > 0)
            {
                WindowQueueOptions queue = new WindowQueueOptions(selectedItems);
                bool? queued = queue.ShowDialog();
                if (queued != null && (bool)queued)
                    this.Close();
            }
        }

        private async void comboSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            period = ((ComboBoxItem)comboSort.SelectedItem).Tag.ToString();
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
                if (!selectedItems.Any(x => x.Id == video.Id))
                {
                    selectedItems.Add(video);
                }
            }

            List<TaskData> oldData = videoList.ToList();
            videoList.Clear();
            foreach (var item in oldData)
            {
                videoList.Add(item);
            }
            textCount.Text = selectedItems.Count.ToString();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title = downloaderType == DownloadType.Video
                ? Translations.Strings.TitleVideoMassDownloader
                : Translations.Strings.TitleClipMassDownloader;
            App.RequestTitleBarChange();
		}

        private async void TextChannel_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await ChangeCurrentChannel();
                e.Handled = true;
            }
        }
    }
}
