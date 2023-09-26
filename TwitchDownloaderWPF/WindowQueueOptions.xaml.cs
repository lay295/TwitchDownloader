using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
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

        private void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_parentPage != null)
            {
                if (_parentPage is PageVodDownload vodDownloadPage)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    VodDownloadTask downloadTask = new VodDownloadTask();
                    VideoDownloadOptions downloadOptions = vodDownloadPage.GetOptions(null, textFolder.Text);
                    downloadTask.DownloadOptions = downloadOptions;
                    downloadTask.Info.Title = vodDownloadPage.textTitle.Text;
                    downloadTask.Info.Thumbnail = vodDownloadPage.imgThumbnail.Source;
                    downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(downloadTask);
                    }

                    if (checkChat.IsChecked.GetValueOrDefault())
                    {
                        ChatDownloadTask chatTask = new ChatDownloadTask();
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

                        if (downloadOptions.CropBeginning)
                        {
                            chatOptions.CropBeginning = true;
                            chatOptions.CropBeginningTime = downloadOptions.CropBeginningTime;
                        }

                        if (downloadOptions.CropEnding)
                        {
                            chatOptions.CropEnding = true;
                            chatOptions.CropEndingTime = downloadOptions.CropEndingTime;
                        }

                        chatTask.DownloadOptions = chatOptions;
                        chatTask.Info.Title = vodDownloadPage.textTitle.Text;
                        chatTask.Info.Thumbnail = vodDownloadPage.imgThumbnail.Source;
                        chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(chatTask);
                        }

                        if (checkRender.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                        {
                            ChatRenderTask renderTask = new ChatRenderTask();
                            ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()));
                            if (renderOptions.OutputFile.Trim() == downloadOptions.Filename!.Trim())
                            {
                                //Just in case VOD and chat paths are the same. Like the previous defaults
                                renderOptions.OutputFile = Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), " - CHAT." + MainWindow.pageChatRender.comboFormat.Text.ToLower());
                            }
                            renderOptions.InputFile = chatOptions.Filename;
                            renderTask.DownloadOptions = renderOptions;
                            renderTask.Info.Title = vodDownloadPage.textTitle.Text;
                            renderTask.Info.Thumbnail = vodDownloadPage.imgThumbnail.Source;
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
                        MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ClipDownloadTask downloadTask = new ClipDownloadTask();
                    ClipDownloadOptions downloadOptions = new ClipDownloadOptions();
                    downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateClip, clipDownloadPage.textTitle.Text, clipDownloadPage.clipId, clipDownloadPage.currentVideoTime, clipDownloadPage.textStreamer.Text, TimeSpan.Zero, clipDownloadPage.clipLength, clipDownloadPage.viewCount.ToString(), clipDownloadPage.game) + ".mp4");
                    downloadOptions.Id = clipDownloadPage.clipId;
                    downloadOptions.Quality = clipDownloadPage.comboQuality.Text;
                    downloadOptions.ThrottleKib = Settings.Default.DownloadThrottleEnabled
                        ? Settings.Default.MaximumBandwidthKib
                        : -1;
                    downloadOptions.TempFolder = Settings.Default.TempPath;
                    downloadOptions.EncodeMetadata = clipDownloadPage.CheckMetadata.IsChecked!.Value;
                    downloadOptions.FfmpegPath = "ffmpeg";
                    downloadTask.DownloadOptions = downloadOptions;
                    downloadTask.Info.Title = clipDownloadPage.textTitle.Text;
                    downloadTask.Info.Thumbnail = clipDownloadPage.imgThumbnail.Source;
                    downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(downloadTask);
                    }

                    if (checkChat.IsChecked.GetValueOrDefault())
                    {
                        ChatDownloadTask chatTask = new ChatDownloadTask();
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
                        chatOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, downloadTask.Info.Title, chatOptions.Id, clipDownloadPage.currentVideoTime, clipDownloadPage.textStreamer.Text, TimeSpan.Zero, clipDownloadPage.clipLength, clipDownloadPage.viewCount.ToString(), clipDownloadPage.game) + "." + chatOptions.FileExtension);

                        chatTask.DownloadOptions = chatOptions;
                        chatTask.Info.Title = clipDownloadPage.textTitle.Text;
                        chatTask.Info.Thumbnail = clipDownloadPage.imgThumbnail.Source;
                        chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(chatTask);
                        }

                        if (checkRender.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json)
                        {
                            ChatRenderTask renderTask = new ChatRenderTask();
                            ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()));
                            if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                            {
                                //Just in case VOD and chat paths are the same. Like the previous defaults
                                renderOptions.OutputFile = Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), " - CHAT." + MainWindow.pageChatRender.comboFormat.Text.ToLower());
                            }
                            renderOptions.InputFile = chatOptions.Filename;
                            renderTask.DownloadOptions = renderOptions;
                            renderTask.Info.Title = clipDownloadPage.textTitle.Text;
                            renderTask.Info.Thumbnail = clipDownloadPage.imgThumbnail.Source;
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

                if (_parentPage is PageChatDownload chatDownloadPage)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ChatDownloadTask chatTask = new ChatDownloadTask();
                    ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                    chatOptions.Id = chatDownloadPage.downloadId;
                    chatOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, chatDownloadPage.textTitle.Text, chatOptions.Id, chatDownloadPage.currentVideoTime, chatDownloadPage.textStreamer.Text,
                        chatOptions.CropBeginning ? TimeSpan.FromSeconds(chatOptions.CropBeginningTime) : TimeSpan.Zero,
                        chatOptions.CropEnding ? TimeSpan.FromSeconds(chatOptions.CropEndingTime) : chatDownloadPage.vodLength,
                        chatDownloadPage.viewCount.ToString(), chatDownloadPage.game) + "." + chatOptions.FileExtension);

                    chatTask.DownloadOptions = chatOptions;
                    chatTask.Info.Title = chatDownloadPage.textTitle.Text;
                    chatTask.Info.Thumbnail = chatDownloadPage.imgThumbnail.Source;
                    chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(chatTask);
                    }

                    if ((bool)checkRender.IsChecked && chatOptions.DownloadFormat == ChatFormat.Json)
                    {
                        ChatRenderTask renderTask = new ChatRenderTask();
                        ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename.Replace(".gz", ""), '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()));
                        renderOptions.InputFile = chatOptions.Filename;
                        renderTask.DownloadOptions = renderOptions;
                        renderTask.Info.Title = chatDownloadPage.textTitle.Text;
                        renderTask.Info.Thumbnail = chatDownloadPage.imgThumbnail.Source;
                        renderTask.ChangeStatus(TwitchTaskStatus.Waiting);
                        renderTask.DependantTask = chatTask;

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
                        MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ChatUpdateTask chatTask = new ChatUpdateTask();
                    ChatUpdateOptions chatOptions = MainWindow.pageChatUpdate.GetOptions(null);
                    chatOptions.InputFile = chatUpdatePage.InputFile;
                    chatOptions.OutputFile = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, chatUpdatePage.textTitle.Text, chatUpdatePage.VideoId, chatUpdatePage.VideoCreatedAt, chatUpdatePage.textStreamer.Text,
                        chatOptions.CropBeginning ? TimeSpan.FromSeconds(chatOptions.CropBeginningTime) : TimeSpan.Zero,
                        chatOptions.CropEnding ? TimeSpan.FromSeconds(chatOptions.CropEndingTime) : chatUpdatePage.VideoLength,
                        chatUpdatePage.ViewCount.ToString(), chatUpdatePage.Game) + "." + chatOptions.FileExtension);

                    chatTask.UpdateOptions = chatOptions;
                    chatTask.Info.Title = chatUpdatePage.textTitle.Text;
                    chatTask.Info.Thumbnail = chatUpdatePage.imgThumbnail.Source;
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
                            MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        ChatRenderTask renderTask = new ChatRenderTask();
                        string fileFormat = chatRenderPage.comboFormat.SelectedItem.ToString()!;
                        string filePath = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(fileName) + "." + fileFormat.ToLower());
                        ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(filePath);
                        renderOptions.InputFile = fileName;
                        renderTask.DownloadOptions = renderOptions;
                        renderTask.Info.Title = Path.GetFileNameWithoutExtension(filePath);
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
            else
            {
                if (_dataList.Count > 0)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    foreach (var taskData in _dataList)
                    {
                        if (checkVideo.IsChecked.GetValueOrDefault())
                        {
                            if (taskData.Id.All(char.IsDigit))
                            {
                                VodDownloadTask downloadTask = new VodDownloadTask();
                                VideoDownloadOptions downloadOptions = new VideoDownloadOptions();
                                downloadOptions.Oauth = Settings.Default.OAuth;
                                downloadOptions.TempFolder = Settings.Default.TempPath;
                                downloadOptions.Id = int.Parse(taskData.Id);
                                downloadOptions.FfmpegPath = "ffmpeg";
                                downloadOptions.CropBeginning = false;
                                downloadOptions.CropEnding = false;
                                downloadOptions.DownloadThreads = Settings.Default.VodDownloadThreads;
                                downloadOptions.ThrottleKib = Settings.Default.DownloadThrottleEnabled
                                    ? Settings.Default.MaximumBandwidthKib
                                    : -1;
                                downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateVod, taskData.Title, taskData.Id, taskData.Time, taskData.Streamer,
                                    downloadOptions.CropBeginning ? TimeSpan.FromSeconds(downloadOptions.CropBeginningTime) : TimeSpan.Zero,
                                    downloadOptions.CropEnding ? TimeSpan.FromSeconds(downloadOptions.CropEndingTime) : TimeSpan.FromSeconds(taskData.Length),
                                    taskData.Views.ToString(), taskData.Game) + ".mp4");
                                downloadTask.DownloadOptions = downloadOptions;
                                downloadTask.Info.Title = taskData.Title;
                                downloadTask.Info.Thumbnail = taskData.Thumbnail;
                                downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                                lock (PageQueue.taskLock)
                                {
                                    PageQueue.taskList.Add(downloadTask);
                                }
                            }
                            else
                            {
                                ClipDownloadTask downloadTask = new ClipDownloadTask();
                                ClipDownloadOptions downloadOptions = new ClipDownloadOptions();
                                downloadOptions.Id = taskData.Id;
                                downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateClip, taskData.Title, taskData.Id, taskData.Time, taskData.Streamer,
                                    TimeSpan.Zero, TimeSpan.FromSeconds(taskData.Length), taskData.Views.ToString(), taskData.Game) + ".mp4");
                                downloadOptions.ThrottleKib = Settings.Default.DownloadThrottleEnabled
                                    ? Settings.Default.MaximumBandwidthKib
                                    : -1;
                                downloadOptions.TempFolder = Settings.Default.TempPath;
                                downloadOptions.EncodeMetadata = Settings.Default.EncodeClipMetadata;
                                downloadOptions.FfmpegPath = "ffmpeg";
                                downloadTask.DownloadOptions = downloadOptions;
                                downloadTask.Info.Title = taskData.Title;
                                downloadTask.Info.Thumbnail = taskData.Thumbnail;
                                downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                                lock (PageQueue.taskLock)
                                {
                                    PageQueue.taskList.Add(downloadTask);
                                }
                            }
                        }

                        if (checkChat.IsChecked.GetValueOrDefault())
                        {
                            ChatDownloadTask downloadTask = new ChatDownloadTask();
                            ChatDownloadOptions downloadOptions = new ChatDownloadOptions();
                            if (radioJson.IsChecked == true)
                                downloadOptions.DownloadFormat = ChatFormat.Json;
                            else if (radioHTML.IsChecked == true)
                                downloadOptions.DownloadFormat = ChatFormat.Html;
                            else
                                downloadOptions.DownloadFormat = ChatFormat.Text;
                            downloadOptions.Compression = RadioCompressionNone.IsChecked == true ? ChatCompression.None : ChatCompression.Gzip;
                            downloadOptions.EmbedData = checkEmbed.IsChecked.GetValueOrDefault();
                            downloadOptions.TimeFormat = TimestampFormat.Relative;
                            downloadOptions.Id = taskData.Id;
                            downloadOptions.CropBeginning = false;
                            downloadOptions.CropEnding = false;
                            downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, taskData.Title, taskData.Id, taskData.Time, taskData.Streamer,
                                downloadOptions.CropBeginning ? TimeSpan.FromSeconds(downloadOptions.CropBeginningTime) : TimeSpan.Zero,
                                downloadOptions.CropEnding ? TimeSpan.FromSeconds(downloadOptions.CropEndingTime) : TimeSpan.FromSeconds(taskData.Length),
                                taskData.Views.ToString(), taskData.Game) + "." + downloadOptions.FileExtension);
                            downloadTask.DownloadOptions = downloadOptions;
                            downloadTask.Info.Title = taskData.Title;
                            downloadTask.Info.Thumbnail = taskData.Thumbnail;
                            downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                            lock (PageQueue.taskLock)
                            {
                                PageQueue.taskList.Add(downloadTask);
                            }

                            if (checkRender.IsChecked.GetValueOrDefault() && downloadOptions.DownloadFormat == ChatFormat.Json)
                            {
                                ChatRenderTask renderTask = new ChatRenderTask();
                                ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(downloadOptions.Filename.Replace(".gz", ""), '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()));
                                if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                                {
                                    //Just in case VOD and chat paths are the same. Like the previous defaults
                                    renderOptions.OutputFile = Path.ChangeExtension(downloadOptions.Filename.Replace(".gz", ""), " - CHAT." + MainWindow.pageChatRender.comboFormat.Text.ToLower());
                                }
                                renderOptions.InputFile = downloadOptions.Filename;
                                renderTask.DownloadOptions = renderOptions;
                                renderTask.Info.Title = taskData.Title;
                                renderTask.Info.Thumbnail = taskData.Thumbnail;
                                renderTask.ChangeStatus(TwitchTaskStatus.Waiting);
                                renderTask.DependantTask = downloadTask;

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
            }
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

        private void Window_loaded(object sender, RoutedEventArgs e)
        {
            Title = Translations.Strings.TitleEnqueueOptions;
            App.RequestTitleBarChange();
        }
    }
}