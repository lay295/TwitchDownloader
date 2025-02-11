using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TwitchDownloaderWPF.TwitchTasks;
using TwitchDownloaderWPF.Properties;
using System.Diagnostics;
using System.IO;
using TwitchDownloaderWPF.Services;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageQueue.xaml
    /// </summary>
    public partial class PageQueue : Page
    {
        public static readonly object taskLock = new object();
        public static ObservableCollection<TwitchTask> taskList { get; } = new();
        private static readonly BackgroundWorker taskManager = new BackgroundWorker();

        public PageQueue()
        {
            InitializeComponent();
            queueList.ItemsSource = taskList;

            numVod.Value = Settings.Default.LimitVod;
            numClip.Value = Settings.Default.LimitClip;
            numChat.Value = Settings.Default.LimitChat;
            numRender.Value = Settings.Default.LimitRender;

            taskManager.DoWork += TaskManager_DoWork;
            taskManager.RunWorkerAsync();
        }

        private static void TaskManager_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                int maxVod = Settings.Default.LimitVod;
                int maxClip = Settings.Default.LimitClip;
                int maxChat = Settings.Default.LimitChat;
                int maxRender = Settings.Default.LimitRender;
                int currentVod = 0;
                int currentClip = 0;
                int currentChat = 0;
                int currentRender = 0;

                lock (taskLock)
                {
                    foreach (var task in taskList)
                    {
                        if (task.Status is not TwitchTaskStatus.Running)
                            continue;

                        switch (task)
                        {
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
                    }

                    foreach (var task in taskList)
                    {
                        if (!task.CanRun())
                            continue;

                        switch (task)
                        {
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
                }

                Thread.Sleep(1000);
            }
        }

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

        private void numVod_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.LimitVod = (int)numVod.Value;
                Settings.Default.Save();
            }
        }

        private void numClip_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.LimitClip = (int)numClip.Value;
                Settings.Default.Save();
            }
        }

        private void numChat_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.LimitChat = (int)numChat.Value;
                Settings.Default.Save();
            }
        }

        private void numRender_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.LimitRender = (int)numRender.Value;
                Settings.Default.Save();
            }
        }

        private void btnUrlList_Click(object sender, RoutedEventArgs e)
        {
            var window = new WindowUrlList
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
        }

        private void btnVods_Click(object sender, RoutedEventArgs e)
        {
            var window = new WindowMassDownload(DownloadType.Video)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
        }

        private void btnClips_Click(object sender, RoutedEventArgs e)
        {
            var window = new WindowMassDownload(DownloadType.Clip)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
        }

        private void BtnCancelTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            CancelTask(task);
        }

        private void MenuItemCancelTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            CancelTask(task);
        }

        private static void CancelTask(TwitchTask task)
        {
            if (task.CanCancel)
            {
                task.Cancel();
            }
        }

        private void BtnTaskError_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            ShowTaskException(task);
        }

        private void MenuItemTaskError_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            ShowTaskException(task);
        }

        private static void ShowTaskException(TwitchTask task)
        {
            var taskException = task.Exception;

            if (taskException is null)
            {
                return;
            }

            var errorMessage = taskException.Message;
            if (Settings.Default.VerboseErrors)
            {
                errorMessage = taskException.ToString();
            }

            MessageBox.Show(Application.Current.MainWindow!, errorMessage, Translations.Strings.MessageBoxTitleError, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void BtnRemoveTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            RemoveTask(task);
        }

        private void MenuItemRemoveTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            RemoveTask(task);
        }

        private static void RemoveTask(TwitchTask task)
        {
            if (task.CanRun() || task.Status is TwitchTaskStatus.Running or TwitchTaskStatus.Waiting)
            {
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.CancelTaskBeforeRemoving, Translations.Strings.TaskCouldNotBeRemoved, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            lock (taskLock)
            {
                if (!taskList.Remove(task))
                {
                    MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.TaskCouldNotBeRemoved, Translations.Strings.UnknownErrorOccurred, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuItemOpenTaskFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            FileService.OpenExplorerForFile(new FileInfo(task.OutputFile));
        }

        private void MenuItemCopyTaskPath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            if (!ClipboardService.TrySetText(task.OutputFile, out var exception))
            {
                MessageBox.Show(Application.Current.MainWindow!, exception.ToString(), Translations.Strings.FailedToCopyToClipboard, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRetryTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            RetryTask(task);
        }

        private void MenuItemTaskRetry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: TwitchTask task })
            {
                return;
            }

            RetryTask(task);
        }

        private static void RetryTask(TwitchTask task)
        {
            if (task.CanReinitialize)
            {
                task.Reinitialize();
            }
        }

        private void BtnMoveTaskUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            lock (taskLock)
            {
                var index = taskList.IndexOf(task);
                if (index < 1)
                    return;

                taskList.Move(index, index - 1);
            }
        }

        private void BtnMoveTaskDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: TwitchTask task })
            {
                return;
            }

            lock (taskLock)
            {
                var index = taskList.IndexOf(task);
                if (index == -1 || index == taskList.Count - 1)
                    return;

                taskList.Move(index, index + 1);
            }
        }
    }
}
