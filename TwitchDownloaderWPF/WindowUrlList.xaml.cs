using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using TwitchDownloader.TwitchTasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF;
using static TwitchDownloaderWPF.App;

namespace TwitchDownloader
{
    /// <summary>
    /// Interaction logic for WindowUrlList.xaml
    /// </summary>
    public partial class WindowUrlList : Window
    {
        public WindowUrlList()
        {
            InitializeComponent();
        }

        private async void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            btnQueue.IsEnabled = false;
            List<string> idList = new List<string>();
            List<string> urlList = new List<string>(textList.Text.Split('\n').Where(x => x.Trim() != ""));
            List<string> invalidList = new List<string>();
            List<string> errorList = new List<string>();
            List<TaskData> dataList = new List<TaskData>();
            Dictionary<string, string> idDict = new Dictionary<string, string>();

            foreach (var url in urlList)
            {
                string id = PageChatDownload.ValidateUrl(url);

                if (id == "")
                {
                    invalidList.Add(url);
                }
                else
                {
                    idList.Add(id);
                    idDict[id] = url;
                }
            }

            if (invalidList.Count > 0)
            {
                MessageBox.Show("Please double check the VOD/Clip link", "Unable to parse inputs\n" + String.Join("\n", invalidList.ToArray()), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Dictionary<int, string> taskDict = new Dictionary<int, string>();
            List<Task<GqlVideoResponse>> taskVideoList = new List<Task<GqlVideoResponse>>();
            List<Task<GqlClipResponse>> taskClipList = new List<Task<GqlClipResponse>>();

            foreach (var id in idList)
            {
                if (id.All(Char.IsDigit))
                {
                    Task<GqlVideoResponse> task = TwitchHelper.GetVideoInfo(int.Parse(id));
                    taskVideoList.Add(task);
                    taskDict[task.Id] = id;
                }
                else
                {
                    Task<GqlClipResponse> task = TwitchHelper.GetClipInfo(id);
                    taskClipList.Add(task);
                    taskDict[task.Id] = id;
                }
            }

            try
            {
                await Task.WhenAll(taskVideoList.ToArray());
            }
            catch { }
            try
            {
                await Task.WhenAll(taskClipList.ToArray());
            }
            catch { }

            for (int i = 0; i < taskVideoList.Count; i++)
            {
                if (taskVideoList[i].IsCompleted)
                {
                    string id = taskDict[taskVideoList[i].Id];
                    if (!taskVideoList[i].IsFaulted)
                    {
                        GqlVideoResponse data = taskVideoList[i].Result;
                        TaskData newData = new TaskData();
                        newData.Id = id;
                        try
                        {
                            string thumbUrl = data.data.video.thumbnailURLs.FirstOrDefault();
                            var bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.UriSource = new Uri(thumbUrl);
                            bitmapImage.EndInit();
                            newData.Thumbnail = bitmapImage;
                        }
                        catch { }
                        newData.Title = data.data.video.title;
                        newData.Streamer = data.data.video.owner.displayName;
                        newData.Time = data.data.video.createdAt.ToLocalTime();
                        dataList.Add(newData);
                    }
                    else
                    {
                        errorList.Add(idDict[id]);
                    }
                }
            }

            for (int i = 0; i < taskClipList.Count; i++)
            {
                if (taskClipList[i].IsCompleted)
                {
                    string id = taskDict[taskClipList[i].Id];
                    if (!taskClipList[i].IsFaulted)
                    {
                        GqlClipResponse data = taskClipList[i].Result;
                        TaskData newData = new TaskData();
                        newData.Id = id;
                        try
                        {
                            string thumbUrl = data.data.clip.thumbnailURL;
                            var bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.UriSource = new Uri(thumbUrl);
                            bitmapImage.EndInit();
                            newData.Thumbnail = bitmapImage;
                        }
                        catch { }
                        newData.Title = data.data.clip.title;
                        newData.Streamer = data.data.clip.broadcaster.displayName;
                        newData.Time = data.data.clip.createdAt.ToLocalTime();
                        dataList.Add(newData);
                    }
                    else
                    {
                        errorList.Add(idDict[id]);
                    }
                }
            }

            if (errorList.Count > 0)
            {
                MessageBox.Show("Error getting VOD/Clip information", "Unable to get info for these VODs/Clips\n" + String.Join("\n", errorList.ToArray()), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            WindowQueueOptions queue = new WindowQueueOptions(dataList);
            bool? queued = queue.ShowDialog();
            if (queued != null && (bool)queued)
                this.Close();

            btnQueue.IsEnabled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			AppSingleton.RequestTitleBarChange();
		}
    }
}
