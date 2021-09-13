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
    public partial class QueueOptions : Window
    {
        Page parentPage { get; set; }
        public QueueOptions(Page page)
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

        private void btnQueue_Click(object sender, RoutedEventArgs e)
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
                    downloadTask.Title = vodPage.textTitle.Text;
                    downloadTask.Thumbnail = vodPage.imgThumbnail.Source;
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
                        chatOptions.Filename = Path.Combine(folderPath, MainWindow.GetFilename(Settings.Default.TemplateChat, downloadTask.Title, chatOptions.Id, vodPage.currentVideoTime, vodPage.textStreamer.Text) + (chatOptions.IsJson ? ".json" : ".txt"));

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
                        chatTask.Title = vodPage.textTitle.Text;
                        chatTask.Thumbnail = vodPage.imgThumbnail.Source;
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
                            renderTask.Title = vodPage.textTitle.Text;
                            renderTask.Thumbnail = vodPage.imgThumbnail.Source;
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
                    downloadTask.Title = clipPage.textTitle.Text;
                    downloadTask.Thumbnail = clipPage.imgThumbnail.Source;
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
                        chatOptions.Filename = Path.Combine(folderPath, MainWindow.GetFilename(Settings.Default.TemplateChat, downloadTask.Title, chatOptions.Id, clipPage.currentVideoTime, clipPage.textStreamer.Text) + (chatOptions.IsJson ? ".json" : ".txt"));

                        chatTask.DownloadOptions = chatOptions;
                        chatTask.Title = clipPage.textTitle.Text;
                        chatTask.Thumbnail = clipPage.imgThumbnail.Source;
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
                            renderTask.Title = clipPage.textTitle.Text;
                            renderTask.Thumbnail = clipPage.imgThumbnail.Source;
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
                    chatTask.Title = chatPage.textTitle.Text;
                    chatTask.Thumbnail = chatPage.imgThumbnail.Source;
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
                        renderTask.Title = chatPage.textTitle.Text;
                        renderTask.Thumbnail = chatPage.imgThumbnail.Source;
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
                    renderTask.Title = Path.GetFileNameWithoutExtension(filePath);
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
    }
}
