using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using HandyControl.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using SkiaSharp;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Translations;
using TwitchDownloaderWPF.Utils;
using WpfAnimatedGif;
using MessageBox = System.Windows.MessageBox;

namespace TwitchDownloaderWPF;

/// <summary>
///     Interaction logic for PageChatRender.xaml
/// </summary>
public partial class PageChatRender : Page {
    private CancellationTokenSource _cancellationTokenSource;
    public List<string> ffmpegLog = new();
    public string[] FileNames = Array.Empty<string>();
    public SKFontManager fontManager = SKFontManager.CreateDefault();

    public PageChatRender() {
        this.InitializeComponent();
        App.CultureServiceSingleton.CultureChanged += this.OnCultureChanged;
    }

    private void OnCultureChanged(object sender, CultureInfo e) {
        if (this.IsInitialized)
            this.LoadSettings();
    }

    private void btnBrowse_Click(object sender, RoutedEventArgs e) {
        var openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "JSON Files | *.json;*.json.gz";
        openFileDialog.Multiselect = true;

        if (openFileDialog.ShowDialog() == false)
            return;

        this.FileNames = openFileDialog.FileNames;
        this.textJson.Text = string.Join("&&", this.FileNames);
        this.UpdateActionButtons(false);
    }

    private void UpdateActionButtons(bool isRendering) {
        if (isRendering) {
            this.SplitBtnRender.Visibility = Visibility.Collapsed;
            this.BtnEnqueue.Visibility = Visibility.Collapsed;
            this.BtnCancel.Visibility = Visibility.Visible;
            return;
        }

        if (this.FileNames.Length > 1) {
            this.SplitBtnRender.Visibility = Visibility.Collapsed;
            this.BtnEnqueue.Visibility = Visibility.Visible;
            this.BtnCancel.Visibility = Visibility.Collapsed;
            return;
        }

        this.SplitBtnRender.Visibility = Visibility.Visible;
        this.BtnEnqueue.Visibility = Visibility.Collapsed;
        this.BtnCancel.Visibility = Visibility.Collapsed;
    }

