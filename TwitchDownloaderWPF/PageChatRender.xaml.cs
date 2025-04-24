using HandyControl.Controls;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderWPF.Extensions;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Utils;
using WpfAnimatedGif;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageChatRender.xaml
    /// </summary>
    public partial class PageChatRender : Page
    {
        public List<string> ffmpegLog = new List<string>();
        public SKFontManager fontManager = SKFontManager.CreateDefault();
        public string[] FileNames = Array.Empty<string>();
        private CancellationTokenSource _cancellationTokenSource;

        public PageChatRender()
        {
            InitializeComponent();
            App.CultureServiceSingleton.CultureChanged += OnCultureChanged;
        }

        private void OnCultureChanged(object sender, CultureInfo e)
        {
            if (IsInitialized)
            {
                LoadSettings();
            }
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
            SKColor altBackgroundColor = new(colorAlternateBackground.SelectedColor.Value.R, colorAlternateBackground.SelectedColor.Value.G, colorAlternateBackground.SelectedColor.Value.B, colorAlternateBackground.SelectedColor.Value.A);
            SKColor messageColor = new(colorFont.SelectedColor.Value.R, colorFont.SelectedColor.Value.G, colorFont.SelectedColor.Value.B);
            ChatRenderOptions options = new()
            {
                OutputFile = filename,
                InputFile = textJson.Text,
                BackgroundColor = backgroundColor,
                AlternateBackgroundColor = altBackgroundColor,
                AlternateMessageBackgrounds = checkAlternateMessageBackgrounds.IsChecked.GetValueOrDefault(),
                ChatHeight = int.Parse(textHeight.Text),
                ChatWidth = int.Parse(textWidth.Text),
                BttvEmotes = checkBTTV.IsChecked.GetValueOrDefault(),
                FfzEmotes = checkFFZ.IsChecked.GetValueOrDefault(),
                StvEmotes = checkSTV.IsChecked.GetValueOrDefault(),
                Outline = checkOutline.IsChecked.GetValueOrDefault(),
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
                IgnoreUsersArray = textIgnoreUsersList.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                BannedWordsArray = textBannedWordsList.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                Timestamp = checkTimestamp.IsChecked.GetValueOrDefault(),
                MessageColor = messageColor,
                Framerate = int.Parse(textFramerate.Text),
                InputArgs = CheckRenderSharpening.IsChecked == true ? textFfmpegInput.Text + " -filter_complex \"smartblur=lr=1:ls=-1.0\"" : textFfmpegInput.Text,
                OutputArgs = textFfmpegOutput.Text,
                MessageFontStyle = SKFontStyle.Normal,
                UsernameFontStyle = SKFontStyle.Bold,
                GenerateMask = checkMask.IsChecked.GetValueOrDefault(),
                OutlineSize = 4 * double.Parse(textOutlineScale.Text, CultureInfo.CurrentCulture),
                FfmpegPath = "ffmpeg",
                TempFolder = Settings.Default.TempPath,
                SubMessages = checkSub.IsChecked.GetValueOrDefault(),
                ChatBadges = checkBadge.IsChecked.GetValueOrDefault(),
                Offline = checkOffline.IsChecked.GetValueOrDefault(),
                AllowUnlistedEmotes = true,
                DisperseCommentOffsets = checkDispersion.IsChecked.GetValueOrDefault(),
                AdjustUsernameVisibility = checkAdjustUsernameVisibility.IsChecked.GetValueOrDefault(),
            };
            if (RadioEmojiNotoColor.IsChecked == true)
                options.EmojiVendor = EmojiVendor.GoogleNotoColor;
            else if (RadioEmojiTwemoji.IsChecked == true)
                options.EmojiVendor = EmojiVendor.TwitterTwemoji;
            else if (RadioEmojiNone.IsChecked == true)
                options.EmojiVendor = EmojiVendor.None;
            foreach (var item in comboBadges.SelectedItems)
            {
                options.ChatBadgeMask += (int)((CheckComboBoxItem)item).Tag;
            }

            return options;
        }

        private void LoadSettings()
        {
            try
            {
                comboFont.SelectedItem = Settings.Default.Font;
                checkOutline.IsChecked = Settings.Default.Outline;
                checkTimestamp.IsChecked = Settings.Default.Timestamp;
                colorBackground.SelectedColor = System.Windows.Media.Color.FromArgb(Settings.Default.BackgroundColorA, Settings.Default.BackgroundColorR, Settings.Default.BackgroundColorG, Settings.Default.BackgroundColorB);
                colorAlternateBackground.SelectedColor = System.Windows.Media.Color.FromArgb(Settings.Default.AlternateBackgroundColorA, Settings.Default.AlternateBackgroundColorR, Settings.Default.AlternateBackgroundColorG, Settings.Default.AlternateBackgroundColorB);
                checkFFZ.IsChecked = Settings.Default.FFZEmotes;
                checkBTTV.IsChecked = Settings.Default.BTTVEmotes;
                checkSTV.IsChecked = Settings.Default.STVEmotes;
                textHeight.Text = Settings.Default.Height.ToString();
                textWidth.Text = Settings.Default.Width.ToString();
                numFontSize.Value = Settings.Default.FontSize;
                textUpdateTime.Text = Settings.Default.UpdateTime.ToString("0.0#");
                colorFont.SelectedColor = System.Windows.Media.Color.FromRgb(Settings.Default.FontColorR, Settings.Default.FontColorG, Settings.Default.FontColorB);
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
                textOutlineScale.Text = Settings.Default.OutlineScale.ToString("0.0#");
                textIgnoreUsersList.Text = Settings.Default.IgnoreUsersList;
                textBannedWordsList.Text = Settings.Default.BannedWordsList;
                checkOffline.IsChecked = Settings.Default.Offline;
                checkDispersion.IsChecked = Settings.Default.DisperseCommentOffsets;
                checkAlternateMessageBackgrounds.IsChecked = Settings.Default.AlternateMessageBackgrounds;
                checkAdjustUsernameVisibility.IsChecked = Settings.Default.AdjustUsernameVisibility;
                RadioEmojiNotoColor.IsChecked = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.GoogleNotoColor;
                RadioEmojiTwemoji.IsChecked = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.TwitterTwemoji;
                RadioEmojiNone.IsChecked = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.None;

                comboBadges.Items.Clear();
                comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskBroadcaster, Tag = ChatBadgeType.Broadcaster });
                comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskModerator, Tag = ChatBadgeType.Moderator });
                comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskVIP, Tag = ChatBadgeType.VIP });
                comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskSubscriber, Tag = ChatBadgeType.Subscriber });
                comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskPredictions, Tag = ChatBadgeType.Predictions });
                comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskNoAudioNoVideo, Tag = ChatBadgeType.NoAudioVisual });
                comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskTwitchPrime, Tag = ChatBadgeType.PrimeGaming });
                comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskOthers, Tag = ChatBadgeType.Other });

                var badgeMask = (ChatBadgeType)Settings.Default.ChatBadgeMask;
                foreach (CheckComboBoxItem item in comboBadges.Items)
                {
                    if (badgeMask.HasFlag((Enum)item.Tag))
                    {
                        comboBadges.SelectedItems.Add(item);
                    }
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
            Settings.Default.Outline = checkOutline.IsChecked.GetValueOrDefault();
            Settings.Default.Timestamp = checkTimestamp.IsChecked.GetValueOrDefault();
            Settings.Default.BackgroundColorR = colorBackground.SelectedColor.GetValueOrDefault().R;
            Settings.Default.BackgroundColorG = colorBackground.SelectedColor.GetValueOrDefault().G;
            Settings.Default.BackgroundColorB = colorBackground.SelectedColor.GetValueOrDefault().B;
            Settings.Default.BackgroundColorA = colorBackground.SelectedColor.GetValueOrDefault().A;
            Settings.Default.AlternateBackgroundColorR = colorAlternateBackground.SelectedColor.GetValueOrDefault().R;
            Settings.Default.AlternateBackgroundColorG = colorAlternateBackground.SelectedColor.GetValueOrDefault().G;
            Settings.Default.AlternateBackgroundColorB = colorAlternateBackground.SelectedColor.GetValueOrDefault().B;
            Settings.Default.AlternateBackgroundColorA = colorAlternateBackground.SelectedColor.GetValueOrDefault().A;
            Settings.Default.FFZEmotes = checkFFZ.IsChecked.GetValueOrDefault();
            Settings.Default.BTTVEmotes = checkBTTV.IsChecked.GetValueOrDefault();
            Settings.Default.STVEmotes = checkSTV.IsChecked.GetValueOrDefault();
            Settings.Default.FontColorR = colorFont.SelectedColor.GetValueOrDefault().R;
            Settings.Default.FontColorG = colorFont.SelectedColor.GetValueOrDefault().G;
            Settings.Default.FontColorB = colorFont.SelectedColor.GetValueOrDefault().B;
            Settings.Default.GenerateMask = checkMask.IsChecked.GetValueOrDefault();
            Settings.Default.ChatRenderSharpening = CheckRenderSharpening.IsChecked.GetValueOrDefault();
            Settings.Default.SubMessages = checkSub.IsChecked.GetValueOrDefault();
            Settings.Default.ChatBadges = checkBadge.IsChecked.GetValueOrDefault();
            Settings.Default.Offline = checkOffline.IsChecked.GetValueOrDefault();
            Settings.Default.DisperseCommentOffsets = checkDispersion.IsChecked.GetValueOrDefault();
            Settings.Default.AlternateMessageBackgrounds = checkAlternateMessageBackgrounds.IsChecked.GetValueOrDefault();
            Settings.Default.AdjustUsernameVisibility = checkAdjustUsernameVisibility.IsChecked.GetValueOrDefault();
            if (comboFormat.SelectedItem != null)
            {
                Settings.Default.VideoContainer = ((VideoContainer)comboFormat.SelectedItem).Name;
            }
            if (comboCodec.SelectedItem != null)
            {
                Settings.Default.VideoCodec = ((Codec)comboCodec.SelectedItem).Name;
            }
            Settings.Default.IgnoreUsersList = string.Join(",", textIgnoreUsersList.Text
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            Settings.Default.BannedWordsList = string.Join(",", textBannedWordsList.Text
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            if (RadioEmojiNotoColor.IsChecked == true)
                Settings.Default.RenderEmojiVendor = (int)EmojiVendor.GoogleNotoColor;
            else if (RadioEmojiTwemoji.IsChecked == true)
                Settings.Default.RenderEmojiVendor = (int)EmojiVendor.TwitterTwemoji;
            else if (RadioEmojiNone.IsChecked == true)
                Settings.Default.RenderEmojiVendor = (int)EmojiVendor.None;
            int newMask = 0;
            foreach (var item in comboBadges.SelectedItems)
            {
                newMask += (int)((CheckComboBoxItem)item).Tag;
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
                Settings.Default.OutlineScale = double.Parse(textOutlineScale.Text, CultureInfo.CurrentCulture);
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
                _ = int.Parse(textHeight.Text);
                _ = int.Parse(textWidth.Text);
                _ = double.Parse(textUpdateTime.Text, CultureInfo.CurrentCulture);
                _ = int.Parse(textFramerate.Text);
                _ = double.Parse(textEmoteScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textBadgeScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textEmojiScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textVerticalScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textSidePaddingScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textSectionHeightScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textWordSpaceScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textEmoteSpaceScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textAccentStrokeScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textAccentIndentScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textOutlineScale.Text, CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                return false;
            }

            if (checkMask.IsChecked == false && (colorBackground.SelectedColor!.Value.A < 255 || ((bool)checkAlternateMessageBackgrounds.IsChecked! && colorAlternateBackground.SelectedColor!.Value.A < 255)))
            {
                if (((VideoContainer)comboFormat.SelectedItem).Name is not "MOV" and not "WEBM" ||
                    ((Codec)comboCodec.SelectedItem).Name is not "RLE" and not "ProRes" and not "VP8" and not "VP9")
                {
                    AppendLog(Translations.Strings.ErrorLog + Translations.Strings.AlphaNotSupportedByCodec);
                    return false;
                }
            }

            if (checkMask.IsChecked == true && colorBackground.SelectedColor!.Value.A == 255 && !((bool)checkAlternateMessageBackgrounds.IsChecked! && colorAlternateBackground.SelectedColor!.Value.A != 255))
            {
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.MaskWithNoAlpha);
                return false;
            }

            if (int.Parse(textHeight.Text) % 2 != 0 || int.Parse(textWidth.Text) % 2 != 0)
            {
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.RenderWidthHeightMustBeEven);
                return false;
            }

            return true;
        }

        private void SetPercent(int percent)
        {
            Dispatcher.BeginInvoke(() =>
                statusProgressBar.Value = percent
            );
        }

        private void SetStatus(string message)
        {
            Dispatcher.BeginInvoke(() =>
                statusMessage.Text = message
            );
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
            Codec h264AmfCodec = new Codec() { Name = "H264 AMD", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v h264_amf -preset:v p4 -cq 20 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265Codec = new Codec() { Name = "H265", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libx265 -preset:v veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265NvencCodec = new Codec() { Name = "H265 NVIDIA", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v hevc_nvenc -preset:v p4 -cq 21 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265AmfCodec = new Codec() { Name = "H265 AMD", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v hevc_amf -preset:v p4 -cq 21 -pix_fmt yuv420p \"{save_path}\"" };
            Codec vp8Codec = new Codec() { Name = "VP8", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx -crf 18 -b:v 2M -pix_fmt yuva420p -auto-alt-ref 0 \"{save_path}\"" };
            Codec vp9Codec = new Codec() { Name = "VP9", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx-vp9 -crf 18 -b:v 2M -deadline realtime -quality realtime -speed 3 -pix_fmt yuva420p \"{save_path}\"" };
            Codec rleCodec = new Codec() { Name = "RLE", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v qtrle -pix_fmt argb \"{save_path}\"" };
            Codec proresCodec = new Codec() { Name = "ProRes", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v prores_ks -qscale:v 62 -pix_fmt argb \"{save_path}\"" };
            VideoContainer mp4Container = new VideoContainer() { Name = "MP4", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, h264NvencCodec, h265NvencCodec, h264AmfCodec, h265AmfCodec } };
            VideoContainer movContainer = new VideoContainer() { Name = "MOV", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, rleCodec, proresCodec, h264NvencCodec, h265NvencCodec, h264AmfCodec, h265AmfCodec } };
            VideoContainer webmContainer = new VideoContainer() { Name = "WEBM", SupportedCodecs = new List<Codec>() { vp8Codec, vp9Codec } };
            VideoContainer mkvContainer = new VideoContainer() { Name = "MKV", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, vp8Codec, vp9Codec, h264NvencCodec, h265NvencCodec, h264AmfCodec, h265AmfCodec } };
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
            SaveSettings();
            var settings = new WindowSettings
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            settings.ShowDialog();
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
            statusImage.Visibility = Settings.Default.ReduceMotion ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
            statusImage.Visibility = Settings.Default.ReduceMotion ? Visibility.Collapsed : Visibility.Visible;
        }

        private void btnResetFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            textFfmpegInput.Text = ((Codec)comboCodec.SelectedItem).InputArgs;
            textFfmpegOutput.Text = ((Codec)comboCodec.SelectedItem).OutputArgs;

            SaveArguments();
        }

        private void SaveArguments()
        {
            List<CustomFfmpegArgs> args = JsonSerializer.Deserialize<List<CustomFfmpegArgs>>(Settings.Default.FfmpegArguments);

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

            Settings.Default.FfmpegArguments = JsonSerializer.Serialize(args);
            Settings.Default.Save();
        }

        private void LoadFfmpegArgs()
        {
            List<CustomFfmpegArgs> args = JsonSerializer.Deserialize<List<CustomFfmpegArgs>>(Settings.Default.FfmpegArguments);

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
                    MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.UnableToParseInputsMessage, Translations.Strings.UnableToParseInputs, MessageBoxButton.OK, MessageBoxImage.Error);
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

                var renderProgress = new WpfTaskProgress((LogLevel)Settings.Default.LogLevels, SetPercent, SetStatus, AppendLog, s => ffmpegLog.Add(s));
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
                        MessageBox.Show(Application.Current.MainWindow!, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return;
                }

                if (ReferenceEquals(sender, MenuItemPartialRender))
                {
                    var window = new WindowRangeSelect(currentRender)
                    {
                        Owner = Application.Current.MainWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
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
                    renderProgress.SetStatus(Translations.Strings.StatusDone);
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && _cancellationTokenSource.IsCancellationRequested)
                {
                    renderProgress.SetStatus(Translations.Strings.StatusCanceled);
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex)
                {
                    renderProgress.SetStatus(Translations.Strings.StatusError);
                    SetImage("Images/peepoSad.png", false);
                    AppendLog(Translations.Strings.ErrorLog + ex.Message);
                    if (Settings.Default.VerboseErrors)
                    {
                        if (ex.Message.Contains("The pipe has been ended"))
                        {
                            string errorLog = String.Join('\n', ffmpegLog.TakeLast(20).ToArray());
                            MessageBox.Show(Application.Current.MainWindow!, errorLog, Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else
                        {
                            MessageBox.Show(Application.Current.MainWindow!, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                renderProgress.ReportProgress(0);
                _cancellationTokenSource.Dispose();
                UpdateActionButtons(false);

                currentRender.Dispose();
                GC.Collect();
            }
        }

        private void BtnEnqueue_Click(object sender, RoutedEventArgs e)
        {
            EnqueueRender();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            statusMessage.Text = Translations.Strings.StatusCanceling;
            SetImage("Images/ppStretch.gif", true);
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
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.UnableToParseInputsMessage, Translations.Strings.UnableToParseInputs, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnqueueRender()
        {
            var queueOptions = new WindowQueueOptions(this)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            queueOptions.ShowDialog();
        }

        private void MenuItemPartialRender_Click(object sender, RoutedEventArgs e)
        {
            SplitBtnRender_Click(sender, e);
        }

        private void TextJson_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            FileNames = textJson.Text.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            UpdateActionButtons(false);
        }

        private void FfmpegParameter_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInitialized || sender is not Run { Text: var parameter })
                return;

            if (e.ChangedButton is not MouseButton.Left and not MouseButton.Middle)
                return;

            var focusedElement = Keyboard.FocusedElement;
            var textBox = GetFfmpegTemplateTextBox(focusedElement);

            if (textBox is null)
                return;

            if (textBox.TryInsertAtCaret(parameter))
            {
                e.Handled = true;
            }
        }

        [return: MaybeNull]
        private TextBox GetFfmpegTemplateTextBox(IInputElement inputElement)
        {
            if (ReferenceEquals(inputElement, textFfmpegInput))
                return textFfmpegInput;

            if (ReferenceEquals(inputElement, textFfmpegOutput))
                return textFfmpegOutput;

            return null;
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