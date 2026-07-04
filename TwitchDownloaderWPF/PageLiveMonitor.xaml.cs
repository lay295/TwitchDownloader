using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;

namespace TwitchDownloaderWPF
{
    public partial class PageLiveMonitor : Page
    {
        private readonly ObservableCollection<MonitoredChannel> _channels = new();
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(60) };
        private bool _monitoring;
        private bool _polling;

        public PageLiveMonitor()
        {
            InitializeComponent();
            listChannels.ItemsSource = _channels;
            _timer.Tick += async (_, _) => await PollAsync();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_channels.Count > 0)
                return;

            var saved = (Settings.Default.LiveMonitorChannels ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var login in saved)
                _channels.Add(new MonitoredChannel { Login = login, Status = "Idle" });
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) => SaveChannels();

        private void SaveChannels()
        {
            Settings.Default.LiveMonitorChannels = string.Join(",", _channels.Select(c => c.Login));
            Settings.Default.Save();
        }

        private void TextChannel_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                AddChannel();
        }

        private void BtnAddChannel_Click(object sender, RoutedEventArgs e) => AddChannel();

        private void AddChannel()
        {
            var login = textChannel.Text.Trim().TrimEnd('/');
            var slash = login.LastIndexOf('/');
            if (slash >= 0)
                login = login[(slash + 1)..];
            login = login.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(login) || _channels.Any(c => c.Login == login))
                return;

            _channels.Add(new MonitoredChannel { Login = login, Status = "Idle" });
            textChannel.Clear();
            SaveChannels();
        }

        private void BtnRemoveChannel_Click(object sender, RoutedEventArgs e)
        {
            if (listChannels.SelectedItem is MonitoredChannel channel)
            {
                _channels.Remove(channel);
                SaveChannels();
            }
        }

        private void BtnToggleMonitor_Click(object sender, RoutedEventArgs e)
        {
            _monitoring = !_monitoring;
            if (_monitoring)
            {
                btnToggleMonitor.Content = Translations.Strings.LiveMonitorStop;
                _timer.Start();
                AppendLog("Monitoring started.");
                _ = PollAsync();
            }
            else
            {
                _timer.Stop();
                btnToggleMonitor.Content = Translations.Strings.LiveMonitorStart;
                AppendLog("Monitoring stopped.");
                foreach (var channel in _channels)
                    channel.Status = "Idle";
            }
        }

        private async Task PollAsync()
        {
            if (_polling)
                return;

            _polling = true;
            try
            {
                foreach (var channel in _channels.ToList())
                {
                    try
                    {
                        var videos = await TwitchHelper.GetGqlVideos(channel.Login, "", 1, "ARCHIVE");
                        var node = videos?.data?.user?.videos?.edges?.FirstOrDefault()?.node;
                        if (node is null || !long.TryParse(node.id, out var vodId))
                        {
                            channel.Status = "Offline";
                            channel.RecordingStreamId = null;
                            continue;
                        }

                        var info = await TwitchHelper.GetVideoInfo(vodId);
                        var isLive = info?.data?.video?.status == "RECORDING";
                        if (!isLive)
                        {
                            channel.Status = "Offline";
                            channel.RecordingStreamId = null;
                            continue;
                        }

                        // Live. Queue a recording once per broadcast (keyed on the recording VOD id).
                        if (channel.RecordingStreamId != node.id)
                        {
                            channel.RecordingStreamId = node.id;
                            EnqueueRecording(channel.Login, vodId, node);
                            AppendLog($"{channel.Login} is live; queued a recording ({node.title}).");
                        }
                        channel.Status = "Recording";
                    }
                    catch (Exception ex)
                    {
                        channel.Status = "Error";
                        AppendLog($"{channel.Login}: {ex.Message}");
                    }
                }
            }
            finally
            {
                _polling = false;
            }
        }

        private void EnqueueRecording(string login, long vodId, VideoNode node)
        {
            var length = TimeSpan.FromSeconds(node.lengthSeconds);
            var filename = Path.Combine(Settings.Default.QueueFolder,
                FilenameService.GetFilename(Settings.Default.TemplateVod, node.title, node.id, node.createdAt, login, "",
                    TimeSpan.Zero, length, length, node.viewCount, node.game?.displayName ?? "") + ".mp4");

            var options = new LiveStreamDownloadOptions
            {
                Id = vodId,
                Channel = login,
                Quality = "chunked",
                Filename = filename,
                Oauth = Settings.Default.OAuth,
                FfmpegPath = "ffmpeg",
                TempFolder = Settings.Default.TempPath,
                DownloadThreads = Settings.Default.VodDownloadThreads,
            };

            var task = new TwitchTasks.LiveStreamDownloadTask
            {
                DownloadOptions = options,
                Info = { Title = node.title }
            };
            PageQueue.taskList.Add(task);
        }

        private void AppendLog(string message)
        {
            textLog.Document.Blocks.Add(new Paragraph(new Run($"[{DateTime.Now:HH:mm:ss}] {message}")));
            textLog.ScrollToEnd();
        }
    }
}
