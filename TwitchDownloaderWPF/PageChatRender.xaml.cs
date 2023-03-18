using HandyControl.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderWPF.Properties;
using WpfAnimatedGif;
using MessageBox = System.Windows.MessageBox;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageChatRender.xaml
    /// </summary>
    public partial class PageChatRender : Page
    {
        public SKPaint imagePaint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        public SKPaint emotePaint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        public List<string> ffmpegLog = new List<string>();
        public SKFontManager fontManager = SKFontManager.CreateDefault();
        public ConcurrentDictionary<char, SKPaint> fallbackCache = new ConcurrentDictionary<char, SKPaint>();
        public string[] FileNames = Array.Empty<string>();
        private CancellationTokenSource _cancellationTokenSource;

        public PageChatRender()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON Files | *.json;*.json.gz";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() == false)
            {
                return;
            }

            FileNames = openFileDialog.FileNames;
            textJson.Text = string.Join("&&", FileNames);
            UpdateActionButtons(false);
        }

        private void UpdateActionButtons(bool isRendering)
        {
            if (isRendering)
            {
                SplitBtnRender.Visibility = Visibility.Collapsed;
                BtnEnqueue.Visibility = Visibility.Collapsed;
                BtnCancel.Visibility = Visibility.Visible;
                return;
            }
            if (FileNames.Length > 1)
            {
                SplitBtnRender.Visibility = Visibility.Collapsed;
                BtnEnqueue.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Collapsed;
                return;
            }
            SplitBtnRender.Visibility = Visibility.Visible;
            BtnEnqueue.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
        }

        public ChatRenderOptions GetOptions(string filename)
        {
            SKColor backgroundColor = new(colorBackground.SelectedColor.Value.R, colorBackground.SelectedColor.Value.G, colorBackground.SelectedColor.Value.B, colorBackground.SelectedColor.Value.A);
            SKColor messageColor = new(colorFont.SelectedColor.Value.R, colorFont.SelectedColor.Value.G, colorFont.SelectedColor.Value.B);
            ChatRenderOptions options = new()
            {
                OutputFile = filename,
                InputFile = textJson.Text,
                BackgroundColor = backgroundColor,
                ChatHeight = int.Parse(textHeight.Text),
                ChatWidth = int.Parse(textWidth.Text),
                BttvEmotes = (bool)checkBTTV.IsChecked,
                FfzEmotes = (bool)checkFFZ.IsChecked,
                StvEmotes = (bool)checkSTV.IsChecked,
                Outline = (bool)checkOutline.IsChecked,
                Font = (string)comboFont.SelectedItem,
                FontSize = numFontSize.Value,
                UpdateRate = double.Parse(textUpdateTime.Text, CultureInfo.CurrentCulture),
                EmoteScale = double.Parse(textEmoteScale.Text, CultureInfo.CurrentCulture),
                BadgeScale = double.Parse(textBadgeScale.Text, CultureInfo.CurrentCulture),
                EmojiScale = double.Parse(textEmojiScale.Text, CultureInfo.CurrentCulture),
                SidePaddingScale = double.Parse(textSidePaddingScale.Text, CultureInfo.CurrentCulture),
                SectionHeightScale = double.Parse(textSectionHeightScale.Text, CultureInfo.CurrentCulture),
                WordSpacingScale = double.Parse(textWordSpaceScale.Text, CultureInfo.CurrentCulture),
                EmoteSpacingScale = double.Parse(textEmoteSpaceScale.Text, CultureInfo.CurrentCulture),
                AccentIndentScale = double.Parse(textAccentIndentScale.Text, CultureInfo.CurrentCulture),
                AccentStrokeScale = double.Parse(textAccentStrokeScale.Text, CultureInfo.CurrentCulture),
                VerticalSpacingScale = double.Parse(textVerticalScale.Text, CultureInfo.CurrentCulture),
                IgnoreUsersArray = textIgnoreUsersList.Text.ToLower().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                BannedWordsArray = textBannedWordsList.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                Timestamp = (bool)checkTimestamp.IsChecked,
                MessageColor = messageColor,
                Framerate = int.Parse(textFramerate.Text),
                InputArgs = CheckRenderSharpening.IsChecked == true ? textFfmpegInput.Text + " -filter_complex \"smartblur=lr=1:ls=-1.0\"" : textFfmpegInput.Text,
                OutputArgs = textFfmpegOutput.Text,
                MessageFontStyle = SKFontStyle.Normal,
                UsernameFontStyle = SKFontStyle.Bold,
                GenerateMask = (bool)checkMask.IsChecked,
                OutlineSize = 4,
                FfmpegPath = "ffmpeg",
                TempFolder = Settings.Default.TempPath,
                SubMessages = (bool)checkSub.IsChecked,
                ChatBadges = (bool)checkBadge.IsChecked,
                Offline = (bool)checkOffline.IsChecked,
                DisperseCommentOffsets = (bool)checkDispersion.IsChecked,
                LogFfmpegOutput = true
            };
            foreach (var item in comboBadges.SelectedItems)
            {
                options.ChatBadgeMask += (int)((ChatBadgeListItem)item).Type;
            }

            return options;
        }

        private void OnProgressChanged(ProgressReport progress)
        {
            switch (progress.ReportType)
            {
                case ReportType.Percent:
                    statusProgressBar.Value = (int)progress.Data;
                    break;
                case ReportType.NewLineStatus or ReportType.SameLineStatus:
                    statusMessage.Text = (string)progress.Data;
                    break;
                case ReportType.Log:
                    AppendLog((string)progress.Data);
                    break;
                case ReportType.FfmpegLog:
                    ffmpegLog.Add((string)progress.Data);
                    break;
            }
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
                CheckRenderSharpening.IsChecked = Settings.Default.ChatRenderSharpening;
                checkSub.IsChecked = Settings.Default.SubMessages;
                checkBadge.IsChecked = Settings.Default.ChatBadges;
                textEmoteScale.Text = Settings.Default.EmoteScale.ToString("0.0#");
                textEmojiScale.Text = Settings.Default.EmojiScale.ToString("0.0#");
                textBadgeScale.Text = Settings.Default.BadgeScale.ToString("0.0#");
                textVerticalScale.Text = Settings.Default.VerticalSpacingScale.ToString("0.0#");
                textSidePaddingScale.Text = Settings.Default.LeftSpacingScale.ToString("0.0#");
                textSectionHeightScale.Text = Settings.Default.SectionHeightScale.ToString("0.0#");
                textWordSpaceScale.Text = Settings.Default.WordSpacingScale.ToString("0.0#");
                textEmoteSpaceScale.Text = Settings.Default.EmoteSpacingScale.ToString("0.0#");
                textAccentStrokeScale.Text = Settings.Default.AccentStrokeScale.ToString("0.0#");
                textAccentIndentScale.Text = Settings.Default.AccentIndentScale.ToString("0.0#");
                textIgnoreUsersList.Text = Settings.Default.IgnoreUsersList;
                textBannedWordsList.Text = Settings.Default.BannedWordsList;
                checkOffline.IsChecked = Settings.Default.Offline;
                checkDispersion.IsChecked = Settings.Default.DisperseCommentOffsets;

                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.Broadcaster, Name = "Broadcaster" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.Moderator, Name = "Mods" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.VIP, Name = "VIPs" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.Subscriber, Name = "Subs" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.Predictions, Name = "Predictions" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.NoAudioVisual, Name = "No Audio/No Video" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.PrimeGaming, Name = "Prime" });
                comboBadges.Items.Add(new ChatBadgeListItem() { Type = ChatBadgeType.Other, Name = "Others" });

                foreach (ChatBadgeListItem item in comboBadges.Items)
                {
                    if (((ChatBadgeType)Settings.Default.ChatBadgeMask).HasFlag(item.Type))
                        comboBadges.SelectedItems.Add(item);
                }

                foreach (VideoContainer container in comboFormat.Items)
                {
                    if (container.Name == Settings.Default.VideoContainer)
                    {
                        comboFormat.SelectedItem = container;
                        foreach (Codec codec in container.SupportedCodecs)
                        {
                            comboCodec.Items.Add(codec);
                            if (codec.Name == Settings.Default.VideoCodec)
                            {
                                comboCodec.SelectedItem = codec;
                            }
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
                {
                    comboCodec.SelectedItem = Settings.Default.VideoCodec;
                }
            }

            if (comboCodec.SelectedItem == null)
            {
                comboCodec.SelectedIndex = 0;
            }
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
            Settings.Default.ChatRenderSharpening = (bool)CheckRenderSharpening.IsChecked;
            Settings.Default.SubMessages = (bool)checkSub.IsChecked;
            Settings.Default.ChatBadges = (bool)checkBadge.IsChecked;
            Settings.Default.Offline = (bool)checkOffline.IsChecked;
            Settings.Default.DisperseCommentOffsets = (bool)checkDispersion.IsChecked;
            if (comboFormat.SelectedItem != null)
            {
                Settings.Default.VideoContainer = ((VideoContainer)comboFormat.SelectedItem).Name;
            }
            if (comboCodec.SelectedItem != null)
            {
                Settings.Default.VideoCodec = ((Codec)comboCodec.SelectedItem).Name;
            }
            Settings.Default.IgnoreUsersList = string.Join(",", textIgnoreUsersList.Text.ToLower()
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            Settings.Default.BannedWordsList = string.Join(",", textBannedWordsList.Text.ToLower()
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            int newMask = 0;
            foreach (var item in comboBadges.SelectedItems)
            {
                newMask += (int)((ChatBadgeListItem)item).Type;
            }
            Settings.Default.ChatBadgeMask = newMask;

            try
            {
                Settings.Default.Height = int.Parse(textHeight.Text);
                Settings.Default.Width = int.Parse(textWidth.Text);
                Settings.Default.FontSize = (float)numFontSize.Value;
                Settings.Default.UpdateTime = double.Parse(textUpdateTime.Text, CultureInfo.CurrentCulture);
                Settings.Default.Framerate = int.Parse(textFramerate.Text);
                Settings.Default.EmoteScale = double.Parse(textEmoteScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.EmojiScale = double.Parse(textEmojiScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.BadgeScale = double.Parse(textBadgeScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.VerticalSpacingScale = double.Parse(textVerticalScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.LeftSpacingScale = double.Parse(textSidePaddingScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.SectionHeightScale = double.Parse(textSectionHeightScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.WordSpacingScale = double.Parse(textWordSpaceScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.EmoteSpacingScale = double.Parse(textEmoteSpaceScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.AccentStrokeScale = double.Parse(textAccentStrokeScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.AccentIndentScale = double.Parse(textAccentIndentScale.Text, CultureInfo.CurrentCulture);
            }
            catch { }
            Settings.Default.Save();
        }

        private bool ValidateInputs()
        {
            if (FileNames.Length == 0)
            {
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.NoJsonFilesSelected);
                return false;
            }
            foreach (string fileName in FileNames)
            {
                if (!File.Exists(fileName))
                {
                    AppendLog(Translations.Strings.ErrorLog + Translations.Strings.FileNotFound + Path.GetFileName(fileName));
                    return false;
                }
            }

            try
            {
                int.Parse(textHeight.Text);
                int.Parse(textWidth.Text);
                double.Parse(textUpdateTime.Text, CultureInfo.CurrentCulture);
                int.Parse(textFramerate.Text);
                double.Parse(textEmoteScale.Text, CultureInfo.CurrentCulture);
                double.Parse(textBadgeScale.Text, CultureInfo.CurrentCulture);
                double.Parse(textEmojiScale.Text, CultureInfo.CurrentCulture);
                double.Parse(textVerticalScale.Text, CultureInfo.CurrentCulture);
                double.Parse(textSidePaddingScale.Text, CultureInfo.CurrentCulture);
                double.Parse(textSectionHeightScale.Text, CultureInfo.CurrentCulture);
                double.Parse(textWordSpaceScale.Text, CultureInfo.CurrentCulture);
                double.Parse(textEmoteSpaceScale.Text, CultureInfo.CurrentCulture);
                double.Parse(textAccentStrokeScale.Text, CultureInfo.CurrentCulture);
                double.Parse(textAccentIndentScale.Text, CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                return false;
            }

            if (colorBackground.SelectedColor.Value.A < 255)
            {
                if ((((VideoContainer)comboFormat.SelectedItem).Name != "MOV" ||
                     (((Codec)comboCodec.SelectedItem).Name != "RLE" && ((Codec)comboCodec.SelectedItem).Name != "ProRes")) &&
                    ((VideoContainer)comboFormat.SelectedItem).Name != "WEBM" && !(bool)checkMask.IsChecked!)
                {
                    AppendLog(Translations.Strings.ErrorLog + Translations.Strings.AlphaNotSupportedByCodec);
                    return false;
                }
            }

            if (checkMask.IsChecked == true && colorBackground.SelectedColor.Value.A == 255)
            {
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.AlphaNotSupportedByCodec);
                return false;
            }

            if (int.Parse(textHeight.Text) % 2 != 0 || int.Parse(textWidth.Text) % 2 != 0)
            {
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.RenderWidthHeightMustBeEven);
                return false;
            }

            return true;
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke(() =>
                textLog.AppendText(message + Environment.NewLine)
            );
        }

        public void SetImage(string imageUri, bool isGif)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(imageUri, UriKind.Relative);
            image.EndInit();
            if (isGif)
            {
                ImageBehavior.SetAnimatedSource(statusImage, image);
            }
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
            {
                fonts.Add(fontFamily);
            }
            fonts.Add("Inter Embedded");
            fonts.Sort();
            foreach (var font in fonts)
            {
                comboFont.Items.Add(font);
            }
            comboFont.SelectedItem = "Inter Embedded";

            Codec h264Codec = new Codec() { Name = "H264", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libx264 -preset:v veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h264NvencCodec = new Codec() { Name = "H264 NVIDIA", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v h264_nvenc -preset:v p4 -cq 20 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265Codec = new Codec() { Name = "H265", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libx265 -preset:v veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265NvencCodec = new Codec() { Name = "H265 NVIDIA", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v hevc_nvenc -preset:v p4 -cq 21 -pix_fmt yuv420p \"{save_path}\"" };
            Codec vp8Codec = new Codec() { Name = "VP8", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx -crf 18 -b:v 2M -pix_fmt yuva420p -auto-alt-ref 0 \"{save_path}\"" };
            Codec vp9Codec = new Codec() { Name = "VP9", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx-vp9 -crf 18 -b:v 2M -deadline realtime -quality realtime -speed 3 -pix_fmt yuva420p \"{save_path}\"" };
            Codec rleCodec = new Codec() { Name = "RLE", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v qtrle -pix_fmt argb \"{save_path}\"" };
            Codec proresCodec = new Codec() { Name = "ProRes", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v prores_ks -qscale:v 62 -pix_fmt argb \"{save_path}\"" };
            VideoContainer mp4Container = new VideoContainer() { Name = "MP4", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, h264NvencCodec, h265NvencCodec } };
            VideoContainer movContainer = new VideoContainer() { Name = "MOV", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, rleCodec, proresCodec, h264NvencCodec, h265NvencCodec } };
            VideoContainer webmContainer = new VideoContainer() { Name = "WEBM", SupportedCodecs = new List<Codec>() { vp8Codec, vp9Codec } };
            VideoContainer mkvContainer = new VideoContainer() { Name = "MKV", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, vp8Codec, vp9Codec, h264NvencCodec, h265NvencCodec } };
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
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            WindowSettings settings = new WindowSettings();
            settings.ShowDialog();
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void btnResetFfmpeg_Click(object sender, RoutedEventArgs e)
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

        private async void SplitBtnRender_Click(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, MenuItemPartialRender) || !((SplitButton)sender).IsDropDownOpen)
            {
                if (!ValidateInputs())
                {
                    MessageBox.Show(Translations.Strings.UnableToParseInputsMessage, Translations.Strings.UnableToParseInputs, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string fileFormat = comboFormat.SelectedItem.ToString()!;
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = $"{fileFormat} Files | *.{fileFormat.ToLower()}",
                    FileName = Path.GetFileNameWithoutExtension(textJson.Text.Replace(".gz", "")) + "." + fileFormat.ToLower()
                };
                if (saveFileDialog.ShowDialog() != true)
                {
                    return;
                }

                SaveSettings();

                ChatRenderOptions options = GetOptions(saveFileDialog.FileName);

                Progress<ProgressReport> renderProgress = new Progress<ProgressReport>(OnProgressChanged);
                ChatRenderer currentRender = new ChatRenderer(options, renderProgress);
                try
                {
                    await currentRender.ParseJsonAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    AppendLog(Translations.Strings.ErrorLog + ex.Message);
                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return;
                }

                if (ReferenceEquals(sender, MenuItemPartialRender))
                {
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
                        {
                            AppendLog(Translations.Strings.ErrorLog + Translations.Strings.InvalidStartEndTime);
                        }
                        return;
                    }
                }

                SetImage("Images/ppOverheat.gif", true);
                statusMessage.Text = Translations.Strings.StatusRendering;
                ffmpegLog.Clear();
                _cancellationTokenSource = new CancellationTokenSource();
                UpdateActionButtons(true);
                try
                {
                    await currentRender.RenderVideoAsync(_cancellationTokenSource.Token);
                    statusMessage.Text = Translations.Strings.StatusDone;
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
                {
                    statusMessage.Text = Translations.Strings.StatusError;
                    SetImage("Images/peepoSad.png", false);
                    AppendLog(Translations.Strings.ErrorLog + ex.Message);
                    if (Settings.Default.VerboseErrors)
                    {
                        if (ex.Message.Contains("The pipe has been ended"))
                        {
                            string errorLog = String.Join('\n', ffmpegLog.TakeLast(20).ToArray());
                            MessageBox.Show(errorLog, Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else
                        {
                            MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch
                {
                    statusMessage.Text = Translations.Strings.StatusCanceled;
                    SetImage("Images/ppHop.gif", true);
                }
                statusProgressBar.Value = 0;
                _cancellationTokenSource.Dispose();
                UpdateActionButtons(false);

                currentRender = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private void BtnEnqueue_Click(object sender, RoutedEventArgs e)
        {
            EnqueueRender();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            statusMessage.Text = Translations.Strings.StatusCanceling;
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e)
        {
            if (!SplitBtnRender.IsDropDownOpen)
            {
                return;
            }

            if (ValidateInputs())
            {
                EnqueueRender();
            }
            else
            {
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.InvalidCropInputs);
            }
        }

        private void EnqueueRender()
        {
            var queueOptions = new WindowQueueOptions(this);
            queueOptions.ShowDialog();
        }

        private void MenuItemPartialRender_Click(object sender, RoutedEventArgs e)
        {
            SplitBtnRender_Click(sender, e);
        }

        private void TextJson_TextChanged(object sender, TextChangedEventArgs e)
        {
            FileNames = textJson.Text.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            UpdateActionButtons(false);
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