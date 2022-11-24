﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TwitchDownloader;
using TwitchDownloader.Properties;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;
using WpfAnimatedGif;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageChatUpdate.xaml
    /// </summary>
    public partial class PageChatUpdate : Page
    {

        public string downloadId;
        public int streamerId;
        public DateTime currentVideoTime;

        public PageChatUpdate()
        {
            InitializeComponent();
        }

        private async void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON Files | *.json";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == true)
            {
                textJson.Text = openFileDialog.FileName;
                SetEnabled(true);

                if (Path.GetExtension(openFileDialog.FileName).ToLower() == ".json")
                {
                    ChatRoot chatJsonInfo = await ChatJsonTools.ParseJsonInfoAsync(openFileDialog.FileName);
                    textStreamer.Text = chatJsonInfo.streamer.name;
                    textCreatedAt.Text = /*chatJsonInfo.video.created_at*/null ?? "Unknown";
                    textTitle.Text = /*chatJsonInfo.video.title*/null ?? "Unknown";

                    TimeSpan chatStart = TimeSpan.FromSeconds(chatJsonInfo.video.start);
                    numStartHour.Value = (int)chatStart.TotalHours;
                    numStartMinute.Value = chatStart.Minutes;
                    numStartSecond.Value = chatStart.Seconds;

                    TimeSpan chatEnd = TimeSpan.FromSeconds(chatJsonInfo.video.end);
                    numEndHour.Value = (int)chatEnd.TotalHours;
                    numEndMinute.Value = chatEnd.Minutes;
                    numEndSecond.Value = chatEnd.Seconds;

                    GqlVideoResponse videoInfo = await TwitchHelper.GetVideoInfo(/*chatJsonInfo.video.id*/0);
                    if (videoInfo.data.video == null)
                    {
                        AppendLog("ERROR: Unable to find thumbnail: VOD is expired or embedded ID is corrupt");

                        TimeSpan vodLength = TimeSpan.FromSeconds(/*chatJsonInfo.video.length ??*/0.0);
                        labelLength.Text = vodLength.Seconds > 0
                            ? string.Format("{0:00}:{1:00}:{2:00}", (int)vodLength.TotalHours, vodLength.Minutes, vodLength.Seconds)
                            : "Unknown";
                    }
                    else
                    {
                        TimeSpan vodLength = TimeSpan.FromSeconds(videoInfo.data.video.lengthSeconds);
                        labelLength.Text = string.Format("{0:00}:{1:00}:{2:00}", (int)vodLength.TotalHours, vodLength.Minutes, vodLength.Seconds);

                        Task<BitmapImage> taskThumb = InfoHelper.GetThumb(videoInfo.data.video.thumbnailURLs.FirstOrDefault());
                        try
                        {
                            imgThumbnail.Source = await taskThumb;
                        }
                        catch
                        {
                            AppendLog("ERROR: Unable to find thumbnail");
                        }
                    }
                }
            }
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(false);
            SetEnabledCropStart(false);
            SetEnabledCropEnd(false);
            checkEmbedMissing.IsChecked = Settings.Default.ChatEmbedEmotes;
            checkReplaceEmbeds.IsChecked = Settings.Default.ChatEmbedEmotes;
            checkBttvEmbed.IsChecked = Settings.Default.BTTVEmotes;
            checkFfzEmbed.IsChecked = Settings.Default.FFZEmotes;
            checkStvEmbed.IsChecked = Settings.Default.STVEmotes;
        }

        private void SetEnabled(bool isEnabled)
        {
            checkStart.IsEnabled = isEnabled;
            checkEnd.IsEnabled = isEnabled;
            checkEmbedMissing.IsEnabled = isEnabled;
            checkReplaceEmbeds.IsEnabled = isEnabled;
            btnDownload.IsEnabled = isEnabled;
            btnQueue.IsEnabled = isEnabled;

            if (isEnabled)
                return;

            checkBttvEmbed.IsEnabled = isEnabled;
            checkFfzEmbed.IsEnabled = isEnabled;
            checkStvEmbed.IsEnabled = isEnabled;
        }

        private void SetEnabledCropStart(bool isEnabled)
        {
            numStartHour.IsEnabled = isEnabled;
            numStartMinute.IsEnabled = isEnabled;
            numStartSecond.IsEnabled = isEnabled;
        }

        private void SetEnabledCropEnd(bool isEnabled)
        {
            numEndHour.IsEnabled = isEnabled;
            numEndMinute.IsEnabled = isEnabled;
            numEndSecond.IsEnabled = isEnabled;
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke(() =>
                textLog.AppendText(message + Environment.NewLine)
            );
        }

        public ChatUpdateOptions GetOptions(string filename)
        {
            ChatUpdateOptions options = new ChatUpdateOptions()
            {
                EmbedMissing = (bool)checkEmbedMissing.IsChecked,
                ReplaceEmbeds = (bool)checkReplaceEmbeds.IsChecked,
                BttvEmotes = (bool)checkBttvEmbed.IsChecked,
                FfzEmotes = (bool)checkFfzEmbed.IsChecked,
                StvEmotes = (bool)checkStvEmbed.IsChecked,
                InputFile = textJson.Text,
                OutputFile = filename,
                FileFormat = Path.GetExtension(filename)!.ToLower() switch
                {
                    ".json" => ChatFormat.Json,
                    ".html" or ".htm" => ChatFormat.Html,
                    _ => ChatFormat.Text // Default is needed to properly throw NIE in ChatUpdater.UpdateAsync()
                }
            };

            return options;
        }

        private void OnProgressChanged(ProgressReport progress)
        {
            if (progress.reportType == ReportType.Percent)
                statusProgressBar.Value = (int)progress.data;
            if (progress.reportType == ReportType.Status || progress.reportType == ReportType.StatusInfo)
                statusMessage.Text = (string)progress.data;
            if (progress.reportType == ReportType.Log)
                AppendLog((string)progress.data);
        }

        public void SetImage(string imageUri, bool isGif)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(imageUri, UriKind.Relative);
            image.EndInit();
            if (isGif)
                ImageBehavior.SetAnimatedSource(statusImage, image);
            else
            {
                ImageBehavior.SetAnimatedSource(statusImage, null);
                statusImage.Source = image;
            }
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPage settings = new SettingsPage();
            settings.ShowDialog();
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void checkEmbedMissing_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedEmotes = true;
                Settings.Default.Save();
                checkReplaceEmbeds.IsChecked = false;
                checkBttvEmbed.IsEnabled = true;
                checkFfzEmbed.IsEnabled = true;
                checkStvEmbed.IsEnabled = true;
            }
        }

        private void checkEmbedMissing_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedEmotes = false;
                Settings.Default.Save();
                checkBttvEmbed.IsEnabled = false;
                checkFfzEmbed.IsEnabled = false;
                checkStvEmbed.IsEnabled = false;
            }
        }

        private void checkReplaceEmbeds_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedEmotes = true;
                Settings.Default.Save();
                checkEmbedMissing.IsChecked = false;
                checkBttvEmbed.IsEnabled = true;
                checkFfzEmbed.IsEnabled = true;
                checkStvEmbed.IsEnabled = true;
            }
        }

        private void checkReplaceEmbeds_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedEmotes = false;
                Settings.Default.Save();
                checkBttvEmbed.IsEnabled = false;
                checkFfzEmbed.IsEnabled = false;
                checkStvEmbed.IsEnabled = false;
            }
        }

        private void checkBttvEmbed_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.BTTVEmotes = true;
                Settings.Default.Save();
            }
        }

        private void checkBttvEmbed_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.BTTVEmotes = false;
                Settings.Default.Save();
            }
        }

        private void checkFfzEmbed_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.FFZEmotes = true;
                Settings.Default.Save();
            }
        }

        private void checkFfzEmbed_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.FFZEmotes = false;
                Settings.Default.Save();
            }
        }

        private void checkStvEmbed_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.STVEmotes = true;
                Settings.Default.Save();
            }
        }

        private void checkStvEmbed_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.STVEmotes = false;
                Settings.Default.Save();
            }
        }

        private async void SplitButton_Click(object sender, RoutedEventArgs e)
        {
            if (!((HandyControl.Controls.SplitButton)sender).IsDropDownOpen)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();

                saveFileDialog.Filter = Path.GetExtension(textJson.Text)!.ToLower() switch
                {
                    ".json" => "JSON Files | *.json",
                    ".html" or ".htm" => "HTML Files | *.html;*.htm",
                    _ => "Text Files | *.txt"
                };

                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.FileName = MainWindow.GetFilename(Settings.Default.TemplateChat, textTitle.Text, downloadId, currentVideoTime, textStreamer.Text);

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        ChatUpdateOptions updateOptions = GetOptions(saveFileDialog.FileName);

                        int startTime = -1;
                        int endTime = -1;

                        if (checkStart.IsChecked == true)
                        {
                            updateOptions.CropBeginning = true;
                            TimeSpan start = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                            startTime = (int)Math.Round(start.TotalSeconds);
                            updateOptions.CropBeginningTime = startTime;
                        }

                        if (checkEnd.IsChecked == true)
                        {
                            updateOptions.CropEnding = true;
                            TimeSpan end = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
                            endTime = (int)Math.Round(end.TotalSeconds);
                            updateOptions.CropEndingTime = endTime;
                        }

                        ChatUpdater currentDownload = new ChatUpdater(updateOptions);
                        await currentDownload.ParseJsonAsync();

                        btnBrowse.IsEnabled = false;
                        SetEnabled(false);
                        SetImage("Images/ppOverheat.gif", true);
                        statusMessage.Text = "Downloading";

                        Progress<ProgressReport> downloadProgress = new Progress<ProgressReport>(OnProgressChanged);

                        try
                        {
                            await currentDownload.UpdateAsync(downloadProgress, new CancellationToken());
                            statusMessage.Text = "Done";
                            SetImage("Images/ppHop.gif", true);
                        }
                        catch (Exception ex)
                        {
                            statusMessage.Text = "ERROR";
                            SetImage("Images/peepoSad.png", false);
                            AppendLog("ERROR: " + ex.Message);
                            if (Settings.Default.VerboseErrors)
                            {
                                MessageBox.Show(ex.ToString(), "Verbose error output", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        btnBrowse.IsEnabled = true;
                        statusProgressBar.Value = 0;
                    }
                    catch (Exception ex)
                    {
                        AppendLog("ERROR: " + ex.Message);
                    }
                }
            }
        }

        private void checkStart_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledCropStart((bool)checkStart.IsChecked);
        }

        private void checkEnd_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledCropEnd((bool)checkEnd.IsChecked);
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            WindowQueueOptions queueOptions = new WindowQueueOptions(this);
            queueOptions.ShowDialog();
        }
    }
}