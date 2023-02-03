using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Properties;
using WpfAnimatedGif;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageClipDownload.xaml
    /// </summary>
    public partial class PageClipDownload : Page
    {
        public string clipId = "";
        public DateTime currentVideoTime;
        public PageClipDownload()
        {
            InitializeComponent();
        }

        private async void btnGetInfo_Click(object sender, RoutedEventArgs e)
        {
            clipId = ValidateUrl(textUrl.Text);
            if (string.IsNullOrWhiteSpace(clipId))
            {
                MessageBox.Show(Translations.Strings.InvalidClipLinkIdMessage.Replace(@"\n", Environment.NewLine), Translations.Strings.InvalidClipLinkId, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                try
                {
                    btnGetInfo.IsEnabled = false;
                    comboQuality.Items.Clear();
                    Task<GqlClipResponse> taskClipInfo = TwitchHelper.GetClipInfo(clipId);
                    Task<List<GqlClipTokenResponse>> taskLinks = TwitchHelper.GetClipLinks(clipId);
                    await Task.WhenAll(taskClipInfo, taskLinks);

                    GqlClipResponse clipData = taskClipInfo.Result;

                    try
                    {
                        string thumbUrl = clipData.data.clip.thumbnailURL;
                        imgThumbnail.Source = await InfoHelper.GetThumb(thumbUrl);
                    }
                    catch
                    {
                        AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail);
                        var (success, image) = await InfoHelper.TryGetThumb(InfoHelper.THUMBNAIL_MISSING_URL);
                        if (success)
                        {
                            imgThumbnail.Source = image;
                        }
                    }
                    TimeSpan clipLength = TimeSpan.FromSeconds(taskClipInfo.Result.data.clip.durationSeconds);
                    textStreamer.Text = clipData.data.clip.broadcaster.displayName;
                    textCreatedAt.Text = clipData.data.clip.createdAt.ToString();
                    currentVideoTime = clipData.data.clip.createdAt.ToLocalTime();
                    textTitle.Text = clipData.data.clip.title;
                    labelLength.Text = string.Format("{0:00}:{1:00}:{2:00}", (int)clipLength.TotalHours, clipLength.Minutes, clipLength.Seconds);

                    foreach (var quality in taskLinks.Result[0].data.clip.videoQualities)
                    {
                        comboQuality.Items.Add(new TwitchClip(quality.quality, quality.frameRate.ToString(), quality.sourceURL));
                    }

                    comboQuality.SelectedIndex = 0;
                    comboQuality.IsEnabled = true;
                    btnDownload.IsEnabled = true;
                    btnQueue.IsEnabled = true;
                    btnGetInfo.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Translations.Strings.UnableToGetClipInfo, Translations.Strings.UnableToGetInfo, MessageBoxButton.OK, MessageBoxImage.Error);
                    AppendLog(Translations.Strings.ErrorLog + ex);
                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    btnGetInfo.IsEnabled = true;
                }
            }
        }

        private string ValidateUrl(string text)
        {
            Regex clipRegex = new Regex(@"twitch.tv\/(\S+)\/clip\/");
            if (text.All(Char.IsLetter))
            {
                return text;
            }
            else if (text.Contains("clips.twitch.tv/") || clipRegex.IsMatch(text))
            {
                Uri url = new UriBuilder(text).Uri;
                string path = String.Format("{0}{1}{2}{3}", url.Scheme, Uri.SchemeDelimiter, url.Authority, url.AbsolutePath);
                return path.Split('/').Last();
            }
            return null;
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke(() =>
                textLog.AppendText(message + Environment.NewLine)
            );
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            comboQuality.IsEnabled = false;
            btnDownload.IsEnabled = false;
            btnQueue.IsEnabled = false;
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

        private async void SplitButton_Click(object sender, RoutedEventArgs e)
        {
            if (!((HandyControl.Controls.SplitButton)sender).IsDropDownOpen)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();

                saveFileDialog.Filter = "MP4 Files | *.mp4";
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.FileName = MainWindow.GetFilename(Settings.Default.TemplateClip, textTitle.Text, clipId, currentVideoTime, textStreamer.Text);

                if (saveFileDialog.ShowDialog() == true)
                {
                    comboQuality.IsEnabled = false;
                    btnGetInfo.IsEnabled = false;
                    btnDownload.IsEnabled = false;
                    btnQueue.IsEnabled = false;
                    SetImage("Images/ppOverheat.gif", true);
                    statusMessage.Text = Translations.Strings.StatusDownloading;
                    try
                    {
                        ClipDownloadOptions downloadOptions = new ClipDownloadOptions();
                        downloadOptions.Filename = saveFileDialog.FileName;
                        downloadOptions.Id = clipId;
                        downloadOptions.Quality = comboQuality.Text;
                        await new ClipDownloader(downloadOptions).DownloadAsync();

                        statusMessage.Text = Translations.Strings.StatusDone;
                        SetImage("Images/ppHop.gif", true);
                    }
                    catch (Exception ex)
                    {
                        statusMessage.Text = Translations.Strings.StatusError;
                        SetImage("Images/peepoSad.png", false);
                        AppendLog(Translations.Strings.ErrorLog + ex.Message);
                        if (Settings.Default.VerboseErrors)
                        {
                            MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    btnGetInfo.IsEnabled = true;
                    btnDownload.IsEnabled = true;
                    btnQueue.IsEnabled = true;
                    statusProgressBar.Value = 0;
                }
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            WindowQueueOptions queueOptions = new WindowQueueOptions(this);
            queueOptions.ShowDialog();
        }
    }
}

public class TwitchClip
{
    public string quality { get; set; }
    public string framerate { get; set; }
    public string url { get; set; }

    public TwitchClip(string Quality, string Framerate, string Url)
    {
        quality = Quality;
        framerate = Framerate;
        url = Url;
    }

    override
    public string ToString()
    {
        //Only show framerate if it's not 30fps
        return String.Format("{0}p{1}", quality, framerate == "30" ? "" : framerate);
    }
}