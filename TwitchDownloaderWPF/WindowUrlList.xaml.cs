using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TwitchDownloader.TwitchTasks;
using TwitchDownloaderCore;
using TwitchDownloaderWPF;

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
            List<Task<JObject>> taskList = new List<Task<JObject>>();

            foreach (var id in idList)
            {
                if (id.All(Char.IsDigit))
                {
                    Task<JObject> task = TwitchHelper.GetVideoInfo(int.Parse(id));
                    taskList.Add(task);
                    taskDict[task.Id] = id;
                }
                else
                {
                    Task<JObject> task = TwitchHelper.GetClipInfo(id);
                    taskList.Add(task);
                    taskDict[task.Id] = id;
                }
            }

            try
            {
                await Task.WhenAll(taskList.ToArray());
            }
            catch { }

            for (int i = 0; i < taskList.Count; i++)
            {
                if (taskList[i].IsCompleted)
                {
                    string id = taskDict[taskList[i].Id];
                    if (!taskList[i].IsFaulted)
                    {
                        JObject data = taskList[i].Result;
                        TaskData newData = new TaskData();
                        newData.Id = id;
                        if (id.All(Char.IsDigit))
                        {
                            
                            try
                            {
                                string thumbUrl = data["preview"]["medium"].ToString();
                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.UriSource = new Uri(thumbUrl);
                                bitmapImage.EndInit();
                                newData.Thumbnail = bitmapImage;
                            }
                            catch { }
                            newData.Title = data["title"].ToString();
                            newData.Streamer = data["channel"]["display_name"].ToString();
                            newData.Time = data["created_at"].ToObject<DateTime>().ToLocalTime();
                            dataList.Add(newData);
                        }
                        else
                        {
                            try
                            {
                                string thumbUrl = data["thumbnails"]["medium"].ToString();
                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.UriSource = new Uri(thumbUrl);
                                bitmapImage.EndInit();
                                newData.Thumbnail = bitmapImage;
                            }
                            catch { }
                            newData.Title = data["title"].ToString();
                            newData.Streamer = data["broadcaster"]["display_name"].ToString();
                            newData.Time = data["created_at"].ToObject<DateTime>().ToLocalTime();
                            dataList.Add(newData);
                        }
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
    }
}
