using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Twitch;
using TwitchDownloaderCore.VideoPlatforms.Twitch.Gql;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF
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
            List<string> urlList = new List<string>(textList.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            List<string> invalidList = new List<string>();
            List<string> errorList = new List<string>();
            List<TaskData> dataList = new List<TaskData>();
            Dictionary<string, string> idDict = new Dictionary<string, string>();

            foreach (var url in urlList)
            {
                if (IdParse.TryParseVideoOrClipId(url, out _, out _, out var id))
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
                MessageBox.Show(Translations.Strings.UnableToParseInputsMessage + Environment.NewLine + string.Join(Environment.NewLine, invalidList.ToArray()), Translations.Strings.UnableToParseInputs, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Dictionary<int, string> taskDict = new Dictionary<int, string>();
            List<Task<TwitchVideoInfo>> taskVideoList = new List<Task<TwitchVideoInfo>>();
            List<Task<GqlClipResponse>> taskClipList = new List<Task<GqlClipResponse>>();

            foreach (var id in idList)
            {
                if (id.All(char.IsDigit))
                {
                    Task<TwitchVideoInfo> task = TwitchHelper.GetVideoInfo(int.Parse(id));
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

            foreach (var task in taskVideoList)
            {
                if (!task.IsCompleted)
                    continue;

                string id = taskDict[task.Id];
                if (!task.IsFaulted)
                {
                    var videoInfo = task.Result.GqlVideoResponse.data.video;
                    var thumbUrl = videoInfo.thumbnailURLs.FirstOrDefault();
                    if (!ThumbnailService.TryGetThumb(thumbUrl, out var thumbnail))
                    {
                        _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out thumbnail);
                    }

                    dataList.Add(new TaskData
                    {
                        Id = id,
                        Thumbnail = thumbnail,
                        Title = videoInfo.title,
                        Streamer = videoInfo.owner.displayName,
                        Time = Settings.Default.UTCVideoTime ? videoInfo.createdAt : videoInfo.createdAt.ToLocalTime(),
                        Views = videoInfo.viewCount,
                        Game = videoInfo.game?.displayName ?? "Unknown",
                        Length = videoInfo.lengthSeconds
                    });
                }
                else
                {
                    errorList.Add(idDict[id]);
                }
            }

            try
            {
                await Task.WhenAll(taskClipList.ToArray());
            }
            catch { }

            foreach (var task in taskClipList)
            {
                if (!task.IsCompleted)
                    continue;

                string id = taskDict[task.Id];
                if (!task.IsFaulted)
                {
                    var clipInfo = task.Result.data.clip;
                    var thumbUrl = clipInfo.thumbnailURL;
                    if (!ThumbnailService.TryGetThumb(thumbUrl, out var thumbnail))
                    {
                        _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out thumbnail);
                    }

                    dataList.Add(new TaskData
                    {
                        Id = id,
                        Thumbnail = thumbnail,
                        Title = clipInfo.title,
                        Streamer = clipInfo.broadcaster.displayName,
                        Time = Settings.Default.UTCVideoTime ? clipInfo.createdAt : clipInfo.createdAt.ToLocalTime(),
                        Views = clipInfo.viewCount,
                        Game = clipInfo.game?.displayName ?? "Unknown",
                        Length = clipInfo.durationSeconds
                    });
                }
                else
                {
                    errorList.Add(idDict[id]);
                }
            }

            if (errorList.Count > 0)
            {
                MessageBox.Show(Translations.Strings.UnableToGetInfoMessage + Environment.NewLine + string.Join(Environment.NewLine, errorList.ToArray()), Translations.Strings.UnableToGetInfo, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var queue = new WindowQueueOptions(dataList)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (queue.ShowDialog().GetValueOrDefault())
                this.Close();

            btnQueue.IsEnabled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title = Translations.Strings.TitleUrlList;
            App.RequestTitleBarChange();
        }
    }
}