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
                List<ITwitchTask> tasks = taskList.ToList();

                foreach (var task in tasks)
                {
                    if (task.Status == TwitchTaskStatus.Running)
                    {
                        if (task is VodDownloadTask)
                            currentVod++;
                        if (task is ClipDownloadTask)
                            currentClip++;
                        if (task is ChatDownloadTask)
                            currentChat++;
                        if (task is ChatUpdateTask)
                            currentChat++;
                        if (task is ChatRenderTask)
                            currentRender++;
                    }
                }

                foreach (var task in tasks)
                {
                    if (task.CanRun())
                    {
                        if (task is VodDownloadTask && (currentVod + 1) <= maxVod)
                        {
                            currentVod++;
                            task.RunAsync();
                        }
                        if (task is ClipDownloadTask && (currentClip + 1) <= maxClip)
                        {
                            currentClip++;
                            task.RunAsync();
                        }
                        if (task is ChatDownloadTask && (currentChat + 1) <= maxChat)
                        {
                            currentChat++;
                            task.RunAsync();
                        }
                        if (task is ChatUpdateTask && (currentChat + 1) <= maxChat)
                        {
                            currentChat++;
                            task.RunAsync();
                        }
                        if (task is ChatRenderTask && (currentRender + 1) <= maxRender)
                        {
                            currentRender++;
                            task.RunAsync();
                        }
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

        private void btnList_Click(object sender, RoutedEventArgs e)
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
    }
}
