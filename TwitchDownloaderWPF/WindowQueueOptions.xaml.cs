using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ookii.Dialogs.Wpf;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Translations;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF;

/// <summary>
///     Interaction logic for QueueOptions.xaml
/// </summary>
public partial class WindowQueueOptions : Window {
    // This file is absolutely atrocious, but fixing it would mean rewriting the entire GUI in a more abstract form

    private readonly List<TaskData> _dataList;
    private readonly Page _parentPage;

    public WindowQueueOptions(Page page) {
        this._parentPage = page;
        this.InitializeComponent();

        var queueFolder = Settings.Default.QueueFolder;
        if (Directory.Exists(queueFolder))
            this.textFolder.Text = queueFolder;

        this.TextPreferredQuality.Visibility = Visibility.Collapsed;
        this.ComboPreferredQuality.Visibility = Visibility.Collapsed;

        if (page is PageVodDownload or PageClipDownload) {
            this.checkVideo.IsChecked = true;
            this.checkVideo.IsEnabled = false;
        } else if (page is PageChatDownload chatPage) {
            this.checkVideo.Visibility = Visibility.Collapsed;
            this.checkChat.IsChecked = true;
            this.checkChat.IsEnabled = false;
            this.TextDownloadFormat.Visibility = Visibility.Collapsed;
            this.radioJson.Visibility = Visibility.Collapsed;
            this.radioTxt.Visibility = Visibility.Collapsed;
            this.radioHTML.Visibility = Visibility.Collapsed;
            this.TextCompression.Visibility = Visibility.Collapsed;
            this.RadioCompressionNone.Visibility = Visibility.Collapsed;
            this.RadioCompressionGzip.Visibility = Visibility.Collapsed;
            this.checkEmbed.Visibility = Visibility.Collapsed;
            if (!chatPage.radioJson.IsChecked.GetValueOrDefault()) {
                this.checkRender.IsChecked = false;
                this.checkRender.IsEnabled = false;
            }
        } else if (page is PageChatUpdate) {
            this.checkVideo.Visibility = Visibility.Collapsed;
            this.checkChat.Visibility = Visibility.Collapsed;
            this.TextDownloadFormat.Visibility = Visibility.Collapsed;
            this.radioJson.Visibility = Visibility.Collapsed;
            this.radioTxt.Visibility = Visibility.Collapsed;
            this.radioHTML.Visibility = Visibility.Collapsed;
            this.TextCompression.Visibility = Visibility.Collapsed;
            this.RadioCompressionNone.Visibility = Visibility.Collapsed;
            this.RadioCompressionGzip.Visibility = Visibility.Collapsed;
            this.checkEmbed.Visibility = Visibility.Collapsed;
            this.checkRender.Visibility = Visibility.Collapsed;
        } else if (page is PageChatRender) {
            this.checkVideo.Visibility = Visibility.Collapsed;
            this.checkChat.Visibility = Visibility.Collapsed;
            this.TextDownloadFormat.Visibility = Visibility.Collapsed;
            this.radioJson.Visibility = Visibility.Collapsed;
            this.radioTxt.Visibility = Visibility.Collapsed;
            this.radioHTML.Visibility = Visibility.Collapsed;
            this.TextCompression.Visibility = Visibility.Collapsed;
            this.RadioCompressionNone.Visibility = Visibility.Collapsed;
            this.RadioCompressionGzip.Visibility = Visibility.Collapsed;
            this.checkEmbed.Visibility = Visibility.Collapsed;
            this.checkRender.IsChecked = true;
            this.checkRender.IsEnabled = false;
        }
    }

    public WindowQueueOptions(List<TaskData> dataList) {
        this._dataList = dataList;
        this.InitializeComponent();

        var queueFolder = Settings.Default.QueueFolder;
        if (Directory.Exists(queueFolder))
            this.textFolder.Text = queueFolder;
    }

    private FileInfo HandleFileCollisionCallback(FileInfo fileInfo) {
        return this.Dispatcher.Invoke(
            () => FileCollisionService.HandleCollisionCallback(fileInfo, Application.Current.MainWindow)
        );
    }

