using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TwitchDownloader;
using TwitchDownloader.Properties;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;
using WpfAnimatedGif;

namespace TwitchDownloaderWPF
{
	public enum DownloadType { Clip, Video }
	/// <summary>
	/// Interaction logic for PageChatDownload.xaml
	/// </summary>
	public partial class PageChatDownload : Page
	{

		public DownloadType downloadType;
		public string downloadId;
		public int streamerId;
		public DateTime currentVideoTime;

		public PageChatDownload()
		{
			InitializeComponent();
		}

		private void Page_Initialized(object sender, EventArgs e)
		{
			SetEnabled(false, false);
			SetEnabledCropStart(false);
			SetEnabledCropEnd(false);
			checkEmbed.IsChecked = Settings.Default.ChatEmbedEmotes;
			checkBttvEmbed.IsChecked = Settings.Default.BTTVEmotes;
			checkFfzEmbed.IsChecked = Settings.Default.FFZEmotes;
			checkStvEmbed.IsChecked = Settings.Default.STVEmotes;
			numChatDownloadConnections.Value = Settings.Default.ChatDownloadThreads;
			switch (Settings.Default.ChatDownloadType)
			{
				case (int)DownloadFormat.Text:
					radioText.IsChecked = true;
					break;
				case (int)DownloadFormat.Html:
					radioHTML.IsChecked = true;
					break;
				case (int)DownloadFormat.Json:
					radioJson.IsChecked = true;
					break;
				default:
					break;
			}
		}

