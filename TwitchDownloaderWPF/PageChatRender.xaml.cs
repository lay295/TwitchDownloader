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
using System.Collections.ObjectModel;
using HandyControl.Controls;
using System.Windows.Interop;
using System.Drawing;

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

        public ChatRenderOptions GetOptions(string filename)
        {
            ChatRenderOptions options = new ChatRenderOptions();
            SKColor backgroundColor = new SKColor(colorBackground.SelectedColor.Value.R, colorBackground.SelectedColor.Value.G, colorBackground.SelectedColor.Value.B, colorBackground.SelectedColor.Value.A);
            SKColor messageColor = new SKColor(colorFont.SelectedColor.Value.R, colorFont.SelectedColor.Value.G, colorFont.SelectedColor.Value.B);
            options.OutputFile = filename;
            options.InputFile = textJson.Text;
            options.BackgroundColor = backgroundColor;
            options.ChatHeight = Int32.Parse(textHeight.Text);
            options.ChatWidth = Int32.Parse(textWidth.Text);
            options.BttvEmotes = (bool)checkBTTV.IsChecked;
            options.FfzEmotes = (bool)checkFFZ.IsChecked;
            options.StvEmotes = (bool)checkSTV.IsChecked;
            options.Outline = (bool)checkOutline.IsChecked;
            options.Font = (string)comboFont.SelectedItem;
            options.FontSize = numFontSize.Value;
            options.UpdateRate = Double.Parse(textUpdateTime.Text);
            options.EmoteScale = Double.Parse(textEmoteScale.Text);
            options.IgnoreUsersList = textIgnoreUsersList.Text
                .ToLower()
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            options.Timestamp = (bool)checkTimestamp.IsChecked;
            options.MessageColor = messageColor;
            options.Framerate = Int32.Parse(textFramerate.Text);
            options.InputArgs = textFfmpegInput.Text;
            options.OutputArgs = textFfmpegOutput.Text;
            options.MessageFontStyle = SKFontStyle.Normal;
            options.UsernameFontStyle = SKFontStyle.Bold;
            options.GenerateMask = (bool)checkMask.IsChecked;
            options.OutlineSize = 4;
            options.FfmpegPath = "ffmpeg";
            options.TempFolder = Settings.Default.TempPath;
            options.SubMessages = (bool)checkSub.IsChecked;
            options.ChatBadges = (bool)checkBadge.IsChecked;
            foreach (var item in comboBadges.SelectedItems)
            {
                options.ChatBadgeMask += (int)((ChatBadgeListItem)item).Type;
            }

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
                checkSTV.IsChecked = Settings.Default.STVEmotes;
                textHeight.Text = Settings.Default.Height.ToString();
                textWidth.Text = Settings.Default.Width.ToString();
                numFontSize.Value = Settings.Default.FontSize;
                textUpdateTime.Text = Settings.Default.UpdateTime.ToString("0.0#");
                colorFont.SelectedColor = System.Windows.Media.Color.FromRgb((byte)Settings.Default.FontColorR, (byte)Settings.Default.FontColorG, (byte)Settings.Default.FontColorB);
                textFramerate.Text = Settings.Default.Framerate.ToString();
                checkMask.IsChecked = Settings.Default.GenerateMask;
                checkSub.IsChecked = Settings.Default.SubMessages;
                checkBadge.IsChecked = Settings.Default.ChatBadges;
                textEmoteScale.Text = Settings.Default.EmoteScale.ToString("0.0#");

                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.Broadcaster, Name = "Broadcaster" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.Moderator, Name = "Mods" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.VIP, Name = "VIPs" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.Subscriber, Name = "Subs" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.Predictions, Name = "Predictions" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.NoAudioVisual, Name = "No Audio/Visual" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.PrimeGaming, Name = "Prime" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.Other, Name = "Others" });

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

                LoadFfmpegArgs();
            }
            catch { }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void ComboCodecOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboCodec.SelectedItem != null)
            {
                LoadFfmpegArgs();
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

        public void SaveSettings()
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
            Settings.Default.STVEmotes = (bool)checkSTV.IsChecked;
            Settings.Default.FontColorR = colorFont.SelectedColor.Value.R;
            Settings.Default.FontColorG = colorFont.SelectedColor.Value.G;
            Settings.Default.FontColorB = colorFont.SelectedColor.Value.B;
            Settings.Default.GenerateMask = (bool)checkMask.IsChecked;
            Settings.Default.SubMessages = (bool)checkSub.IsChecked;
            Settings.Default.ChatBadges = (bool)checkBadge.IsChecked;
            if (comboFormat.SelectedItem != null)
                Settings.Default.VideoContainer = ((VideoContainer)comboFormat.SelectedItem).Name;
            if (comboCodec.SelectedItem != null)
                Settings.Default.VideoCodec = ((Codec)comboCodec.SelectedItem).Name;
            try
            {
                Settings.Default.Height = Int32.Parse(textHeight.Text);
                Settings.Default.Width = Int32.Parse(textWidth.Text);
                Settings.Default.FontSize = (float)numFontSize.Value;
                Settings.Default.UpdateTime = float.Parse(textUpdateTime.Text);
                Settings.Default.Framerate = Int32.Parse(textFramerate.Text);
                Settings.Default.EmoteScale = Int32.Parse(textEmoteScale.Text);
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
                Int32.Parse(textHeight.Text);
                Int32.Parse(textWidth.Text);
                Double.Parse(textUpdateTime.Text);
                Int32.Parse(textFramerate.Text);
                Double.Parse(textEmoteScale.Text);
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

            if (Int32.Parse(textHeight.Text) % 2 != 0 || Int32.Parse(textWidth.Text) % 2 != 0)
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
            imageWarning.Source = Imaging.CreateBitmapSourceFromHIcon(SystemIcons.Warning.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            List<string> fonts = new List<string>();
            foreach (var fontFamily in fontManager.FontFamilies)
                fonts.Add(fontFamily);
            fonts.Add("Inter");
            fonts.Sort();
            foreach (var font in fonts)
            {
                comboFont.Items.Add(font);
            }
            comboFont.SelectedItem = "Inter";

            Codec h264Codec = new Codec() { Name = "H264", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h264NvencCodec = new Codec() { Name = "H264 NVIDIA", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v h264_nvenc -preset fast -cq 20 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265Codec = new Codec() { Name = "H265", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libx265 -preset veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec vp8Codec = new Codec() { Name = "VP8", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx -crf 18 -b:v 2M -pix_fmt yuva420p -auto-alt-ref 0 \"{save_path}\"" };
            Codec vp9Codec = new Codec() { Name = "VP9", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx-vp9 -crf 18 -b:v 2M -pix_fmt yuva420p \"{save_path}\"" };
            Codec rleCodec = new Codec() { Name = "RLE", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v qtrle -pix_fmt argb \"{save_path}\"" };
            Codec proresCodec = new Codec() { Name = "ProRes", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v prores_ks -qscale:v 62 -pix_fmt argb \"{save_path}\"" };
            VideoContainer mp4Container = new VideoContainer() { Name = "MP4", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, h264NvencCodec } };
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            textFfmpegInput.Text = ((Codec)comboCodec.SelectedItem).InputArgs;
            textFfmpegOutput.Text = ((Codec)comboCodec.SelectedItem).OutputArgs;

            SaveArguments();
        }

        private void SaveArguments()
        {
            List<CustomFfmpegArgs> args = JsonConvert.DeserializeObject<List<CustomFfmpegArgs>>(Settings.Default.FfmpegArguments);

            bool foundArg = false;
            foreach (CustomFfmpegArgs arg in args)
            {
                if (arg.CodecName == ((Codec)comboCodec.SelectedItem).Name && arg.ContainerName == ((VideoContainer)comboFormat.SelectedItem).Name)
                {
                    arg.InputArgs = textFfmpegInput.Text;
                    arg.OutputArgs = textFfmpegOutput.Text;
                    foundArg = true;
                    break;
                }
            }

            //Didn't find pre-existing save, make a new one
            if (!foundArg)
            {
                CustomFfmpegArgs newArgs = new CustomFfmpegArgs() { CodecName = ((Codec)comboCodec.SelectedItem).Name, ContainerName = ((VideoContainer)comboFormat.SelectedItem).Name, InputArgs = textFfmpegInput.Text, OutputArgs = textFfmpegOutput.Text };
                args.Add(newArgs);
            }

            Settings.Default.FfmpegArguments = JsonConvert.SerializeObject(args);
            Settings.Default.Save();
        }

        private void LoadFfmpegArgs()
        {
            List<CustomFfmpegArgs> args = JsonConvert.DeserializeObject<List<CustomFfmpegArgs>>(Settings.Default.FfmpegArguments);

            bool foundArg = false;
            foreach (CustomFfmpegArgs arg in args)
            {
                if (arg.CodecName == ((Codec)comboCodec.SelectedItem).Name && arg.ContainerName == ((VideoContainer)comboFormat.SelectedItem).Name)
                {
                    textFfmpegInput.Text = arg.InputArgs;
                    textFfmpegOutput.Text = arg.OutputArgs;
                    foundArg = true;
                    break;
                }
            }

            if (!foundArg)
            {
                textFfmpegInput.Text = ((Codec)comboCodec.SelectedItem).InputArgs;
                textFfmpegOutput.Text = ((Codec)comboCodec.SelectedItem).OutputArgs;
            }
        }

        private void textFfmpegInput_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveArguments();
        }

        private void textFfmpegOutput_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveArguments();
        }

        private async void SplitButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender == null || !((SplitButton)sender).IsDropDownOpen)
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

                        ChatRenderOptions options = GetOptions(saveFileDialog.FileName);

                        SetImage("Images/ppOverheat.gif", true);
                        btnRender.IsEnabled = false;

                        ChatRenderer currentRender = new ChatRenderer(options);
                        await currentRender.ParseJson();

                        if (sender == null)
                        {
                            //We're just gonna assume a caller with a null sender is the partial render button
                            WindowRangeSelect window = new WindowRangeSelect(currentRender);
                            window.ShowDialog();

                            if (window.OK)
                            {
                                options.StartOverride = window.startSeconds;
                                options.EndOverride = window.endSeconds;
                            }
                            else
                            {
                                if (window.Invalid)
                                    AppendLog("Invalid start or end time");
                                return;
                            }
                        }

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
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInputs())
            {
                WindowQueueOptions queueOptions = new WindowQueueOptions(this);
                queueOptions.ShowDialog();
            }
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            SplitButton_Click(null, null);
        }
    }

    public class ChatBadgeListItem
    {
        public ChatBadgeType Type { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
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
