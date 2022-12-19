using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TwitchDownloader.Properties;
using TwitchDownloader.TwitchTasks;
using TwitchDownloaderWPF;

namespace TwitchDownloader
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
                IEnumerable<ITwitchTask> tasks = taskList.AsEnumerable();

                foreach (var task in tasks)
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

                foreach (var task in tasks)
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
            WindowUrlList window = new WindowUrlList();
            window.ShowDialog();
        }

        private void btnVods_Click(object sender, RoutedEventArgs e)
        {
            WindowMassDownload window = new WindowMassDownload(DownloadType.Video);
            window.ShowDialog();
        }

        private void btnClips_Click(object sender, RoutedEventArgs e)
        {
            WindowMassDownload window = new WindowMassDownload(DownloadType.Clip);
            window.ShowDialog();
        }

        private void btnCancelTask_Click(object sender, RoutedEventArgs e)
        {
            Button cancelButton = (Button)sender;
            cancelButton.IsEnabled = false;

            ITwitchTask task = (ITwitchTask)cancelButton.DataContext;
            if (task.Status is TwitchTaskStatus.Failed or TwitchTaskStatus.Cancelled or TwitchTaskStatus.Finished)
            {
                return;
            }

            task.Cancel();
        }

        private void btnTaskError_Click(object sender, RoutedEventArgs e)
        {
            Button errorButton = (Button)sender;
            TwitchTaskException taskException = ((ITwitchTask)errorButton.DataContext).Exception;

            if (taskException == null || taskException.Exception == null)
            {
                return;
            }

            string errorMessage = taskException.Exception.Message;
            if (Settings.Default.VerboseErrors)
            {
                errorMessage = taskException.Exception.ToString();
            }

            MessageBox.Show(errorMessage, "Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
