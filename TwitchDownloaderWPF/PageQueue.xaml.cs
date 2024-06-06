using System;
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
        public static object taskLock = new object();
        public static ObservableCollection<ITwitchTask> taskList { get; set; } = new ObservableCollection<ITwitchTask>();
        BackgroundWorker taskManager = new BackgroundWorker();

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

            taskList.CollectionChanged += TaskList_CollectionChanged;
        }

        private void TaskList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {

        }

        private void TaskManager_DoWork(object sender, DoWorkEventArgs e)
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

                foreach (var task in taskList)
                {
                    if (task.Status == TwitchTaskStatus.Running)
                    {
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
                }

                foreach (var task in taskList)
                {
                    if (task.CanRun())
                    {
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
                        continue;
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
            if (sender is not Button { DataContext: ITwitchTask task })
            {
                return;
            }

            CancelTask(task);
        }

        private void MenuItemCancelTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: ITwitchTask task })
            {
                return;
            }

            CancelTask(task);
        }

        private static void CancelTask(ITwitchTask task)
        {
            if (task.CanCancel)
            {
                task.Cancel();
            }
        }

        private void BtnTaskError_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: ITwitchTask task })
            {
                return;
            }

            ShowTaskException(task);
        }

        private void MenuItemTaskError_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: ITwitchTask task })
            {
                return;
            }

            ShowTaskException(task);
        }

        private static void ShowTaskException(ITwitchTask task)
        {
            var taskException = task.Exception;

            if (taskException?.Exception == null)
            {
                return;
            }

            var errorMessage = taskException.Exception.Message;
            if (Settings.Default.VerboseErrors)
            {
                errorMessage = taskException.Exception.ToString();
            }

            MessageBox.Show(Application.Current.MainWindow!, errorMessage, Translations.Strings.MessageBoxTitleError, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void BtnRemoveTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: ITwitchTask task })
            {
                return;
            }

            RemoveTask(task);
        }

        private void MenuItemRemoveTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: ITwitchTask task })
            {
                return;
            }

            RemoveTask(task);
        }

        private static void RemoveTask(ITwitchTask task)
        {
            if (task.CanRun() || task.Status is TwitchTaskStatus.Running or TwitchTaskStatus.Waiting)
            {
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.CancelTaskBeforeRemoving, Translations.Strings.TaskCouldNotBeRemoved, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!taskList.Remove(task))
            {
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.TaskCouldNotBeRemoved, Translations.Strings.UnknownErrorOccurred, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuItemOpenTaskFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { DataContext: ITwitchTask task })
            {
                return;
            }

            FileService.OpenExplorerForFile(new FileInfo(task.OutputFile));
        }
    }
}