    public ChatRenderOptions GetOptions(string filename) {
        SKColor backgroundColor = new(
            this.colorBackground.SelectedColor.Value.R,
            this.colorBackground.SelectedColor.Value.G,
            this.colorBackground.SelectedColor.Value.B,
            this.colorBackground.SelectedColor.Value.A
        );
        SKColor altBackgroundColor = new(
            this.colorAlternateBackground.SelectedColor.Value.R,
            this.colorAlternateBackground.SelectedColor.Value.G,
            this.colorAlternateBackground.SelectedColor.Value.B,
            this.colorAlternateBackground.SelectedColor.Value.A
        );
        SKColor messageColor = new(
            this.colorFont.SelectedColor.Value.R,
            this.colorFont.SelectedColor.Value.G,
            this.colorFont.SelectedColor.Value.B
        );
        ChatRenderOptions options = new() {
            OutputFile = filename,
            InputFile = this.textJson.Text,
            BackgroundColor = backgroundColor,
            AlternateBackgroundColor = altBackgroundColor,
            AlternateMessageBackgrounds = this.checkAlternateMessageBackgrounds.IsChecked.GetValueOrDefault(),
            ChatHeight = int.Parse(this.textHeight.Text),
            ChatWidth = int.Parse(this.textWidth.Text),
            BttvEmotes = this.checkBTTV.IsChecked.GetValueOrDefault(),
            FfzEmotes = this.checkFFZ.IsChecked.GetValueOrDefault(),
            StvEmotes = this.checkSTV.IsChecked.GetValueOrDefault(),
            Outline = this.checkOutline.IsChecked.GetValueOrDefault(),
            Font = (string)this.comboFont.SelectedItem,
            FontSize = this.numFontSize.Value,
            UpdateRate = double.Parse(this.textUpdateTime.Text, CultureInfo.CurrentCulture),
            EmoteScale = double.Parse(this.textEmoteScale.Text, CultureInfo.CurrentCulture),
            BadgeScale = double.Parse(this.textBadgeScale.Text, CultureInfo.CurrentCulture),
            EmojiScale = double.Parse(this.textEmojiScale.Text, CultureInfo.CurrentCulture),
            SidePaddingScale = double.Parse(this.textSidePaddingScale.Text, CultureInfo.CurrentCulture),
            SectionHeightScale = double.Parse(this.textSectionHeightScale.Text, CultureInfo.CurrentCulture),
            WordSpacingScale = double.Parse(this.textWordSpaceScale.Text, CultureInfo.CurrentCulture),
            EmoteSpacingScale = double.Parse(this.textEmoteSpaceScale.Text, CultureInfo.CurrentCulture),
            AccentIndentScale = double.Parse(this.textAccentIndentScale.Text, CultureInfo.CurrentCulture),
            AccentStrokeScale = double.Parse(this.textAccentStrokeScale.Text, CultureInfo.CurrentCulture),
            VerticalSpacingScale = double.Parse(this.textVerticalScale.Text, CultureInfo.CurrentCulture),
            IgnoreUsersArray = this.textIgnoreUsersList.Text.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            ),
            BannedWordsArray = this.textBannedWordsList.Text.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            ),
            Timestamp = this.checkTimestamp.IsChecked.GetValueOrDefault(),
            MessageColor = messageColor,
            Framerate = int.Parse(this.textFramerate.Text),
            InputArgs = this.CheckRenderSharpening.IsChecked == true
                ? this.textFfmpegInput.Text + " -filter_complex \"smartblur=lr=1:ls=-1.0\""
                : this.textFfmpegInput.Text,
            OutputArgs = this.textFfmpegOutput.Text,
            MessageFontStyle = SKFontStyle.Normal,
            UsernameFontStyle = SKFontStyle.Bold,
            GenerateMask = this.checkMask.IsChecked.GetValueOrDefault(),
            OutlineSize = 4 * double.Parse(this.textOutlineScale.Text, CultureInfo.CurrentCulture),
            FfmpegPath = "ffmpeg",
            TempFolder = Settings.Default.TempPath,
            SubMessages = this.checkSub.IsChecked.GetValueOrDefault(),
            ChatBadges = this.checkBadge.IsChecked.GetValueOrDefault(),
            Offline = this.checkOffline.IsChecked.GetValueOrDefault(),
            AllowUnlistedEmotes = true,
            DisperseCommentOffsets = this.checkDispersion.IsChecked.GetValueOrDefault(),
            AdjustUsernameVisibility = this.checkAdjustUsernameVisibility.IsChecked.GetValueOrDefault()
        };
        if (this.RadioEmojiNotoColor.IsChecked == true)
            options.EmojiVendor = EmojiVendor.GoogleNotoColor;
        else if (this.RadioEmojiTwemoji.IsChecked == true)
            options.EmojiVendor = EmojiVendor.TwitterTwemoji;
        else if (this.RadioEmojiNone.IsChecked == true)
            options.EmojiVendor = EmojiVendor.None;
        foreach (var item in this.comboBadges.SelectedItems)
            options.ChatBadgeMask += (int)((CheckComboBoxItem)item).Tag;

        return options;
    }

    private void LoadSettings() {
        try {
            this.comboFont.SelectedItem = Settings.Default.Font;
            this.checkOutline.IsChecked = Settings.Default.Outline;
            this.checkTimestamp.IsChecked = Settings.Default.Timestamp;
            this.colorBackground.SelectedColor = Color.FromArgb(
                Settings.Default.BackgroundColorA,
                Settings.Default.BackgroundColorR,
                Settings.Default.BackgroundColorG,
                Settings.Default.BackgroundColorB
            );
            this.colorAlternateBackground.SelectedColor = Color.FromArgb(
                Settings.Default.AlternateBackgroundColorA,
                Settings.Default.AlternateBackgroundColorR,
                Settings.Default.AlternateBackgroundColorG,
                Settings.Default.AlternateBackgroundColorB
            );
            this.checkFFZ.IsChecked = Settings.Default.FFZEmotes;
            this.checkBTTV.IsChecked = Settings.Default.BTTVEmotes;
            this.checkSTV.IsChecked = Settings.Default.STVEmotes;
            this.textHeight.Text = Settings.Default.Height.ToString();
            this.textWidth.Text = Settings.Default.Width.ToString();
            this.numFontSize.Value = Settings.Default.FontSize;
            this.textUpdateTime.Text = Settings.Default.UpdateTime.ToString("0.0#");
            this.colorFont.SelectedColor = Color.FromRgb(
                Settings.Default.FontColorR,
                Settings.Default.FontColorG,
                Settings.Default.FontColorB
            );
            this.textFramerate.Text = Settings.Default.Framerate.ToString();
            this.checkMask.IsChecked = Settings.Default.GenerateMask;
            this.CheckRenderSharpening.IsChecked = Settings.Default.ChatRenderSharpening;
            this.checkSub.IsChecked = Settings.Default.SubMessages;
            this.checkBadge.IsChecked = Settings.Default.ChatBadges;
            this.textEmoteScale.Text = Settings.Default.EmoteScale.ToString("0.0#");
            this.textEmojiScale.Text = Settings.Default.EmojiScale.ToString("0.0#");
            this.textBadgeScale.Text = Settings.Default.BadgeScale.ToString("0.0#");
            this.textVerticalScale.Text = Settings.Default.VerticalSpacingScale.ToString("0.0#");
            this.textSidePaddingScale.Text = Settings.Default.LeftSpacingScale.ToString("0.0#");
            this.textSectionHeightScale.Text = Settings.Default.SectionHeightScale.ToString("0.0#");
            this.textWordSpaceScale.Text = Settings.Default.WordSpacingScale.ToString("0.0#");
            this.textEmoteSpaceScale.Text = Settings.Default.EmoteSpacingScale.ToString("0.0#");
            this.textAccentStrokeScale.Text = Settings.Default.AccentStrokeScale.ToString("0.0#");
            this.textAccentIndentScale.Text = Settings.Default.AccentIndentScale.ToString("0.0#");
            this.textOutlineScale.Text = Settings.Default.OutlineScale.ToString("0.0#");
            this.textIgnoreUsersList.Text = Settings.Default.IgnoreUsersList;
            this.textBannedWordsList.Text = Settings.Default.BannedWordsList;
            this.checkOffline.IsChecked = Settings.Default.Offline;
            this.checkDispersion.IsChecked = Settings.Default.DisperseCommentOffsets;
            this.checkAlternateMessageBackgrounds.IsChecked = Settings.Default.AlternateMessageBackgrounds;
            this.checkAdjustUsernameVisibility.IsChecked = Settings.Default.AdjustUsernameVisibility;
            this.RadioEmojiNotoColor.IsChecked
                = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.GoogleNotoColor;
            this.RadioEmojiTwemoji.IsChecked
                = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.TwitterTwemoji;
            this.RadioEmojiNone.IsChecked = (EmojiVendor)Settings.Default.RenderEmojiVendor == EmojiVendor.None;

            this.comboBadges.Items.Clear();
            this.comboBadges.Items.Add(
                new CheckComboBoxItem { Content = Strings.BadgeMaskBroadcaster, Tag = ChatBadgeType.Broadcaster }
            );
            this.comboBadges.Items.Add(
                new CheckComboBoxItem { Content = Strings.BadgeMaskModerator, Tag = ChatBadgeType.Moderator }
            );
            this.comboBadges.Items.Add(
                new CheckComboBoxItem { Content = Strings.BadgeMaskVIP, Tag = ChatBadgeType.Vip }
            );
            this.comboBadges.Items.Add(
                new CheckComboBoxItem { Content = Strings.BadgeMaskSubscriber, Tag = ChatBadgeType.Subscriber }
            );
            this.comboBadges.Items.Add(
                new CheckComboBoxItem { Content = Strings.BadgeMaskPredictions, Tag = ChatBadgeType.Predictions }
            );
            this.comboBadges.Items.Add(
                new CheckComboBoxItem { Content = Strings.BadgeMaskNoAudioNoVideo, Tag = ChatBadgeType.NoAudioVisual }
            );
            this.comboBadges.Items.Add(
                new CheckComboBoxItem { Content = Strings.BadgeMaskTwitchPrime, Tag = ChatBadgeType.PrimeGaming }
            );
            this.comboBadges.Items.Add(
                new CheckComboBoxItem { Content = Strings.BadgeMaskOthers, Tag = ChatBadgeType.Other }
            );

            var badgeMask = (ChatBadgeType)Settings.Default.ChatBadgeMask;
            foreach (CheckComboBoxItem item in this.comboBadges.Items)
                if (badgeMask.HasFlag((Enum)item.Tag))
                    this.comboBadges.SelectedItems.Add(item);

            foreach (VideoContainer container in this.comboFormat.Items)
                if (container.Name == Settings.Default.VideoContainer) {
                    this.comboFormat.SelectedItem = container;
                    foreach (var codec in container.SupportedCodecs) {
                        this.comboCodec.Items.Add(codec);
                        if (codec.Name == Settings.Default.VideoCodec)
                            this.comboCodec.SelectedItem = codec;
                    }

                    break;
                }

            this.comboFormat.SelectionChanged += this.ComboFormatOnSelectionChanged;
            this.comboCodec.SelectionChanged += this.ComboCodecOnSelectionChanged;

            this.LoadFfmpegArgs();
        } catch { }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ComboCodecOnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (this.comboCodec.SelectedItem != null)
            this.LoadFfmpegArgs();
    }

    private void ComboFormatOnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var currentContainer = (VideoContainer)this.comboFormat.SelectedItem;
        this.comboCodec.Items.Clear();
        foreach (var codec in currentContainer.SupportedCodecs) {
            this.comboCodec.Items.Add(codec);
            if (Settings.Default.VideoCodec == codec.Name)
                this.comboCodec.SelectedItem = Settings.Default.VideoCodec;
        }

        if (this.comboCodec.SelectedItem == null)
            this.comboCodec.SelectedIndex = 0;
    }

    public void SaveSettings() {
        Settings.Default.Font = this.comboFont.SelectedItem.ToString();
        Settings.Default.Outline = this.checkOutline.IsChecked.GetValueOrDefault();
        Settings.Default.Timestamp = this.checkTimestamp.IsChecked.GetValueOrDefault();
        Settings.Default.BackgroundColorR = this.colorBackground.SelectedColor.GetValueOrDefault().R;
        Settings.Default.BackgroundColorG = this.colorBackground.SelectedColor.GetValueOrDefault().G;
        Settings.Default.BackgroundColorB = this.colorBackground.SelectedColor.GetValueOrDefault().B;
        Settings.Default.BackgroundColorA = this.colorBackground.SelectedColor.GetValueOrDefault().A;
        Settings.Default.AlternateBackgroundColorR = this.colorAlternateBackground.SelectedColor.GetValueOrDefault().R;
        Settings.Default.AlternateBackgroundColorG = this.colorAlternateBackground.SelectedColor.GetValueOrDefault().G;
        Settings.Default.AlternateBackgroundColorB = this.colorAlternateBackground.SelectedColor.GetValueOrDefault().B;
        Settings.Default.AlternateBackgroundColorA = this.colorAlternateBackground.SelectedColor.GetValueOrDefault().A;
        Settings.Default.FFZEmotes = this.checkFFZ.IsChecked.GetValueOrDefault();
        Settings.Default.BTTVEmotes = this.checkBTTV.IsChecked.GetValueOrDefault();
        Settings.Default.STVEmotes = this.checkSTV.IsChecked.GetValueOrDefault();
        Settings.Default.FontColorR = this.colorFont.SelectedColor.GetValueOrDefault().R;
        Settings.Default.FontColorG = this.colorFont.SelectedColor.GetValueOrDefault().G;
        Settings.Default.FontColorB = this.colorFont.SelectedColor.GetValueOrDefault().B;
        Settings.Default.GenerateMask = this.checkMask.IsChecked.GetValueOrDefault();
        Settings.Default.ChatRenderSharpening = this.CheckRenderSharpening.IsChecked.GetValueOrDefault();
        Settings.Default.SubMessages = this.checkSub.IsChecked.GetValueOrDefault();
        Settings.Default.ChatBadges = this.checkBadge.IsChecked.GetValueOrDefault();
        Settings.Default.Offline = this.checkOffline.IsChecked.GetValueOrDefault();
        Settings.Default.DisperseCommentOffsets = this.checkDispersion.IsChecked.GetValueOrDefault();
        Settings.Default.AlternateMessageBackgrounds
            = this.checkAlternateMessageBackgrounds.IsChecked.GetValueOrDefault();
        Settings.Default.AdjustUsernameVisibility = this.checkAdjustUsernameVisibility.IsChecked.GetValueOrDefault();
        if (this.comboFormat.SelectedItem != null)
            Settings.Default.VideoContainer = ((VideoContainer)this.comboFormat.SelectedItem).Name;
        if (this.comboCodec.SelectedItem != null)
            Settings.Default.VideoCodec = ((Codec)this.comboCodec.SelectedItem).Name;
        Settings.Default.IgnoreUsersList = string.Join(
            ",",
            this.textIgnoreUsersList.Text
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        );
        Settings.Default.BannedWordsList = string.Join(
            ",",
            this.textBannedWordsList.Text
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        );
        if (this.RadioEmojiNotoColor.IsChecked == true)
            Settings.Default.RenderEmojiVendor = (int)EmojiVendor.GoogleNotoColor;
        else if (this.RadioEmojiTwemoji.IsChecked == true)
            Settings.Default.RenderEmojiVendor = (int)EmojiVendor.TwitterTwemoji;
        else if (this.RadioEmojiNone.IsChecked == true)
            Settings.Default.RenderEmojiVendor = (int)EmojiVendor.None;
        var newMask = 0;
        foreach (var item in this.comboBadges.SelectedItems)
            newMask += (int)((CheckComboBoxItem)item).Tag;
        Settings.Default.ChatBadgeMask = newMask;

        try {
            Settings.Default.Height = int.Parse(this.textHeight.Text);
            Settings.Default.Width = int.Parse(this.textWidth.Text);
            Settings.Default.FontSize = (float)this.numFontSize.Value;
            Settings.Default.UpdateTime = double.Parse(this.textUpdateTime.Text, CultureInfo.CurrentCulture);
            Settings.Default.Framerate = int.Parse(this.textFramerate.Text);
            Settings.Default.EmoteScale = double.Parse(this.textEmoteScale.Text, CultureInfo.CurrentCulture);
            Settings.Default.EmojiScale = double.Parse(this.textEmojiScale.Text, CultureInfo.CurrentCulture);
            Settings.Default.BadgeScale = double.Parse(this.textBadgeScale.Text, CultureInfo.CurrentCulture);
            Settings.Default.VerticalSpacingScale = double.Parse(
                this.textVerticalScale.Text,
                CultureInfo.CurrentCulture
            );
            Settings.Default.LeftSpacingScale = double.Parse(
                this.textSidePaddingScale.Text,
                CultureInfo.CurrentCulture
            );
            Settings.Default.SectionHeightScale = double.Parse(
                this.textSectionHeightScale.Text,
                CultureInfo.CurrentCulture
            );
            Settings.Default.WordSpacingScale = double.Parse(this.textWordSpaceScale.Text, CultureInfo.CurrentCulture);
            Settings.Default.EmoteSpacingScale = double.Parse(
                this.textEmoteSpaceScale.Text,
                CultureInfo.CurrentCulture
            );
            Settings.Default.AccentStrokeScale = double.Parse(
                this.textAccentStrokeScale.Text,
                CultureInfo.CurrentCulture
            );
            Settings.Default.AccentIndentScale = double.Parse(
                this.textAccentIndentScale.Text,
                CultureInfo.CurrentCulture
            );
            Settings.Default.OutlineScale = double.Parse(this.textOutlineScale.Text, CultureInfo.CurrentCulture);
        } catch { }

        Settings.Default.Save();
    }

    private bool ValidateInputs() {
        if (this.FileNames.Length == 0) {
            this.AppendLog(Strings.ErrorLog + Strings.NoJsonFilesSelected);
            return false;
        }

        foreach (var fileName in this.FileNames)
            if (!File.Exists(fileName)) {
                this.AppendLog(Strings.ErrorLog + Strings.FileNotFound + Path.GetFileName(fileName));
                return false;
            }

        try {
            _ = int.Parse(this.textHeight.Text);
            _ = int.Parse(this.textWidth.Text);
            _ = double.Parse(this.textUpdateTime.Text, CultureInfo.CurrentCulture);
            _ = int.Parse(this.textFramerate.Text);
            _ = double.Parse(this.textEmoteScale.Text, CultureInfo.CurrentCulture);
            _ = double.Parse(this.textBadgeScale.Text, CultureInfo.CurrentCulture);
            _ = double.Parse(this.textEmojiScale.Text, CultureInfo.CurrentCulture);
            _ = double.Parse(this.textVerticalScale.Text, CultureInfo.CurrentCulture);
            _ = double.Parse(this.textSidePaddingScale.Text, CultureInfo.CurrentCulture);
            _ = double.Parse(this.textSectionHeightScale.Text, CultureInfo.CurrentCulture);
            _ = double.Parse(this.textWordSpaceScale.Text, CultureInfo.CurrentCulture);
            _ = double.Parse(this.textEmoteSpaceScale.Text, CultureInfo.CurrentCulture);
            _ = double.Parse(this.textAccentStrokeScale.Text, CultureInfo.CurrentCulture);
            _ = double.Parse(this.textAccentIndentScale.Text, CultureInfo.CurrentCulture);
            _ = double.Parse(this.textOutlineScale.Text, CultureInfo.CurrentCulture);
        } catch (Exception ex) {
            this.AppendLog(Strings.ErrorLog + ex.Message);
            return false;
        }

        if (this.checkMask.IsChecked == false
            && (this.colorBackground.SelectedColor!.Value.A < 255
                || ((bool)this.checkAlternateMessageBackgrounds.IsChecked!
                    && this.colorAlternateBackground.SelectedColor!.Value.A < 255)))
            if (((VideoContainer)this.comboFormat.SelectedItem).Name is not "MOV" and not "WEBM"
                || ((Codec)this.comboCodec.SelectedItem).Name is not "RLE"
                and not "ProRes"
                and not "VP8"
                and not "VP9") {
                this.AppendLog(Strings.ErrorLog + Strings.AlphaNotSupportedByCodec);
                return false;
            }

        if (this.checkMask.IsChecked == true
            && this.colorBackground.SelectedColor!.Value.A == 255
            && !((bool)this.checkAlternateMessageBackgrounds.IsChecked!
                && this.colorAlternateBackground.SelectedColor!.Value.A != 255)) {
            this.AppendLog(Strings.ErrorLog + Strings.MaskWithNoAlpha);
            return false;
        }

        if (int.Parse(this.textHeight.Text) % 2 != 0 || int.Parse(this.textWidth.Text) % 2 != 0) {
            this.AppendLog(Strings.ErrorLog + Strings.RenderWidthHeightMustBeEven);
            return false;
        }

        return true;
    }

    private void SetPercent(int percent) {
        this.Dispatcher.BeginInvoke(
            () => this.statusProgressBar.Value = percent
        );
    }

    private void SetStatus(string message) {
        this.Dispatcher.BeginInvoke(
            () => this.statusMessage.Text = message
        );
    }

    private void AppendLog(string message) {
        this.textLog.Dispatcher.BeginInvoke(
            () => this.textLog.AppendText(message + Environment.NewLine)
        );
    }

    public void SetImage(string imageUri, bool isGif) {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new(imageUri, UriKind.Relative);
        image.EndInit();
        if (isGif)
            ImageBehavior.SetAnimatedSource(this.statusImage, image);
        else {
            ImageBehavior.SetAnimatedSource(this.statusImage, null);
            this.statusImage.Source = image;
        }
    }

    private void Page_Initialized(object sender, EventArgs e) {
        var fonts = new List<string>();
        foreach (var fontFamily in this.fontManager.FontFamilies)
            fonts.Add(fontFamily);
        fonts.Add("Inter Embedded");
        fonts.Sort();
        foreach (var font in fonts)
            this.comboFont.Items.Add(font);
        this.comboFont.SelectedItem = "Inter Embedded";

        var h264Codec = new Codec {
            Name = "H264",
            InputArgs
                = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -",
            OutputArgs = "-c:v libx264 -preset:v veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\""
        };
        var h264NvencCodec = new Codec {
            Name = "H264 NVIDIA",
            InputArgs
                = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -",
            OutputArgs = "-c:v h264_nvenc -preset:v p4 -cq 20 -pix_fmt yuv420p \"{save_path}\""
        };
        var h265Codec = new Codec {
            Name = "H265",
            InputArgs
                = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -",
            OutputArgs = "-c:v libx265 -preset:v veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\""
        };
        var h265NvencCodec = new Codec {
            Name = "H265 NVIDIA",
            InputArgs
                = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -",
            OutputArgs = "-c:v hevc_nvenc -preset:v p4 -cq 21 -pix_fmt yuv420p \"{save_path}\""
        };
        var vp8Codec = new Codec {
            Name = "VP8",
            InputArgs
                = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -",
            OutputArgs = "-c:v libvpx -crf 18 -b:v 2M -pix_fmt yuva420p -auto-alt-ref 0 \"{save_path}\""
        };
        var vp9Codec = new Codec {
            Name = "VP9",
            InputArgs
                = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -",
            OutputArgs
                = "-c:v libvpx-vp9 -crf 18 -b:v 2M -deadline realtime -quality realtime -speed 3 -pix_fmt yuva420p \"{save_path}\""
        };
        var rleCodec = new Codec {
            Name = "RLE",
            InputArgs
                = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -",
            OutputArgs = "-c:v qtrle -pix_fmt argb \"{save_path}\""
        };
        var proresCodec = new Codec {
            Name = "ProRes",
            InputArgs
                = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -",
            OutputArgs = "-c:v prores_ks -qscale:v 62 -pix_fmt argb \"{save_path}\""
        };
        var mp4Container = new VideoContainer
            { Name = "MP4", SupportedCodecs = new() { h264Codec, h265Codec, h264NvencCodec, h265NvencCodec } };
        var movContainer = new VideoContainer {
            Name = "MOV",
            SupportedCodecs = new() { h264Codec, h265Codec, rleCodec, proresCodec, h264NvencCodec, h265NvencCodec }
        };
        var webmContainer = new VideoContainer { Name = "WEBM", SupportedCodecs = new() { vp8Codec, vp9Codec } };
        var mkvContainer = new VideoContainer {
            Name = "MKV",
            SupportedCodecs = new() { h264Codec, h265Codec, vp8Codec, vp9Codec, h264NvencCodec, h265NvencCodec }
        };
        this.comboFormat.Items.Add(mp4Container);
        this.comboFormat.Items.Add(movContainer);
        this.comboFormat.Items.Add(webmContainer);
        this.comboFormat.Items.Add(mkvContainer);

        this.LoadSettings();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e) { this.SaveSettings(); }

    private void btnDonate_Click(object sender, RoutedEventArgs e) {
        Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
    }

    private void btnSettings_Click(object sender, RoutedEventArgs e) {
        this.SaveSettings();
        var settings = new WindowSettings {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        settings.ShowDialog();
        this.btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e) {
        this.btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
    }

    private void btnResetFfmpeg_Click(object sender, RoutedEventArgs e) {
        this.textFfmpegInput.Text = ((Codec)this.comboCodec.SelectedItem).InputArgs;
        this.textFfmpegOutput.Text = ((Codec)this.comboCodec.SelectedItem).OutputArgs;

        this.SaveArguments();
    }

    private void SaveArguments() {
        var args = JsonConvert.DeserializeObject<List<CustomFfmpegArgs>>(Settings.Default.FfmpegArguments);

        var foundArg = false;
        foreach (var arg in args)
            if (arg.CodecName == ((Codec)this.comboCodec.SelectedItem).Name
                && arg.ContainerName == ((VideoContainer)this.comboFormat.SelectedItem).Name) {
                arg.InputArgs = this.textFfmpegInput.Text;
                arg.OutputArgs = this.textFfmpegOutput.Text;
                foundArg = true;
                break;
            }

        //Didn't find pre-existing save, make a new one
        if (!foundArg) {
            var newArgs = new CustomFfmpegArgs {
                CodecName = ((Codec)this.comboCodec.SelectedItem).Name,
                ContainerName = ((VideoContainer)this.comboFormat.SelectedItem).Name,
                InputArgs = this.textFfmpegInput.Text, OutputArgs = this.textFfmpegOutput.Text
            };
            args.Add(newArgs);
        }

        Settings.Default.FfmpegArguments = JsonConvert.SerializeObject(args);
        Settings.Default.Save();
    }

    private void LoadFfmpegArgs() {
        var args = JsonConvert.DeserializeObject<List<CustomFfmpegArgs>>(Settings.Default.FfmpegArguments);

        var foundArg = false;
        foreach (var arg in args)
            if (arg.CodecName == ((Codec)this.comboCodec.SelectedItem).Name
                && arg.ContainerName == ((VideoContainer)this.comboFormat.SelectedItem).Name) {
                this.textFfmpegInput.Text = arg.InputArgs;
                this.textFfmpegOutput.Text = arg.OutputArgs;
                foundArg = true;
                break;
            }

        if (!foundArg) {
            this.textFfmpegInput.Text = ((Codec)this.comboCodec.SelectedItem).InputArgs;
            this.textFfmpegOutput.Text = ((Codec)this.comboCodec.SelectedItem).OutputArgs;
        }
    }

    private void textFfmpegInput_LostFocus(object sender, RoutedEventArgs e) { this.SaveArguments(); }

    private void textFfmpegOutput_LostFocus(object sender, RoutedEventArgs e) { this.SaveArguments(); }

    private async void SplitBtnRender_Click(object sender, RoutedEventArgs e) {
        if (ReferenceEquals(sender, this.MenuItemPartialRender) || !((SplitButton)sender).IsDropDownOpen) {
            if (!this.ValidateInputs()) {
                MessageBox.Show(
                    Application.Current.MainWindow!,
                    Strings.UnableToParseInputsMessage,
                    Strings.UnableToParseInputs,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            var fileFormat = this.comboFormat.SelectedItem.ToString()!;
            var saveFileDialog = new SaveFileDialog {
                Filter = $"{fileFormat} Files | *.{fileFormat.ToLower()}",
                FileName = Path.GetFileNameWithoutExtension(this.textJson.Text.Replace(".gz", ""))
                    + "."
                    + fileFormat.ToLower()
            };
            if (saveFileDialog.ShowDialog() != true)
                return;

            this.SaveSettings();

            var options = this.GetOptions(saveFileDialog.FileName);

            var renderProgress = new WpfTaskProgress(
                (LogLevel)Settings.Default.LogLevels,
                this.SetPercent,
                this.SetStatus,
                this.AppendLog,
                s => this.ffmpegLog.Add(s)
            );
            var currentRender = new ChatRenderer(options, renderProgress);
            try {
                await currentRender.ParseJsonAsync(CancellationToken.None);
            } catch (Exception ex) {
                this.AppendLog(Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                    MessageBox.Show(
                        Application.Current.MainWindow!,
                        ex.ToString(),
                        Strings.VerboseErrorOutput,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                return;
            }

            if (ReferenceEquals(sender, this.MenuItemPartialRender)) {
                var window = new WindowRangeSelect(currentRender) {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.ShowDialog();

                if (window.OK) {
                    options.StartOverride = window.startSeconds;
                    options.EndOverride = window.endSeconds;
                } else {
                    if (window.Invalid)
                        this.AppendLog(Strings.ErrorLog + Strings.InvalidStartEndTime);
                    return;
                }
            }

            this.SetImage("Images/ppOverheat.gif", true);
            this.statusMessage.Text = Strings.StatusRendering;
            this.ffmpegLog.Clear();
            this._cancellationTokenSource = new();
            this.UpdateActionButtons(true);
            try {
                await currentRender.RenderVideoAsync(this._cancellationTokenSource.Token);
                renderProgress.SetStatus(Strings.StatusDone);
                this.SetImage("Images/ppHop.gif", true);
            } catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException
                && this._cancellationTokenSource.IsCancellationRequested) {
                renderProgress.SetStatus(Strings.StatusCanceled);
                this.SetImage("Images/ppHop.gif", true);
            } catch (Exception ex) {
                renderProgress.SetStatus(Strings.StatusError);
                this.SetImage("Images/peepoSad.png", false);
                this.AppendLog(Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors) {
                    if (ex.Message.Contains("The pipe has been ended")) {
                        var errorLog = string.Join('\n', this.ffmpegLog.TakeLast(20).ToArray());
                        MessageBox.Show(
                            Application.Current.MainWindow!,
                            errorLog,
                            Strings.VerboseErrorOutput,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    } else
                        MessageBox.Show(
                            Application.Current.MainWindow!,
                            ex.ToString(),
                            Strings.VerboseErrorOutput,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                }
            }

            renderProgress.ReportProgress(0);
            this._cancellationTokenSource.Dispose();
            this.UpdateActionButtons(false);

            currentRender.Dispose();
            GC.Collect(2, GCCollectionMode.Default, false);
        }
    }

    private void BtnEnqueue_Click(object sender, RoutedEventArgs e) { this.EnqueueRender(); }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) {
        this.statusMessage.Text = Strings.StatusCanceling;
        try {
            this._cancellationTokenSource.Cancel();
        } catch (ObjectDisposedException) { }
    }

    private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e) {
        if (!this.SplitBtnRender.IsDropDownOpen)
            return;

        if (this.ValidateInputs())
            this.EnqueueRender();
        else
            MessageBox.Show(
                Application.Current.MainWindow!,
                Strings.UnableToParseInputsMessage,
                Strings.UnableToParseInputs,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
    }

    private void EnqueueRender() {
        var queueOptions = new WindowQueueOptions(this) {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        queueOptions.ShowDialog();
    }

    private void MenuItemPartialRender_Click(object sender, RoutedEventArgs e) { this.SplitBtnRender_Click(sender, e); }

    private void TextJson_TextChanged(object sender, TextChangedEventArgs e) {
        this.FileNames = this.textJson.Text.Split(
            "&&",
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );
        this.UpdateActionButtons(false);
    }
}

public class VideoContainer {
    public string Name;
    public List<Codec> SupportedCodecs;

    public override string ToString() => this.Name;
}

public class Codec {
    public string InputArgs;
    public string Name;
    public string OutputArgs;

    public override string ToString() => this.Name;
}
