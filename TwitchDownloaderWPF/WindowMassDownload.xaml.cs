using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TwitchDownloader.TwitchTasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF;
using static TwitchDownloaderWPF.App;

namespace TwitchDownloader
{
    /// <summary>
    /// Interaction logic for WindowMassDownload.xaml
    /// </summary>
    public partial class WindowMassDownload : Window
    {
        public DownloadType type { get; set; }
        public ObservableCollection<TaskData> videoList { get; set; } = new ObservableCollection<TaskData>();
        public List<TaskData> selectedItems = new List<TaskData>();
        public List<string> cursorList = new List<string>();
        public int cursorIndex = -1;
        public string currnetChannel = "";
        public string period = "";

        public WindowMassDownload(DownloadType Type)
        {
            type = Type;
            InitializeComponent();
            itemList.ItemsSource = videoList;
            if (type == DownloadType.Video)
            {
                comboSort.Visibility = Visibility.Hidden;
                labelSort.Visibility = Visibility.Hidden;
            }
            btnNext.IsEnabled = false;
            btnPrev.IsEnabled = false;
        }

        private async void btnChannel_Click(object sender, RoutedEventArgs e)
        {
            currnetChannel = textChannel.Text;
            videoList.Clear();
            cursorList.Clear();
            cursorIndex = -1;
            await UpdateList();
        }

        private async Task UpdateList()
        {
            if (type == DownloadType.Video)
            {
                string currentCursor = "";
                if (cursorList.Count > 0 && cursorIndex >= 0)
                {
                    currentCursor = cursorList[cursorIndex];
                }
                GqlVideoSearchResponse res = await TwitchHelper.GetGqlVideos(currnetChannel, currentCursor, 100);
                videoList.Clear();
                if (res.data.user != null)
                {
                    foreach (var video in res.data.user.videos.edges)
                    {
                        TaskData data = new TaskData();
                        data.Title = video.node.title;
                        data.Length = video.node.lengthSeconds;
                        data.Id = video.node.id;
                        data.Time = video.node.createdAt;
                        data.Views = video.node.viewCount;
                        data.Streamer = currnetChannel;
                        try
                        {
                            var bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.UriSource = new Uri(video.node.previewThumbnailURL);
                            bitmapImage.EndInit();
                            data.Thumbnail = bitmapImage;
                        }
                        catch { }
                        videoList.Add(data);
                    }

                    if (res.data.user.videos.pageInfo.hasNextPage)
                        btnNext.IsEnabled = true;
                    else
                        btnNext.IsEnabled = false;
                    if (res.data.user.videos.pageInfo.hasPreviousPage)
                        btnPrev.IsEnabled = true;
                    else
                        btnPrev.IsEnabled = false;
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
                GqlClipSearchResponse res = await TwitchHelper.GetGqlClips(currnetChannel, period, currentCursor, 50);
                videoList.Clear();
                if (res.data.user != null)
                {
                    foreach (var clip in res.data.user.clips.edges)
                    {
                        TaskData data = new TaskData();
                        data.Title = clip.node.title;
                        data.Length = clip.node.durationSeconds;
                        data.Id = clip.node.slug;
                        data.Time = clip.node.createdAt;
                        data.Views = clip.node.viewCount;
                        data.Streamer = currnetChannel;
                        try
                        {
                            var bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.UriSource = new Uri(clip.node.thumbnailURL);
                            bitmapImage.EndInit();
                            data.Thumbnail = bitmapImage;
                        }
                        catch { }
                        videoList.Add(data);
                    }

                    if (res.data.user.clips.pageInfo.hasNextPage)
                        btnNext.IsEnabled = true;
                    else
                        btnNext.IsEnabled = false;
                    if (cursorIndex >= 0)
                        btnPrev.IsEnabled = true;
                    else
                        btnPrev.IsEnabled = false;
                    if (res.data.user.clips.pageInfo.hasNextPage)
                    {
                        string newCursor = res.data.user.clips.edges.Where(x => x.cursor != null).First().cursor;
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
            textCount.Content = "Selected Items: " + selectedItems.Count;
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
            //I'm sure there is a much better way to do this. Could not find a way to itterate over each itemcontrol border
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
            textCount.Content = "Selected Items: " + selectedItems.Count;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
			AppSingleton.RequestTitleBarChange();
		}
    }
}
