using Microsoft.Win32;
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
using TwitchDownloaderCore.TwitchObjects.Gql;
using WpfAnimatedGif;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageChatUpdate.xaml
    /// </summary>
    public partial class PageChatUpdate : Page
    {

        public string InputFile;
        public ChatRoot ChatJsonInfo;
        public string VideoId;
        public DateTime VideoCreatedAt;

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
                InputFile = openFileDialog.FileName;
                SetEnabled(true);

                if (Path.GetExtension(InputFile).ToLower() == ".json")
                {
                    ChatJsonInfo = await ChatJson.DeserializeAsync(InputFile, getComments: false, getEmbeds: false);
                    textStreamer.Text = ChatJsonInfo.streamer.name;
                    textCreatedAt.Text = ChatJsonInfo.video.created_at.ToShortDateString();
                    textTitle.Text = ChatJsonInfo.video.title ?? "Unknown";

                    VideoCreatedAt = ChatJsonInfo.video.created_at;

                    TimeSpan chatStart = TimeSpan.FromSeconds(ChatJsonInfo.video.start);
                    numStartHour.Value = (int)chatStart.TotalHours;
                    numStartMinute.Value = chatStart.Minutes;
                    numStartSecond.Value = chatStart.Seconds;

                    TimeSpan chatEnd = TimeSpan.FromSeconds(ChatJsonInfo.video.end);
                    numEndHour.Value = (int)chatEnd.TotalHours;
                    numEndMinute.Value = chatEnd.Minutes;
                    numEndSecond.Value = chatEnd.Seconds;

                    TimeSpan videoLength = TimeSpan.FromSeconds(double.IsNegative(ChatJsonInfo.video.length) ? 0.0 : ChatJsonInfo.video.length);
                    labelLength.Text = videoLength.Seconds > 0
                        ? string.Format("{0:00}:{1:00}:{2:00}", (int)videoLength.TotalHours, videoLength.Minutes, videoLength.Seconds)
                        : "Unknown";

                    VideoId = ChatJsonInfo.video.id ?? "-1";

                    if (VideoId.All(char.IsDigit))
                    {
                        GqlVideoResponse videoInfo = await TwitchHelper.GetVideoInfo(int.Parse(VideoId));
                        if (videoInfo.data.video == null)
                        {
                            AppendLog("ERROR: Unable to find thumbnail: VOD is expired or embedded ID is corrupt");
                        }
                        else
                        {
                            videoLength = TimeSpan.FromSeconds(videoInfo.data.video.lengthSeconds);
                            labelLength.Text = string.Format("{0:00}:{1:00}:{2:00}", (int)videoLength.TotalHours, videoLength.Minutes, videoLength.Seconds);

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
                    else
                    {
                        GqlClipResponse videoInfo = await TwitchHelper.GetClipInfo(VideoId);
                        if (videoInfo.data.clip.video == null)
                        {
                            AppendLog("ERROR: Unable to find thumbnail: VOD is expired or embedded ID is corrupt");
                        }
                        else
                        {
                            videoLength = TimeSpan.FromSeconds(videoInfo.data.clip.durationSeconds);
                            labelLength.Text = string.Format("{0:00}:{1:00}:{2:00}", (int)videoLength.TotalHours, videoLength.Minutes, videoLength.Seconds);

                            Task<BitmapImage> taskThumb = InfoHelper.GetThumb(videoInfo.data.clip.thumbnailURL);
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
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(false);
            SetEnabledCropStart(false);
            SetEnabledCropEnd(false);
            checkEmbedMissing.IsChecked = Settings.Default.ChatEmbedMissing;
            checkReplaceEmbeds.IsChecked = Settings.Default.ChatReplaceEmbeds;
            checkBttvEmbed.IsChecked = Settings.Default.BTTVEmotes;
            checkFfzEmbed.IsChecked = Settings.Default.FFZEmotes;
            checkStvEmbed.IsChecked = Settings.Default.STVEmotes;
            _ = (ChatFormat)Settings.Default.ChatDownloadType switch
            {
                ChatFormat.Text => radioText.IsChecked = true,
                ChatFormat.Html => radioHTML.IsChecked = true,
                _ => radioJson.IsChecked = true
            };
        }

        private void SetEnabled(bool isEnabled)
        {
            checkStart.IsEnabled = isEnabled;
            checkEnd.IsEnabled = isEnabled;
            checkEmbedMissing.IsEnabled = isEnabled;
            checkReplaceEmbeds.IsEnabled = isEnabled;
            btnDownload.IsEnabled = isEnabled;
            btnQueue.IsEnabled = isEnabled;
            radioRelative.IsEnabled = isEnabled;
            radioUTC.IsEnabled = isEnabled;
            radioNone.IsEnabled = isEnabled;
            radioJson.IsEnabled = isEnabled;
            radioText.IsEnabled = isEnabled;
            radioHTML.IsEnabled = isEnabled;

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

        public ChatUpdateOptions GetOptions(string outputFile)
        {
            ChatUpdateOptions options = new ChatUpdateOptions()
            {
                EmbedMissing = (bool)checkEmbedMissing.IsChecked,
                ReplaceEmbeds = (bool)checkReplaceEmbeds.IsChecked,
                BttvEmotes = (bool)checkBttvEmbed.IsChecked,
                FfzEmotes = (bool)checkFfzEmbed.IsChecked,
                StvEmotes = (bool)checkStvEmbed.IsChecked,
                InputFile = textJson.Text,
                OutputFile = outputFile,
                CropBeginningTime = -1,
                CropEndingTime = -1
            };

            if ((bool)radioJson.IsChecked)
                options.OutputFormat = ChatFormat.Json;
            else if ((bool)radioHTML.IsChecked)
                options.OutputFormat = ChatFormat.Html;
            else if ((bool)radioText.IsChecked)
                options.OutputFormat = ChatFormat.Text;

            if (checkStart.IsChecked == true)
            {
                options.CropBeginning = true;
                TimeSpan start = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                options.CropBeginningTime = (int)Math.Round(start.TotalSeconds);
            }
            if (checkEnd.IsChecked == true)
            {
                options.CropEnding = true;
                TimeSpan end = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
                options.CropEndingTime = (int)Math.Round(end.TotalSeconds);
            }

            if ((bool)radioUTC.IsChecked)
                options.TextTimestampFormat = TimestampFormat.Utc;
            else if ((bool)radioRelative.IsChecked)
                options.TextTimestampFormat = TimestampFormat.Relative;
            else if ((bool)radioNone.IsChecked)
                options.TextTimestampFormat = TimestampFormat.None;

            return options;
        }

        private void OnProgressChanged(ProgressReport progress)
        {
            switch (progress.ReportType)
            {
                case ReportType.Percent:
                    statusProgressBar.Value = (int)progress.Data;
                    break;
                case ReportType.Status or ReportType.StatusInfo:
                    statusMessage.Text = (string)progress.Data;
                    break;
                case ReportType.Log:
                    AppendLog((string)progress.Data);
                    break;
            }
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
                Settings.Default.ChatEmbedMissing = true;
                Settings.Default.ChatReplaceEmbeds = false;
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
                Settings.Default.ChatEmbedMissing = false;
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
                Settings.Default.ChatEmbedMissing = false;
                Settings.Default.ChatReplaceEmbeds = true;
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
                Settings.Default.ChatReplaceEmbeds = false;
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

                if ((bool)radioJson.IsChecked)
                    saveFileDialog.Filter = "JSON Files | *.json";
                if ((bool)radioHTML.IsChecked)
                    saveFileDialog.Filter = "HTML Files | *.html;*.htm";
                if ((bool)radioText.IsChecked)
                    saveFileDialog.Filter = "TXT Files | *.txt";

                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.FileName = MainWindow.GetFilename(Settings.Default.TemplateChat, textTitle.Text, ChatJsonInfo.video.id ?? "-1", VideoCreatedAt, textStreamer.Text);

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        ChatUpdateOptions updateOptions = GetOptions(saveFileDialog.FileName);

                        ChatUpdater currentUpdate = new ChatUpdater(updateOptions);
                        await currentUpdate.ParseJsonAsync(new CancellationToken());

                        btnBrowse.IsEnabled = false;
                        SetEnabled(false);
                        SetImage("Images/ppOverheat.gif", true);
                        statusMessage.Text = "Downloading";

                        Progress<ProgressReport> downloadProgress = new Progress<ProgressReport>(OnProgressChanged);

                        try
                        {
                            await currentUpdate.UpdateAsync(downloadProgress, new CancellationToken());
                            await Task.Delay(300); // we need to wait a bit incase the "writing to output file" report comes late
                            textJson.Text = "";
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

                        currentUpdate = null;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                    catch (Exception ex)
                    {
                        AppendLog("ERROR: " + ex.Message);
                    }
                }
            }
        }

        private void radioJson_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                timeText.Visibility = Visibility.Collapsed;
                timeOptions.Visibility = Visibility.Collapsed;
                stackEmbedText.Visibility = Visibility.Visible;
                stackEmbedChecks.Visibility = Visibility.Visible;
                textCrop.Margin = new Thickness(0, 15, 0, 0);

                Settings.Default.ChatDownloadType = (int)ChatFormat.Json;
                Settings.Default.Save();
            }
        }

        private void radioHTML_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                timeText.Visibility = Visibility.Collapsed;
                timeOptions.Visibility = Visibility.Collapsed;
                stackEmbedText.Visibility = Visibility.Visible;
                stackEmbedChecks.Visibility = Visibility.Visible;
                textCrop.Margin = new Thickness(0, 15, 0, 0);

                Settings.Default.ChatDownloadType = (int)ChatFormat.Html;
                Settings.Default.Save();
            }
        }

        private void radioText_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                timeText.Visibility = Visibility.Visible;
                timeOptions.Visibility = Visibility.Visible;
                stackEmbedText.Visibility = Visibility.Collapsed;
                stackEmbedChecks.Visibility = Visibility.Collapsed;
                textCrop.Margin = new Thickness(0, 10, 0, 0);

                Settings.Default.ChatDownloadType = (int)ChatFormat.Text;
                Settings.Default.Save();
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