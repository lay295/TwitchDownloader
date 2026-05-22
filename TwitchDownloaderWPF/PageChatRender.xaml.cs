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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderWPF.Extensions;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
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
        public List<string> ffmpegLog = [];
        public SKFontManager fontManager = SKFontManager.CreateDefault();
        public string[] FileNames = [];
        private CancellationTokenSource _cancellationTokenSource;

        // Preset support
        private bool _applyingPreset = false;

        // Embedded preview support
        private readonly List<string> _previewTempFiles = new();
        private CancellationTokenSource _previewCts;
        private bool _previewIsPlaying = false;
        private DispatcherTimer _previewTimer;
        private double _previewMediaDuration = 30;
        private double _chatTotalDuration = 0;
        private bool _previewSliderUserDragging = true;
        private List<BitmapImage> _previewFrames = new();
        private int _currentPreviewFrameIdx = 0;

        public PageChatRender()
        {
            InitializeComponent();
            App.CultureServiceSingleton.CultureChanged += OnCultureChanged;
        }

        public bool IsActionInProgress => BtnCancel.Visibility == Visibility.Visible;

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
            SKColor highlightUsersColor = new(colorHighlightUsers.SelectedColor.Value.R, colorHighlightUsers.SelectedColor.Value.G, colorHighlightUsers.SelectedColor.Value.B);
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
                UsernameFontScale = double.Parse(textUsernameScale.Text, CultureInfo.CurrentCulture),
                UpdateRate = double.Parse(textUpdateTime.Text, CultureInfo.CurrentCulture),
                EmoteScale = double.Parse(textEmoteScale.Text, CultureInfo.CurrentCulture),
                BadgeScale = double.Parse(textBadgeScale.Text, CultureInfo.CurrentCulture),
                EmojiScale = double.Parse(textEmojiScale.Text, CultureInfo.CurrentCulture),
                AvatarScale = double.Parse(textAvatarScale.Text, CultureInfo.CurrentCulture),
                SidePaddingScale = double.Parse(textSidePaddingScale.Text, CultureInfo.CurrentCulture),
                SectionHeightScale = double.Parse(textSectionHeightScale.Text, CultureInfo.CurrentCulture),
                WordSpacingScale = double.Parse(textWordSpaceScale.Text, CultureInfo.CurrentCulture),
                EmoteSpacingScale = double.Parse(textEmoteSpaceScale.Text, CultureInfo.CurrentCulture),
                AccentIndentScale = double.Parse(textAccentIndentScale.Text, CultureInfo.CurrentCulture),
                AccentStrokeScale = double.Parse(textAccentStrokeScale.Text, CultureInfo.CurrentCulture),
                VerticalSpacingScale = double.Parse(textVerticalScale.Text, CultureInfo.CurrentCulture),
                IgnoreUsersArray = textIgnoreUsersList.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                HighlightUsersArray = textHighlightUsersList.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                HighlightUsersColor = highlightUsersColor,
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
                RenderUserAvatars = checkRenderAvatars.IsChecked.GetValueOrDefault(),
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
                textUsernameScale.Text = Settings.Default.UsernameFontScale.ToString("0.0#");
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
                textAvatarScale.Text = Settings.Default.AvatarScale.ToString("0.0#");
                textVerticalScale.Text = Settings.Default.VerticalSpacingScale.ToString("0.0#");
                textSidePaddingScale.Text = Settings.Default.LeftSpacingScale.ToString("0.0#");
                textSectionHeightScale.Text = Settings.Default.SectionHeightScale.ToString("0.0#");
                textWordSpaceScale.Text = Settings.Default.WordSpacingScale.ToString("0.0#");
                textEmoteSpaceScale.Text = Settings.Default.EmoteSpacingScale.ToString("0.0#");
                textAccentStrokeScale.Text = Settings.Default.AccentStrokeScale.ToString("0.0#");
                textAccentIndentScale.Text = Settings.Default.AccentIndentScale.ToString("0.0#");
                textOutlineScale.Text = Settings.Default.OutlineScale.ToString("0.0#");
                textIgnoreUsersList.Text = Settings.Default.IgnoreUsersList;
                textHighlightUsersList.Text = Settings.Default.HighlightUsersList;
                colorHighlightUsers.SelectedColor = System.Windows.Media.Color.FromRgb(Settings.Default.HighlightUsersColorR, Settings.Default.HighlightUsersColorG, Settings.Default.HighlightUsersColorB);
                textBannedWordsList.Text = Settings.Default.BannedWordsList;
                checkOffline.IsChecked = Settings.Default.Offline;
                checkRenderAvatars.IsChecked = Settings.Default.RenderUserAvatars;
                checkDispersion.IsChecked = Settings.Default.DisperseCommentOffsets;
                checkAlternateMessageBackgrounds.IsChecked = Settings.Default.AlternateMessageBackgrounds;
                checkAdjustUsernameVisibility.IsChecked = Settings.Default.AdjustUsernameVisibility;
                RadioEmojiNotoColor.IsChecked = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.GoogleNotoColor;
                RadioEmojiTwemoji.IsChecked = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.TwitterTwemoji;
                RadioEmojiNone.IsChecked = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.None;

                comboBadges.SelectedItems.Clear();
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

                        comboCodec.Items.Clear();
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

                LoadFfmpegArgs();
            }
            catch { }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void ComboCodec_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboCodec.SelectedItem != null)
            {
                LoadFfmpegArgs();
            }
        }

        private void ComboFormat_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VideoContainer currentContainer = (VideoContainer)comboFormat.SelectedItem;
            comboCodec.Items.Clear();
            foreach (Codec codec in currentContainer.SupportedCodecs)
            {
                comboCodec.Items.Add(codec);
                if (Settings.Default.VideoCodec == codec.Name)
                {
                    comboCodec.SelectedItem = codec;
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
            Settings.Default.RenderUserAvatars = checkRenderAvatars.IsChecked.GetValueOrDefault();
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
            Settings.Default.HighlightUsersList = string.Join(",", textHighlightUsersList.Text
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            Settings.Default.HighlightUsersColorR = colorHighlightUsers.SelectedColor.GetValueOrDefault().R;
            Settings.Default.HighlightUsersColorG = colorHighlightUsers.SelectedColor.GetValueOrDefault().G;
            Settings.Default.HighlightUsersColorB = colorHighlightUsers.SelectedColor.GetValueOrDefault().B;
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
                Settings.Default.UsernameFontScale = double.Parse(textUsernameScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.UpdateTime = double.Parse(textUpdateTime.Text, CultureInfo.CurrentCulture);
                Settings.Default.Framerate = int.Parse(textFramerate.Text);
                Settings.Default.EmoteScale = double.Parse(textEmoteScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.EmojiScale = double.Parse(textEmojiScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.BadgeScale = double.Parse(textBadgeScale.Text, CultureInfo.CurrentCulture);
                Settings.Default.AvatarScale = double.Parse(textAvatarScale.Text, CultureInfo.CurrentCulture);
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
                _ = double.Parse(textAvatarScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textVerticalScale.Text, CultureInfo.CurrentCulture);
                _ = double.Parse(textUsernameScale.Text, CultureInfo.CurrentCulture);
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
            BtnClearLog.Dispatcher.BeginInvoke(() =>
                BtnClearLog.IsEnabled = true
            );
            textLog.Dispatcher.BeginInvoke(() =>
                textLog.AppendText(message + Environment.NewLine)
            );
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            BtnClearLog.IsEnabled = false;
            textLog.Dispatcher.BeginInvoke(() =>
                textLog.Document.Blocks.Clear()
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
            var fonts = fontManager.FontFamilies.ToList();
            fonts.Add("Inter Embedded");
            fonts.Sort();
            foreach (var font in fonts)
            {
                comboFont.Items.Add(font);
            }
            comboFont.SelectedItem = "Inter Embedded";

            Codec h264Codec = new Codec() { Name = "H264", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v libx264 -preset:v veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart \"{save_path}\"" };
            Codec h264NvencCodec = new Codec() { Name = "H264 NVIDIA", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v h264_nvenc -preset:v p4 -cq 20 -pix_fmt yuv420p -movflags +faststart \"{save_path}\"" };
            Codec h264AmfCodec = new Codec() { Name = "H264 AMD", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v h264_amf -preset:v p4 -cq 20 -pix_fmt yuv420p -movflags +faststart \"{save_path}\"" };
            Codec h265Codec = new Codec() { Name = "H265", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v libx265 -preset:v veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart \"{save_path}\"" };
            Codec h265NvencCodec = new Codec() { Name = "H265 NVIDIA", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v hevc_nvenc -preset:v p4 -cq 21 -pix_fmt yuv420p -movflags +faststart \"{save_path}\"" };
            Codec h265AmfCodec = new Codec() { Name = "H265 AMD", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v hevc_amf -preset:v p4 -cq 21 -pix_fmt yuv420p -movflags +faststart \"{save_path}\"" };
            Codec vp8Codec = new Codec() { Name = "VP8", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx -crf 18 -b:v 2M -pix_fmt yuva420p -auto-alt-ref 0 \"{save_path}\"" };
            Codec vp9Codec = new Codec() { Name = "VP9", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx-vp9 -crf 18 -b:v 2M -deadline realtime -quality realtime -speed 3 -pix_fmt yuva420p \"{save_path}\"" };
            Codec rleCodec = new Codec() { Name = "RLE", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v qtrle -pix_fmt argb \"{save_path}\"" };
            Codec proresCodec = new Codec() { Name = "ProRes", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", OutputArgs = "-c:v prores_ks -qscale:v 62 -pix_fmt argb \"{save_path}\"" };
            VideoContainer mp4Container = new VideoContainer() { Name = "MP4", SupportedCodecs = [h264Codec, h265Codec, h264NvencCodec, h265NvencCodec, h264AmfCodec, h265AmfCodec] };
            VideoContainer movContainer = new VideoContainer() { Name = "MOV", SupportedCodecs = [h264Codec, h265Codec, rleCodec, proresCodec, h264NvencCodec, h265NvencCodec, h264AmfCodec, h265AmfCodec] };
            VideoContainer webmContainer = new VideoContainer() { Name = "WEBM", SupportedCodecs = [vp8Codec, vp9Codec] };
            VideoContainer mkvContainer = new VideoContainer() { Name = "MKV", SupportedCodecs = [h264Codec, h265Codec, vp8Codec, vp9Codec, h264NvencCodec, h265NvencCodec, h264AmfCodec, h265AmfCodec] };
            comboFormat.Items.Add(mp4Container);
            comboFormat.Items.Add(movContainer);
            comboFormat.Items.Add(webmContainer);
            comboFormat.Items.Add(mkvContainer);

            comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskBroadcaster, Tag = ChatBadgeType.Broadcaster });
            comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskModerator, Tag = ChatBadgeType.Moderator });
            comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskVIP, Tag = ChatBadgeType.VIP });
            comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskSubscriber, Tag = ChatBadgeType.Subscriber });
            comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskPredictions, Tag = ChatBadgeType.Predictions });
            comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskNoAudioNoVideo, Tag = ChatBadgeType.NoAudioVisual });
            comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskTwitchPrime, Tag = ChatBadgeType.PrimeGaming });
            comboBadges.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.BadgeMaskOthers, Tag = ChatBadgeType.Other });

            LoadSettings();
            LoadRenderPresets();

            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _previewTimer.Tick += PreviewTimer_Tick;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            _previewTimer?.Stop();
            _previewCts?.Cancel();
            _previewFrames = new();
            Task.Run(async () =>
            {
                await Task.Delay(1500);
                foreach (var f in _previewTempFiles)
                {
                    try
                    {
                        if (Directory.Exists(f)) Directory.Delete(f, recursive: true);
                        else File.Delete(f);
                    }
                    catch { /* ignored */ }
                }
            });
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
            LoadSettings();
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

            // Update preview position slider when a new file is loaded
            if (FileNames.Length > 0 && File.Exists(FileNames[0]))
            {
                _chatTotalDuration = TryReadChatDuration(FileNames[0]);
                if (_chatTotalDuration > 0)
                {
                    sliderPreview.Maximum = _chatTotalDuration;
                    double mid = _chatTotalDuration / 2.0;
                    sliderPreview.Value = mid;
                    var midTs = TimeSpan.FromSeconds(mid);
                    textPreviewPos.Text = $"{(int)midTs.TotalHours}:{midTs.Minutes:D2}:{midTs.Seconds:D2}";
                }
            }
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

        // ── Preset support ─────────────────────────────────────────────────

        private void LoadRenderPresets()
        {
            _applyingPreset = true;
            try
            {
                var presets = ChatRenderPresetService.Load();
                comboRenderPresets.ItemsSource = presets;
                comboRenderPresets.SelectedIndex = -1;
                var lastName = Settings.Default.LastRenderPresetName;
                if (!string.IsNullOrEmpty(lastName))
                {
                    var idx = presets.FindIndex(p => p.Name == lastName);
                    if (idx >= 0) comboRenderPresets.SelectedIndex = idx;
                }
            }
            finally
            {
                _applyingPreset = false;
            }
        }

        private void ComboRenderPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_applyingPreset || !IsInitialized)
                return;

            if (comboRenderPresets.SelectedItem is ChatRenderPreset preset)
            {
                ApplyRenderPreset(preset);
                Settings.Default.LastRenderPresetName = preset.Name;
                Settings.Default.Save();
            }
        }

        private void ApplyRenderPreset(ChatRenderPreset preset)
        {
            _applyingPreset = true;
            try
            {
                if (preset.Width > 0) textWidth.Text = preset.Width.ToString();
                if (preset.Height > 0) textHeight.Text = preset.Height.ToString();
                if (!string.IsNullOrEmpty(preset.Font)) comboFont.SelectedItem = preset.Font;
                if (preset.FontSize > 0) numFontSize.Value = preset.FontSize;

                colorBackground.SelectedColor = System.Windows.Media.Color.FromArgb(
                    (byte)preset.BackgroundColorA, (byte)preset.BackgroundColorR,
                    (byte)preset.BackgroundColorG, (byte)preset.BackgroundColorB);
                colorAlternateBackground.SelectedColor = System.Windows.Media.Color.FromArgb(
                    (byte)preset.AltBackgroundColorA, (byte)preset.AltBackgroundColorR,
                    (byte)preset.AltBackgroundColorG, (byte)preset.AltBackgroundColorB);
                colorFont.SelectedColor = System.Windows.Media.Color.FromRgb(
                    (byte)preset.MessageColorR, (byte)preset.MessageColorG, (byte)preset.MessageColorB);
                colorHighlightUsers.SelectedColor = System.Windows.Media.Color.FromRgb(
                    (byte)preset.HighlightUsersColorR, (byte)preset.HighlightUsersColorG, (byte)preset.HighlightUsersColorB);

                checkOutline.IsChecked = preset.Outline;
                checkTimestamp.IsChecked = preset.Timestamp;
                checkBTTV.IsChecked = preset.Bttv;
                checkFFZ.IsChecked = preset.Ffz;
                checkSTV.IsChecked = preset.Stv;
                checkSub.IsChecked = preset.SubMessages;
                checkBadge.IsChecked = preset.Badges;
                checkRenderAvatars.IsChecked = preset.RenderAvatars;
                checkOffline.IsChecked = preset.Offline;
                checkDispersion.IsChecked = preset.Dispersion;
                checkAlternateMessageBackgrounds.IsChecked = preset.AlternateBackgrounds;
                checkAdjustUsernameVisibility.IsChecked = preset.AdjustUsernameVisibility;

                textUpdateTime.Text = preset.UpdateRate.ToString("0.0#", CultureInfo.CurrentCulture);
                textFramerate.Text = preset.Framerate.ToString();
                checkMask.IsChecked = preset.GenerateMask;
                CheckRenderSharpening.IsChecked = preset.ChatRenderSharpening;

                textEmoteScale.Text = preset.EmoteScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textBadgeScale.Text = preset.BadgeScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textEmojiScale.Text = preset.EmojiScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textAvatarScale.Text = preset.AvatarScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textVerticalScale.Text = preset.VerticalScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textSidePaddingScale.Text = preset.SidePaddingScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textSectionHeightScale.Text = preset.SectionHeightScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textWordSpaceScale.Text = preset.WordSpaceScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textEmoteSpaceScale.Text = preset.EmoteSpaceScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textAccentStrokeScale.Text = preset.AccentStrokeScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textAccentIndentScale.Text = preset.AccentIndentScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textOutlineScale.Text = preset.OutlineScale.ToString("0.0#", CultureInfo.CurrentCulture);
                textUsernameScale.Text = preset.UsernameScale.ToString("0.0#", CultureInfo.CurrentCulture);

                textIgnoreUsersList.Text = preset.IgnoreUsers ?? "";
                textHighlightUsersList.Text = preset.HighlightUsers ?? "";
                textBannedWordsList.Text = preset.BannedWords ?? "";

                RadioEmojiNotoColor.IsChecked = preset.EmojiVendor == (int)EmojiVendor.GoogleNotoColor;
                RadioEmojiTwemoji.IsChecked = preset.EmojiVendor == (int)EmojiVendor.TwitterTwemoji;
                RadioEmojiNone.IsChecked = preset.EmojiVendor == (int)EmojiVendor.None;

                comboBadges.SelectedItems.Clear();
                var badgeMask = (ChatBadgeType)preset.ChatBadgeMask;
                foreach (CheckComboBoxItem item in comboBadges.Items)
                {
                    if (badgeMask.HasFlag((Enum)item.Tag))
                        comboBadges.SelectedItems.Add(item);
                }

                if (!string.IsNullOrEmpty(preset.VideoContainer))
                {
                    foreach (VideoContainer container in comboFormat.Items)
                    {
                        if (container.Name == preset.VideoContainer)
                        {
                            comboFormat.SelectedItem = container;
                            comboCodec.Items.Clear();
                            foreach (Codec codec in container.SupportedCodecs)
                            {
                                comboCodec.Items.Add(codec);
                                if (codec.Name == preset.VideoCodec)
                                    comboCodec.SelectedItem = codec;
                            }
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(preset.FfmpegInput)) textFfmpegInput.Text = preset.FfmpegInput;
                if (!string.IsNullOrEmpty(preset.FfmpegOutput)) textFfmpegOutput.Text = preset.FfmpegOutput;
            }
            finally
            {
                _applyingPreset = false;
            }
        }

        private ChatRenderPreset GetCurrentRenderPreset()
        {
            int badgeMask = 0;
            foreach (var item in comboBadges.SelectedItems)
                badgeMask += (int)((CheckComboBoxItem)item).Tag;

            int emojiVendor = RadioEmojiTwemoji.IsChecked == true
                ? (int)EmojiVendor.TwitterTwemoji
                : RadioEmojiNone.IsChecked == true
                    ? (int)EmojiVendor.None
                    : (int)EmojiVendor.GoogleNotoColor;

            return new ChatRenderPreset
            {
                Width = int.TryParse(textWidth.Text, out var w) ? w : 0,
                Height = int.TryParse(textHeight.Text, out var h) ? h : 0,
                Font = comboFont.SelectedItem?.ToString(),
                FontSize = numFontSize.Value,
                BackgroundColorA = colorBackground.SelectedColor?.A ?? 255,
                BackgroundColorR = colorBackground.SelectedColor?.R ?? 17,
                BackgroundColorG = colorBackground.SelectedColor?.G ?? 17,
                BackgroundColorB = colorBackground.SelectedColor?.B ?? 17,
                AltBackgroundColorA = colorAlternateBackground.SelectedColor?.A ?? 255,
                AltBackgroundColorR = colorAlternateBackground.SelectedColor?.R ?? 25,
                AltBackgroundColorG = colorAlternateBackground.SelectedColor?.G ?? 25,
                AltBackgroundColorB = colorAlternateBackground.SelectedColor?.B ?? 25,
                MessageColorR = colorFont.SelectedColor?.R ?? 255,
                MessageColorG = colorFont.SelectedColor?.G ?? 255,
                MessageColorB = colorFont.SelectedColor?.B ?? 255,
                HighlightUsersColorR = colorHighlightUsers.SelectedColor?.R ?? 255,
                HighlightUsersColorG = colorHighlightUsers.SelectedColor?.G ?? 215,
                HighlightUsersColorB = colorHighlightUsers.SelectedColor?.B ?? 0,
                Outline = checkOutline.IsChecked.GetValueOrDefault(),
                Timestamp = checkTimestamp.IsChecked.GetValueOrDefault(),
                Bttv = checkBTTV.IsChecked.GetValueOrDefault(),
                Ffz = checkFFZ.IsChecked.GetValueOrDefault(),
                Stv = checkSTV.IsChecked.GetValueOrDefault(),
                SubMessages = checkSub.IsChecked.GetValueOrDefault(),
                Badges = checkBadge.IsChecked.GetValueOrDefault(),
                RenderAvatars = checkRenderAvatars.IsChecked.GetValueOrDefault(),
                Offline = checkOffline.IsChecked.GetValueOrDefault(),
                Dispersion = checkDispersion.IsChecked.GetValueOrDefault(),
                AlternateBackgrounds = checkAlternateMessageBackgrounds.IsChecked.GetValueOrDefault(),
                AdjustUsernameVisibility = checkAdjustUsernameVisibility.IsChecked.GetValueOrDefault(),
                UpdateRate = double.TryParse(textUpdateTime.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var ur) ? ur : 0.5,
                Framerate = int.TryParse(textFramerate.Text, out var fr) ? fr : 30,
                GenerateMask = checkMask.IsChecked.GetValueOrDefault(),
                ChatRenderSharpening = CheckRenderSharpening.IsChecked.GetValueOrDefault(),
                EmoteScale = double.TryParse(textEmoteScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var es) ? es : 1.0,
                BadgeScale = double.TryParse(textBadgeScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var bs) ? bs : 1.0,
                EmojiScale = double.TryParse(textEmojiScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var ejs) ? ejs : 1.0,
                AvatarScale = double.TryParse(textAvatarScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var avs) ? avs : 1.0,
                VerticalScale = double.TryParse(textVerticalScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var vs) ? vs : 1.0,
                SidePaddingScale = double.TryParse(textSidePaddingScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var sps) ? sps : 1.0,
                SectionHeightScale = double.TryParse(textSectionHeightScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var shs) ? shs : 1.0,
                WordSpaceScale = double.TryParse(textWordSpaceScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var wss) ? wss : 1.0,
                EmoteSpaceScale = double.TryParse(textEmoteSpaceScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var ess) ? ess : 1.0,
                AccentStrokeScale = double.TryParse(textAccentStrokeScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var ass) ? ass : 1.0,
                AccentIndentScale = double.TryParse(textAccentIndentScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var ais) ? ais : 1.0,
                OutlineScale = double.TryParse(textOutlineScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var os) ? os : 1.0,
                UsernameScale = double.TryParse(textUsernameScale.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var us) ? us : 1.0,
                IgnoreUsers = textIgnoreUsersList.Text,
                HighlightUsers = textHighlightUsersList.Text,
                BannedWords = textBannedWordsList.Text,
                EmojiVendor = emojiVendor,
                ChatBadgeMask = badgeMask,
                FfmpegInput = textFfmpegInput.Text,
                FfmpegOutput = textFfmpegOutput.Text,
                VideoContainer = (comboFormat.SelectedItem as VideoContainer)?.Name,
                VideoCodec = (comboCodec.SelectedItem as Codec)?.Name,
            };
        }

        private void BtnSaveRenderPreset_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WindowInputText("Save Preset", "Enter a name for this preset:", (comboRenderPresets.SelectedItem as ChatRenderPreset)?.Name ?? "")
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputValue))
                return;

            var preset = GetCurrentRenderPreset();
            preset.Name = dialog.InputValue;
            ChatRenderPresetService.AddOrUpdate(preset);

            LoadRenderPresets();

            var presets = ChatRenderPresetService.Load();
            var savedIndex = presets.FindIndex(p => p.Name == preset.Name);
            if (savedIndex >= 0)
            {
                _applyingPreset = true;
                comboRenderPresets.SelectedIndex = savedIndex;
                _applyingPreset = false;
            }
        }

        private void BtnDeleteRenderPreset_Click(object sender, RoutedEventArgs e)
        {
            if (comboRenderPresets.SelectedItem is not ChatRenderPreset preset)
                return;

            var result = MessageBox.Show(Application.Current.MainWindow!, $"Delete preset '{preset.Name}'?", "Delete Preset", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            ChatRenderPresetService.Delete(preset.Name);
            LoadRenderPresets();
        }

        // ── Embedded preview support ───────────────────────────────────────

        private static double TryReadChatDuration(string inputFile)
        {
            if (string.IsNullOrEmpty(inputFile) || !File.Exists(inputFile))
                return 0;
            try
            {
                var buffer = new byte[8192];
                using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int bytesRead = fs.Read(buffer, 0, buffer.Length);
                var text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

                var endMatch = Regex.Match(text, @"""end""\s*:\s*([\d.]+)");
                var startMatch = Regex.Match(text, @"""start""\s*:\s*([\d.]+)");

                double end = 0, start = 0;
                if (endMatch.Success)
                    double.TryParse(endMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out end);
                if (startMatch.Success)
                    double.TryParse(startMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out start);

                return end > 0 ? (end - start) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void RadioPreviewMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            bool imageMode = radioPreviewImage.IsChecked == true;
            panelVideoPreview.Visibility = imageMode ? Visibility.Collapsed : Visibility.Visible;
            if (!imageMode && _previewFrames.Count > 0)
            {
                sliderPreview.Maximum = _previewMediaDuration;
            }
            else if (_chatTotalDuration > 0)
            {
                sliderPreview.Maximum = _chatTotalDuration;
            }
        }

        private void BtnPreviewMidpoint_Click(object sender, RoutedEventArgs e)
        {
            if (_chatTotalDuration > 0)
            {
                double mid = _chatTotalDuration / 2.0;
                sliderPreview.Value = mid;
                var ts = TimeSpan.FromSeconds(mid);
                textPreviewPos.Text = $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
        }

        private void SliderPreview_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_previewSliderUserDragging || !IsInitialized) return;

            bool imageMode = radioPreviewImage.IsChecked == true;
            if (imageMode)
            {
                var ts = TimeSpan.FromSeconds(e.NewValue);
                textPreviewPos.Text = $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
            else if (_previewFrames.Count > 0)
            {
                int idx = (int)(e.NewValue * 4.0);
                _currentPreviewFrameIdx = Math.Clamp(idx, 0, _previewFrames.Count - 1);
                imgPreview.Source = _previewFrames[_currentPreviewFrameIdx];
            }
        }

        private async void BtnPreviewRender_Click(object sender, RoutedEventArgs e)
        {
            if (FileNames.Length == 0 || string.IsNullOrWhiteSpace(FileNames[0]))
            {
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.UnableToParseInputsMessage, Translations.Strings.UnableToParseInputs, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!ValidateInputs())
            {
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.UnableToParseInputsMessage, Translations.Strings.UnableToParseInputs, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!TimeSpan.TryParse(textPreviewPos.Text, out TimeSpan pos))
            {
                MessageBox.Show(Application.Current.MainWindow!, "Invalid position. Use H:MM:SS format.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool imageMode = radioPreviewImage.IsChecked == true;
            int videoDuration = 30;
            if (!imageMode && (!int.TryParse(textPreviewDuration.Text, out videoDuration) || videoDuration <= 0))
                videoDuration = 30;

            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();

            _previewFrames = new();
            _currentPreviewFrameIdx = 0;
            _previewIsPlaying = false;
            btnPreviewPlayPause.Content = "Play";
            _previewTimer?.Stop();

            var tempFile = Path.Combine(Path.GetTempPath(), $"tdw_preview_{Guid.NewGuid():N}.mp4");
            _previewTempFiles.Add(tempFile);

            var options = GetOptions(tempFile);
            options.InputFile = FileNames[0];
            options.StartOverride = (int)pos.TotalSeconds;
            options.GenerateMask = false;
            options.EndOverride = imageMode
                ? (int)pos.TotalSeconds + 2
                : (int)pos.TotalSeconds + videoDuration;
            if (imageMode) options.Framerate = 1;


            Dispatcher.BeginInvoke(() =>
            {
                progressPreview.Visibility = Visibility.Visible;
                progressPreview.IsIndeterminate = true;
                btnPreviewRender.IsEnabled = false;
                imgPreview.Visibility = Visibility.Collapsed;
                panelVideoPreview.Visibility = Visibility.Collapsed;
                textPreviewStatus.Text = "Rendering...";
                textPreviewStatus.Visibility = Visibility.Visible;
            });

            string capturedTempFile = tempFile;
            bool cancelled = false;
            try
            {
                var progress = new WpfTaskProgress(
                    pct => Dispatcher.BeginInvoke(() => { progressPreview.IsIndeterminate = false; progressPreview.Value = pct; }),
                    _ => { });

                var renderer = new ChatRenderer(options, progress);
                await renderer.ParseJsonAsync(_previewCts.Token);
                await renderer.RenderVideoAsync(_previewCts.Token);

                if (_previewCts.Token.IsCancellationRequested)
                {
                    cancelled = true;
                    return;
                }

                if (imageMode)
                {
                    // Extract first frame as PNG using ffmpeg
                    var tempPng = Path.Combine(Path.GetTempPath(), $"tdw_preview_{Guid.NewGuid():N}.png");
                    _previewTempFiles.Add(tempPng);

                    var psi = new ProcessStartInfo("ffmpeg", $"-y -i \"{capturedTempFile}\" -vframes 1 \"{tempPng}\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var ffmpegProcess = Process.Start(psi);
                    if (ffmpegProcess != null)
                        await ffmpegProcess.WaitForExitAsync(_previewCts.Token);

                    if (File.Exists(tempPng) && new FileInfo(tempPng).Length > 0)
                    {
                        // Load on UI thread to avoid cross-thread bitmap issues
                        Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.UriSource = new Uri(tempPng);
                                bitmap.EndInit();
                                bitmap.Freeze();
                                imgPreview.Source = bitmap;
                                imgPreview.Visibility = Visibility.Visible;
                                textPreviewStatus.Visibility = Visibility.Collapsed;
                            }
                            catch (Exception bex)
                            {
                                textPreviewStatus.Text = $"Image load error: {bex.Message}";
                                textPreviewStatus.Visibility = Visibility.Visible;
                            }
                        });
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            textPreviewStatus.Text = "Frame extraction failed. Ensure ffmpeg is on PATH.";
                            textPreviewStatus.Visibility = Visibility.Visible;
                        });
                    }
                }
                else
                {
                    // Video mode: extract frames via ffmpeg to avoid WMF codec dependency
                    _previewMediaDuration = videoDuration;
                    var tempFrameDir = Path.Combine(Path.GetTempPath(), $"tdw_prevframes_{Guid.NewGuid():N}");
                    Directory.CreateDirectory(tempFrameDir);
                    _previewTempFiles.Add(tempFrameDir);

                    Dispatcher.BeginInvoke(() => { progressPreview.IsIndeterminate = true; progressPreview.Visibility = Visibility.Visible; });

                    var framePsi = new ProcessStartInfo(options.FfmpegPath,
                        $"-y -i \"{capturedTempFile}\" -vf fps=4 \"{tempFrameDir}\\frame%05d.png\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    using var frameProc = Process.Start(framePsi);
                    if (frameProc != null) await frameProc.WaitForExitAsync(_previewCts.Token);

                    var frames = new List<BitmapImage>();
                    foreach (var f in Directory.GetFiles(tempFrameDir, "frame*.png").OrderBy(x => x))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = new Uri(f);
                            bmp.EndInit();
                            bmp.Freeze();
                            frames.Add(bmp);
                        }
                        catch { /* skip unreadable frame */ }
                    }

                    _previewFrames = frames;
                    _currentPreviewFrameIdx = 0;

                    Dispatcher.BeginInvoke(() =>
                    {
                        sliderPreview.Maximum = videoDuration;
                        if (_previewFrames.Count > 0)
                        {
                            imgPreview.Source = _previewFrames[0];
                            imgPreview.Visibility = Visibility.Visible;
                            panelVideoPreview.Visibility = Visibility.Visible;
                            textPreviewStatus.Visibility = Visibility.Collapsed;
                            _previewIsPlaying = true;
                            btnPreviewPlayPause.Content = "Pause";
                            textPreviewTime.Text = $"0:00 / {FormatPreviewTime(TimeSpan.FromSeconds(_previewMediaDuration))}";
                            _previewTimer?.Start();
                        }
                        else
                        {
                            textPreviewStatus.Text = "Frame extraction failed. Ensure ffmpeg is on PATH.";
                            textPreviewStatus.Visibility = Visibility.Visible;
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                Dispatcher.BeginInvoke(() =>
                {
                    textPreviewStatus.Text = "Render canceled.";
                    textPreviewStatus.Visibility = Visibility.Visible;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    textPreviewStatus.Text = $"Render error: {ex.Message}";
                    textPreviewStatus.Visibility = Visibility.Visible;
                });
            }
            finally
            {
                Dispatcher.BeginInvoke(() =>
                {
                    progressPreview.Visibility = Visibility.Collapsed;
                    btnPreviewRender.IsEnabled = true;
                });
            }
        }

        private void BtnPreviewFullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (imgPreview.Source == null) return;

            var win = new System.Windows.Window
            {
                Title = "Preview",
                WindowState = WindowState.Maximized,
                Background = System.Windows.Media.Brushes.Black
            };
            win.Content = new System.Windows.Controls.Image
            {
                Source = imgPreview.Source,
                Stretch = System.Windows.Media.Stretch.Uniform
            };
            win.KeyDown += (s, ke) => { if (ke.Key == System.Windows.Input.Key.Escape || ke.Key == System.Windows.Input.Key.F11) win.Close(); };
            win.Show();
        }

        private static string FormatPreviewTime(TimeSpan t) =>
            t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}";

        private void BtnPreviewPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_previewFrames.Count == 0) return;

            if (_previewIsPlaying)
            {
                _previewIsPlaying = false;
                btnPreviewPlayPause.Content = "Play";
                _previewTimer?.Stop();
            }
            else
            {
                _previewIsPlaying = true;
                btnPreviewPlayPause.Content = "Pause";
                _previewTimer?.Start();
            }
        }

        private void PreviewTimer_Tick(object sender, EventArgs e)
        {
            if (_previewFrames.Count == 0) return;
            _currentPreviewFrameIdx = (_currentPreviewFrameIdx + 1) % _previewFrames.Count;
            imgPreview.Source = _previewFrames[_currentPreviewFrameIdx];
            double t = _currentPreviewFrameIdx / 4.0;
            _previewSliderUserDragging = false;
            sliderPreview.Value = Math.Min(t, _previewMediaDuration);
            _previewSliderUserDragging = true;
            textPreviewTime.Text = $"{FormatPreviewTime(TimeSpan.FromSeconds(t))} / {FormatPreviewTime(TimeSpan.FromSeconds(_previewMediaDuration))}";
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