    private void btnQueue_Click(object sender, RoutedEventArgs e) {
        if (this._parentPage != null) {
            if (this._parentPage is PageVodDownload vodDownloadPage) {
                var folderPath = this.textFolder.Text;
                if (!Directory.Exists(folderPath)) {
                    MessageBox.Show(
                        this,
                        Strings.InvaliFolderPathMessage,
                        Strings.InvalidFolderPath,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }

                var downloadOptions = vodDownloadPage.GetOptions(null, this.textFolder.Text);
                downloadOptions.FileCollisionCallback = this.HandleFileCollisionCallback;

                var downloadTask = new VodDownloadTask {
                    DownloadOptions = downloadOptions,
                    Info = {
                        Title = vodDownloadPage.textTitle.Text,
                        Thumbnail = vodDownloadPage.imgThumbnail.Source
                    }
                };
                downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                lock (PageQueue.taskLock)
                    PageQueue.taskList.Add(downloadTask);

                if (this.checkChat.IsChecked.GetValueOrDefault()) {
                    var chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                    chatOptions.Id = downloadOptions.Id.ToString();
                    if (this.radioJson.IsChecked == true)
                        chatOptions.DownloadFormat = ChatFormat.Json;
                    else if (this.radioHTML.IsChecked == true)
                        chatOptions.DownloadFormat = ChatFormat.Html;
                    else
                        chatOptions.DownloadFormat = ChatFormat.Text;
                    chatOptions.EmbedData = this.checkEmbed.IsChecked.GetValueOrDefault();
                    chatOptions.Filename = Path.Combine(
                        folderPath,
                        Path.GetFileNameWithoutExtension(downloadOptions.Filename) + "." + chatOptions.DownloadFormat
                    );
                    chatOptions.FileCollisionCallback = this.HandleFileCollisionCallback;

                    if (downloadOptions.TrimBeginning) {
                        chatOptions.TrimBeginning = true;
                        chatOptions.TrimBeginningTime = downloadOptions.TrimBeginningTime.TotalSeconds;
                    }

                    if (downloadOptions.TrimEnding) {
                        chatOptions.TrimEnding = true;
                        chatOptions.TrimEndingTime = downloadOptions.TrimEndingTime.TotalSeconds;
                    }

                    var chatTask = new ChatDownloadTask {
                        DownloadOptions = chatOptions,
                        Info = {
                            Title = vodDownloadPage.textTitle.Text,
                            Thumbnail = vodDownloadPage.imgThumbnail.Source
                        }
                    };
                    chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                        PageQueue.taskList.Add(chatTask);

                    if (this.checkRender.IsChecked.GetValueOrDefault()
                        && chatOptions.DownloadFormat == ChatFormat.Json) {
                        var renderOptions = MainWindow.pageChatRender.GetOptions(
                            Path.ChangeExtension(
                                chatOptions.Filename.Replace(".gz", ""),
                                '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()
                            )
                        );
                        if (renderOptions.OutputFile.Trim() == downloadOptions.Filename!.Trim())
                            //Just in case VOD and chat paths are the same. Like the previous defaults
                            renderOptions.OutputFile = Path.ChangeExtension(
                                chatOptions.Filename.Replace(".gz", ""),
                                " - CHAT." + MainWindow.pageChatRender.comboFormat.Text.ToLower()
                            );
                        renderOptions.InputFile = chatOptions.Filename;
                        renderOptions.FileCollisionCallback = this.HandleFileCollisionCallback;

                        var renderTask = new ChatRenderTask {
                            DownloadOptions = renderOptions,
                            Info = {
                                Title = vodDownloadPage.textTitle.Text,
                                Thumbnail = vodDownloadPage.imgThumbnail.Source
                            }
                        };
                        renderTask.ChangeStatus(TwitchTaskStatus.Waiting);
                        renderTask.DependantTask = chatTask;

                        lock (PageQueue.taskLock)
                            PageQueue.taskList.Add(renderTask);
                    }
                }

                this.Close();
            }

            if (this._parentPage is PageClipDownload clipDownloadPage) {
                var folderPath = this.textFolder.Text;
                if (!Directory.Exists(folderPath)) {
                    MessageBox.Show(
                        this,
                        Strings.InvaliFolderPathMessage,
                        Strings.InvalidFolderPath,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }

                var downloadOptions = new ClipDownloadOptions {
                    Filename = Path.Combine(
                        folderPath,
                        FilenameService.GetFilename(
                            Settings.Default.TemplateClip,
                            clipDownloadPage.textTitle.Text,
                            clipDownloadPage.clipId,
                            clipDownloadPage.currentVideoTime,
                            clipDownloadPage.textStreamer.Text,
                            TimeSpan.Zero,
                            clipDownloadPage.clipLength,
                            clipDownloadPage.viewCount,
                            clipDownloadPage.game
                        )
                        + ".mp4"
                    ),
                    Id = clipDownloadPage.clipId,
                    Quality = clipDownloadPage.comboQuality.Text,
                    ThrottleKib = Settings.Default.DownloadThrottleEnabled
                        ? Settings.Default.MaximumBandwidthKib
                        : -1,
                    TempFolder = Settings.Default.TempPath,
                    EncodeMetadata = clipDownloadPage.CheckMetadata.IsChecked!.Value,
                    FfmpegPath = "ffmpeg",
                    FileCollisionCallback = this.HandleFileCollisionCallback
                };

                var downloadTask = new ClipDownloadTask {
                    DownloadOptions = downloadOptions,
                    Info = {
                        Title = clipDownloadPage.textTitle.Text,
                        Thumbnail = clipDownloadPage.imgThumbnail.Source
                    }
                };
                downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                lock (PageQueue.taskLock)
                    PageQueue.taskList.Add(downloadTask);

                if (this.checkChat.IsChecked.GetValueOrDefault()) {
                    var chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                    chatOptions.Id = downloadOptions.Id;
                    if (this.radioJson.IsChecked == true)
                        chatOptions.DownloadFormat = ChatFormat.Json;
                    else if (this.radioHTML.IsChecked == true)
                        chatOptions.DownloadFormat = ChatFormat.Html;
                    else
                        chatOptions.DownloadFormat = ChatFormat.Text;
                    chatOptions.TimeFormat = TimestampFormat.Relative;
                    chatOptions.EmbedData = this.checkEmbed.IsChecked.GetValueOrDefault();
                    chatOptions.Filename = Path.Combine(
                        folderPath,
                        FilenameService.GetFilename(
                            Settings.Default.TemplateChat,
                            downloadTask.Info.Title,
                            chatOptions.Id,
                            clipDownloadPage.currentVideoTime,
                            clipDownloadPage.textStreamer.Text,
                            TimeSpan.Zero,
                            clipDownloadPage.clipLength,
                            clipDownloadPage.viewCount,
                            clipDownloadPage.game
                        )
                        + "."
                        + chatOptions.FileExtension
                    );
                    chatOptions.FileCollisionCallback = this.HandleFileCollisionCallback;

                    var chatTask = new ChatDownloadTask {
                        DownloadOptions = chatOptions,
                        Info = {
                            Title = clipDownloadPage.textTitle.Text,
                            Thumbnail = clipDownloadPage.imgThumbnail.Source
                        }
                    };
                    chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                        PageQueue.taskList.Add(chatTask);

                    if (this.checkRender.IsChecked.GetValueOrDefault()
                        && chatOptions.DownloadFormat == ChatFormat.Json) {
                        var renderOptions = MainWindow.pageChatRender.GetOptions(
                            Path.ChangeExtension(
                                chatOptions.Filename.Replace(".gz", ""),
                                '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()
                            )
                        );
                        if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                            //Just in case VOD and chat paths are the same. Like the previous defaults
                            renderOptions.OutputFile = Path.ChangeExtension(
                                chatOptions.Filename.Replace(".gz", ""),
                                " - CHAT." + MainWindow.pageChatRender.comboFormat.Text.ToLower()
                            );
                        renderOptions.InputFile = chatOptions.Filename;
                        renderOptions.FileCollisionCallback = this.HandleFileCollisionCallback;

                        var renderTask = new ChatRenderTask {
                            DownloadOptions = renderOptions,
                            Info = {
                                Title = clipDownloadPage.textTitle.Text,
                                Thumbnail = clipDownloadPage.imgThumbnail.Source
                            },
                            DependantTask = chatTask
                        };
                        renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                        lock (PageQueue.taskLock)
                            PageQueue.taskList.Add(renderTask);
                    }
                }

                this.Close();
            }

            if (this._parentPage is PageChatDownload chatDownloadPage) {
                var folderPath = this.textFolder.Text;
                if (!Directory.Exists(folderPath)) {
                    MessageBox.Show(
                        this,
                        Strings.InvaliFolderPathMessage,
                        Strings.InvalidFolderPath,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }

                var chatOptions = MainWindow.pageChatDownload.GetOptions(null);
                chatOptions.Id = chatDownloadPage.downloadId;
                chatOptions.Filename = Path.Combine(
                    folderPath,
                    FilenameService.GetFilename(
                        Settings.Default.TemplateChat,
                        chatDownloadPage.textTitle.Text,
                        chatOptions.Id,
                        chatDownloadPage.currentVideoTime,
                        chatDownloadPage.textStreamer.Text,
                        chatOptions.TrimBeginning ? TimeSpan.FromSeconds(chatOptions.TrimBeginningTime) : TimeSpan.Zero,
                        chatOptions.TrimEnding
                            ? TimeSpan.FromSeconds(chatOptions.TrimEndingTime)
                            : chatDownloadPage.vodLength,
                        chatDownloadPage.viewCount,
                        chatDownloadPage.game
                    )
                    + "."
                    + chatOptions.FileExtension
                );
                chatOptions.FileCollisionCallback = this.HandleFileCollisionCallback;

                var chatTask = new ChatDownloadTask {
                    DownloadOptions = chatOptions,
                    Info = {
                        Title = chatDownloadPage.textTitle.Text,
                        Thumbnail = chatDownloadPage.imgThumbnail.Source
                    }
                };
                chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                lock (PageQueue.taskLock)
                    PageQueue.taskList.Add(chatTask);

                if (this.checkRender.IsChecked.GetValueOrDefault() && chatOptions.DownloadFormat == ChatFormat.Json) {
                    var renderOptions = MainWindow.pageChatRender.GetOptions(
                        Path.ChangeExtension(
                            chatOptions.Filename.Replace(".gz", ""),
                            '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()
                        )
                    );
                    renderOptions.InputFile = chatOptions.Filename;
                    renderOptions.FileCollisionCallback = this.HandleFileCollisionCallback;

                    var renderTask = new ChatRenderTask {
                        DownloadOptions = renderOptions,
                        Info = {
                            Title = chatDownloadPage.textTitle.Text,
                            Thumbnail = chatDownloadPage.imgThumbnail.Source
                        },
                        DependantTask = chatTask
                    };
                    renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                    lock (PageQueue.taskLock)
                        PageQueue.taskList.Add(renderTask);
                }

                this.Close();
            }

            if (this._parentPage is PageChatUpdate chatUpdatePage) {
                var folderPath = this.textFolder.Text;
                if (!Directory.Exists(folderPath)) {
                    MessageBox.Show(
                        this,
                        Strings.InvaliFolderPathMessage,
                        Strings.InvalidFolderPath,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }

                var chatOptions = MainWindow.pageChatUpdate.GetOptions(null);
                chatOptions.InputFile = chatUpdatePage.InputFile;
                chatOptions.OutputFile = Path.Combine(
                    folderPath,
                    FilenameService.GetFilename(
                        Settings.Default.TemplateChat,
                        chatUpdatePage.textTitle.Text,
                        chatUpdatePage.VideoId,
                        chatUpdatePage.VideoCreatedAt,
                        chatUpdatePage.textStreamer.Text,
                        chatOptions.TrimBeginning ? TimeSpan.FromSeconds(chatOptions.TrimBeginningTime) : TimeSpan.Zero,
                        chatOptions.TrimEnding
                            ? TimeSpan.FromSeconds(chatOptions.TrimEndingTime)
                            : chatUpdatePage.VideoLength,
                        chatUpdatePage.ViewCount,
                        chatUpdatePage.Game
                    )
                    + "."
                    + chatOptions.FileExtension
                );
                chatOptions.FileCollisionCallback = this.HandleFileCollisionCallback;

                var chatTask = new ChatUpdateTask {
                    UpdateOptions = chatOptions,
                    Info = {
                        Title = chatUpdatePage.textTitle.Text,
                        Thumbnail = chatUpdatePage.imgThumbnail.Source
                    }
                };
                chatTask.ChangeStatus(TwitchTaskStatus.Ready);

                lock (PageQueue.taskLock)
                    PageQueue.taskList.Add(chatTask);

                this.Close();
            }

            if (this._parentPage is PageChatRender chatRenderPage) {
                var folderPath = this.textFolder.Text;
                foreach (var fileName in chatRenderPage.FileNames) {
                    if (!Directory.Exists(folderPath)) {
                        MessageBox.Show(
                            this,
                            Strings.InvaliFolderPathMessage,
                            Strings.InvalidFolderPath,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                        return;
                    }

                    var fileFormat = chatRenderPage.comboFormat.SelectedItem.ToString()!;
                    var filePath = Path.Combine(
                        folderPath,
                        Path.GetFileNameWithoutExtension(fileName) + "." + fileFormat.ToLower()
                    );
                    var renderOptions = MainWindow.pageChatRender.GetOptions(filePath);
                    renderOptions.InputFile = fileName;
                    renderOptions.FileCollisionCallback = this.HandleFileCollisionCallback;

                    var renderTask = new ChatRenderTask {
                        DownloadOptions = renderOptions,
                        Info = {
                            Title = Path.GetFileNameWithoutExtension(filePath)
                        }
                    };

                    if (ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out var image))
                        renderTask.Info.Thumbnail = image;
                    renderTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                        PageQueue.taskList.Add(renderTask);

                    this.Close();
                }
            }
        } else if (this._dataList.Count > 0)
            this.EnqueueDataList();
    }

    private void EnqueueDataList() {
        var folderPath = this.textFolder.Text;
        if (!Directory.Exists(folderPath)) {
            MessageBox.Show(
                this,
                Strings.InvaliFolderPathMessage,
                Strings.InvalidFolderPath,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            return;
        }

        foreach (var taskData in this._dataList) {
            if (this.checkVideo.IsChecked.GetValueOrDefault()) {
                if (taskData.Id.All(char.IsDigit)) {
                    var downloadOptions = new VideoDownloadOptions {
                        Oauth = Settings.Default.OAuth,
                        TempFolder = Settings.Default.TempPath,
                        Id = long.Parse(taskData.Id),
                        Quality = (this.ComboPreferredQuality.SelectedItem as ComboBoxItem)?.Content as string,
                        FfmpegPath = "ffmpeg",
                        TrimBeginning = false,
                        TrimEnding = false,
                        DownloadThreads = Settings.Default.VodDownloadThreads,
                        ThrottleKib = Settings.Default.DownloadThrottleEnabled
                            ? Settings.Default.MaximumBandwidthKib
                            : -1,
                        FileCollisionCallback = this.HandleFileCollisionCallback
                    };
                    downloadOptions.Filename = Path.Combine(
                        folderPath,
                        FilenameService.GetFilename(
                            Settings.Default.TemplateVod,
                            taskData.Title,
                            taskData.Id,
                            taskData.Time,
                            taskData.Streamer,
                            downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : TimeSpan.Zero,
                            downloadOptions.TrimEnding
                                ? downloadOptions.TrimEndingTime
                                : TimeSpan.FromSeconds(taskData.Length),
                            taskData.Views,
                            taskData.Game
                        )
                        + ".mp4"
                    );

                    var downloadTask = new VodDownloadTask {
                        DownloadOptions = downloadOptions,
                        Info = {
                            Title = taskData.Title,
                            Thumbnail = taskData.Thumbnail
                        }
                    };
                    downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                        PageQueue.taskList.Add(downloadTask);
                } else {
                    var downloadOptions = new ClipDownloadOptions {
                        Id = taskData.Id,
                        Quality = (this.ComboPreferredQuality.SelectedItem as ComboBoxItem)?.Content as string,
                        Filename = Path.Combine(
                            folderPath,
                            FilenameService.GetFilename(
                                Settings.Default.TemplateClip,
                                taskData.Title,
                                taskData.Id,
                                taskData.Time,
                                taskData.Streamer,
                                TimeSpan.Zero,
                                TimeSpan.FromSeconds(taskData.Length),
                                taskData.Views,
                                taskData.Game
                            )
                            + ".mp4"
                        ),
                        ThrottleKib = Settings.Default.DownloadThrottleEnabled
                            ? Settings.Default.MaximumBandwidthKib
                            : -1,
                        TempFolder = Settings.Default.TempPath,
                        EncodeMetadata = Settings.Default.EncodeClipMetadata,
                        FfmpegPath = "ffmpeg",
                        FileCollisionCallback = this.HandleFileCollisionCallback
                    };

                    var downloadTask = new ClipDownloadTask {
                        DownloadOptions = downloadOptions,
                        Info = {
                            Title = taskData.Title,
                            Thumbnail = taskData.Thumbnail
                        }
                    };
                    downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                    lock (PageQueue.taskLock)
                        PageQueue.taskList.Add(downloadTask);
                }
            }

            if (this.checkChat.IsChecked.GetValueOrDefault()) {
                var downloadOptions = new ChatDownloadOptions {
                    Compression = this.RadioCompressionNone.IsChecked.GetValueOrDefault()
                        ? ChatCompression.None
                        : ChatCompression.Gzip,
                    EmbedData = this.checkEmbed.IsChecked.GetValueOrDefault(),
                    TimeFormat = TimestampFormat.Relative,
                    Id = taskData.Id,
                    TrimBeginning = false,
                    TrimEnding = false,
                    FileCollisionCallback = this.HandleFileCollisionCallback
                };
                if (this.radioJson.IsChecked == true)
                    downloadOptions.DownloadFormat = ChatFormat.Json;
                else if (this.radioHTML.IsChecked == true)
                    downloadOptions.DownloadFormat = ChatFormat.Html;
                else
                    downloadOptions.DownloadFormat = ChatFormat.Text;
                downloadOptions.Filename = Path.Combine(
                    folderPath,
                    FilenameService.GetFilename(
                        Settings.Default.TemplateChat,
                        taskData.Title,
                        taskData.Id,
                        taskData.Time,
                        taskData.Streamer,
                        downloadOptions.TrimBeginning
                            ? TimeSpan.FromSeconds(downloadOptions.TrimBeginningTime)
                            : TimeSpan.Zero,
                        downloadOptions.TrimEnding
                            ? TimeSpan.FromSeconds(downloadOptions.TrimEndingTime)
                            : TimeSpan.FromSeconds(taskData.Length),
                        taskData.Views,
                        taskData.Game
                    )
                    + "."
                    + downloadOptions.FileExtension
                );

                var downloadTask = new ChatDownloadTask {
                    DownloadOptions = downloadOptions,
                    Info = {
                        Title = taskData.Title,
                        Thumbnail = taskData.Thumbnail
                    }
                };
                downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                lock (PageQueue.taskLock)
                    PageQueue.taskList.Add(downloadTask);

                if (this.checkRender.IsChecked.GetValueOrDefault()
                    && downloadOptions.DownloadFormat == ChatFormat.Json) {
                    var renderOptions =
                        MainWindow.pageChatRender.GetOptions(
                            Path.ChangeExtension(
                                downloadOptions.Filename.Replace(".gz", ""),
                                '.' + MainWindow.pageChatRender.comboFormat.Text.ToLower()
                            )
                        );
                    if (renderOptions.OutputFile.Trim() == downloadOptions.Filename.Trim())
                        //Just in case VOD and chat paths are the same. Like the previous defaults
                        renderOptions.OutputFile = Path.ChangeExtension(
                            downloadOptions.Filename.Replace(".gz", ""),
                            " - CHAT." + MainWindow.pageChatRender.comboFormat.Text.ToLower()
                        );
                    renderOptions.InputFile = downloadOptions.Filename;
                    renderOptions.FileCollisionCallback = this.HandleFileCollisionCallback;

                    var renderTask = new ChatRenderTask {
                        DownloadOptions = renderOptions,
                        Info = {
                            Title = taskData.Title,
                            Thumbnail = taskData.Thumbnail
                        },
                        DependantTask = downloadTask
                    };
                    renderTask.ChangeStatus(TwitchTaskStatus.Waiting);

                    lock (PageQueue.taskLock)
                        PageQueue.taskList.Add(renderTask);
                }
            }
        }

        this.DialogResult = true;
        this.Close();
    }

    private void btnFolder_Click(object sender, RoutedEventArgs e) {
        var dialog = new VistaFolderBrowserDialog();
        if (Directory.Exists(this.textFolder.Text))
            dialog.RootFolder = dialog.RootFolder;
        if (dialog.ShowDialog(this).GetValueOrDefault()) {
            this.textFolder.Text = dialog.SelectedPath;
            Settings.Default.QueueFolder = this.textFolder.Text;
            Settings.Default.Save();
        }
    }

    private void checkChat_Checked(object sender, RoutedEventArgs e) {
        this.checkRender.IsEnabled = true;
        this.radioJson.IsEnabled = true;
        this.radioTxt.IsEnabled = true;
        this.radioHTML.IsEnabled = true;
        this.checkEmbed.IsEnabled = true;
        this.RadioCompressionNone.IsEnabled = true;
        this.RadioCompressionGzip.IsEnabled = true;
        try {
            var appTextBrush = (Brush)Application.Current.Resources["AppText"];
            this.TextDownloadFormat.Foreground = appTextBrush;
            this.TextCompression.Foreground = appTextBrush;
        } catch {
            /* Ignored */
        }
    }

    private void checkChat_Unchecked(object sender, RoutedEventArgs e) {
        this.checkRender.IsEnabled = false;
        this.checkRender.IsChecked = false;
        this.radioJson.IsEnabled = false;
        this.radioTxt.IsEnabled = false;
        this.radioHTML.IsEnabled = false;
        this.checkEmbed.IsEnabled = false;
        this.RadioCompressionNone.IsEnabled = false;
        this.RadioCompressionGzip.IsEnabled = false;
        try {
            var appTextDisabledBrush = (Brush)Application.Current.Resources["AppTextDisabled"];
            this.TextDownloadFormat.Foreground = appTextDisabledBrush;
            this.TextCompression.Foreground = appTextDisabledBrush;
        } catch {
            /* Ignored */
        }
    }

    private void radioJson_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            this.checkEmbed.IsEnabled = true;
            this.checkRender.IsEnabled = true;
            this.StackChatCompression.Visibility = Visibility.Visible;
        }
    }

    private void radioTxt_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            this.checkEmbed.IsEnabled = false;
            this.checkRender.IsEnabled = false;
            this.StackChatCompression.Visibility = Visibility.Collapsed;
        }
    }

    private void radioHTML_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            this.checkEmbed.IsEnabled = true;
            this.checkRender.IsEnabled = false;
            this.StackChatCompression.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_OnSourceInitialized(object sender, EventArgs e) { App.RequestTitleBarChange(); }

    private void CheckVideo_OnChecked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            this.ComboPreferredQuality.IsEnabled = this.checkVideo.IsChecked.GetValueOrDefault();
            try {
                var newBrush = (Brush)Application.Current.Resources[this.checkVideo.IsChecked.GetValueOrDefault()
                    ? "AppText"
                    : "AppTextDisabled"];
                this.TextPreferredQuality.Foreground = newBrush;
            } catch {
                /* Ignored */
            }
        }
    }
}
