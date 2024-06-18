using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
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

        public WindowQueueOptions(Page page)
        {
            _parentPage = page;
            InitializeComponent();

            string queueFolder = Settings.Default.QueueFolder;
            if (Directory.Exists(queueFolder))
                textFolder.Text = queueFolder;

            TextPreferredQuality.Visibility = Visibility.Collapsed;
            ComboPreferredQuality.Visibility = Visibility.Collapsed;

            if (page is PageVodDownload or PageClipDownload)
            {
                checkVideo.IsChecked = true;
                checkVideo.IsEnabled = false;
            }
            else if (page is PageChatDownload chatPage)
            {
                checkVideo.Visibility = Visibility.Collapsed;
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
                if (!chatPage.radioJson.IsChecked.GetValueOrDefault())
                {
                    checkRender.IsChecked = false;
                    checkRender.IsEnabled = false;
                }
            }
            else if (page is PageChatUpdate)
            {
                checkVideo.Visibility = Visibility.Collapsed;
                checkChat.Visibility = Visibility.Collapsed;
                TextDownloadFormat.Visibility = Visibility.Collapsed;
                radioJson.Visibility = Visibility.Collapsed;
                radioTxt.Visibility = Visibility.Collapsed;
                radioHTML.Visibility = Visibility.Collapsed;
                TextCompression.Visibility = Visibility.Collapsed;
                RadioCompressionNone.Visibility = Visibility.Collapsed;
                RadioCompressionGzip.Visibility = Visibility.Collapsed;
                checkEmbed.Visibility = Visibility.Collapsed;
                checkRender.Visibility = Visibility.Collapsed;
            }
            else if (page is PageChatRender)
            {
                checkVideo.Visibility = Visibility.Collapsed;
                checkChat.Visibility = Visibility.Collapsed;
                TextDownloadFormat.Visibility = Visibility.Collapsed;
                radioJson.Visibility = Visibility.Collapsed;
                radioTxt.Visibility = Visibility.Collapsed;
                radioHTML.Visibility = Visibility.Collapsed;
                TextCompression.Visibility = Visibility.Collapsed;
                RadioCompressionNone.Visibility = Visibility.Collapsed;
                RadioCompressionGzip.Visibility = Visibility.Collapsed;
                checkEmbed.Visibility = Visibility.Collapsed;
                checkRender.IsChecked = true;
                checkRender.IsEnabled = false;
            }
        }

        public WindowQueueOptions(List<TaskData> dataList)
        {
            _dataList = dataList;
            InitializeComponent();

            string queueFolder = Settings.Default.QueueFolder;
            if (Directory.Exists(queueFolder))
                textFolder.Text = queueFolder;
        }

        private FileInfo HandleFileCollisionCallback(FileInfo fileInfo)
        {
            return Dispatcher.Invoke(() => FileCollisionService.HandleCollisionCallback(fileInfo, Application.Current.MainWindow));
        }

        private void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_parentPage != null)
            {
                if (_parentPage is PageVodDownload vodDownloadPage)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        MessageBox.Show(this, Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    VideoDownloadOptions downloadOptions = vodDownloadPage.GetOptions(null, textFolder.Text);
                    downloadOptions.FileCollisionCallback = HandleFileCollisionCallback;

                    VodDownloadTask downloadTask = new VodDownloadTask
                    {
                        DownloadOptions = downloadOptions,
                        Info =
                        {
                            Title = vodDownloadPage.textTitle.Text,
                            Thumbnail = vodDownloadPage.imgThumbnail.Source
                        }
                    };
                    downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(downloadTask);
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
                        chatOptions.EmbedData = checkEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.Filename = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(downloadOptions.Filename) + "." + chatOptions.DownloadFormat);
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
                        chatTask.ChangeStatus(TwitchTaskStatus.Ready);

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
                                }
                            };
                            renderTask.ChangeStatus(TwitchTaskStatus.Waiting);
                            renderTask.DependantTask = chatTask;

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
                        MessageBox.Show(this, Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ClipDownloadOptions downloadOptions = new ClipDownloadOptions
                    {
                        Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateClip, clipDownloadPage.textTitle.Text, clipDownloadPage.clipId,
                            clipDownloadPage.currentVideoTime, clipDownloadPage.textStreamer.Text, TimeSpan.Zero, clipDownloadPage.clipLength,
                            clipDownloadPage.viewCount, clipDownloadPage.game) + ".mp4"),
                        Id = clipDownloadPage.clipId,
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
                    downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

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
                        chatOptions.TimeFormat = TimestampFormat.Relative;
                        chatOptions.EmbedData = checkEmbed.IsChecked.GetValueOrDefault();
                        chatOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, downloadTask.Info.Title, chatOptions.Id,
                            clipDownloadPage.currentVideoTime, clipDownloadPage.textStreamer.Text, TimeSpan.Zero, clipDownloadPage.clipLength,
                            clipDownloadPage.viewCount, clipDownloadPage.game) + "." + chatOptions.FileExtension);
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
                        chatTask.ChangeStatus(TwitchTaskStatus.Ready);

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
                        MessageBox.Show(this, Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                    chatOptions.Id = chatDownloadPage.downloadId;
                    chatOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, chatDownloadPage.textTitle.Text, chatOptions.Id,chatDownloadPage.currentVideoTime, chatDownloadPage.textStreamer.Text,
                        chatOptions.TrimBeginning ? TimeSpan.FromSeconds(chatOptions.TrimBeginningTime) : TimeSpan.Zero,
                        chatOptions.TrimEnding ? TimeSpan.FromSeconds(chatOptions.TrimEndingTime) : chatDownloadPage.vodLength,
                        chatDownloadPage.viewCount, chatDownloadPage.game) + "." + chatOptions.FileExtension);
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
                    chatTask.ChangeStatus(TwitchTaskStatus.Ready);

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
                        MessageBox.Show(this, Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ChatUpdateOptions chatOptions = MainWindow.pageChatUpdate.GetOptions(null);
                    chatOptions.InputFile = chatUpdatePage.InputFile;
                    chatOptions.OutputFile = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, chatUpdatePage.textTitle.Text, chatUpdatePage.VideoId, chatUpdatePage.VideoCreatedAt, chatUpdatePage.textStreamer.Text,
                        chatOptions.TrimBeginning ? TimeSpan.FromSeconds(chatOptions.TrimBeginningTime) : TimeSpan.Zero,
                        chatOptions.TrimEnding ? TimeSpan.FromSeconds(chatOptions.TrimEndingTime) : chatUpdatePage.VideoLength,
                        chatUpdatePage.ViewCount, chatUpdatePage.Game) + "." + chatOptions.FileExtension);
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
                    chatTask.ChangeStatus(TwitchTaskStatus.Ready);

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
                            MessageBox.Show(this, Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
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
                        renderTask.ChangeStatus(TwitchTaskStatus.Ready);

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
                EnqueueDataList();
            }
        }

        private void EnqueueDataList()
        {
            string folderPath = textFolder.Text;
            if (!Directory.Exists(folderPath))
            {
                MessageBox.Show(this, Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (var taskData in _dataList)
            {
                if (checkVideo.IsChecked.GetValueOrDefault())
                {
                    if (taskData.Id.All(char.IsDigit))
                    {
                        VideoDownloadOptions downloadOptions = new VideoDownloadOptions
                        {
                            Oauth = Settings.Default.OAuth,
                            TempFolder = Settings.Default.TempPath,
                            Id = long.Parse(taskData.Id),
                            Quality = (ComboPreferredQuality.SelectedItem as ComboBoxItem)?.Content as string,
                            FfmpegPath = "ffmpeg",
                            TrimBeginning = false,
                            TrimEnding = false,
                            DownloadThreads = Settings.Default.VodDownloadThreads,
                            ThrottleKib = Settings.Default.DownloadThrottleEnabled
                                ? Settings.Default.MaximumBandwidthKib
                                : -1,
                            FileCollisionCallback = HandleFileCollisionCallback,
                        };
                        downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateVod, taskData.Title, taskData.Id, taskData.Time, taskData.Streamer,
                            downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : TimeSpan.Zero,
                            downloadOptions.TrimEnding ? downloadOptions.TrimEndingTime : TimeSpan.FromSeconds(taskData.Length),
                            taskData.Views, taskData.Game) + ".mp4");

                        VodDownloadTask downloadTask = new VodDownloadTask
                        {
                            DownloadOptions = downloadOptions,
                            Info =
                            {
                                Title = taskData.Title,
                                Thumbnail = taskData.Thumbnail
                            }
                        };
                        downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(downloadTask);
                        }
                    }
                    else
                    {
                        ClipDownloadOptions downloadOptions = new ClipDownloadOptions
                        {
                            Id = taskData.Id,
                            Quality = (ComboPreferredQuality.SelectedItem as ComboBoxItem)?.Content as string,
                            Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateClip, taskData.Title, taskData.Id, taskData.Time, taskData.Streamer,
                                TimeSpan.Zero, TimeSpan.FromSeconds(taskData.Length), taskData.Views, taskData.Game) + ".mp4"),
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
                        downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

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
                        Compression = RadioCompressionNone.IsChecked.GetValueOrDefault() ? ChatCompression.None : ChatCompression.Gzip,
                        EmbedData = checkEmbed.IsChecked.GetValueOrDefault(),
                        TimeFormat = TimestampFormat.Relative,
                        Id = taskData.Id,
                        TrimBeginning = false,
                        TrimEnding = false,
                        FileCollisionCallback = HandleFileCollisionCallback,
                    };
                    if (radioJson.IsChecked == true)
                        downloadOptions.DownloadFormat = ChatFormat.Json;
                    else if (radioHTML.IsChecked == true)
                        downloadOptions.DownloadFormat = ChatFormat.Html;
                    else
                        downloadOptions.DownloadFormat = ChatFormat.Text;
                    downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, taskData.Title, taskData.Id, taskData.Time, taskData.Streamer,
                        downloadOptions.TrimBeginning ? TimeSpan.FromSeconds(downloadOptions.TrimBeginningTime) : TimeSpan.Zero,
                        downloadOptions.TrimEnding ? TimeSpan.FromSeconds(downloadOptions.TrimEndingTime) : TimeSpan.FromSeconds(taskData.Length),
                        taskData.Views, taskData.Game) + "." + downloadOptions.FileExtension);

                    ChatDownloadTask downloadTask = new ChatDownloadTask
                    {
                        DownloadOptions = downloadOptions,
                        Info =
                        {
                            Title = taskData.Title,
                            Thumbnail = taskData.Thumbnail
                        }
                    };
                    downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

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
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (Directory.Exists(textFolder.Text))
                dialog.RootFolder = dialog.RootFolder;
            if (dialog.ShowDialog(this).GetValueOrDefault())
            {
                textFolder.Text = dialog.SelectedPath;
                Settings.Default.QueueFolder = textFolder.Text;
                Settings.Default.Save();
            }
        }

        private void checkChat_Checked(object sender, RoutedEventArgs e)
        {
            checkRender.IsEnabled = true;
            radioJson.IsEnabled = true;
            radioTxt.IsEnabled = true;
            radioHTML.IsEnabled = true;
            checkEmbed.IsEnabled = true;
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
                checkRender.IsEnabled = true;
                StackChatCompression.Visibility = Visibility.Visible;
            }
        }

        private void radioTxt_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                checkEmbed.IsEnabled = false;
                checkRender.IsEnabled = false;
                StackChatCompression.Visibility = Visibility.Collapsed;
            }
        }

        private void radioHTML_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                checkEmbed.IsEnabled = true;
                checkRender.IsEnabled = false;
                StackChatCompression.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_OnSourceInitialized(object sender, EventArgs e)
        {
            App.RequestTitleBarChange();
        }

        private void CheckVideo_OnChecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                ComboPreferredQuality.IsEnabled = checkVideo.IsChecked.GetValueOrDefault();
                try
                {
                    var newBrush = (Brush)Application.Current.Resources[checkVideo.IsChecked.GetValueOrDefault() ? "AppText" : "AppTextDisabled"];
                    TextPreferredQuality.Foreground = newBrush;
                }
                catch { /* Ignored */ }
            }
        }
    }
}