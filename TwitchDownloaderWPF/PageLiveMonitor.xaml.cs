using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageLiveMonitor.xaml
    /// </summary>
    public partial class PageLiveMonitor : Page
    {
        private readonly ObservableCollection<string> _channels = new();
        private readonly HashSet<long> _queuedVodIds = new();
        // vodId → (channel login, output folder) for streams waiting to be chapter-split
        private readonly Dictionary<long, (string channel, string folder)> _pendingChapterSplits = new();
        private readonly DispatcherTimer _pollTimer = new();
        private bool _polling;

        public bool IsMonitoring { get; private set; }

        public PageLiveMonitor()
        {
            InitializeComponent();
            _pollTimer.Tick += async (_, _) => await PollAsync();
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            listChannels.ItemsSource = _channels;

            foreach (var channel in (Settings.Default.LiveMonitorChannels ?? "")
                         .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                _channels.Add(channel.ToLowerInvariant());
            }

            textFolder.Text = Settings.Default.LiveMonitorFolder;
            numPoll.Value = Math.Max(30, Settings.Default.LiveMonitorPollSeconds);
            numThreads.Value = Math.Max(1, Settings.Default.VodDownloadThreads);
            checkChat.IsChecked = Settings.Default.LiveMonitorDownloadChat;
            checkSplitChapters.IsChecked = Settings.Default.LiveMonitorSplitChapters;

            // Load H/M/S before the checkbox so that SaveSettings (fired by the Checked event) reads correct values
            numTrimStartHour.Value = Settings.Default.LiveMonitorTrimBeginningHour;
            numTrimStartMinute.Value = Settings.Default.LiveMonitorTrimBeginningMinute;
            numTrimStartSecond.Value = Settings.Default.LiveMonitorTrimBeginningSecond;
            checkTrimStart.IsChecked = Settings.Default.LiveMonitorTrimBeginning;

            numTrimEndHour.Value = Settings.Default.LiveMonitorTrimEndingHour;
            numTrimEndMinute.Value = Settings.Default.LiveMonitorTrimEndingMinute;
            numTrimEndSecond.Value = Settings.Default.LiveMonitorTrimEndingSecond;
            checkTrimEnd.IsChecked = Settings.Default.LiveMonitorTrimEnding;

            SetTrimStartEnabled(checkTrimStart.IsChecked.GetValueOrDefault());
            SetTrimEndEnabled(checkTrimEnd.IsChecked.GetValueOrDefault());

            var savedQuality = Settings.Default.LiveMonitorQuality;
            if (!string.IsNullOrWhiteSpace(savedQuality))
            {
                foreach (ComboBoxItem item in comboQuality.Items)
                {
                    if ((item.Content as string) == savedQuality)
                    {
                        comboQuality.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private string SelectedQuality => (comboQuality.SelectedItem as ComboBoxItem)?.Content as string ?? "Source";

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new WindowSettings
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            settings.ShowDialog();
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SaveSettings()
        {
            Settings.Default.LiveMonitorChannels = string.Join(",", _channels);
            Settings.Default.LiveMonitorFolder = textFolder.Text;
            Settings.Default.LiveMonitorQuality = SelectedQuality;
            Settings.Default.VodDownloadThreads = (int)numThreads.Value;
            Settings.Default.LiveMonitorPollSeconds = (int)numPoll.Value;
            Settings.Default.LiveMonitorDownloadChat = checkChat.IsChecked.GetValueOrDefault();
            Settings.Default.LiveMonitorSplitChapters = checkSplitChapters.IsChecked.GetValueOrDefault();

            Settings.Default.LiveMonitorTrimBeginning = checkTrimStart.IsChecked.GetValueOrDefault();
            Settings.Default.LiveMonitorTrimBeginningHour = (int)numTrimStartHour.Value;
            Settings.Default.LiveMonitorTrimBeginningMinute = (int)numTrimStartMinute.Value;
            Settings.Default.LiveMonitorTrimBeginningSecond = (int)numTrimStartSecond.Value;

            Settings.Default.LiveMonitorTrimEnding = checkTrimEnd.IsChecked.GetValueOrDefault();
            Settings.Default.LiveMonitorTrimEndingHour = (int)numTrimEndHour.Value;
            Settings.Default.LiveMonitorTrimEndingMinute = (int)numTrimEndMinute.Value;
            Settings.Default.LiveMonitorTrimEndingSecond = (int)numTrimEndSecond.Value;

            Settings.Default.Save();
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke(() =>
                textLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"));
        }

        private void btnAddChannel_Click(object sender, RoutedEventArgs e)
        {
            AddChannel();
        }

        private void textChannel_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddChannel();
                e.Handled = true;
            }
        }

        private void AddChannel()
        {
            var name = textChannel.Text.Trim().ToLowerInvariant();
            if (name.Length == 0)
                return;

            // Accept a pasted channel URL too
            var slash = name.LastIndexOf('/');
            if (slash >= 0)
                name = name[(slash + 1)..];

            if (name.Length == 0 || _channels.Contains(name))
            {
                textChannel.Clear();
                return;
            }

            _channels.Add(name);
            textChannel.Clear();
            SaveSettings();

            // If monitoring is already running, check this channel immediately rather
            // than waiting for the next timer tick — it may already be live.
            if (IsMonitoring)
                _ = PollAsync();
        }

        private void btnRemoveChannel_Click(object sender, RoutedEventArgs e)
        {
            if (listChannels.SelectedItem is string channel)
            {
                _channels.Remove(channel);
                SaveSettings();
            }
        }

        private void btnFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (Directory.Exists(textFolder.Text))
                dialog.InitialDirectory = textFolder.Text;

            if (dialog.ShowDialog() == true)
            {
                textFolder.Text = dialog.FolderName;
                SaveSettings();
            }
        }

        private void btnToggle_Click(object sender, RoutedEventArgs e)
        {
            if (IsMonitoring)
            {
                StopMonitoring();
                return;
            }

            if (_channels.Count == 0)
            {
                MessageBox.Show(Application.Current.MainWindow!, "Add at least one channel to monitor.", "Live Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(textFolder.Text) || !Directory.Exists(textFolder.Text))
            {
                MessageBox.Show(Application.Current.MainWindow!, "Choose a valid output folder.", "Live Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveSettings();

            IsMonitoring = true;
            btnToggle.Content = "Stop Monitoring";
            textState.Text = "Monitoring";
            _pollTimer.Interval = TimeSpan.FromSeconds(Math.Max(30, (int)numPoll.Value));
            _pollTimer.Start();
            AppendLog($"Started monitoring {_channels.Count} channel(s), polling every {(int)numPoll.Value}s.");
            _ = PollAsync();
        }

        private void StopMonitoring()
        {
            _pollTimer.Stop();
            IsMonitoring = false;
            btnToggle.Content = "Start Monitoring";
            textState.Text = "Stopped";
            AppendLog("Stopped monitoring.");
        }

        private void SetTrimStartEnabled(bool isEnabled)
        {
            numTrimStartHour.IsEnabled = isEnabled;
            numTrimStartMinute.IsEnabled = isEnabled;
            numTrimStartSecond.IsEnabled = isEnabled;
        }

        private void SetTrimEndEnabled(bool isEnabled)
        {
            numTrimEndHour.IsEnabled = isEnabled;
            numTrimEndMinute.IsEnabled = isEnabled;
            numTrimEndSecond.IsEnabled = isEnabled;
        }

        private void checkTrimStart_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetTrimStartEnabled(checkTrimStart.IsChecked.GetValueOrDefault());
            SaveSettings();
        }

        private void checkTrimEnd_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetTrimEndEnabled(checkTrimEnd.IsChecked.GetValueOrDefault());
            SaveSettings();
        }

        private async System.Threading.Tasks.Task PollAsync()
        {
            if (_polling)
                return;

            _polling = true;
            try
            {
                foreach (var channel in _channels.ToArray())
                {
                    try
                    {
                        var videos = await TwitchHelper.GetGqlVideos(channel, "", 1, "ARCHIVE");
                        var node = videos?.data?.user?.videos?.edges?.FirstOrDefault()?.node;
                        if (node is null || !long.TryParse(node.id, out var vodId))
                            continue;

                        if (_queuedVodIds.Contains(vodId))
                            continue;

                        var info = await TwitchHelper.GetVideoInfo(vodId);
                        if (info?.data?.video is null || info.data.video.status != "RECORDING")
                            continue;

                        EnqueueRecording(channel, vodId, info);
                        _queuedVodIds.Add(vodId);
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Error checking '{channel}': {ex.Message}");
                    }
                }

                await CheckPendingChapterSplitsAsync();
            }
            finally
            {
                _polling = false;
            }
        }

        private async System.Threading.Tasks.Task CheckPendingChapterSplitsAsync()
        {
            if (_pendingChapterSplits.Count == 0)
                return;

            foreach (var (vodId, (channel, folder)) in _pendingChapterSplits.ToArray())
            {
                try
                {
                    var info = await TwitchHelper.GetVideoInfo(vodId);
                    if (info?.data?.video is null || info.data.video.status == "RECORDING")
                        continue; // still live

                    _pendingChapterSplits.Remove(vodId);
                    await EnqueueChapterSplitsAsync(vodId, info, channel, folder);
                }
                catch (Exception ex)
                {
                    AppendLog($"Error checking chapter split for VOD {vodId}: {ex.Message}");
                }
            }
        }

        private async System.Threading.Tasks.Task EnqueueChapterSplitsAsync(long vodId, GqlVideoResponse info, string channel, string folder)
        {
            var video = info.data.video;
            var vodLength = TimeSpan.FromSeconds(video.lengthSeconds);
            var title = video.title ?? "";
            var streamer = video.owner?.displayName ?? channel;
            var streamerId = video.owner?.id;
            var game = video.game?.displayName ?? "";
            var createdAt = video.createdAt;
            var ext = FilenameService.GuessVodFileExtension(SelectedQuality);

            try
            {
                var chapterResponse = await TwitchHelper.GetOrGenerateVideoChapters(vodId, video);
                var chapters = chapterResponse.data.video.moments.edges;

                if (chapters.Count <= 1)
                {
                    AppendLog($"VOD {vodId} has only one chapter — queuing as single download.");
                    var baseName = FilenameService.GetFilename(Settings.Default.TemplateVod, title, vodId.ToString(),
                        createdAt, streamer, streamerId, TimeSpan.Zero, vodLength, vodLength, video.viewCount, game);
                    var vodPath = Path.Combine(folder, baseName + ext);
                    TwitchHelper.CreateDirectory(Path.GetDirectoryName(vodPath));
                    var singleOptions = BuildVodOptions(vodId, vodPath, TimeSpan.Zero, vodLength, false, false);
                    var singleTask = new VodDownloadTask { DownloadOptions = singleOptions, Info = { Title = title } };
                    singleTask.ChangeStatus(TwitchTaskStatus.Ready);
                    lock (PageQueue.taskLock) { PageQueue.taskList.Add(singleTask); }
                    return;
                }

                AppendLog($"Stream ended — enqueueing {chapters.Count} chapter downloads for VOD {vodId} ({title}).");

                lock (PageQueue.taskLock)
                {
                    for (int i = 0; i < chapters.Count; i++)
                    {
                        var chapter = chapters[i].node;
                        var startSec = chapter.positionMilliseconds / 1000;
                        var endSec = startSec + chapter.durationMilliseconds / 1000;
                        var chapterStart = TimeSpan.FromSeconds(startSec);
                        var chapterEnd = TimeSpan.FromSeconds(Math.Min(endSec, (int)vodLength.TotalSeconds));
                        var gameName = chapter.details?.game?.displayName ?? chapter.description ?? game;

                        var baseName = FilenameService.GetFilename(Settings.Default.TemplateVod, title, vodId.ToString(),
                            createdAt, streamer, streamerId, chapterStart, chapterEnd, vodLength, video.viewCount, gameName)
                            + $"_ch{i + 1:D2}";
                        var vodPath = Path.Combine(folder, baseName + ext);
                        TwitchHelper.CreateDirectory(Path.GetDirectoryName(vodPath));

                        var options = BuildVodOptions(vodId, vodPath, chapterStart, chapterEnd, true, true);
                        var task = new VodDownloadTask
                        {
                            DownloadOptions = options,
                            Info = { Title = $"{title} — {gameName} (Ch. {i + 1})" }
                        };
                        task.ChangeStatus(TwitchTaskStatus.Ready);
                        PageQueue.taskList.Add(task);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to enqueue chapter splits for VOD {vodId}: {ex.Message}");
            }
        }

        private VideoDownloadOptions BuildVodOptions(long vodId, string filename, TimeSpan trimStart, TimeSpan trimEnd, bool trimBeginning, bool trimEnding)
        {
            return new VideoDownloadOptions
            {
                Id = vodId,
                Quality = SelectedQuality,
                Filename = filename,
                TrimBeginning = trimBeginning,
                TrimBeginningTime = trimStart,
                TrimEnding = trimEnding,
                TrimEndingTime = trimEnd,
                DownloadThreads = (int)numThreads.Value,
                ThrottleKib = Settings.Default.DownloadThrottleEnabled ? Settings.Default.MaximumBandwidthKib : -1,
                Oauth = Settings.Default.OAuth,
                FfmpegPath = "ffmpeg",
                TempFolder = Settings.Default.TempPath,
                TrimMode = VideoTrimMode.Exact,
                FileCollisionCallback = info => info,
            };
        }

        private void EnqueueRecording(string channel, long vodId, GqlVideoResponse info)
        {
            var video = info.data.video;
            var streamer = video.owner?.displayName ?? channel;
            var streamerId = video.owner?.id;
            var createdAt = video.createdAt;
            var title = video.title ?? "";
            var game = video.game?.displayName ?? "";
            var folder = textFolder.Text;

            var baseName = FilenameService.GetFilename(Settings.Default.TemplateVod, title, vodId.ToString(), createdAt, streamer, streamerId,
                TimeSpan.Zero, TimeSpan.FromSeconds(video.lengthSeconds), TimeSpan.FromSeconds(video.lengthSeconds), video.viewCount, game);

            if (checkSplitChapters.IsChecked.GetValueOrDefault())
            {
                // Track the VOD; chapter downloads will be queued once the stream ends
                _pendingChapterSplits[vodId] = (channel, folder);
                AppendLog($"'{channel}' is LIVE — will split VOD {vodId} by chapters when stream ends.");
            }
            else
            {
                // Use LiveStreamDownloadTask so the back-catalog is downloaded immediately with
                // multiple threads while the live tail is recorded concurrently with ffmpeg.
                var vodPath = Path.Combine(folder, baseName + FilenameService.GuessVodFileExtension(SelectedQuality));
                TwitchHelper.CreateDirectory(Path.GetDirectoryName(vodPath));

                var liveOptions = new LiveStreamDownloadOptions
                {
                    Id = vodId,
                    Channel = channel,
                    Quality = SelectedQuality,
                    Filename = vodPath,
                    DownloadThreads = (int)numThreads.Value,
                    ThrottleKib = Settings.Default.DownloadThrottleEnabled ? Settings.Default.MaximumBandwidthKib : -1,
                    Oauth = Settings.Default.OAuth,
                    FfmpegPath = "ffmpeg",
                    TempFolder = Settings.Default.TempPath,
                    TrimBeginning = checkTrimStart.IsChecked.GetValueOrDefault(),
                    TrimBeginningTime = new TimeSpan((int)numTrimStartHour.Value, (int)numTrimStartMinute.Value, (int)numTrimStartSecond.Value),
                    TrimEnding = checkTrimEnd.IsChecked.GetValueOrDefault(),
                    TrimEndingTime = new TimeSpan((int)numTrimEndHour.Value, (int)numTrimEndMinute.Value, (int)numTrimEndSecond.Value),
                };

                var downloadTask = new LiveStreamDownloadTask
                {
                    DownloadOptions = liveOptions,
                    Info = { Title = title.Length > 0 ? title : $"{streamer} live" }
                };
                downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                lock (PageQueue.taskLock)
                {
                    PageQueue.taskList.Add(downloadTask);
                }

                AppendLog($"'{channel}' is LIVE — queued live download for VOD {vodId} (back-catalog + real-time recording).");
            }

            if (checkChat.IsChecked.GetValueOrDefault())
            {
                var chatOptions = new ChatDownloadOptions
                {
                    Id = vodId.ToString(),
                    DownloadFormat = ChatFormat.Json,
                    Compression = ChatCompression.Gzip,
                    DelayDownload = true,
                    DownloadThreads = 1,
                    TempFolder = Settings.Default.TempPath,
                };
                chatOptions.Filename = Path.Combine(folder, baseName + chatOptions.FileExtension);

                var chatTask = new ChatDownloadTask
                {
                    DownloadOptions = chatOptions,
                    Info = { Title = title.Length > 0 ? title : $"{streamer} live chat" }
                };
                chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                lock (PageQueue.taskLock)
                {
                    PageQueue.taskList.Add(chatTask);
                }
            }
        }
    }
}
