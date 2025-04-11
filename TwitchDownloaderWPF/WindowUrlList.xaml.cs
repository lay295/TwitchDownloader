using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TwitchDownloaderCore;
using TwitchDownloaderCore.TwitchObjects.Gql;
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
            List<string> invalidList = new List<string>();
            List<string> errorList = new List<string>();
            List<TaskData> dataList = new List<TaskData>();
            Dictionary<string, string> idDict = new Dictionary<string, string>();

            var urls = textList.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var url in urls)
            {
                string id = PageChatDownload.ValidateUrl(url);

                if (string.IsNullOrWhiteSpace(id))
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
                MessageBox.Show(this, Translations.Strings.UnableToParseInputsMessage + Environment.NewLine + string.Join(Environment.NewLine, invalidList.ToArray()), Translations.Strings.UnableToParseInputs, MessageBoxButton.OK, MessageBoxImage.Error);
                btnQueue.IsEnabled = true;
                return;
            }

            Dictionary<int, string> taskDict = new Dictionary<int, string>();
            List<Task<GqlVideoResponse>> taskVideoList = new List<Task<GqlVideoResponse>>();
            List<Task<GqlClipResponse>> taskClipList = new List<Task<GqlClipResponse>>();

            foreach (var id in idList)
            {
                if (id.All(char.IsDigit))
                {
                    Task<GqlVideoResponse> task = TwitchHelper.GetVideoInfo(long.Parse(id));
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
                if (!task.IsFaulted && task.Result.data.video is { } videoInfo)
                {
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
                        StreamerName = videoInfo.owner?.displayName ?? Translations.Strings.UnknownUser,
                        StreamerId = videoInfo.owner?.id,
                        Time = Settings.Default.UTCVideoTime ? videoInfo.createdAt : videoInfo.createdAt.ToLocalTime(),
                        Views = videoInfo.viewCount,
                        Game = videoInfo.game?.displayName ?? Translations.Strings.UnknownGame,
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
                if (!task.IsFaulted && task.Result.data.clip is { } clipInfo)
                {
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
                        StreamerName = clipInfo.broadcaster?.displayName ?? Translations.Strings.UnknownUser,
                        StreamerId = clipInfo.broadcaster?.id,
                        ClipperName = clipInfo.curator?.displayName ?? Translations.Strings.UnknownUser,
                        ClipperId = clipInfo.curator?.id,
                        Time = Settings.Default.UTCVideoTime ? clipInfo.createdAt : clipInfo.createdAt.ToLocalTime(),
                        Views = clipInfo.viewCount,
                        Game = clipInfo.game?.displayName ?? Translations.Strings.UnknownGame,
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
                MessageBox.Show(this, Translations.Strings.UnableToGetInfoMessage + Environment.NewLine + string.Join(Environment.NewLine, errorList.ToArray()), Translations.Strings.UnableToGetInfo, MessageBoxButton.OK, MessageBoxImage.Error);
                btnQueue.IsEnabled = true;
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

        private void Window_OnSourceInitialized(object sender, EventArgs e)
        {
            App.RequestTitleBarChange();
        }
    }
}