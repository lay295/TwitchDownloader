using AutoUpdaterDotNET;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TwitchDownloader;
using TwitchDownloader.Properties;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using static TwitchDownloaderWPF.App;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static public PageVodDownload pageVodDownload = new PageVodDownload();
        static public PageClipDownload pageClipDownload = new PageClipDownload();
        static public PageChatDownload pageChatDownload = new PageChatDownload();
        static public PageChatRender pageChatRender = new PageChatRender();
        static public PageQueue pageQueue = new PageQueue();

        public MainWindow()
        {
            InitializeComponent();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        private void btnVodDownload_Click(object sender, RoutedEventArgs e)
        {
            Main.Content = pageVodDownload;
        }

        private void btnClipDownload_Click(object sender, RoutedEventArgs e)
        {
            Main.Content = pageClipDownload;
        }

        private void btnChatDownload_Click(object sender, RoutedEventArgs e)
        {
            Main.Content = pageChatDownload;
        }

        private void btnChatRender_Click(object sender, RoutedEventArgs e)
        {
            Main.Content = pageChatRender;
        }

        private void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            Main.Content = pageQueue;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            themeHelper.UpdateTitleBarTheme(this);

            Main.Content = pageVodDownload;
            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }

            if (!File.Exists("ffmpeg.exe"))
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full);

            Version currentVersion = new Version("1.50.5");

            AutoUpdater.InstalledVersion = currentVersion;
            AutoUpdater.Start("https://downloader-update.twitcharchives.workers.dev");

            Title = String.Format("Twitch Downloader v{0}", currentVersion);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            pageChatRender.SaveSettings();
            string tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TwitchDownloader", "Chat Render");
            try
            {
                DeleteDirectory(tempFolder);
            }
            catch { }
        }

        public static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }

        internal static string GetFilename(string template, string title, string id, DateTime date, string channel)
        {
            StringBuilder returnString = new StringBuilder(template.Replace("{title}", title).Replace("{id}", id).Replace("{channel}", channel).Replace("{date}", date.ToString("Mdyy")));
            Regex regex = new Regex("{date_custom=\"(.*)\"}");
            bool done = false;
            while (!done)
            {
                Match match = regex.Match(returnString.ToString());
                if (match.Success)
                {
                    string formatString = match.Groups[1].ToString();
                    returnString.Remove(match.Groups[0].Index, match.Groups[0].Length);
                    returnString.Insert(match.Groups[0].Index, date.ToString(formatString));
                }
                else
                {
                    done = true;
                }
            }

            return RemoveInvalidChars(returnString.ToString());
        }

        public static string RemoveInvalidChars(string filename)
        {
            return string.Concat(filename.Split(System.IO.Path.GetInvalidFileNameChars()));
        }
    }
}
