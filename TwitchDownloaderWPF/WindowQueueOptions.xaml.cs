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
        // This file is absolutely attrocious, but fixing it would mean rewriting the entire GUI in a more abstract form

        List<TaskData> dataList;

        Page parentPage { get; set; }
        public WindowQueueOptions(Page page)
        {
            parentPage = page;
            InitializeComponent();

            string queueFolder = Settings.Default.QueueFolder;
            if (Directory.Exists(queueFolder))
                textFolder.Text = queueFolder;

            if (page is PageVodDownload || page is PageClipDownload)
            {
                checkVideo.IsChecked = true;
                checkVideo.IsEnabled = false;
            }
            if (page is PageChatDownload)
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
                var chatPage = page as PageChatDownload;
                if (chatPage.radioJson.IsChecked != true)
                {
                    checkRender.IsChecked = false;
                    checkRender.IsEnabled = false;
                }
            }
            if (page is PageChatUpdate)
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
            if (page is PageChatRender)
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

        public WindowQueueOptions(List<TaskData> DataList)
        {
            this.dataList = DataList;
            InitializeComponent();

            string queueFolder = Settings.Default.QueueFolder;
            if (Directory.Exists(queueFolder))
                textFolder.Text = queueFolder;
        }

        private async void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            if (parentPage != null)
            {
                if (parentPage is PageVodDownload)
                {
                    PageVodDownload vodPage = (PageVodDownload)parentPage;
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    VodDownloadTask downloadTask = new VodDownloadTask();
                    VideoDownloadOptions downloadOptions = vodPage.GetOptions(null, textFolder.Text);
                    downloadTask.DownloadOptions = downloadOptions;
                    downloadTask.Info.Title = vodPage.textTitle.Text;
                    downloadTask.Info.Thumbnail = vodPage.imgThumbnail.Source;
                    downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(downloadTask);
                    }

                    if ((bool)checkChat.IsChecked)
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
                        chatOptions.EmbedData = (bool)checkEmbed.IsChecked;
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
                        chatTask.Info.Title = vodPage.textTitle.Text;
                        chatTask.Info.Thumbnail = vodPage.imgThumbnail.Source;
                        chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(chatTask);
                        }

                        if ((bool)checkRender.IsChecked && chatOptions.DownloadFormat == ChatFormat.Json)
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
                            renderTask.Info.Title = vodPage.textTitle.Text;
                            renderTask.Info.Thumbnail = vodPage.imgThumbnail.Source;
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

                if (parentPage is PageClipDownload)
                {
                    PageClipDownload clipPage = (PageClipDownload)parentPage;
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ClipDownloadTask downloadTask = new ClipDownloadTask();
                    ClipDownloadOptions downloadOptions = new ClipDownloadOptions();
                    downloadOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateClip, clipPage.textTitle.Text, clipPage.clipId, clipPage.currentVideoTime, clipPage.textStreamer.Text, TimeSpan.Zero, clipPage.clipLength, clipPage.viewCount.ToString(), clipPage.game) + ".mp4");
                    downloadOptions.Id = clipPage.clipId;
                    downloadOptions.Quality = clipPage.comboQuality.Text;
                    downloadOptions.ThrottleKib = Settings.Default.DownloadThrottleEnabled
                        ? Settings.Default.MaximumBandwidthKib
                        : -1;
                    downloadOptions.TempFolder = Settings.Default.TempPath;
                    downloadOptions.EncodeMetadata = clipPage.CheckMetadata.IsChecked!.Value;
                    downloadOptions.FfmpegPath = "ffmpeg";
                    downloadTask.DownloadOptions = downloadOptions;
                    downloadTask.Info.Title = clipPage.textTitle.Text;
                    downloadTask.Info.Thumbnail = clipPage.imgThumbnail.Source;
                    downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(downloadTask);
                    }

                    if ((bool)checkChat.IsChecked)
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
                        chatOptions.EmbedData = (bool)checkEmbed.IsChecked;
                        chatOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, downloadTask.Info.Title, chatOptions.Id, clipPage.currentVideoTime, clipPage.textStreamer.Text, TimeSpan.Zero, clipPage.clipLength, clipPage.viewCount.ToString(), clipPage.game) + "." + chatOptions.FileExtension);

                        chatTask.DownloadOptions = chatOptions;
                        chatTask.Info.Title = clipPage.textTitle.Text;
                        chatTask.Info.Thumbnail = clipPage.imgThumbnail.Source;
                        chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(chatTask);
                        }

                        if ((bool)checkRender.IsChecked && chatOptions.DownloadFormat == ChatFormat.Json)
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
                            renderTask.Info.Title = clipPage.textTitle.Text;
                            renderTask.Info.Thumbnail = clipPage.imgThumbnail.Source;
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

                if (parentPage is PageChatDownload)
                {
                    PageChatDownload chatPage = (PageChatDownload)parentPage;
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ChatDownloadTask chatTask = new ChatDownloadTask();
                    ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                    chatOptions.Id = chatPage.downloadId;
                    chatOptions.Filename = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, chatPage.textTitle.Text, chatOptions.Id, chatPage.currentVideoTime, chatPage.textStreamer.Text,
                        chatOptions.CropBeginning ? TimeSpan.FromSeconds(chatOptions.CropBeginningTime) : TimeSpan.Zero,
                        chatOptions.CropEnding ? TimeSpan.FromSeconds(chatOptions.CropEndingTime) : chatPage.vodLength,
                        chatPage.viewCount.ToString(), chatPage.game) + "." + chatOptions.FileExtension);

                    chatTask.DownloadOptions = chatOptions;
                    chatTask.Info.Title = chatPage.textTitle.Text;
                    chatTask.Info.Thumbnail = chatPage.imgThumbnail.Source;
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
                        renderTask.Info.Title = chatPage.textTitle.Text;
                        renderTask.Info.Thumbnail = chatPage.imgThumbnail.Source;
                        renderTask.ChangeStatus(TwitchTaskStatus.Waiting);
                        renderTask.DependantTask = chatTask;

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(renderTask);
                        }
                    }

                    this.Close();
                }

                if (parentPage is PageChatUpdate)
                {
                    PageChatUpdate chatPage = (PageChatUpdate)parentPage;
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ChatUpdateTask chatTask = new ChatUpdateTask();
                    ChatUpdateOptions chatOptions = MainWindow.pageChatUpdate.GetOptions(null);
                    chatOptions.InputFile = chatPage.InputFile;
                    chatOptions.OutputFile = Path.Combine(folderPath, FilenameService.GetFilename(Settings.Default.TemplateChat, chatPage.textTitle.Text, chatPage.VideoId, chatPage.VideoCreatedAt, chatPage.textStreamer.Text,
                        chatOptions.CropBeginning ? TimeSpan.FromSeconds(chatOptions.CropBeginningTime) : TimeSpan.Zero,
                        chatOptions.CropEnding ? TimeSpan.FromSeconds(chatOptions.CropEndingTime) : chatPage.VideoLength,
                        chatPage.ViewCount.ToString(), chatPage.Game) + "." + chatOptions.FileExtension);

                    chatTask.UpdateOptions = chatOptions;
                    chatTask.Info.Title = chatPage.textTitle.Text;
                    chatTask.Info.Thumbnail = chatPage.imgThumbnail.Source;
                    chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                    {
                        PageQueue.taskList.Add(chatTask);
                    }

                    this.Close();
                }

                if (parentPage is PageChatRender)
                {
                    PageChatRender renderPage = (PageChatRender)parentPage;
                    string folderPath = textFolder.Text;
                    foreach (string fileName in renderPage.FileNames)
                    {
                        if (!Directory.Exists(folderPath))
                        {
                            MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        ChatRenderTask renderTask = new ChatRenderTask();
                        string fileFormat = renderPage.comboFormat.SelectedItem.ToString();
                        string filePath = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(fileName) + "." + fileFormat.ToLower());
                        ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(filePath);
                        renderOptions.InputFile = fileName;
                        renderTask.DownloadOptions = renderOptions;
                        renderTask.Info.Title = Path.GetFileNameWithoutExtension(filePath);
                        var (success, image) = await ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL);
                        if (success)
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
                if (dataList.Count > 0)
                {
                    string folderPath = textFolder.Text;
                    if (!Directory.Exists(folderPath))
                    {
                        MessageBox.Show(Translations.Strings.InvaliFolderPathMessage, Translations.Strings.InvalidFolderPath, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    for (int i = 0; i < dataList.Count; i++)
                    {
                        var taskData = dataList[i];
                        if ((bool)checkVideo.IsChecked)
                        {
                            if (taskData.Id.All(Char.IsDigit))
                            {
                                VodDownloadTask downloadTask = new VodDownloadTask();
                                VideoDownloadOptions downloadOptions = new VideoDownloadOptions();
                                downloadOptions.Oauth = Settings.Default.OAuth;
                                downloadOptions.TempFolder = Settings.Default.TempPath;
                                downloadOptions.Id = taskData.Id;
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

                        if ((bool)checkChat.IsChecked)
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
                            downloadOptions.EmbedData = (bool)checkEmbed.IsChecked;
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

                            if ((bool)checkRender.IsChecked && downloadOptions.DownloadFormat == ChatFormat.Json)
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