using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;
using System.Net;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using SkiaSharp;
using TwitchDownloader.Properties;
using MessageBox = System.Windows.MessageBox;
using TwitchDownloader;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore;
using System.Threading;
using TwitchDownloaderCore.TwitchObjects;
using System.Windows.Navigation;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageChatRender.xaml
    /// </summary>
    public partial class PageChatRender : System.Windows.Controls.Page
    {
        public SKPaint imagePaint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        public SKPaint emotePaint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        public SKFontManager fontManager = SKFontManager.CreateDefault();
        public ConcurrentDictionary<char, SKPaint> fallbackCache = new ConcurrentDictionary<char, SKPaint>();
        public WindowPreview windowPreview = null;
        public PageChatRender()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON Files | *.json";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == true)
            {
                textJson.Text = openFileDialog.FileName;
            }
        }

        private async void btnRender_Click(object sender, RoutedEventArgs e)
        {
            bool validInputs = ValidateInputs();
            if (validInputs)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();

                string fileFormat = comboFormat.SelectedItem.ToString();
                saveFileDialog.Filter = $"{fileFormat} Files | *.{fileFormat.ToLower()}";
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.FileName = Path.GetFileNameWithoutExtension(textJson.Text) + "." + fileFormat.ToLower();

                if (saveFileDialog.ShowDialog() == true)
                {
                    SKColor backgroundColor = new SKColor(colorBackground.SelectedColor.Value.R, colorBackground.SelectedColor.Value.G, colorBackground.SelectedColor.Value.B, colorBackground.SelectedColor.Value.A);
                    SKColor messageColor = new SKColor(colorFont.SelectedColor.Value.R, colorFont.SelectedColor.Value.G, colorFont.SelectedColor.Value.B);
                    SaveSettings();

                    ChatRenderOptions options = new ChatRenderOptions() {
                        InputFile = textJson.Text,
                        OutputFile = saveFileDialog.FileName,
                        BackgroundColor = backgroundColor,
                        ChatHeight = Int32.Parse(textHeight.Text),
                        ChatWidth = Int32.Parse(textWidth.Text),
                        BttvEmotes = (bool)checkBTTV.IsChecked,
                        FfzEmotes = (bool)checkFFZ.IsChecked,
                        Outline = (bool)checkOutline.IsChecked,
                        Font = (string)comboFont.SelectedItem,
                        FontSize = Double.Parse(textFontSize.Text),
                        UpdateRate = Double.Parse(textUpdateTime.Text),
                        Timestamp = (bool)checkTimestamp.IsChecked,
                        MessageColor = messageColor,
                        Framerate = Int32.Parse(textFramerate.Text),
                        InputArgs = Settings.Default.FfmpegInputArgs,
                        OutputArgs = Settings.Default.FfmpegOutputArgs,
                        MessageFontStyle = SKFontStyle.Normal,
                        UsernameFontStyle = SKFontStyle.Bold,
                        GenerateMask = (bool)checkMask.IsChecked,
                        OutlineSize = 4,
                        FfmpegPath = "ffmpeg",
                        TempFolder = Settings.Default.TempPath,
                        SubMessages = (bool)checkSub.IsChecked
                    };
                    options.PaddingLeft = (int)Math.Floor(2 * options.EmoteScale);

                    SetImage("Images/ppOverheat.gif", true);
                    btnRender.IsEnabled = false;

                    ChatRenderer currentRender = new ChatRenderer(options);
                    Progress<ProgressReport> renderProgress = new Progress<ProgressReport>(OnProgressChanged);

                    try
                    {
                        await currentRender.RenderVideoAsync(renderProgress, new CancellationToken());
                        statusMessage.Text = "Done";
                        SetImage("Images/ppHop.gif", true);
                    }
                    catch (Exception ex)
                    {
                        statusMessage.Text = "ERROR";
                        SetImage("Images/peepoSad.png", false);
                        AppendLog("ERROR: " + ex.Message);
                    }
                    statusProgressBar.Value = 0;
                    btnRender.IsEnabled = true;
                }
            }
            else
            {
                MessageBox.Show("Please double check your inputs are valid", "Unable to parse inputs", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void LoadSettings()
        {
            try
            {
                comboFont.SelectedItem = Settings.Default.Font;
                checkOutline.IsChecked = Settings.Default.Outline;
                checkTimestamp.IsChecked = Settings.Default.Timestamp;
                colorBackground.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.BackgroundColorA, (byte)Settings.Default.BackgroundColorR, (byte)Settings.Default.BackgroundColorG, (byte)Settings.Default.BackgroundColorB);
                checkFFZ.IsChecked = Settings.Default.FFZEmotes;
                checkBTTV.IsChecked = Settings.Default.BTTVEmotes;
                textHeight.Text = Settings.Default.Height.ToString();
                textWidth.Text = Settings.Default.Width.ToString();
                textFontSize.Text = Settings.Default.FontSize.ToString("0.##");
                textUpdateTime.Text = Settings.Default.UpdateTime.ToString("0.##");
                colorFont.SelectedColor = System.Windows.Media.Color.FromRgb((byte)Settings.Default.FontColorR, (byte)Settings.Default.FontColorG, (byte)Settings.Default.FontColorB);
                textFramerate.Text = Settings.Default.Framerate.ToString();
                checkMask.IsChecked = Settings.Default.GenerateMask;
                checkSub.IsChecked = Settings.Default.SubMessages;

                foreach (VideoContainer container in comboFormat.Items)
                {
                    if (container.Name == Settings.Default.VideoContainer)
                    {
                        comboFormat.SelectedItem = container;
                        foreach (Codec codec in container.SupportedCodecs)
                        {
                            comboCodec.Items.Add(codec);
                            if (codec.Name == Settings.Default.VideoCodec)
                                comboCodec.SelectedItem = codec;
                        }
                        break;
                    }
                }

                comboFormat.SelectionChanged += ComboFormatOnSelectionChanged;
                comboCodec.SelectionChanged += ComboCodecOnSelectionChanged;
            }
            catch { }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void ComboCodecOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboCodec.SelectedItem != null)
            {
                Settings.Default.FfmpegInputArgs = ((Codec)comboCodec.SelectedItem).InputArgs;
                Settings.Default.FfmpegOutputArgs = ((Codec)comboCodec.SelectedItem).OutputArgs;
                Settings.Default.Save();
            }
        }

        private void ComboFormatOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VideoContainer currentContainer = (VideoContainer)comboFormat.SelectedItem;
            comboCodec.Items.Clear();
            foreach (Codec codec in currentContainer.SupportedCodecs)
            {
                comboCodec.Items.Add(codec);
                if (Settings.Default.VideoCodec == codec.Name)
                    comboCodec.SelectedItem = Settings.Default.VideoCodec;
            }

            if (comboCodec.SelectedItem == null)
                comboCodec.SelectedIndex = 0;
        }

        private void SaveSettings()
        {
            Settings.Default.Font = comboFont.SelectedItem.ToString();
            Settings.Default.Outline = (bool)checkOutline.IsChecked;
            Settings.Default.Timestamp = (bool)checkTimestamp.IsChecked;
            Settings.Default.BackgroundColorR = colorBackground.SelectedColor.Value.R;
            Settings.Default.BackgroundColorG = colorBackground.SelectedColor.Value.G;
            Settings.Default.BackgroundColorB = colorBackground.SelectedColor.Value.B;
            Settings.Default.BackgroundColorA = colorBackground.SelectedColor.Value.A;
            Settings.Default.FFZEmotes = (bool)checkFFZ.IsChecked;
            Settings.Default.BTTVEmotes = (bool)checkBTTV.IsChecked;
            Settings.Default.FontColorR = colorFont.SelectedColor.Value.R;
            Settings.Default.FontColorG = colorFont.SelectedColor.Value.G;
            Settings.Default.FontColorB = colorFont.SelectedColor.Value.B;
            Settings.Default.GenerateMask = (bool)checkMask.IsChecked;
            Settings.Default.SubMessages = (bool)checkSub.IsChecked;
            if (comboFormat.SelectedItem != null)
                Settings.Default.VideoContainer = ((VideoContainer)comboFormat.SelectedItem).Name;
            if (comboCodec.SelectedItem != null)
                Settings.Default.VideoCodec = ((Codec)comboCodec.SelectedItem).Name;
            try
            {
                Settings.Default.Height = Int32.Parse(textHeight.Text);
                Settings.Default.Width = Int32.Parse(textWidth.Text);
                Settings.Default.FontSize = float.Parse(textFontSize.Text);
                Settings.Default.UpdateTime = float.Parse(textUpdateTime.Text);
                Settings.Default.Framerate = Int32.Parse(textFramerate.Text);
            }
            catch { }
            Settings.Default.Save();
        }

        private bool ValidateInputs()
        {
            if (!File.Exists(textJson.Text))
            {
                AppendLog("ERROR: JSON File Not Found");
                return false;
            }

            try
            {
                Double.Parse(textFontSize.Text);
                Int32.Parse(textHeight.Text);
                Int32.Parse(textWidth.Text);
                Double.Parse(textUpdateTime.Text);
                Int32.Parse(textFramerate.Text);
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                return false;
            }

            if (colorBackground.SelectedColor.Value.A < 255)
            {
                if ((((VideoContainer)comboFormat.SelectedItem).Name == "MOV" && ( ((Codec)comboCodec.SelectedItem).Name == "RLE") || ((Codec)comboCodec.SelectedItem).Name == "ProRes") || ((VideoContainer)comboFormat.SelectedItem).Name == "WEBM" || (bool)checkMask.IsChecked)
                {
                    return true;
                }
                else
                {
                    AppendLog("ERROR: You've selected an alpha channel (transparency) for a container/codec that does not support it.");
                    AppendLog("Remove transparency or encode with MOV and RLE/PRORES (file size will be large)");
                    return false;
                }
            }

            if (Int32.Parse(textHeight.Text) % 2 != 0 && Int32.Parse(textWidth.Text) % 2 != 0)
            {
                AppendLog("ERROR: Height and Width must be even");
                return false;
            }

            return true;
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke((Action)(() =>
                textLog.AppendText(message + Environment.NewLine)
            ));
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

        private void Page_Initialized(object sender, EventArgs e)
        {
            List<string> fonts = new List<string>();
            foreach (var fontFamily in fontManager.FontFamilies)
                fonts.Add(fontFamily);
            fonts.Sort();
            foreach (var font in fonts)
            {
                comboFont.Items.Add(font);
            }
            if (comboFont.Items.Contains("Arial"))
                comboFont.SelectedItem = "Arial";

            Codec h264Codec = new Codec() { Name = "H264", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265Codec = new Codec() { Name = "H265", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libx265 -preset veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec vp8Codec = new Codec() { Name = "VP8", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx -crf 18 -b:v 2M -pix_fmt yuva420p -auto-alt-ref 0 \"{save_path}\"" };
            Codec vp9Codec = new Codec() { Name = "VP9", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx-vp9 -crf 18 -b:v 2M -pix_fmt yuva420p \"{save_path}\"" };
            Codec rleCodec = new Codec() { Name = "RLE", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v qtrle -pix_fmt argb \"{save_path}\"" };
            Codec proresCodec = new Codec() { Name = "ProRes", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v prores_ks -qscale:v 62 -pix_fmt argb \"{save_path}\"" };
            VideoContainer mp4Container = new VideoContainer() { Name = "MP4", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec } };
            VideoContainer movContainer = new VideoContainer() { Name = "MOV", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, rleCodec, proresCodec } };
            VideoContainer webmContainer = new VideoContainer() { Name = "WEBM", SupportedCodecs = new List<Codec>() { vp8Codec, vp9Codec } };
            VideoContainer mkvContainer = new VideoContainer() { Name = "MKV", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, vp8Codec, vp9Codec } };
            comboFormat.Items.Add(mp4Container);
            comboFormat.Items.Add(movContainer);
            comboFormat.Items.Add(webmContainer);
            comboFormat.Items.Add(mkvContainer);

            LoadSettings();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            if (windowPreview != null && windowPreview.IsLoaded)
            {
                windowPreview.Close();
            }
        }

        private void btnFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            FfmpegOptions ffmpegOptions = new FfmpegOptions(this);
            ffmpegOptions.ShowDialog();
        }

        private void btnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (windowPreview == null || (windowPreview != null && !windowPreview.IsLoaded))
            {
                windowPreview = new WindowPreview();
                windowPreview.Update(this);
                windowPreview.Show();
            }
        }

        public void Update()
        {
            if (windowPreview != null && windowPreview.IsInitialized)
                windowPreview.Update(this);
        }

        private void UpdatePreview(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Update();
        }

        private void UpdateColor(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            Update();
        }

        private void UpdateFont(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Update();
        }

        private void UpdateCheckbox(object sender, RoutedEventArgs e)
        {
            Update();
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.buymeacoffee.com/lay295");
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
    }

    public class VideoContainer
    {
        public string Name;
        public List<Codec> SupportedCodecs;

        public override string ToString()
        {
            return Name;
        }
    }

    public class Codec
    {
        public string Name;
        public string InputArgs;
        public string OutputArgs;

        public override string ToString()
        {
            return Name;
        }
    }
}