using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Data;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Translations;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF;

/// <summary>
///     Interaction logic for PageQueue.xaml
/// </summary>
public partial class PageQueue : Page {
    public static object taskLock = new();
    private readonly BackgroundWorker taskManager = new();

    public PageQueue() {
        this.InitializeComponent();
        this.queueList.ItemsSource = PageQueue.taskList;

        this.numVod.Value = Settings.Default.LimitVod;
        this.numClip.Value = Settings.Default.LimitClip;
        this.numChat.Value = Settings.Default.LimitChat;
        this.numRender.Value = Settings.Default.LimitRender;

        this.taskManager.DoWork += this.TaskManager_DoWork;
        this.taskManager.RunWorkerAsync();

        PageQueue.taskList.CollectionChanged += this.TaskList_CollectionChanged;
    }

    public static ObservableCollection<ITwitchTask> taskList { get; set; } = new();

    private void TaskList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) { }

    private void TaskManager_DoWork(object sender, DoWorkEventArgs e) {
        while (true) {
            var maxVod = Settings.Default.LimitVod;
            var maxClip = Settings.Default.LimitClip;
            var maxChat = Settings.Default.LimitChat;
            var maxRender = Settings.Default.LimitRender;
            var currentVod = 0;
            var currentClip = 0;
            var currentChat = 0;
            var currentRender = 0;

            foreach (var task in PageQueue.taskList)
                if (task.Status == TwitchTaskStatus.Running)
                    switch (task) {
                        case VodDownloadTask:
                            currentVod++;
                            break;

                        case ClipDownloadTask:
                            currentClip++;
                            break;

                        case ChatDownloadTask:
                            currentChat++;
                            break;

                        case ChatUpdateTask:
                            currentChat++;
                            break;

                        case ChatRenderTask:
                            currentRender++;
                            break;
                    }

            foreach (var task in PageQueue.taskList)
                if (task.CanRun()) {
                    switch (task) {
                        case VodDownloadTask when currentVod < maxVod:
                            currentVod++;
                            task.RunAsync();
                            break;

                        case ClipDownloadTask when currentClip < maxClip:
                            currentClip++;
                            task.RunAsync();
                            break;

                        case ChatDownloadTask when currentChat < maxChat:
                            currentChat++;
                            task.RunAsync();
                            break;

                        case ChatUpdateTask when currentChat < maxChat:
                            currentChat++;
                            task.RunAsync();
                            break;

                        case ChatRenderTask when currentRender < maxRender:
                            currentRender++;
                            task.RunAsync();
                            break;
                    }
                }

            Thread.Sleep(1000);
        }
    }

    private void btnDonate_Click(object sender, RoutedEventArgs e) {
        Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
    }

    private void btnSettings_Click(object sender, RoutedEventArgs e) {
        var settings = new WindowSettings {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        settings.ShowDialog();
        this.btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e) {
        this.btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
    }

    private void numVod_ValueChanged(object sender, FunctionEventArgs<double> e) {
        if (this.IsInitialized) {
            Settings.Default.LimitVod = (int)this.numVod.Value;
            Settings.Default.Save();
        }
    }

    private void numClip_ValueChanged(object sender, FunctionEventArgs<double> e) {
        if (this.IsInitialized) {
            Settings.Default.LimitClip = (int)this.numClip.Value;
            Settings.Default.Save();
        }
    }

    private void numChat_ValueChanged(object sender, FunctionEventArgs<double> e) {
        if (this.IsInitialized) {
            Settings.Default.LimitChat = (int)this.numChat.Value;
            Settings.Default.Save();
        }
    }

    private void numRender_ValueChanged(object sender, FunctionEventArgs<double> e) {
        if (this.IsInitialized) {
            Settings.Default.LimitRender = (int)this.numRender.Value;
            Settings.Default.Save();
        }
    }

    private void btnUrlList_Click(object sender, RoutedEventArgs e) {
        var window = new WindowUrlList {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        window.ShowDialog();
    }

    private void btnVods_Click(object sender, RoutedEventArgs e) {
        var window = new WindowMassDownload(DownloadType.Video) {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        window.ShowDialog();
    }

    private void btnClips_Click(object sender, RoutedEventArgs e) {
        var window = new WindowMassDownload(DownloadType.Clip) {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        window.ShowDialog();
    }

    private void BtnCancelTask_Click(object sender, RoutedEventArgs e) {
        if (sender is not Button { DataContext: ITwitchTask task })
            return;

        CancelTask(task);
    }

    private void MenuItemCancelTask_Click(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem { DataContext: ITwitchTask task })
            return;

        CancelTask(task);
    }

    private static void CancelTask(ITwitchTask task) {
        if (task.CanCancel)
            task.Cancel();
    }

    private void BtnTaskError_Click(object sender, RoutedEventArgs e) {
        if (sender is not Button { DataContext: ITwitchTask task })
            return;

        ShowTaskException(task);
    }

    private void MenuItemTaskError_Click(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem { DataContext: ITwitchTask task })
            return;

        ShowTaskException(task);
    }

    private static void ShowTaskException(ITwitchTask task) {
        var taskException = task.Exception;

        if (taskException?.Exception == null)
            return;

        var errorMessage = taskException.Exception.Message;
        if (Settings.Default.VerboseErrors)
            errorMessage = taskException.Exception.ToString();

        MessageBox.Show(
            Application.Current.MainWindow!,
            errorMessage,
            Strings.MessageBoxTitleError,
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
    }

    private void BtnRemoveTask_Click(object sender, RoutedEventArgs e) {
        if (sender is not Button { DataContext: ITwitchTask task })
            return;

        RemoveTask(task);
    }

    private void MenuItemRemoveTask_Click(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem { DataContext: ITwitchTask task })
            return;

        RemoveTask(task);
    }

    private static void RemoveTask(ITwitchTask task) {
        if (task.CanRun() || task.Status is TwitchTaskStatus.Running or TwitchTaskStatus.Waiting) {
            MessageBox.Show(
                Application.Current.MainWindow!,
                Strings.CancelTaskBeforeRemoving,
                Strings.TaskCouldNotBeRemoved,
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        if (!PageQueue.taskList.Remove(task))
            MessageBox.Show(
                Application.Current.MainWindow!,
                Strings.TaskCouldNotBeRemoved,
                Strings.UnknownErrorOccurred,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
    }

    private void MenuItemOpenTaskFolder_Click(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem { DataContext: ITwitchTask task })
            return;

        FileService.OpenExplorerForFile(new(task.OutputFile));
    }
}