		private void SetEnabled(bool isEnabled, bool isClip)
		{
			checkStart.IsEnabled = isEnabled & !isClip;
			checkEnd.IsEnabled = isEnabled & !isClip;
			radioRelative.IsEnabled = isEnabled;
			radioUTC.IsEnabled = isEnabled;
			radioNone.IsEnabled = isEnabled;
			checkEmbed.IsEnabled = isEnabled;
			checkBttvEmbed.IsEnabled = isEnabled;
			checkFfzEmbed.IsEnabled = isEnabled;
			checkStvEmbed.IsEnabled = isEnabled;
			btnDownload.IsEnabled = isEnabled;
			btnQueue.IsEnabled = isEnabled;
			radioJson.IsEnabled = isEnabled;
			radioText.IsEnabled = isEnabled;
			radioHTML.IsEnabled = isEnabled;
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

		private async void btnGetInfo_Click(object sender, RoutedEventArgs e)
		{
			string id = ValidateUrl(textUrl.Text);
			if (id != "")
			{
				btnGetInfo.IsEnabled = false;
				downloadId = id;
				if (id.All(Char.IsDigit))
					downloadType = DownloadType.Video;
				else
					downloadType = DownloadType.Clip;

				try
				{
					if (downloadType == DownloadType.Video)
					{
						GqlVideoResponse taskInfo = await TwitchHelper.GetVideoInfo(Int32.Parse(downloadId));

						string thumbUrl = taskInfo.data.video.thumbnailURLs.FirstOrDefault();
						Task<BitmapImage> taskThumb = InfoHelper.GetThumb(thumbUrl);

						try
						{
							await taskThumb;
						}
						catch
						{
							AppendLog("ERROR: Unable to find thumbnail");
						}
						if (!taskThumb.IsFaulted)
							imgThumbnail.Source = taskThumb.Result;
						textTitle.Text = taskInfo.data.video.title;
						textStreamer.Text = taskInfo.data.video.owner.displayName;
						textCreatedAt.Text = taskInfo.data.video.createdAt.ToString();
						currentVideoTime = taskInfo.data.video.createdAt.ToLocalTime();
						streamerId = int.Parse(taskInfo.data.video.owner.id);
						SetEnabled(true, false);
					}
					else if (downloadType == DownloadType.Clip)
					{
						string clipId = downloadId;
						GqlClipResponse taskInfo = await TwitchHelper.GetClipInfo(clipId);

						string thumbUrl = taskInfo.data.clip.thumbnailURL;
						Task<BitmapImage> taskThumb = InfoHelper.GetThumb(thumbUrl);
						await Task.WhenAll(taskThumb);

						imgThumbnail.Source = taskThumb.Result;
						textStreamer.Text = taskInfo.data.clip.broadcaster.displayName;
						textCreatedAt.Text = taskInfo.data.clip.createdAt.ToString();
						currentVideoTime = taskInfo.data.clip.createdAt.ToLocalTime();
						textTitle.Text = taskInfo.data.clip.title;
						streamerId = int.Parse(taskInfo.data.clip.broadcaster.id);
						SetEnabled(true, true);
						SetEnabledCropStart(false);
						SetEnabledCropEnd(false);
					}

					btnGetInfo.IsEnabled = true;
				}
				catch (Exception ex)
				{
					MessageBox.Show("Unable to get Clip/Video information. Please double check your link and try again", "Unable to get info", MessageBoxButton.OK, MessageBoxImage.Error);
					AppendLog("ERROR: " + ex.Message);
					btnGetInfo.IsEnabled = true;
				}
			}
			else
			{
				MessageBox.Show("Please double check the VOD/Clip link", "Unable to parse input", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		static public string ValidateUrl(string text)
		{
			Regex clipRegex = new Regex(@"twitch.tv\/(\S+)\/clip\/");
			if (text.All(Char.IsLetter) || text.All(Char.IsDigit))
			{
				return text;
			}
			else if (text.Contains("twitch.tv/videos/"))
			{
				int number;
				Uri url = new UriBuilder(text).Uri;
				string path = String.Format("{0}{1}{2}{3}", url.Scheme, Uri.SchemeDelimiter, url.Authority, url.AbsolutePath);
				bool success = Int32.TryParse(Regex.Match(path, @"\d+").Value, out number);
				if (success)
					return number.ToString();
				else
					return "";
			}
			else if (text.Contains("clips.twitch.tv/") || clipRegex.IsMatch(text))
			{
				Uri url = new UriBuilder(text).Uri;
				string path = String.Format("{0}{1}{2}{3}", url.Scheme, Uri.SchemeDelimiter, url.Authority, url.AbsolutePath);
				return path.Split('/').Last();
			}
			return "";
		}

		private void AppendLog(string message)
		{
			textLog.Dispatcher.BeginInvoke(() =>
				textLog.AppendText(message + Environment.NewLine)
			);
		}

		public ChatDownloadOptions GetOptions(string filename)
		{
			ChatDownloadOptions options = new ChatDownloadOptions();

			if (radioJson.IsChecked == true)
				options.DownloadFormat = DownloadFormat.Json;
			else if (radioHTML.IsChecked == true)
				options.DownloadFormat = DownloadFormat.Html;
			else if (radioText.IsChecked == true)
				options.DownloadFormat = DownloadFormat.Text;

			options.Timestamp = true;
			options.EmbedEmotes = (bool)checkEmbed.IsChecked;
			options.BttvEmotes = (bool)checkBttvEmbed.IsChecked;
			options.FfzEmotes = (bool)checkFfzEmbed.IsChecked;
			options.StvEmotes = (bool)checkStvEmbed.IsChecked;
			options.Filename = filename;
			options.ConnectionCount = (int)numChatDownloadConnections.Value;
			return options;
		}

		private void OnProgressChanged(ProgressReport progress)
		{
			if (progress.reportType == ReportType.Percent)
				statusProgressBar.Value = (int)progress.data;
			if (progress.reportType == ReportType.Message || progress.reportType == ReportType.MessageInfo)
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

		private void radioText_Checked(object sender, RoutedEventArgs e)
		{
			if (this.IsInitialized)
			{
				timeText.Visibility = Visibility.Visible;
				timeOptions.Visibility = Visibility.Visible;
				checkEmbed.IsEnabled = false;
				checkBttvEmbed.IsEnabled = false;
				checkFfzEmbed.IsEnabled = false;
				checkStvEmbed.IsEnabled = false;
				textCrop.Margin = new Thickness(0, 14, 0, 0);

				Settings.Default.ChatDownloadType = (int)DownloadFormat.Text;
				Settings.Default.Save();
			}
		}

		private void numChatDownloadConnections_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
		{
			if (this.IsInitialized)
			{
				numChatDownloadConnections.Value = Math.Clamp((int)numChatDownloadConnections.Value, 1, 50);
				Settings.Default.ChatDownloadThreads = (int)numChatDownloadConnections.Value;
				Settings.Default.Save();
			}
		}

		private void checkEmbed_Checked(object sender, RoutedEventArgs e)
		{
			if (this.IsInitialized)
			{
				Settings.Default.ChatEmbedEmotes = true;
				Settings.Default.Save();
				thirdPartyEmoteText.Visibility = Visibility.Visible;
				thirdPartyEmbedOptions.Visibility = Visibility.Visible;
			}
		}

		private void checkEmbed_Unchecked(object sender, RoutedEventArgs e)
		{
			if (this.IsInitialized)
			{
				Settings.Default.ChatEmbedEmotes = false;
				Settings.Default.Save();
				thirdPartyEmoteText.Visibility = Visibility.Collapsed;
				thirdPartyEmbedOptions.Visibility = Visibility.Collapsed;
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

		private void radioJson_Checked(object sender, RoutedEventArgs e)
		{
			if (this.IsInitialized)
			{
				timeText.Visibility = Visibility.Collapsed;
				timeOptions.Visibility = Visibility.Collapsed;
				checkEmbed.IsEnabled = true;
				checkBttvEmbed.IsEnabled = true;
				checkFfzEmbed.IsEnabled = true;
				checkStvEmbed.IsEnabled = true;
				textCrop.Margin = new Thickness(0, 20, 0, 0);

				Settings.Default.ChatDownloadType = (int)DownloadFormat.Json;
				Settings.Default.Save();
			}
		}

		private void radioHTML_Checked(object sender, RoutedEventArgs e)
		{
			if (this.IsInitialized)
			{
				timeText.Visibility = Visibility.Collapsed;
				timeOptions.Visibility = Visibility.Collapsed;
				checkEmbed.IsEnabled = true;
				checkBttvEmbed.IsEnabled = true;
				checkFfzEmbed.IsEnabled = true;
				checkStvEmbed.IsEnabled = true;
				textCrop.Margin = new Thickness(0, 20, 0, 0);

				Settings.Default.ChatDownloadType = (int)DownloadFormat.Html;
				Settings.Default.Save();
			}
		}

		private async void SplitButton_Click(object sender, RoutedEventArgs e)
		{
			if (!((HandyControl.Controls.SplitButton)sender).IsDropDownOpen)
			{
				SaveFileDialog saveFileDialog = new SaveFileDialog();

				if (radioJson.IsChecked == true)
					saveFileDialog.Filter = "JSON Files | *.json";
				else if (radioHTML.IsChecked == true)
					saveFileDialog.Filter = "HTML Files | *.html";
				else
					saveFileDialog.Filter = "TXT Files | *.txt";

				saveFileDialog.RestoreDirectory = true;
				saveFileDialog.FileName = MainWindow.GetFilename(Settings.Default.TemplateChat, textTitle.Text, downloadId, currentVideoTime, textStreamer.Text);

				if (saveFileDialog.ShowDialog() == true)
				{
					try
					{
						ChatDownloadOptions downloadOptions = GetOptions(saveFileDialog.FileName);
						if (downloadType == DownloadType.Video)
						{
							int startTime = 0;
							int endTime = 0;

							if (checkStart.IsChecked == true)
							{
								downloadOptions.CropBeginning = true;
								TimeSpan start = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
								startTime = (int)Math.Round(start.TotalSeconds);
								downloadOptions.CropBeginningTime = startTime;
							}

							if (checkEnd.IsChecked == true)
							{
								downloadOptions.CropEnding = true;
								TimeSpan end = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
								endTime = (int)Math.Round(end.TotalSeconds);
								downloadOptions.CropEndingTime = endTime;
							}

							downloadOptions.Id = downloadId;
						}
						else
						{
							downloadOptions.Id = downloadId;
						}

						if ((bool)radioUTC.IsChecked)
							downloadOptions.TimeFormat = TimestampFormat.Utc;
						if ((bool)radioRelative.IsChecked)
							downloadOptions.TimeFormat = TimestampFormat.Relative;
						if ((bool)radioNone.IsChecked)
							downloadOptions.TimeFormat = TimestampFormat.None;

						ChatDownloader currentDownload = new ChatDownloader(downloadOptions);

						btnGetInfo.IsEnabled = false;
						SetEnabled(false, false);
						SetImage("Images/ppOverheat.gif", true);
						statusMessage.Text = "Downloading";

						Progress<ProgressReport> downloadProgress = new Progress<ProgressReport>(OnProgressChanged);

						try
						{
							await currentDownload.DownloadAsync(downloadProgress, new CancellationToken());
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
						btnGetInfo.IsEnabled = true;
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