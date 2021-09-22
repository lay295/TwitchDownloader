using System;
using System.Collections.Generic;
using System.IO;
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
using TwitchDownloader.Properties;
using TwitchDownloader.TwitchTasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderWPF;

namespace TwitchDownloader
{
    /// <summary>
    /// Interaction logic for QueueOptions.xaml
    /// </summary>
    public partial class WindowQueueOptions : Window
    {
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
                radioJson.IsEnabled = false;
                radioTxt.IsEnabled = false;
                checkEmbed.IsEnabled = false;
            }
            if (page is PageChatRender)
            {
                checkVideo.Visibility = Visibility.Collapsed;
                checkChat.Visibility = Visibility.Collapsed;
                radioJson.Visibility = Visibility.Collapsed;
                radioTxt.Visibility = Visibility.Collapsed;
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

        private void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            if (parentPage != null)
            {
                if (parentPage is PageVodDownload)
                {
                    PageVodDownload vodPage = (PageVodDownload)parentPage;
                    string folderPath = textFolder.Text;
                    if (!String.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                    {
                        VodDownloadTask downloadTask = new VodDownloadTask();
                        VideoDownloadOptions downloadOptions = vodPage.GetOptions(null, textFolder.Text);
                        downloadTask.DownloadOptions = downloadOptions;
                        downloadTask.Info.Title = vodPage.textTitle.Text;
                        downloadTask.Info.Thumbnail = vodPage.imgThumbnail.Source;
                        downloadTask.Status = TwitchTasks.TwitchTaskStatus.Ready;

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(downloadTask);
                        }

                        if ((bool)checkChat.IsChecked)
                        {
                            ChatDownloadTask chatTask = new ChatDownloadTask();
                            ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                            chatOptions.Id = downloadOptions.Id.ToString();
                            chatOptions.IsJson = (bool)radioJson.IsChecked;
                            chatOptions.EmbedEmotes = (bool)checkEmbed.IsChecked;
                            chatOptions.Filename = Path.Combine(folderPath, MainWindow.GetFilename(Settings.Default.TemplateChat, downloadTask.Info.Title, chatOptions.Id, vodPage.currentVideoTime, vodPage.textStreamer.Text) + (chatOptions.IsJson ? ".json" : ".txt"));

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
                            chatTask.Status = TwitchTasks.TwitchTaskStatus.Ready;

                            lock (PageQueue.taskLock)
                            {
                                PageQueue.taskList.Add(chatTask);
                            }

                            if ((bool)checkRender.IsChecked && chatOptions.IsJson)
                            {
                                ChatRenderTask renderTask = new ChatRenderTask();
                                ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename, ".mp4"));
                                if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                                {
                                    //Just in case VOD and chat paths are the same. Like the previous defaults
                                    renderOptions.OutputFile = Path.ChangeExtension(chatOptions.Filename, " - CHAT.mp4");
                                }
                                renderOptions.InputFile = chatOptions.Filename;
                                renderTask.DownloadOptions = renderOptions;
                                renderTask.Info.Title = vodPage.textTitle.Text;
                                renderTask.Info.Thumbnail = vodPage.imgThumbnail.Source;
                                renderTask.Status = TwitchTasks.TwitchTaskStatus.Waiting;
                                renderTask.DependantTask = chatTask;

                                lock (PageQueue.taskLock)
                                {
                                    PageQueue.taskList.Add(renderTask);
                                }
                            }
                        }

                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Invalid folder path (doesn't exist?)", "Invalid Folder Path", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                if (parentPage is PageClipDownload)
                {
                    PageClipDownload clipPage = (PageClipDownload)parentPage;
                    string folderPath = textFolder.Text;
                    if (!String.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                    {
                        ClipDownloadTask downloadTask = new ClipDownloadTask();
                        ClipDownloadOptions downloadOptions = new ClipDownloadOptions();
                        downloadOptions.Filename = Path.Combine(folderPath, MainWindow.GetFilename(Settings.Default.TemplateClip, clipPage.textTitle.Text, clipPage.clipId, clipPage.currentVideoTime, clipPage.textStreamer.Text) + ".mp4");
                        downloadOptions.Id = clipPage.clipId;
                        downloadOptions.Quality = clipPage.comboQuality.Text;
                        downloadTask.DownloadOptions = downloadOptions;
                        downloadTask.Info.Title = clipPage.textTitle.Text;
                        downloadTask.Info.Thumbnail = clipPage.imgThumbnail.Source;
                        downloadTask.Status = TwitchTasks.TwitchTaskStatus.Ready;

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(downloadTask);
                        }

                        if ((bool)checkChat.IsChecked)
                        {
                            ChatDownloadTask chatTask = new ChatDownloadTask();
                            ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                            chatOptions.Id = downloadOptions.Id.ToString();
                            chatOptions.IsJson = (bool)radioJson.IsChecked;
                            chatOptions.TimeFormat = TimestampFormat.Relative;
                            chatOptions.EmbedEmotes = (bool)checkEmbed.IsChecked;
                            chatOptions.Filename = Path.Combine(folderPath, MainWindow.GetFilename(Settings.Default.TemplateChat, downloadTask.Info.Title, chatOptions.Id, clipPage.currentVideoTime, clipPage.textStreamer.Text) + (chatOptions.IsJson ? ".json" : ".txt"));

                            chatTask.DownloadOptions = chatOptions;
                            chatTask.Info.Title = clipPage.textTitle.Text;
                            chatTask.Info.Thumbnail = clipPage.imgThumbnail.Source;
                            chatTask.Status = TwitchTasks.TwitchTaskStatus.Ready;

                            lock (PageQueue.taskLock)
                            {
                                PageQueue.taskList.Add(chatTask);
                            }

                            if ((bool)checkRender.IsChecked && chatOptions.IsJson)
                            {
                                ChatRenderTask renderTask = new ChatRenderTask();
                                ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename, ".mp4"));
                                if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                                {
                                    //Just in case VOD and chat paths are the same. Like the previous defaults
                                    renderOptions.OutputFile = Path.ChangeExtension(chatOptions.Filename, " - CHAT.mp4");
                                }
                                renderOptions.InputFile = chatOptions.Filename;
                                renderTask.DownloadOptions = renderOptions;
                                renderTask.Info.Title = clipPage.textTitle.Text;
                                renderTask.Info.Thumbnail = clipPage.imgThumbnail.Source;
                                renderTask.Status = TwitchTasks.TwitchTaskStatus.Waiting;
                                renderTask.DependantTask = chatTask;

                                lock (PageQueue.taskLock)
                                {
                                    PageQueue.taskList.Add(renderTask);
                                }
                            }
                        }

                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Invalid folder path (doesn't exist?)", "Invalid Folder Path", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                if (parentPage is PageChatDownload)
                {
                    PageChatDownload chatPage = (PageChatDownload)parentPage;
                    string folderPath = textFolder.Text;
                    if (!String.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                    {
                        ChatDownloadTask chatTask = new ChatDownloadTask();
                        ChatDownloadOptions chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                        chatOptions.Id = chatPage.downloadId;
                        chatOptions.Filename = Path.Combine(folderPath, MainWindow.GetFilename(Settings.Default.TemplateChat, chatPage.textTitle.Text, chatOptions.Id, chatPage.currentVideoTime, chatPage.textStreamer.Text) + (chatOptions.IsJson ? ".json" : ".txt"));

                        chatTask.DownloadOptions = chatOptions;
                        chatTask.Info.Title = chatPage.textTitle.Text;
                        chatTask.Info.Thumbnail = chatPage.imgThumbnail.Source;
                        chatTask.Status = TwitchTasks.TwitchTaskStatus.Ready;

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(chatTask);
                        }

                        if ((bool)checkRender.IsChecked && chatOptions.IsJson)
                        {
                            ChatRenderTask renderTask = new ChatRenderTask();
                            ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(chatOptions.Filename, ".mp4"));
                            renderOptions.InputFile = chatOptions.Filename;
                            renderTask.DownloadOptions = renderOptions;
                            renderTask.Info.Title = chatPage.textTitle.Text;
                            renderTask.Info.Thumbnail = chatPage.imgThumbnail.Source;
                            renderTask.Status = TwitchTasks.TwitchTaskStatus.Waiting;
                            renderTask.DependantTask = chatTask;

                            lock (PageQueue.taskLock)
                            {
                                PageQueue.taskList.Add(renderTask);
                            }
                        }

                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Invalid folder path (doesn't exist?)", "Invalid Folder Path", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                if (parentPage is PageChatRender)
                {
                    PageChatRender renderPage = (PageChatRender)parentPage;
                    string folderPath = textFolder.Text;
                    if (!String.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                    {
                        ChatRenderTask renderTask = new ChatRenderTask();
                        string fileFormat = renderPage.comboFormat.SelectedItem.ToString();
                        string filePath = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(renderPage.textJson.Text) + "." + fileFormat.ToLower());
                        ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(filePath);
                        renderTask.DownloadOptions = renderOptions;
                        renderTask.Info.Title = Path.GetFileNameWithoutExtension(filePath);
                        renderTask.Status = TwitchTasks.TwitchTaskStatus.Ready;

                        lock (PageQueue.taskLock)
                        {
                            PageQueue.taskList.Add(renderTask);
                        }

                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Invalid folder path (doesn't exist?)", "Invalid Folder Path", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            if (dataList.Count > 0)
            {
                string folderPath = textFolder.Text;
                if (!String.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                {
                    for (int i = 0; i < dataList.Count; i++)
                    {
                        if ((bool)checkVideo.IsChecked)
                        {
                            if (dataList[i].Id.All(Char.IsDigit))
                            {
                                VodDownloadTask downloadTask = new VodDownloadTask();
                                VideoDownloadOptions downloadOptions = new VideoDownloadOptions();
                                downloadOptions.Oauth = Settings.Default.OAuth;
                                downloadOptions.TempFolder = Settings.Default.TempPath;
                                downloadOptions.Id = int.Parse(dataList[i].Id);
                                downloadOptions.FfmpegPath = "ffmpeg";
                                downloadOptions.CropBeginning = false;
                                downloadOptions.CropEnding = false;
                                downloadOptions.DownloadThreads = Settings.Default.VodDownloadThreads;
                                downloadOptions.Filename = Path.Combine(folderPath, MainWindow.GetFilename(Settings.Default.TemplateVod, dataList[i].Title, dataList[i].Id, dataList[i].Time, dataList[i].Streamer) + ".mp4");
                                downloadTask.DownloadOptions = downloadOptions;
                                downloadTask.Info.Title = dataList[i].Title;
                                downloadTask.Info.Thumbnail = dataList[i].Thumbnail;
                                downloadTask.Status = TwitchTaskStatus.Ready;

                                lock (PageQueue.taskLock)
                                {
                                    PageQueue.taskList.Add(downloadTask);
                                }
                            }
                            else
                            {
                                ClipDownloadTask downloadTask = new ClipDownloadTask();
                                ClipDownloadOptions downloadOptions = new ClipDownloadOptions();
                                downloadOptions.Id = dataList[i].Id;
                                downloadOptions.Filename = Path.Combine(folderPath, MainWindow.GetFilename(Settings.Default.TemplateClip, dataList[i].Title, dataList[i].Id, dataList[i].Time, dataList[i].Streamer) + ".mp4");
                                downloadTask.DownloadOptions = downloadOptions;
                                downloadTask.Info.Title = dataList[i].Title;
                                downloadTask.Info.Thumbnail = dataList[i].Thumbnail;
                                downloadTask.Status = TwitchTaskStatus.Ready;

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
                            downloadOptions.IsJson = (bool)radioJson.IsChecked;
                            downloadOptions.EmbedEmotes = (bool)checkEmbed.IsChecked;
                            downloadOptions.TimeFormat = TimestampFormat.Relative;
                            downloadOptions.Id = dataList[i].Id;
                            downloadOptions.CropBeginning = false;
                            downloadOptions.CropEnding = false;
                            downloadOptions.Filename = Path.Combine(folderPath, MainWindow.GetFilename(Settings.Default.TemplateChat, dataList[i].Title, dataList[i].Id, dataList[i].Time, dataList[i].Streamer) + (downloadOptions.IsJson ? ".json" : ".txt"));
                            downloadTask.DownloadOptions = downloadOptions;
                            downloadTask.Info.Title = dataList[i].Title;
                            downloadTask.Info.Thumbnail = dataList[i].Thumbnail;
                            downloadTask.Status = TwitchTaskStatus.Ready;

                            lock (PageQueue.taskLock)
                            {
                                PageQueue.taskList.Add(downloadTask);
                            }

                            if ((bool)checkRender.IsChecked && downloadOptions.IsJson)
                            {
                                ChatRenderTask renderTask = new ChatRenderTask();
                                ChatRenderOptions renderOptions = MainWindow.pageChatRender.GetOptions(Path.ChangeExtension(downloadOptions.Filename, ".mp4"));
                                if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                                {
                                    //Just in case VOD and chat paths are the same. Like the previous defaults
                                    renderOptions.OutputFile = Path.ChangeExtension(downloadOptions.Filename, " - CHAT.mp4");
                                }
                                renderOptions.InputFile = downloadOptions.Filename;
                                renderTask.DownloadOptions = renderOptions;
                                renderTask.Info.Title = dataList[i].Title;
                                renderTask.Info.Thumbnail = dataList[i].Thumbnail;
                                renderTask.Status = TwitchTasks.TwitchTaskStatus.Waiting;
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
                else
                {
                    MessageBox.Show("Invalid folder path (doesn't exist?)", "Invalid Folder Path", MessageBoxButton.OK, MessageBoxImage.Error);
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
            checkEmbed.IsEnabled = true;
        }

        private void checkChat_Unchecked(object sender, RoutedEventArgs e)
        {
            checkRender.IsEnabled = false;
            checkRender.IsChecked = false;
            radioJson.IsEnabled = false;
            radioTxt.IsEnabled = false;
            checkEmbed.IsEnabled = false;
        }

        private void radioJson_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                checkEmbed.IsEnabled = true;
                checkRender.IsEnabled = true;
            }
            
        }

        private void radioTxt_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                checkEmbed.IsEnabled = false;
                checkRender.IsEnabled = false;
            }
        }
    }
}
