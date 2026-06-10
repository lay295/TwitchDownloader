using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for QueueOptions.xaml
    /// </summary>
    public partial class WindowQueueOptions : Window
    {
        // This file is absolutely atrocious, but fixing it would mean rewriting the entire GUI in a more abstract form

        private readonly List<TaskData> _dataList;
        private readonly Page _parentPage;
        private bool _applyingPreset = false;
        private bool _isVodSource = false;

        public WindowQueueOptions(Page page)
        {
            _parentPage = page;
            InitializeComponent();

            textFolder.Text = Settings.Default.QueueFolder;
            LoadPresets();

            TextPreferredQuality.Visibility = Visibility.Collapsed;
            ComboPreferredQuality.Visibility = Visibility.Collapsed;

            if (page is PageVodDownload)
            {
                _isVodSource = true;
                checkVideo.IsChecked = true;
                checkVideo.IsEnabled = false;
                checkDelay.IsEnabled = true;
                checkLive.IsEnabled = true;
            }
            else if (page is PageClipDownload)
            {
                checkVideo.IsChecked = true;
                checkVideo.IsEnabled = false;
                checkDelay.Visibility = Visibility.Collapsed;
                checkLive.Visibility = Visibility.Collapsed;
                checkDelayChat.Visibility = Visibility.Collapsed;
            }
            else if (page is PageChatDownload chatPage)
            {
                checkVideo.Visibility = Visibility.Collapsed;
                checkDelay.Visibility = Visibility.Collapsed;
                checkLive.Visibility = Visibility.Collapsed;
                checkChat.IsChecked = true;
                checkChat.IsEnabled = false;
                TextDownloadFormat.Visibility = Visibility.Collapsed;
                radioJson.Visibility = Visibility.Collapsed;
                radioTxt.Visibility = Visibility.Collapsed;
                radioHTML.Visibility = Visibility.Collapsed;
                TextCompression.Visibility = Visibility.Collapsed;
                RadioCompressionNone.Visibility = Visibility.Collapsed;
                RadioCompressionGzip.Visibility = Visibility.Collapsed;
                checkEmbed.Visibility = Visibility.Collapsed;
                StackThirdPartyEmbed.Visibility = Visibility.Collapsed;
                if (!chatPage.radioJson.IsChecked.GetValueOrDefault())
                {
                    checkRender.IsChecked = false;
                    checkRender.IsEnabled = false;
                }
                if (chatPage.downloadType == DownloadType.Clip)
                {
                    checkDelayChat.Visibility = Visibility.Collapsed;
                }
            }
            else if (page is PageChatUpdate)
            {
                checkVideo.Visibility = Visibility.Collapsed;
                checkDelay.Visibility = Visibility.Collapsed;
                checkLive.Visibility = Visibility.Collapsed;
                checkChat.Visibility = Visibility.Collapsed;
                TextDownloadFormat.Visibility = Visibility.Collapsed;
                radioJson.Visibility = Visibility.Collapsed;
                radioTxt.Visibility = Visibility.Collapsed;
                radioHTML.Visibility = Visibility.Collapsed;
                TextCompression.Visibility = Visibility.Collapsed;
                RadioCompressionNone.Visibility = Visibility.Collapsed;
                RadioCompressionGzip.Visibility = Visibility.Collapsed;
                checkEmbed.Visibility = Visibility.Collapsed;
                StackThirdPartyEmbed.Visibility = Visibility.Collapsed;
                checkDelayChat.Visibility = Visibility.Collapsed;
                checkRender.Visibility = Visibility.Collapsed;
            }
            else if (page is PageChatRender)
            {
                checkVideo.Visibility = Visibility.Collapsed;
                checkDelay.Visibility = Visibility.Collapsed;
                checkLive.Visibility = Visibility.Collapsed;
                checkChat.Visibility = Visibility.Collapsed;
                TextDownloadFormat.Visibility = Visibility.Collapsed;
                radioJson.Visibility = Visibility.Collapsed;
                radioTxt.Visibility = Visibility.Collapsed;
                radioHTML.Visibility = Visibility.Collapsed;
                TextCompression.Visibility = Visibility.Collapsed;
                RadioCompressionNone.Visibility = Visibility.Collapsed;
                RadioCompressionGzip.Visibility = Visibility.Collapsed;
                checkEmbed.Visibility = Visibility.Collapsed;
                StackThirdPartyEmbed.Visibility = Visibility.Collapsed;
                checkDelayChat.Visibility = Visibility.Collapsed;
                checkRender.IsChecked = true;
                checkRender.IsEnabled = false;
            }
        }

        public WindowQueueOptions(List<TaskData> dataList)
        {
            _dataList = dataList;
            InitializeComponent();

            textFolder.Text = Settings.Default.QueueFolder;
            LoadPresets();

            if (_dataList.Any(x => !x.Id.All(char.IsDigit)))
            {
                ComboPreferredQuality.Items.Insert(1, new ComboBoxItem { Content = "Source Portrait" });
                ComboPreferredQuality.Items.Add(new ComboBoxItem { Content = "Worst Portrait" });
            }

            if (_dataList.Any(x => x.Id.All(char.IsDigit)))
            {
                ComboPreferredQuality.Items.Add(new ComboBoxItem { Content = "Audio Only" });

                checkVideo.IsChecked = true;
                checkDelay.Visibility = Visibility.Visible;
                checkDelay.IsEnabled = true;
                checkLive.Visibility = Visibility.Visible;
                checkLive.IsEnabled = true;
                ComboPreferredQuality.IsEnabled = true;
            }
            else
            {
                checkDelay.Visibility = Visibility.Collapsed;
                checkLive.Visibility = Visibility.Collapsed;
            }

            var preferredQuality = Settings.Default.PreferredQuality;
            for (var i = 0; i < ComboPreferredQuality.Items.Count; i++)
            {
                if (ComboPreferredQuality.Items[i] is ComboBoxItem { Content: string quality } && quality == preferredQuality)
                {
                    ComboPreferredQuality.SelectedIndex = i;
                    break;
                }
            }
        }

        private FileInfo HandleFileCollisionCallback(FileInfo fileInfo)
        {
            return Dispatcher.Invoke(() => FileCollisionService.HandleCollisionCallback(fileInfo, Application.Current.MainWindow));
        }

        private async void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_parentPage != null)
            {
                if (_parentPage is PageVodDownload vodDownloadPage)
                {
                    // Handle Split by Chapters
                    if (checkSplitByChapters.IsChecked == true)
                    {
                        string chaptersFolder = textFolder.Text;
                        if (!Directory.Exists(chaptersFolder))
                        {
                            try { TwitchHelper.CreateDirectory(chaptersFolder); }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                                if (Settings.Default.VerboseErrors)
                                    MessageBox.Show(this, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }

                        if (vodDownloadPage.currentVideoId == 0)
                        {
                            MessageBox.Show(this, "Please load a VOD first.", "No VOD Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        try
                        {
                            btnQueue.IsEnabled = false;
                            var videoInfo = new TwitchDownloaderCore.TwitchObjects.Gql.VideoInfo
                            {
                                lengthSeconds = (int)vodDownloadPage.vodLength.TotalSeconds,
                                game = new TwitchDownloaderCore.TwitchObjects.Gql.Game { displayName = vodDownloadPage.game }
                            };

                            var chapterResponse = await TwitchHelper.GetOrGenerateVideoChapters(vodDownloadPage.currentVideoId, videoInfo);
                            var chapters = chapterResponse.data.video.moments.edges;

                            if (chapters.Count == 0)
                            {
                                MessageBox.Show(this, "Could not retrieve chapter data for this VOD.", "No Chapters", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }

                            VideoDownloadOptions baseOptions = vodDownloadPage.GetOptions(null, textFolder.Text);
                            baseOptions.DelayDownload = checkDelay.IsChecked.GetValueOrDefault();
                            baseOptions.FileCollisionCallback = HandleFileCollisionCallback;

                            string quality = baseOptions.Quality;
                            string ext = TwitchDownloaderCore.Services.FilenameService.GuessVodFileExtension(quality);

                            lock (PageQueue.taskLock)
                            {
                                for (int i = 0; i < chapters.Count; i++)
                                {
                                    var chapter = chapters[i].node;
                                    var startSec = chapter.positionMilliseconds / 1000;
                                    var endSec = startSec + chapter.durationMilliseconds / 1000;
                                    var chapterStart = TimeSpan.FromSeconds(startSec);
                                    var chapterEnd = TimeSpan.FromSeconds(Math.Min(endSec, (int)vodDownloadPage.vodLength.TotalSeconds));
                                    var gameName = chapter.details?.game?.displayName ?? chapter.description ?? vodDownloadPage.game;

                                    var options = new VideoDownloadOptions
                                    {
                                        DownloadThreads = baseOptions.DownloadThreads,
                                        ThrottleKib = baseOptions.ThrottleKib,
                                        Oauth = baseOptions.Oauth,
                                        Quality = quality,
                                        Id = vodDownloadPage.currentVideoId,
                                        TrimBeginning = true,
                                        TrimBeginningTime = chapterStart,
                                        TrimEnding = true,
                                        TrimEndingTime = chapterEnd,
                                        FfmpegPath = "ffmpeg",
                                        TempFolder = Settings.Default.TempPath,
                                        TrimMode = baseOptions.TrimMode,
                                        DelayDownload = checkDelay.IsChecked.GetValueOrDefault(),
                                        FileCollisionCallback = HandleFileCollisionCallback,
                                    };
                                    options.Filename = Path.Combine(chaptersFolder,
                                        TwitchDownloaderCore.Services.FilenameService.GetFilename(Settings.Default.TemplateVod,
                                            vodDownloadPage.textTitle.Text, vodDownloadPage.currentVideoId.ToString(),
                                            vodDownloadPage.currentVideoTime, vodDownloadPage.textStreamer.Text, vodDownloadPage.streamerId,
                                            chapterStart, chapterEnd, vodDownloadPage.vodLength, vodDownloadPage.viewCount, gameName)
                                        + $"_ch{i + 1:D2}" + ext);

                                    var task = new TwitchTasks.VodDownloadTask
                                    {
                                        DownloadOptions = options,
                                        Info =
                                        {
                                            Title = $"{vodDownloadPage.textTitle.Text} — {gameName} (Ch. {i + 1})",
                                            Thumbnail = vodDownloadPage.imgThumbnail.Source
                                        }
                                    };
                                    PageQueue.taskList.Add(task);
                                }
                            }

                            this.Close();
                            return;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, "Failed to fetch chapters: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        finally
                        {
                            btnQueue.IsEnabled = true;
                        }
                    }

                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        try
                        {
                            TwitchHelper.CreateDirectory(folderPath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);

                            if (Settings.Default.VerboseErrors)
                            {
                                MessageBox.Show(this, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                            }

                            return;
                        }
                    }

                    VideoDownloadOptions downloadOptions = vodDownloadPage.GetOptions(null, textFolder.Text);
                    downloadOptions.DelayDownload = checkDelay.IsChecked.GetValueOrDefault();
                    downloadOptions.FileCollisionCallback = HandleFileCollisionCallback;

                    if (checkLive.IsChecked.GetValueOrDefault())
                    {
                        var liveOptions = new LiveStreamDownloadOptions
                        {
                            Id = downloadOptions.Id,
                            Quality = downloadOptions.Quality,
                            Filename = downloadOptions.Filename,
                            Oauth = Settings.Default.OAuth,
                            FfmpegPath = "ffmpeg",
                            DownloadThreads = downloadOptions.DownloadThreads,
                            ThrottleKib = downloadOptions.ThrottleKib,
                            TempFolder = downloadOptions.TempFolder,
                        };

                        var liveTask = new LiveStreamDownloadTask
                        {
                            DownloadOptions = liveOptions,
                            Info =
                            {
                                Title = vodDownloadPage.textTitle.Text,
                                Thumbnail = vodDownloadPage.imgThumbnail.Source
                            }
                        };

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(liveTask);
                        }
                    }
                    else
                    {
                        VodDownloadTask downloadTask = new VodDownloadTask
                        {
                            DownloadOptions = downloadOptions,
                            Info =
                            {
                                Title = vodDownloadPage.textTitle.Text,
                                Thumbnail = vodDownloadPage.imgThumbnail.Source
                            }
                        };

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(downloadTask);
                        }
                    }

                    if (checkChat.IsChecked.GetValueOrDefault())
                    {
                        ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                        chatOptions.Id = downloadOptions.Id.ToString();
                        if (radioJson.IsChecked == true)
                            chatOptions.DownloadFormat = ChatFormat.Json;
                        else if (radioHTML.IsChecked == true)
                            chatOptions.DownloadFormat = ChatFormat.Html;
                        else
                            chatOptions.DownloadFormat = ChatFormat.Text;
                        // TODO: Support non-json chat compression
                        if (RadioCompressionGzip.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                            chatOptions.Compression = ChatCompression.Gzip;
                        chatOptions.EmbedData = checkEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.BttvEmotes = CheckBttvEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.FfzEmotes = CheckFfzEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.StvEmotes = CheckStvEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.DelayDownload = checkDelayChat.IsChecked.GetValueOrDefault();
                        chatOptions.Filename = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(downloadOptions.Filename) + chatOptions.FileExtension);
                        chatOptions.FileCollisionCallback = HandleFileCollisionCallback;

                        if (downloadOptions.TrimBeginning)
                        {
                            chatOptions.TrimBeginning = true;
                            chatOptions.TrimBeginningTime = downloadOptions.TrimBeginningTime.TotalSeconds;
                        }

                        if (downloadOptions.TrimEnding)
                        {
                            chatOptions.TrimEnding = true;
                            chatOptions.TrimEndingTime = downloadOptions.TrimEndingTime.TotalSeconds;
                        }

                        ChatDownloadTask chatTask = new ChatDownloadTask
                        {
                            DownloadOptions = chatOptions,
                            Info =
                            {
                                Title = vodDownloadPage.textTitle.Text,
                                Thumbnail = vodDownloadPage.imgThumbnail.Source
                            }
                        };

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(chatTask);
                        }

                        if (checkRender.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                        {
                            ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()));
                            if (renderOptions.OutputFile.Trim() == downloadOptions.Filename!.Trim())
                            {
                                //Just in case VOD and chat paths are the same. Like the previous defaults
                                renderOptions.OutputFile = Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), " - CHAT." + MainWindow.pageChatRender.comboFormat.Text.ToLower());
                            }
                            renderOptions.InputFile = chatOptions.Filename;
                            renderOptions.FileCollisionCallback = HandleFileCollisionCallback;

                            ChatRenderTask renderTask = new ChatRenderTask
                            {
                                DownloadOptions = renderOptions,
                                Info =
                                {
                                    Title = vodDownloadPage.textTitle.Text,
                                    Thumbnail = vodDownloadPage.imgThumbnail.Source
                                },
                                DependantTask = chatTask,
                            };
                            renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                            lock (PageQueue.taskLock)
                            {
                                PageQueue.taskList.Add(renderTask);
                            }
                        }
                    }

                    this.Close();
                }

                if (_parentPage is PageClipDownload clipDownloadPage)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        try
                        {
                            TwitchHelper.CreateDirectory(folderPath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);

                            if (Settings.Default.VerboseErrors)
                            {
                                MessageBox.Show(this, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                            }

                            return;
                        }
                    }

                    ClipDownloadOptions downloadOptions = new ClipDownloadOptions
                    {
                        Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateClip, clipDownloadPage.textTitle.Text, clipDownloadPage.clipId,
                            clipDownloadPage.currentVideoTime, clipDownloadPage.textStreamer.Text, clipDownloadPage.streamerId, TimeSpan.Zero, clipDownloadPage.clipLength,
                            clipDownloadPage.clipLength, clipDownloadPage.viewCount, clipDownloadPage.game, clipDownloadPage.clipperName, clipDownloadPage.clipperId) + ".mp4"),
                        Id = clipDownloadPage.clipId,
                        Oauth = Settings.Default.OAuth,
                        Quality = clipDownloadPage.comboQuality.Text,
                        ThrottleKib = Settings.Default.DownloadThrottleEnabled
                            ? Settings.Default.MaximumBandwidthKib
                            : -1,
                        TempFolder = Settings.Default.TempPath,
                        EncodeMetadata = clipDownloadPage.CheckMetadata.IsChecked!.Value,
                        FfmpegPath = "ffmpeg",
                        FileCollisionCallback = HandleFileCollisionCallback,
                    };

                    ClipDownloadTask downloadTask = new ClipDownloadTask
                    {
                        DownloadOptions = downloadOptions,
                        Info =
                        {
                            Title = clipDownloadPage.textTitle.Text,
                            Thumbnail = clipDownloadPage.imgThumbnail.Source
                        }
                    };

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(downloadTask);
                    }

                    if (checkChat.IsChecked.GetValueOrDefault())
                    {
                        ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                        chatOptions.Id = downloadOptions.Id;
                        if (radioJson.IsChecked == true)
                            chatOptions.DownloadFormat = ChatFormat.Json;
                        else if (radioHTML.IsChecked == true)
                            chatOptions.DownloadFormat = ChatFormat.Html;
                        else
                            chatOptions.DownloadFormat = ChatFormat.Text;
                        // TODO: Support non-json chat compression
                        if (RadioCompressionGzip.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                            chatOptions.Compression = ChatCompression.Gzip;
                        chatOptions.TimeFormat = TimestampFormat.Relative;
                        chatOptions.EmbedData = checkEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.BttvEmotes = CheckBttvEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.FfzEmotes = CheckFfzEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.StvEmotes = CheckStvEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.DelayDownload = checkDelayChat.IsChecked.GetValueOrDefault();
                        chatOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, downloadTask.Info.Title, chatOptions.Id,
                            clipDownloadPage.currentVideoTime, clipDownloadPage.textStreamer.Text, clipDownloadPage.streamerId, TimeSpan.Zero, clipDownloadPage.clipLength, clipDownloadPage.clipLength,
                            clipDownloadPage.viewCount, clipDownloadPage.game, clipDownloadPage.clipperName, clipDownloadPage.clipId) + chatOptions.FileExtension);
                        chatOptions.FileCollisionCallback = HandleFileCollisionCallback;

                        ChatDownloadTask chatTask = new ChatDownloadTask
                        {
                            DownloadOptions = chatOptions,
                            Info =
                            {
                                Title = clipDownloadPage.textTitle.Text,
                                Thumbnail = clipDownloadPage.imgThumbnail.Source
                            }
                        };

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(chatTask);
                        }

                        if (checkRender.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                        {
                            ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()));
                            if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                            {
                                //Just in case VOD and chat paths are the same. Like the previous defaults
                                renderOptions.OutputFile = Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), " - CHAT." + MainWindow.pageChatRender.comboFormat.Text.ToLower());
                            }
                            renderOptions.InputFile = chatOptions.Filename;
                            renderOptions.FileCollisionCallback = HandleFileCollisionCallback;

                            ChatRenderTask renderTask = new ChatRenderTask
                            {
                                DownloadOptions = renderOptions,
                                Info =
                                {
                                    Title = clipDownloadPage.textTitle.Text,
                                    Thumbnail = clipDownloadPage.imgThumbnail.Source
                                },
                                DependantTask = chatTask
                            };
                            renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                            lock (PageQueue.taskLock)
                            {
                                PageQueue.taskList.Add(renderTask);
                            }
                        }
                    }

                    this.Close();
                }

                if (_parentPage is PageChatDownload chatDownloadPage)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        try
                        {
                            TwitchHelper.CreateDirectory(folderPath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);

                            if (Settings.Default.VerboseErrors)
                            {
                                MessageBox.Show(this, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                            }

                            return;
                        }
                    }

                    ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                    chatOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, chatDownloadPage.textTitle.Text, chatOptions.Id,chatDownloadPage.currentVideoTime, chatDownloadPage.textStreamer.Text,
                        chatDownloadPage.streamerId,
                        chatOptions.TrimBeginning ? TimeSpan.FromSeconds(chatOptions.TrimBeginningTime) : TimeSpan.Zero,
                        chatOptions.TrimEnding ? TimeSpan.FromSeconds(chatOptions.TrimEndingTime) : chatDownloadPage.vodLength,
                        chatDownloadPage.vodLength, chatDownloadPage.viewCount, chatDownloadPage.game) + chatOptions.FileExtension);
                    chatOptions.DelayDownload = checkDelayChat.IsChecked.GetValueOrDefault();
                    chatOptions.FileCollisionCallback = HandleFileCollisionCallback;

                    ChatDownloadTask chatTask = new ChatDownloadTask
                    {
                        DownloadOptions = chatOptions,
                        Info =
                        {
                            Title = chatDownloadPage.textTitle.Text,
                            Thumbnail = chatDownloadPage.imgThumbnail.Source
                        }
                    };

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(chatTask);
                    }

                    if (checkRender.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                    {
                        ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()));
                        renderOptions.InputFile = chatOptions.Filename;
                        renderOptions.FileCollisionCallback = HandleFileCollisionCallback;

                        ChatRenderTask renderTask = new ChatRenderTask
                        {
                            DownloadOptions = renderOptions,
                            Info =
                            {
                                Title = chatDownloadPage.textTitle.Text,
                                Thumbnail = chatDownloadPage.imgThumbnail.Source
                            },
                            DependantTask = chatTask
                        };
                        renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(renderTask);
                        }
                    }

                    this.Close();
                }

                if (_parentPage is PageChatUpdate chatUpdatePage)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        try
                        {
                            TwitchHelper.CreateDirectory(folderPath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);

                            if (Settings.Default.VerboseErrors)
                            {
                                MessageBox.Show(this, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                            }

                            return;
                        }
                    }

                    ChatUpdateOptions chatOptions = MainWindow.pageChatUpdate.GetOptions(null);
                    chatOptions.InputFile = chatUpdatePage.InputFile;
                    chatOptions.OutputFile = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, chatUpdatePage.textTitle.Text, chatUpdatePage.VideoId, chatUpdatePage.VideoCreatedAt, chatUpdatePage.textStreamer.Text,
                        chatUpdatePage.StreamerId,
                        chatOptions.TrimBeginning ? TimeSpan.FromSeconds(chatOptions.TrimBeginningTime) : TimeSpan.Zero,
                        chatOptions.TrimEnding ? TimeSpan.FromSeconds(chatOptions.TrimEndingTime) : chatUpdatePage.VideoLength,
                        chatUpdatePage.VideoLength, chatUpdatePage.ViewCount, chatUpdatePage.Game, chatUpdatePage.ClipperName, chatUpdatePage.ClipperId) + chatOptions.FileExtension);
                    chatOptions.FileCollisionCallback = HandleFileCollisionCallback;

                    ChatUpdateTask chatTask = new ChatUpdateTask
                    {
                        UpdateOptions = chatOptions,
                        Info =
                        {
                            Title = chatUpdatePage.textTitle.Text,
                            Thumbnail = chatUpdatePage.imgThumbnail.Source
                        }
                    };

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(chatTask);
                    }

                    this.Close();
                }

                if (_parentPage is PageChatRender chatRenderPage)
                {
                    string folderPath = textFolder.Text;
                    foreach (string fileName in chatRenderPage.FileNames)
                    {
                        if (!Directory.Exists(folderPath))
                        {
                            try
                            {
                                TwitchHelper.CreateDirectory(folderPath);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);

                                if (Settings.Default.VerboseErrors)
                                {
                                    MessageBox.Show(this, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                                }

                                return;
                            }
                        }

                        string fileFormat = chatRenderPage.comboFormat.SelectedItem.ToString()!;
                        string filePath = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(fileName) + "." + fileFormat.ToLower());
                        ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(filePath);
                        renderOptions.InputFile = fileName;
                        renderOptions.FileCollisionCallback = HandleFileCollisionCallback;

                        ChatRenderTask renderTask = new ChatRenderTask
                        {
                            DownloadOptions = renderOptions,
                            Info =
                            {
                                Title = Path.GetFileNameWithoutExtension(filePath)
                            }
                        };

                        if (ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out var image))
                        {
                            renderTask.Info.Thumbnail = image;
                        }

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(renderTask);
                        }

                        this.Close();
                    }
                }
            }
            else if (_dataList.Count > 0)
            {
                await EnqueueDataList();
            }
        }

        private async Task EnqueueDataList()
        {
            string folderPath = textFolder.Text;
            if (!Directory.Exists(folderPath))
            {
                try
                {
                    TwitchHelper.CreateDirectory(folderPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, Translations.Strings.InvalidFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);

                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(this, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    return;
                }
            }

            foreach (var taskData in _dataList)
            {
                if (checkVideo.IsChecked.GetValueOrDefault())
                {
                    if (taskData.Id.All(char.IsDigit))
                    {
                        long vodId = long.Parse(taskData.Id);
                        string quality = (ComboPreferredQuality.SelectedItem as ComboBoxItem)?.Content as string;
                        string ext = FilenameService.GuessVodFileExtension(quality);
                        var vodLength = TimeSpan.FromSeconds(taskData.Length);

                        if (checkSplitByChapters.IsChecked == true && !checkLive.IsChecked.GetValueOrDefault())
                        {
                            try
                            {
                                var videoInfo = new TwitchDownloaderCore.TwitchObjects.Gql.VideoInfo
                                {
                                    lengthSeconds = taskData.Length,
                                    game = new TwitchDownloaderCore.TwitchObjects.Gql.Game { displayName = taskData.Game }
                                };
                                var chapterResponse = await TwitchHelper.GetOrGenerateVideoChapters(vodId, videoInfo);
                                var chapters = chapterResponse.data.video.moments.edges;

                                lock (PageQueue.taskLock)
                                {
                                    for (int i = 0; i < chapters.Count; i++)
                                    {
                                        var chapter = chapters[i].node;
                                        var startSec = chapter.positionMilliseconds / 1000;
                                        var endSec = startSec + chapter.durationMilliseconds / 1000;
                                        var chapterStart = TimeSpan.FromSeconds(startSec);
                                        var chapterEnd = TimeSpan.FromSeconds(Math.Min(endSec, taskData.Length));
                                        var gameName = chapter.details?.game?.displayName ?? chapter.description ?? taskData.Game;

                                        var options = new VideoDownloadOptions
                                        {
                                            Oauth = Settings.Default.OAuth,
                                            TempFolder = Settings.Default.TempPath,
                                            Id = vodId,
                                            Quality = quality,
                                            FfmpegPath = "ffmpeg",
                                            TrimBeginning = true,
                                            TrimBeginningTime = chapterStart,
                                            TrimEnding = true,
                                            TrimEndingTime = chapterEnd,
                                            TrimMode = VideoTrimMode.Safe,
                                            DownloadThreads = Settings.Default.VodDownloadThreads,
                                            ThrottleKib = Settings.Default.DownloadThrottleEnabled ? Settings.Default.MaximumBandwidthKib : -1,
                                            FileCollisionCallback = HandleFileCollisionCallback,
                                            DelayDownload = checkDelay.IsChecked.GetValueOrDefault(),
                                            Filename = Path.Combine(folderPath,
                                                FilenameService.GetFilename(Settings.Default.TemplateVod, taskData.Title, taskData.Id, taskData.Time, taskData.StreamerName, taskData.StreamerId,
                                                    chapterStart, chapterEnd, vodLength, taskData.Views, gameName)
                                                + $"_ch{i + 1:D2}" + ext)
                                        };

                                        PageQueue.taskList.Add(new VodDownloadTask
                                        {
                                            DownloadOptions = options,
                                            Info = { Title = $"{taskData.Title} — {gameName} (Ch. {i + 1})", Thumbnail = taskData.Thumbnail }
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, $"Failed to fetch chapters for {taskData.Title}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            continue;
                        }

                        VideoDownloadOptions downloadOptions = new VideoDownloadOptions
                        {
                            Oauth = Settings.Default.OAuth,
                            TempFolder = Settings.Default.TempPath,
                            Id = vodId,
                            Quality = quality,
                            FfmpegPath = "ffmpeg",
                            TrimBeginning = false,
                            TrimEnding = false,
                            DownloadThreads = Settings.Default.VodDownloadThreads,
                            ThrottleKib = Settings.Default.DownloadThrottleEnabled
                                ? Settings.Default.MaximumBandwidthKib
                                : -1,
                            FileCollisionCallback = HandleFileCollisionCallback,
                            DelayDownload = checkDelay.IsChecked.GetValueOrDefault()
                        };
                        downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateVod, taskData.Title, taskData.Id, taskData.Time, taskData.StreamerName, taskData.StreamerId,
                            downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : TimeSpan.Zero,
                            downloadOptions.TrimEnding ? downloadOptions.TrimEndingTime : vodLength,
                            vodLength, taskData.Views, taskData.Game) + ext);

                        if (checkLive.IsChecked.GetValueOrDefault())
                        {
                            var liveOptions = new LiveStreamDownloadOptions
                            {
                                Id = downloadOptions.Id,
                                Quality = downloadOptions.Quality,
                                Filename = downloadOptions.Filename,
                                Oauth = Settings.Default.OAuth,
                                FfmpegPath = "ffmpeg",
                                DownloadThreads = downloadOptions.DownloadThreads,
                                ThrottleKib = downloadOptions.ThrottleKib,
                                TempFolder = downloadOptions.TempFolder,
                            };

                            var liveTask = new LiveStreamDownloadTask
                            {
                                DownloadOptions = liveOptions,
                                Info =
                                {
                                    Title = taskData.Title,
                                    Thumbnail = taskData.Thumbnail
                                }
                            };

                            lock (PageQueue.taskLock)
                            {
                                PageQueue.taskList.Add(liveTask);
                            }
                        }
                        else
                        {
                            VodDownloadTask downloadTask = new VodDownloadTask
                            {
                                DownloadOptions = downloadOptions,
                                Info =
                                {
                                    Title = taskData.Title,
                                    Thumbnail = taskData.Thumbnail
                                }
                            };

                            lock (PageQueue.taskLock)
                            {
                                PageQueue.taskList.Add(downloadTask);
                            }
                        }
                    }
                    else
                    {
                        ClipDownloadOptions downloadOptions = new ClipDownloadOptions
                        {
                            Id = taskData.Id,
                            Oauth = Settings.Default.OAuth,
                            Quality = (ComboPreferredQuality.SelectedItem as ComboBoxItem)?.Content as string,
                            Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateClip, taskData.Title, taskData.Id, taskData.Time, taskData.StreamerName, taskData.StreamerId,
                                TimeSpan.Zero, TimeSpan.FromSeconds(taskData.Length), TimeSpan.FromSeconds(taskData.Length), taskData.Views, taskData.Game, taskData.ClipperName, taskData.ClipperId) + ".mp4"),
                            ThrottleKib = Settings.Default.DownloadThrottleEnabled
                                ? Settings.Default.MaximumBandwidthKib
                                : -1,
                            TempFolder = Settings.Default.TempPath,
                            EncodeMetadata = Settings.Default.EncodeClipMetadata,
                            FfmpegPath = "ffmpeg",
                            FileCollisionCallback = HandleFileCollisionCallback,
                        };

                        ClipDownloadTask downloadTask = new ClipDownloadTask
                        {
                            DownloadOptions = downloadOptions,
                            Info =
                            {
                                Title = taskData.Title,
                                Thumbnail = taskData.Thumbnail
                            }
                        };

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(downloadTask);
                        }
                    }
                }

                if (checkChat.IsChecked.GetValueOrDefault())
                {
                    ChatDownloadOptions downloadOptions = new ChatDownloadOptions
                    {
                        EmbedData = checkEmbed.IsChecked.GetValueOrDefault(),
                        BttvEmotes = CheckBttvEmbed.IsChecked.GetValueOrDefault(),
                        FfzEmotes = CheckFfzEmbed.IsChecked.GetValueOrDefault(),
                        StvEmotes = CheckStvEmbed.IsChecked.GetValueOrDefault(),
                        TimeFormat = TimestampFormat.Relative,
                        Id = taskData.Id,
                        TrimBeginning = false,
                        TrimEnding = false,
                        FileCollisionCallback = HandleFileCollisionCallback,
                        DelayDownload = checkDelayChat.IsChecked.GetValueOrDefault()
                    };
                    if (radioJson.IsChecked == true)
                        downloadOptions.DownloadFormat = ChatFormat.Json;
                    else if (radioHTML.IsChecked == true)
                        downloadOptions.DownloadFormat = ChatFormat.Html;
                    else
                        downloadOptions.DownloadFormat = ChatFormat.Text;
                    // TODO: Support non-json chat compression
                    if (RadioCompressionGzip.IsChecked.GetValueOrDefault() && downloadOptions.DownloadFormat == ChatFormat.Json)
                        downloadOptions.Compression = ChatCompression.Gzip;
                    downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, taskData.Title, taskData.Id, taskData.Time, taskData.StreamerName, taskData.StreamerId,
                        downloadOptions.TrimBeginning ? TimeSpan.FromSeconds(downloadOptions.TrimBeginningTime) : TimeSpan.Zero,
                        downloadOptions.TrimEnding ? TimeSpan.FromSeconds(downloadOptions.TrimEndingTime) : TimeSpan.FromSeconds(taskData.Length),
                        TimeSpan.FromSeconds(taskData.Length), taskData.Views, taskData.Game, taskData.ClipperName, taskData.ClipperId) + downloadOptions.FileExtension);

                    ChatDownloadTask downloadTask = new ChatDownloadTask
                    {
                        DownloadOptions = downloadOptions,
                        Info =
                        {
                            Title = taskData.Title,
                            Thumbnail = taskData.Thumbnail
                        }
                    };

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(downloadTask);
                    }

                    if (checkRender.IsChecked.GetValueOrDefault() && downloadOptions.DownloadFormat == ChatFormat.Json)
                    {
                        ChatRenderOptions renderOptions =
                            MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(downloadOptions.Filename.Replace(".gz", ""), '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()));
                        if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                        {
                            //Just in case VOD and chat paths are the same. Like the previous defaults
                            renderOptions.OutputFile = Path.ChangeExtension(downloadOptions.Filename.Replace(".gz", ""), " - CHAT." + MainWindow.pageChatRender.comboFormat.Text.ToLower());
                        }
                        renderOptions.InputFile = downloadOptions.Filename;
                        renderOptions.FileCollisionCallback = HandleFileCollisionCallback;

                        ChatRenderTask renderTask = new ChatRenderTask
                        {
                            DownloadOptions = renderOptions,
                            Info =
                            {
                                Title = taskData.Title,
                                Thumbnail = taskData.Thumbnail
                            },
                            DependantTask = downloadTask
                        };
                        renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(renderTask);
                        }
                    }
                }
            }

            this.DialogResult = true;
            this.Close();
        }

        private void btnFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (Directory.Exists(textFolder.Text))
            {
                dialog.InitialDirectory = textFolder.Text;
            }

            if (dialog.ShowDialog(this).GetValueOrDefault())
            {
                textFolder.Text = dialog.FolderName;
            }
        }

        private void TextFolder_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.QueueFolder = textFolder.Text;
            Settings.Default.Save();
        }

        private void checkChat_Checked(object sender, RoutedEventArgs e)
        {
            checkRender.IsEnabled = true;
            radioJson.IsEnabled = true;
            radioTxt.IsEnabled = true;
            radioHTML.IsEnabled = true;
            checkEmbed.IsEnabled = true;
            CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = checkEmbed.IsChecked.GetValueOrDefault();
            checkDelayChat.IsEnabled = true;
            RadioCompressionNone.IsEnabled = true;
            RadioCompressionGzip.IsEnabled = true;
            try
            {
                var appTextBrush = (Brush)Application.Current.Resources["AppText"];
                TextDownloadFormat.Foreground = appTextBrush;
                TextCompression.Foreground = appTextBrush;
            }
            catch { /* Ignored */ }
        }

        private void checkChat_Unchecked(object sender, RoutedEventArgs e)
        {
            checkRender.IsEnabled = false;
            checkRender.IsChecked = false;
            radioJson.IsEnabled = false;
            radioTxt.IsEnabled = false;
            radioHTML.IsEnabled = false;
            checkEmbed.IsEnabled = false;
            CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = false;
            checkDelayChat.IsEnabled = false;
            RadioCompressionNone.IsEnabled = false;
            RadioCompressionGzip.IsEnabled = false;
            try
            {
                var appTextDisabledBrush = (Brush)Application.Current.Resources["AppTextDisabled"];
                TextDownloadFormat.Foreground = appTextDisabledBrush;
                TextCompression.Foreground = appTextDisabledBrush;
            }
            catch { /* Ignored */ }
        }

        private void radioJson_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                checkEmbed.IsEnabled = true;
                CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = checkEmbed.IsChecked.GetValueOrDefault();
                checkRender.IsEnabled = true;
                StackChatCompression.Visibility = Visibility.Visible;
            }
        }

        private void radioTxt_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                checkEmbed.IsEnabled = false;
                CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = false;
                checkRender.IsEnabled = false;
                StackChatCompression.Visibility = Visibility.Collapsed;
            }
        }

        private void radioHTML_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                checkEmbed.IsEnabled = true;
                CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = checkEmbed.IsChecked.GetValueOrDefault();
                checkRender.IsEnabled = false;
                StackChatCompression.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_OnSourceInitialized(object sender, EventArgs e)
        {
            App.RequestTitleBarChange();
        }

        private void WindowQueueOptions_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void checkDelay_Checked(object sender, RoutedEventArgs e)
        {
            if (IsInitialized && checkLive.IsChecked.GetValueOrDefault())
                checkLive.IsChecked = false;
        }

        private void checkLive_Checked(object sender, RoutedEventArgs e)
        {
            if (IsInitialized && checkDelay.IsChecked.GetValueOrDefault())
                checkDelay.IsChecked = false;
        }

        private void CheckVideo_OnChecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                ComboPreferredQuality.IsEnabled = checkVideo.IsChecked.GetValueOrDefault();
                checkDelay.IsEnabled = checkVideo.IsChecked.GetValueOrDefault();
                checkLive.IsEnabled = checkVideo.IsChecked.GetValueOrDefault();
                try
                {
                    var newBrush = (Brush)Application.Current.Resources[checkVideo.IsChecked.GetValueOrDefault() ? "AppText" : "AppTextDisabled"];
                    TextPreferredQuality.Foreground = newBrush;
                }
                catch { /* Ignored */ }
            }
        }

        private void ComboPreferredQuality_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            if (ComboPreferredQuality.SelectedItem is ComboBoxItem { Content: string preferredQuality })
            {
                Settings.Default.PreferredQuality = preferredQuality;
            }
        }

        private void CheckEmbed_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            bool embedEnabled = checkEmbed.IsChecked.GetValueOrDefault();
            CheckBttvEmbed.IsEnabled = CheckFfzEmbed.IsEnabled = CheckStvEmbed.IsEnabled = embedEnabled;
            CheckBttvEmbed.IsChecked = CheckFfzEmbed.IsChecked = CheckStvEmbed.IsChecked = embedEnabled;
        }

        private void LoadPresets()
        {
            EnqueuePreset presetToApply = null;
            _applyingPreset = true;
            try
            {
                var presets = EnqueuePresetService.Load();
                comboPresets.ItemsSource = presets;
                comboPresets.SelectedIndex = -1;
                var lastName = Settings.Default.LastEnqueuePresetName;
                if (!string.IsNullOrEmpty(lastName))
                {
                    var idx = presets.FindIndex(p => p.Name == lastName);
                    if (idx >= 0)
                    {
                        comboPresets.SelectedIndex = idx;
                        presetToApply = presets[idx];
                    }
                }
            }
            finally
            {
                _applyingPreset = false;
            }

            if (presetToApply != null)
                ApplyPreset(presetToApply);
        }

        private void ComboPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_applyingPreset || !IsInitialized)
                return;

            if (comboPresets.SelectedItem is EnqueuePreset preset)
            {
                ApplyPreset(preset);
                Settings.Default.LastEnqueuePresetName = preset.Name;
                Settings.Default.Save();
            }
        }

        private void ApplyPreset(EnqueuePreset preset)
        {
            _applyingPreset = true;
            try
            {
                if (preset.Folder != null)
                    textFolder.Text = preset.Folder;

                if (preset.Quality != null && ComboPreferredQuality.Visibility == Visibility.Visible)
                {
                    for (int i = 0; i < ComboPreferredQuality.Items.Count; i++)
                    {
                        if (ComboPreferredQuality.Items[i] is ComboBoxItem { Content: string q } && q == preset.Quality)
                        {
                            ComboPreferredQuality.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (checkDelay.Visibility == Visibility.Visible && checkDelay.IsEnabled)
                    checkDelay.IsChecked = preset.DelayDownload;

                if (checkLive.Visibility == Visibility.Visible && checkLive.IsEnabled)
                    checkLive.IsChecked = preset.LiveDownload;

                if (checkChat.Visibility == Visibility.Visible && checkChat.IsEnabled)
                    checkChat.IsChecked = preset.DownloadChat;

                if (radioJson.Visibility == Visibility.Visible)
                {
                    radioJson.IsChecked = preset.ChatFormat == "JSON";
                    radioTxt.IsChecked = preset.ChatFormat == "TXT";
                    radioHTML.IsChecked = preset.ChatFormat == "HTML";
                }

                if (RadioCompressionGzip.Visibility == Visibility.Visible)
                    RadioCompressionGzip.IsChecked = preset.ChatCompressGzip;

                if (checkEmbed.Visibility == Visibility.Visible)
                    checkEmbed.IsChecked = preset.EmbedImages;

                if (CheckBttvEmbed.Visibility == Visibility.Visible)
                    CheckBttvEmbed.IsChecked = preset.EmbedBttv;

                if (CheckFfzEmbed.Visibility == Visibility.Visible)
                    CheckFfzEmbed.IsChecked = preset.EmbedFfz;

                if (CheckStvEmbed.Visibility == Visibility.Visible)
                    CheckStvEmbed.IsChecked = preset.EmbedStv;

                if (checkDelayChat.Visibility == Visibility.Visible && checkDelayChat.IsEnabled)
                    checkDelayChat.IsChecked = preset.DelayChat;

                if (checkRender.Visibility == Visibility.Visible && checkRender.IsEnabled)
                    checkRender.IsChecked = preset.RenderChat;
            }
            finally
            {
                _applyingPreset = false;
            }
        }

        private EnqueuePreset GetCurrentPreset()
        {
            return new EnqueuePreset
            {
                Folder = textFolder.Text,
                Quality = (ComboPreferredQuality.SelectedItem as ComboBoxItem)?.Content as string,
                DelayDownload = checkDelay.IsChecked.GetValueOrDefault(),
                LiveDownload = checkLive.IsChecked.GetValueOrDefault(),
                DownloadChat = checkChat.IsChecked.GetValueOrDefault(),
                ChatFormat = radioTxt.IsChecked == true ? "TXT" : radioHTML.IsChecked == true ? "HTML" : "JSON",
                ChatCompressGzip = RadioCompressionGzip.IsChecked.GetValueOrDefault(),
                EmbedImages = checkEmbed.IsChecked.GetValueOrDefault(),
                EmbedBttv = CheckBttvEmbed.IsChecked.GetValueOrDefault(),
                EmbedFfz = CheckFfzEmbed.IsChecked.GetValueOrDefault(),
                EmbedStv = CheckStvEmbed.IsChecked.GetValueOrDefault(),
                DelayChat = checkDelayChat.IsChecked.GetValueOrDefault(),
                RenderChat = checkRender.IsChecked.GetValueOrDefault(),
            };
        }

        private void BtnSavePreset_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WindowInputText("Save Preset", "Enter a name for this preset:", (comboPresets.SelectedItem as EnqueuePreset)?.Name ?? "")
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputValue))
                return;

            var preset = GetCurrentPreset();
            preset.Name = dialog.InputValue;
            EnqueuePresetService.AddOrUpdate(preset);

            LoadPresets();

            // Re-select the saved preset
            var presets = EnqueuePresetService.Load();
            var savedIndex = presets.FindIndex(p => p.Name == preset.Name);
            if (savedIndex >= 0)
            {
                _applyingPreset = true;
                comboPresets.SelectedIndex = savedIndex;
                _applyingPreset = false;
            }
        }

        private void BtnDeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (comboPresets.SelectedItem is not EnqueuePreset preset)
                return;

            var result = MessageBox.Show(this, $"Delete preset '{preset.Name}'?", "Delete Preset", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            EnqueuePresetService.Delete(preset.Name);
            LoadPresets();
        }
    }
}