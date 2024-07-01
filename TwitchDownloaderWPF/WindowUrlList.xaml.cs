using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TwitchDownloaderCore;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Translations;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF;

/// <summary>
///     Interaction logic for WindowUrlList.xaml
/// </summary>
public partial class WindowUrlList : Window {
    public WindowUrlList() { this.InitializeComponent(); }

    private async void btnQueue_Click(object sender, RoutedEventArgs e) {
        this.btnQueue.IsEnabled = false;
        var idList = new List<string>();
        var invalidList = new List<string>();
        var errorList = new List<string>();
        var dataList = new List<TaskData>();
        var idDict = new Dictionary<string, string>();

        var urls = this.textList.Text.Split(
            '\n',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );
        foreach (var url in urls) {
            var id = PageChatDownload.ValidateUrl(url);

            if (string.IsNullOrWhiteSpace(id))
                invalidList.Add(url);
            else {
                idList.Add(id);
                idDict[id] = url;
            }
        }

        if (invalidList.Count > 0) {
            MessageBox.Show(
                this,
                Strings.UnableToParseInputsMessage
                + Environment.NewLine
                + string.Join(Environment.NewLine, invalidList.ToArray()),
                Strings.UnableToParseInputs,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            this.btnQueue.IsEnabled = true;
            return;
        }

        var taskDict = new Dictionary<int, string>();
        var taskVideoList = new List<Task<GqlVideoResponse>>();
        var taskClipList = new List<Task<GqlClipResponse>>();

        foreach (var id in idList)
            if (id.All(char.IsDigit)) {
                var task = TwitchHelper.GetVideoInfo(long.Parse(id));
                taskVideoList.Add(task);
                taskDict[task.Id] = id;
            } else {
                var task = TwitchHelper.GetClipInfo(id);
                taskClipList.Add(task);
                taskDict[task.Id] = id;
            }

        try {
            await Task.WhenAll(taskVideoList.ToArray());
        } catch { }

        foreach (var task in taskVideoList) {
            if (!task.IsCompleted)
                continue;

            var id = taskDict[task.Id];
            if (!task.IsFaulted && task.Result.data.video is { } videoInfo) {
                var thumbUrl = videoInfo.thumbnailURLs.FirstOrDefault();
                if (!ThumbnailService.TryGetThumb(thumbUrl, out var thumbnail))
                    _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out thumbnail);

                dataList.Add(
                    new() {
                        Id = id,
                        Thumbnail = thumbnail,
                        Title = videoInfo.title,
                        Streamer = videoInfo.owner.displayName,
                        Time = Settings.Default.UTCVideoTime ? videoInfo.createdAt : videoInfo.createdAt.ToLocalTime(),
                        Views = videoInfo.viewCount,
                        Game = videoInfo.game?.displayName ?? Strings.UnknownGame,
                        Length = videoInfo.lengthSeconds
                    }
                );
            } else
                errorList.Add(idDict[id]);
        }

        try {
            await Task.WhenAll(taskClipList.ToArray());
        } catch { }

        foreach (var task in taskClipList) {
            if (!task.IsCompleted)
                continue;

            var id = taskDict[task.Id];
            if (!task.IsFaulted && task.Result.data.clip is { } clipInfo) {
                var thumbUrl = clipInfo.thumbnailURL;
                if (!ThumbnailService.TryGetThumb(thumbUrl, out var thumbnail))
                    _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out thumbnail);

                dataList.Add(
                    new() {
                        Id = id,
                        Thumbnail = thumbnail,
                        Title = clipInfo.title,
                        Streamer = clipInfo.broadcaster.displayName,
                        Time = Settings.Default.UTCVideoTime ? clipInfo.createdAt : clipInfo.createdAt.ToLocalTime(),
                        Views = clipInfo.viewCount,
                        Game = clipInfo.game?.displayName ?? Strings.UnknownGame,
                        Length = clipInfo.durationSeconds
                    }
                );
            } else
                errorList.Add(idDict[id]);
        }

        if (errorList.Count > 0) {
            MessageBox.Show(
                this,
                Strings.UnableToGetInfoMessage
                + Environment.NewLine
                + string.Join(Environment.NewLine, errorList.ToArray()),
                Strings.UnableToGetInfo,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            this.btnQueue.IsEnabled = true;
            return;
        }

        var queue = new WindowQueueOptions(dataList) {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (queue.ShowDialog().GetValueOrDefault())
            this.Close();

        this.btnQueue.IsEnabled = true;
    }

    private void Window_OnSourceInitialized(object sender, EventArgs e) { App.RequestTitleBarChange(); }
}
