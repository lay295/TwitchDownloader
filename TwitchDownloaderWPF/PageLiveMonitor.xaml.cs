using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageLiveMonitor.xaml
    /// </summary>
    public partial class PageLiveMonitor : Page
    {
        // Download modes
        private const int ModeLive = 0;
        private const int ModeAfterEnd = 1;
        private const int ModeAfterEndSplit = 2;

        private readonly ObservableCollection<string> _channels = new();
        private readonly HashSet<long> _queuedVodIds = new();

        // vodId → (channel login, settings snapshot) for deferred (post-stream) downloads
        private readonly Dictionary<long, (string channel, LiveMonitorChannelSettings settings)> _pendingDownloads = new();

        // Render presets loaded from ChatRenderPresetService
        private List<ChatRenderPreset> _renderPresets = new();

        // Per-channel full settings profiles
        private Dictionary<string, LiveMonitorChannelSettings> _channelSettings = new();

        // Guard: suppresses SaveSettings() side-effects during programmatic combo updates
        private bool _suppressSelectionChanged;

        private readonly DispatcherTimer _pollTimer = new();
        private bool _polling;

        public bool IsMonitoring { get; private set; }

        public PageLiveMonitor()
        {
            InitializeComponent();
            _pollTimer.Tick += async (_, _) => await PollAsync();
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            listChannels.ItemsSource = _channels;

            foreach (var channel in (Settings.Default.LiveMonitorChannels ?? "")
                         .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                _channels.Add(channel.ToLowerInvariant());
            }

            textFolder.Text = Settings.Default.LiveMonitorFolder;
            numPoll.Value = Math.Max(30, Settings.Default.LiveMonitorPollSeconds);
            numThreads.Value = Math.Max(1, Settings.Default.VodDownloadThreads);
            checkChat.IsChecked = Settings.Default.LiveMonitorDownloadChat;

            var downloadMode = Math.Clamp(Settings.Default.LiveMonitorDownloadMode, 0, 2);
            comboDownloadMode.SelectedIndex = downloadMode;

            checkAutoStart.IsChecked = Settings.Default.LiveMonitorAutoStart;

            // Load H/M/S before checkbox so SaveSettings (fired by Checked event) reads correct values
            numTrimStartHour.Value = Settings.Default.LiveMonitorTrimBeginningHour;
            numTrimStartMinute.Value = Settings.Default.LiveMonitorTrimBeginningMinute;
            numTrimStartSecond.Value = Settings.Default.LiveMonitorTrimBeginningSecond;
            checkTrimStart.IsChecked = Settings.Default.LiveMonitorTrimBeginning;

            numTrimEndHour.Value = Settings.Default.LiveMonitorTrimEndingHour;
            numTrimEndMinute.Value = Settings.Default.LiveMonitorTrimEndingMinute;
            numTrimEndSecond.Value = Settings.Default.LiveMonitorTrimEndingSecond;
            checkTrimEnd.IsChecked = Settings.Default.LiveMonitorTrimEnding;

            SetTrimStartEnabled(checkTrimStart.IsChecked.GetValueOrDefault() && downloadMode != ModeAfterEndSplit);
            SetTrimEndEnabled(checkTrimEnd.IsChecked.GetValueOrDefault() && downloadMode != ModeAfterEndSplit);
            SetTrimControlsAvailable(downloadMode != ModeAfterEndSplit);

            var savedQuality = Settings.Default.LiveMonitorQuality;
            if (!string.IsNullOrWhiteSpace(savedQuality))
            {
                foreach (ComboBoxItem item in comboQuality.Items)
                {
                    if ((item.Content as string) == savedQuality)
                    {
                        comboQuality.SelectedItem = item;
                        break;
                    }
                }
            }

            // Load render presets and per-channel settings profiles
            _channelSettings = LiveMonitorChannelSettingsService.Load();
            RefreshPresetCombos();

            // Restore auto-render settings
            checkAutoRender.IsChecked = Settings.Default.LiveMonitorAutoRender;
            UpdateAutoRenderEnabled();

            // Initialise per-channel settings UI
            RefreshChannelSettingsUI();

            // Defer auto-start so the main window has finished loading first
            if (Settings.Default.LiveMonitorAutoStart)
                Dispatcher.InvokeAsync(TryAutoStart, DispatcherPriority.Background);
        }

        private string SelectedQuality => (comboQuality.SelectedItem as ComboBoxItem)?.Content as string ?? "Source";
        private int DownloadMode => comboDownloadMode.SelectedIndex;

        // ── Render preset helpers ─────────────────────────────────────────────

        /// <summary>
        /// Reloads the render preset list and repopulates <see cref="comboRenderPreset"/>.
        /// </summary>
        private void RefreshPresetCombos()
        {
            _renderPresets = ChatRenderPresetService.Load();
            var presetNames = _renderPresets.Select(p => p.Name).ToList();

            _suppressSelectionChanged = true;
            comboRenderPreset.Items.Clear();
            foreach (var name in presetNames)
                comboRenderPreset.Items.Add(name);

            var savedDefault = Settings.Default.LiveMonitorAutoRenderPreset;
            if (!string.IsNullOrEmpty(savedDefault) && presetNames.Contains(savedDefault))
                comboRenderPreset.SelectedItem = savedDefault;
            else if (comboRenderPreset.Items.Count > 0)
                comboRenderPreset.SelectedIndex = 0;
            _suppressSelectionChanged = false;

            UpdateAutoRenderEnabled();
        }

        /// <summary>
        /// Enables/disables auto-render controls based on whether chat download and presets are available.
        /// </summary>
        private void UpdateAutoRenderEnabled()
        {
            var chatEnabled = checkChat.IsChecked.GetValueOrDefault();
            var hasPresets = comboRenderPreset.Items.Count > 0;

            checkAutoRender.IsEnabled = chatEnabled && hasPresets;
            comboRenderPreset.IsEnabled = chatEnabled && hasPresets && checkAutoRender.IsChecked.GetValueOrDefault();
            if (!chatEnabled || !hasPresets)
                checkAutoRender.IsChecked = false;
        }

        /// <summary>
        /// Returns the render preset to use for a given effective settings snapshot.
        /// Falls back to the first available preset if the specified name is not found.
        /// </summary>
        private ChatRenderPreset GetRenderPresetForSettings(LiveMonitorChannelSettings eff)
        {
            if (!string.IsNullOrEmpty(eff.RenderPreset))
            {
                var found = _renderPresets.FirstOrDefault(p => p.Name == eff.RenderPreset);
                if (found != null) return found;
            }
            return _renderPresets.FirstOrDefault();
        }

        /// <summary>
        /// Builds <see cref="ChatRenderOptions"/> from a saved preset without requiring
        /// the PageChatRender UI to be active.
        /// </summary>
        private static ChatRenderOptions ChatRenderOptionsFromPreset(
            ChatRenderPreset p, string inputFile, string outputFile)
        {
            var bgColor = new SKColor((byte)p.BackgroundColorR, (byte)p.BackgroundColorG, (byte)p.BackgroundColorB, (byte)p.BackgroundColorA);
            var altBgColor = new SKColor((byte)p.AltBackgroundColorR, (byte)p.AltBackgroundColorG, (byte)p.AltBackgroundColorB, (byte)p.AltBackgroundColorA);
            var msgColor = new SKColor((byte)p.MessageColorR, (byte)p.MessageColorG, (byte)p.MessageColorB);
            var highlightColor = new SKColor((byte)p.HighlightUsersColorR, (byte)p.HighlightUsersColorG, (byte)p.HighlightUsersColorB);

            var inputArgs = p.ChatRenderSharpening
                ? (p.FfmpegInput ?? "") + " -filter_complex \"smartblur=lr=1:ls=-1.0\""
                : (p.FfmpegInput ?? "");

            return new ChatRenderOptions
            {
                InputFile = inputFile,
                OutputFile = outputFile,
                BackgroundColor = bgColor,
                AlternateBackgroundColor = altBgColor,
                AlternateMessageBackgrounds = p.AlternateBackgrounds,
                ChatHeight = p.Height,
                ChatWidth = p.Width,
                BttvEmotes = p.Bttv,
                FfzEmotes = p.Ffz,
                StvEmotes = p.Stv,
                Outline = p.Outline,
                Font = string.IsNullOrEmpty(p.Font) ? "Inter Embedded" : p.Font,
                FontSize = p.FontSize,
                UsernameFontScale = p.UsernameScale,
                UpdateRate = p.UpdateRate,
                EmoteScale = p.EmoteScale,
                BadgeScale = p.BadgeScale,
                EmojiScale = p.EmojiScale,
                AvatarScale = p.AvatarScale,
                SidePaddingScale = p.SidePaddingScale,
                SectionHeightScale = p.SectionHeightScale,
                WordSpacingScale = p.WordSpaceScale,
                EmoteSpacingScale = p.EmoteSpaceScale,
                AccentIndentScale = p.AccentIndentScale,
                AccentStrokeScale = p.AccentStrokeScale,
                VerticalSpacingScale = p.VerticalScale,
                IgnoreUsersArray = (p.IgnoreUsers ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                HighlightUsersArray = (p.HighlightUsers ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                HighlightUsersColor = highlightColor,
                BannedWordsArray = (p.BannedWords ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                Timestamp = p.Timestamp,
                MessageColor = msgColor,
                Framerate = p.Framerate,
                InputArgs = inputArgs,
                OutputArgs = p.FfmpegOutput ?? "",
                MessageFontStyle = SKFontStyle.Normal,
                UsernameFontStyle = SKFontStyle.Bold,
                GenerateMask = p.GenerateMask,
                OutlineSize = 4.0 * p.OutlineScale,
                FfmpegPath = "ffmpeg",
                TempFolder = Settings.Default.TempPath,
                SubMessages = p.SubMessages,
                ChatBadges = p.Badges,
                Offline = p.Offline,
                RenderUserAvatars = p.RenderAvatars,
                AllowUnlistedEmotes = true,
                DisperseCommentOffsets = p.Dispersion,
                AdjustUsernameVisibility = p.AdjustUsernameVisibility,
                EmojiVendor = (EmojiVendor)p.EmojiVendor,
                ChatBadgeMask = p.ChatBadgeMask,
            };
        }

        private static string GetRenderExtension(string videoContainer) =>
            (videoContainer ?? "MP4").ToUpperInvariant() switch
            {
                "MOV" => ".mov",
                "MKV" => ".mkv",
                "WEBM" => ".webm",
                _ => ".mp4",
            };

        // ── Per-channel settings helpers ──────────────────────────────────────

        /// <summary>
        /// Captures the current page UI state as a <see cref="LiveMonitorChannelSettings"/> snapshot.
        /// </summary>
        private LiveMonitorChannelSettings GetCurrentPageSettings() => new()
        {
            Folder = textFolder.Text,
            Quality = SelectedQuality,
            DownloadMode = DownloadMode,
            PollSeconds = (int)numPoll.Value,
            Threads = (int)numThreads.Value,
            DownloadChat = checkChat.IsChecked.GetValueOrDefault(),
            AutoRender = checkAutoRender.IsChecked.GetValueOrDefault(),
            RenderPreset = comboRenderPreset.SelectedItem as string ?? "",
            TrimBeginning = checkTrimStart.IsChecked.GetValueOrDefault(),
            TrimBeginningHour = (int)numTrimStartHour.Value,
            TrimBeginningMinute = (int)numTrimStartMinute.Value,
            TrimBeginningSecond = (int)numTrimStartSecond.Value,
            TrimEnding = checkTrimEnd.IsChecked.GetValueOrDefault(),
            TrimEndingHour = (int)numTrimEndHour.Value,
            TrimEndingMinute = (int)numTrimEndMinute.Value,
            TrimEndingSecond = (int)numTrimEndSecond.Value,
        };

        /// <summary>
        /// Applies a <see cref="LiveMonitorChannelSettings"/> snapshot to the page UI controls.
        /// </summary>
        private void ApplySettingsToPage(LiveMonitorChannelSettings s)
        {
            textFolder.Text = s.Folder;

            foreach (ComboBoxItem item in comboQuality.Items)
            {
                if ((item.Content as string) == s.Quality)
                {
                    comboQuality.SelectedItem = item;
                    break;
                }
            }

            comboDownloadMode.SelectedIndex = Math.Clamp(s.DownloadMode, 0, 2);
            numPoll.Value = Math.Max(30, s.PollSeconds);
            numThreads.Value = Math.Max(1, s.Threads);

            checkChat.IsChecked = s.DownloadChat;
            checkAutoRender.IsChecked = s.AutoRender;

            if (!string.IsNullOrEmpty(s.RenderPreset))
            {
                foreach (string item in comboRenderPreset.Items)
                {
                    if (item == s.RenderPreset)
                    {
                        comboRenderPreset.SelectedItem = item;
                        break;
                    }
                }
            }

            numTrimStartHour.Value = s.TrimBeginningHour;
            numTrimStartMinute.Value = s.TrimBeginningMinute;
            numTrimStartSecond.Value = s.TrimBeginningSecond;
            checkTrimStart.IsChecked = s.TrimBeginning;

            numTrimEndHour.Value = s.TrimEndingHour;
            numTrimEndMinute.Value = s.TrimEndingMinute;
            numTrimEndSecond.Value = s.TrimEndingSecond;
            checkTrimEnd.IsChecked = s.TrimEnding;

            var isSplitMode = s.DownloadMode == ModeAfterEndSplit;
            SetTrimControlsAvailable(!isSplitMode);
            SetTrimStartEnabled(s.TrimBeginning && !isSplitMode);
            SetTrimEndEnabled(s.TrimEnding && !isSplitMode);
            UpdateAutoRenderEnabled();
        }

        /// <summary>
        /// Returns the saved settings for <paramref name="channel"/> if they exist,
        /// otherwise a snapshot of the current page state.
        /// </summary>
        private LiveMonitorChannelSettings GetEffectiveSettings(string channel) =>
            _channelSettings.TryGetValue(channel, out var saved) ? saved : GetCurrentPageSettings();

        /// <summary>
        /// Refreshes the per-channel settings buttons, status label, and copy-from combo
        /// to reflect the currently selected channel.
        /// </summary>
        private void RefreshChannelSettingsUI()
        {
            var channel = listChannels.SelectedItem as string;
            var hasChannel = channel != null;
            var hasSaved = hasChannel && _channelSettings.ContainsKey(channel!);

            btnSaveChannelSettings.IsEnabled = hasChannel;
            btnLoadChannelSettings.IsEnabled = hasSaved;
            btnClearChannelSettings.IsEnabled = hasSaved;

            textChannelSettingsStatus.Text = hasChannel
                ? (hasSaved
                    ? $"Custom settings saved for '{channel}'"
                    : $"No saved settings for '{channel}' — using page defaults")
                : "";

            RefreshCopyFromCombo(channel);
        }

        /// <summary>
        /// Rebuilds the copy-from combo with channels (other than <paramref name="selectedChannel"/>)
        /// that have saved settings.
        /// </summary>
        private void RefreshCopyFromCombo(string selectedChannel)
        {
            comboCopyFrom.Items.Clear();
            foreach (var ch in _channelSettings.Keys.OrderBy(x => x))
            {
                if (ch != selectedChannel)
                    comboCopyFrom.Items.Add(ch);
            }
            if (comboCopyFrom.Items.Count > 0)
                comboCopyFrom.SelectedIndex = 0;

            btnCopyChannelSettings.IsEnabled = selectedChannel != null && comboCopyFrom.Items.Count > 0;
        }

        // ── UI event handlers ─────────────────────────────────────────────────

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new WindowSettings
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            settings.ShowDialog();
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;

            // Re-load presets in case the user added/edited them in Settings
            RefreshPresetCombos();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SaveSettings()
        {
            Settings.Default.LiveMonitorChannels = string.Join(",", _channels);
            Settings.Default.LiveMonitorFolder = textFolder.Text;
            Settings.Default.LiveMonitorQuality = SelectedQuality;
            Settings.Default.VodDownloadThreads = (int)numThreads.Value;
            Settings.Default.LiveMonitorPollSeconds = (int)numPoll.Value;
            Settings.Default.LiveMonitorDownloadChat = checkChat.IsChecked.GetValueOrDefault();
            Settings.Default.LiveMonitorDownloadMode = DownloadMode;
            Settings.Default.LiveMonitorAutoStart = checkAutoStart.IsChecked.GetValueOrDefault();
            Settings.Default.LiveMonitorAutoRender = checkAutoRender.IsChecked.GetValueOrDefault();
            Settings.Default.LiveMonitorAutoRenderPreset = comboRenderPreset.SelectedItem as string ?? "";

            Settings.Default.LiveMonitorTrimBeginning = checkTrimStart.IsChecked.GetValueOrDefault();
            Settings.Default.LiveMonitorTrimBeginningHour = (int)numTrimStartHour.Value;
            Settings.Default.LiveMonitorTrimBeginningMinute = (int)numTrimStartMinute.Value;
            Settings.Default.LiveMonitorTrimBeginningSecond = (int)numTrimStartSecond.Value;

            Settings.Default.LiveMonitorTrimEnding = checkTrimEnd.IsChecked.GetValueOrDefault();
            Settings.Default.LiveMonitorTrimEndingHour = (int)numTrimEndHour.Value;
            Settings.Default.LiveMonitorTrimEndingMinute = (int)numTrimEndMinute.Value;
            Settings.Default.LiveMonitorTrimEndingSecond = (int)numTrimEndSecond.Value;

            Settings.Default.Save();
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke(() =>
                textLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"));
        }

        private void btnAddChannel_Click(object sender, RoutedEventArgs e) => AddChannel();

        private void textChannel_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddChannel();
                e.Handled = true;
            }
        }

        private void AddChannel()
        {
            var name = textChannel.Text.Trim().ToLowerInvariant();
            if (name.Length == 0)
                return;

            // Accept a pasted channel URL too
            var slash = name.LastIndexOf('/');
            if (slash >= 0)
                name = name[(slash + 1)..];

            if (name.Length == 0 || _channels.Contains(name))
            {
                textChannel.Clear();
                return;
            }

            _channels.Add(name);
            textChannel.Clear();
            SaveSettings();

            // If monitoring is already running, check this channel immediately
            if (IsMonitoring)
                _ = PollAsync();
        }

        private void btnRemoveChannel_Click(object sender, RoutedEventArgs e)
        {
            if (listChannels.SelectedItem is string channel)
            {
                _channels.Remove(channel);
                if (_channelSettings.Remove(channel))
                    LiveMonitorChannelSettingsService.Save(_channelSettings);
                SaveSettings();
                RefreshChannelSettingsUI();
            }
        }

        private void btnFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (Directory.Exists(textFolder.Text))
                dialog.InitialDirectory = textFolder.Text;

            if (dialog.ShowDialog() == true)
            {
                textFolder.Text = dialog.FolderName;
                SaveSettings();
            }
        }

        private void btnToggle_Click(object sender, RoutedEventArgs e)
        {
            if (IsMonitoring)
            {
                StopMonitoring();
                return;
            }

            if (_channels.Count == 0)
            {
                MessageBox.Show(Application.Current.MainWindow!, "Add at least one channel to monitor.", "Live Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // A valid global output folder is required for any channel that does not have its own saved settings
            bool anyChannelNeedsGlobalFolder = _channels.Any(c => !_channelSettings.ContainsKey(c));
            if (anyChannelNeedsGlobalFolder && (string.IsNullOrWhiteSpace(textFolder.Text) || !Directory.Exists(textFolder.Text)))
            {
                MessageBox.Show(Application.Current.MainWindow!,
                    "Choose a valid output folder, or save per-channel settings for every monitored channel.",
                    "Live Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveSettings();
            StartMonitoring();
        }

        private void StartMonitoring()
        {
            IsMonitoring = true;
            btnToggle.Content = "Stop Monitoring";
            textState.Text = "Monitoring";
            _pollTimer.Interval = TimeSpan.FromSeconds(Math.Max(30, (int)numPoll.Value));
            _pollTimer.Start();
            AppendLog($"Started monitoring {_channels.Count} channel(s), polling every {(int)numPoll.Value}s.");
            _ = PollAsync();
        }

        private void TryAutoStart()
        {
            if (IsMonitoring) return;
            if (_channels.Count == 0) return;

            // Same folder check as btnToggle_Click
            bool anyChannelNeedsGlobalFolder = _channels.Any(c => !_channelSettings.ContainsKey(c));
            if (anyChannelNeedsGlobalFolder && !Directory.Exists(textFolder.Text))
                return;

            AppendLog("Auto-starting monitoring (configured at launch).");
            StartMonitoring();
        }

        private void StopMonitoring()
        {
            _pollTimer.Stop();
            IsMonitoring = false;
            btnToggle.Content = "Start Monitoring";
            textState.Text = "Stopped";
            AppendLog("Stopped monitoring.");
        }

        private void listChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshChannelSettingsUI();
        }

        private void comboDownloadMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard: fires during XAML init before the trim controls exist yet
            if (checkTrimStart is null || checkTrimEnd is null)
                return;

            var isSplitMode = comboDownloadMode.SelectedIndex == ModeAfterEndSplit;
            SetTrimControlsAvailable(!isSplitMode);
            if (isSplitMode)
            {
                SetTrimStartEnabled(false);
                SetTrimEndEnabled(false);
            }
            else
            {
                SetTrimStartEnabled(checkTrimStart.IsChecked.GetValueOrDefault());
                SetTrimEndEnabled(checkTrimEnd.IsChecked.GetValueOrDefault());
            }
            SaveSettings();
        }

        private void comboRenderPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return;
            SaveSettings();
        }

        private void checkChat_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            UpdateAutoRenderEnabled();
            SaveSettings();
        }

        private void checkAutoRender_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            UpdateAutoRenderEnabled();
            SaveSettings();
        }

        // ── Per-channel settings buttons ──────────────────────────────────────

        private void btnSaveChannelSettings_Click(object sender, RoutedEventArgs e)
        {
            if (listChannels.SelectedItem is not string channel) return;
            _channelSettings[channel] = GetCurrentPageSettings();
            LiveMonitorChannelSettingsService.Save(_channelSettings);
            RefreshChannelSettingsUI();
            AppendLog($"Saved settings for '{channel}'.");
        }

        private void btnLoadChannelSettings_Click(object sender, RoutedEventArgs e)
        {
            if (listChannels.SelectedItem is not string channel) return;
            if (!_channelSettings.TryGetValue(channel, out var settings)) return;
            ApplySettingsToPage(settings);
            SaveSettings();
            AppendLog($"Loaded saved settings for '{channel}'.");
        }

        private void btnClearChannelSettings_Click(object sender, RoutedEventArgs e)
        {
            if (listChannels.SelectedItem is not string channel) return;
            _channelSettings.Remove(channel);
            LiveMonitorChannelSettingsService.Save(_channelSettings);
            RefreshChannelSettingsUI();
            AppendLog($"Cleared saved settings for '{channel}' — will use page defaults.");
        }

        private void btnCopyChannelSettings_Click(object sender, RoutedEventArgs e)
        {
            if (listChannels.SelectedItem is not string targetChannel) return;
            if (comboCopyFrom.SelectedItem is not string sourceChannel) return;
            if (!_channelSettings.TryGetValue(sourceChannel, out var source)) return;

            // Deep-copy via JSON round-trip
            var json = JsonSerializer.Serialize(source);
            _channelSettings[targetChannel] = JsonSerializer.Deserialize<LiveMonitorChannelSettings>(json)!;

            ApplySettingsToPage(_channelSettings[targetChannel]);
            LiveMonitorChannelSettingsService.Save(_channelSettings);
            RefreshChannelSettingsUI();
            AppendLog($"Copied settings from '{sourceChannel}' to '{targetChannel}'.");
        }

        // ── Trim helpers ──────────────────────────────────────────────────────

        private void SetTrimControlsAvailable(bool available)
        {
            checkTrimStart.IsEnabled = available;
            checkTrimEnd.IsEnabled = available;
        }

        private void SetTrimStartEnabled(bool isEnabled)
        {
            numTrimStartHour.IsEnabled = isEnabled;
            numTrimStartMinute.IsEnabled = isEnabled;
            numTrimStartSecond.IsEnabled = isEnabled;
        }

        private void SetTrimEndEnabled(bool isEnabled)
        {
            numTrimEndHour.IsEnabled = isEnabled;
            numTrimEndMinute.IsEnabled = isEnabled;
            numTrimEndSecond.IsEnabled = isEnabled;
        }

        private void checkTrimStart_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetTrimStartEnabled(checkTrimStart.IsChecked.GetValueOrDefault());
            SaveSettings();
        }

        private void checkTrimEnd_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetTrimEndEnabled(checkTrimEnd.IsChecked.GetValueOrDefault());
            SaveSettings();
        }

        // ── Polling / download logic ──────────────────────────────────────────

        private async System.Threading.Tasks.Task PollAsync()
        {
            if (_polling) return;

            _polling = true;
            try
            {
                foreach (var channel in _channels.ToArray())
                {
                    try
                    {
                        var videos = await TwitchHelper.GetGqlVideos(channel, "", 1, "ARCHIVE");
                        var node = videos?.data?.user?.videos?.edges?.FirstOrDefault()?.node;
                        if (node is null || !long.TryParse(node.id, out var vodId))
                            continue;

                        if (_queuedVodIds.Contains(vodId))
                            continue;

                        var info = await TwitchHelper.GetVideoInfo(vodId);
                        if (info?.data?.video is null || info.data.video.status != "RECORDING")
                            continue;

                        // Snapshot settings at detection time; used for the entire lifetime of this recording
                        var eff = GetEffectiveSettings(channel);
                        EnqueueRecording(channel, vodId, info, eff);
                        _queuedVodIds.Add(vodId);
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Error checking '{channel}': {ex.Message}");
                    }
                }

                await CheckPendingDownloadsAsync();
            }
            finally
            {
                _polling = false;
            }
        }

        private async System.Threading.Tasks.Task CheckPendingDownloadsAsync()
        {
            if (_pendingDownloads.Count == 0) return;

            foreach (var (vodId, (channel, settings)) in _pendingDownloads.ToArray())
            {
                try
                {
                    var info = await TwitchHelper.GetVideoInfo(vodId);
                    if (info?.data?.video is null || info.data.video.status == "RECORDING")
                        continue; // still live

                    _pendingDownloads.Remove(vodId);

                    if (settings.DownloadMode == ModeAfterEndSplit)
                        await EnqueueChapterSplitsAsync(vodId, info, channel, settings);
                    else
                        EnqueueFinishedVod(vodId, info, channel, settings);
                }
                catch (Exception ex)
                {
                    AppendLog($"Error checking deferred download for VOD {vodId}: {ex.Message}");
                }
            }
        }

        private void EnqueueFinishedVod(long vodId, GqlVideoResponse info, string channel, LiveMonitorChannelSettings settings)
        {
            var video = info.data.video;
            var vodLength = TimeSpan.FromSeconds(video.lengthSeconds);
            var title = video.title ?? "";
            var streamer = video.owner?.displayName ?? channel;
            var streamerId = video.owner?.id;
            var game = video.game?.displayName ?? "";
            var createdAt = video.createdAt;
            var ext = FilenameService.GuessVodFileExtension(settings.Quality);

            var trimBeginning = settings.TrimBeginning;
            var trimEnding = settings.TrimEnding;
            var trimStart = trimBeginning
                ? new TimeSpan(settings.TrimBeginningHour, settings.TrimBeginningMinute, settings.TrimBeginningSecond)
                : TimeSpan.Zero;
            var trimEnd = trimEnding
                ? new TimeSpan(settings.TrimEndingHour, settings.TrimEndingMinute, settings.TrimEndingSecond)
                : vodLength;

            var baseName = FilenameService.GetFilename(Settings.Default.TemplateVod, title, vodId.ToString(),
                createdAt, streamer, streamerId, trimStart, trimEnd, vodLength, video.viewCount, game);
            var vodPath = Path.Combine(settings.Folder, baseName + ext);
            TwitchHelper.CreateDirectory(Path.GetDirectoryName(vodPath));

            var options = BuildVodOptions(vodId, vodPath, trimStart, trimEnd, trimBeginning, trimEnding, settings);
            var task = new VodDownloadTask { DownloadOptions = options, Info = { Title = title.Length > 0 ? title : streamer } };
            task.ChangeStatus(TwitchTaskStatus.Ready);
            lock (PageQueue.taskLock) { PageQueue.taskList.Add(task); }

            AppendLog($"Stream ended — queued full VOD download for {vodId} ({title}).");
        }

        private async System.Threading.Tasks.Task EnqueueChapterSplitsAsync(long vodId, GqlVideoResponse info, string channel, LiveMonitorChannelSettings settings)
        {
            var video = info.data.video;
            var vodLength = TimeSpan.FromSeconds(video.lengthSeconds);
            var title = video.title ?? "";
            var streamer = video.owner?.displayName ?? channel;
            var streamerId = video.owner?.id;
            var game = video.game?.displayName ?? "";
            var createdAt = video.createdAt;
            var ext = FilenameService.GuessVodFileExtension(settings.Quality);

            try
            {
                var chapterResponse = await TwitchHelper.GetOrGenerateVideoChapters(vodId, video);
                var chapters = chapterResponse.data.video.moments.edges;

                if (chapters.Count <= 1)
                {
                    AppendLog($"VOD {vodId} has only one chapter — queuing as single download.");
                    var baseName = FilenameService.GetFilename(Settings.Default.TemplateVod, title, vodId.ToString(),
                        createdAt, streamer, streamerId, TimeSpan.Zero, vodLength, vodLength, video.viewCount, game);
                    var vodPath = Path.Combine(settings.Folder, baseName + ext);
                    TwitchHelper.CreateDirectory(Path.GetDirectoryName(vodPath));
                    var singleOptions = BuildVodOptions(vodId, vodPath, TimeSpan.Zero, vodLength, false, false, settings);
                    var singleTask = new VodDownloadTask { DownloadOptions = singleOptions, Info = { Title = title } };
                    singleTask.ChangeStatus(TwitchTaskStatus.Ready);
                    lock (PageQueue.taskLock) { PageQueue.taskList.Add(singleTask); }
                    return;
                }

                AppendLog($"Stream ended — enqueueing {chapters.Count} chapter downloads for VOD {vodId} ({title}).");

                lock (PageQueue.taskLock)
                {
                    for (int i = 0; i < chapters.Count; i++)
                    {
                        var chapter = chapters[i].node;
                        var startSec = chapter.positionMilliseconds / 1000;
                        var endSec = startSec + chapter.durationMilliseconds / 1000;
                        var chapterStart = TimeSpan.FromSeconds(startSec);
                        var chapterEnd = TimeSpan.FromSeconds(Math.Min(endSec, (int)vodLength.TotalSeconds));
                        var gameName = chapter.details?.game?.displayName ?? chapter.description ?? game;

                        var baseName = FilenameService.GetFilename(Settings.Default.TemplateVod, title, vodId.ToString(),
                            createdAt, streamer, streamerId, chapterStart, chapterEnd, vodLength, video.viewCount, gameName)
                            + $"_ch{i + 1:D2}";
                        var vodPath = Path.Combine(settings.Folder, baseName + ext);
                        TwitchHelper.CreateDirectory(Path.GetDirectoryName(vodPath));

                        var options = BuildVodOptions(vodId, vodPath, chapterStart, chapterEnd, true, true, settings);
                        var task = new VodDownloadTask
                        {
                            DownloadOptions = options,
                            Info = { Title = $"{title} — {gameName} (Ch. {i + 1})" }
                        };
                        task.ChangeStatus(TwitchTaskStatus.Ready);
                        PageQueue.taskList.Add(task);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to enqueue chapter splits for VOD {vodId}: {ex.Message}");
            }
        }

        private static VideoDownloadOptions BuildVodOptions(
            long vodId, string filename,
            TimeSpan trimStart, TimeSpan trimEnd,
            bool trimBeginning, bool trimEnding,
            LiveMonitorChannelSettings settings)
        {
            return new VideoDownloadOptions
            {
                Id = vodId,
                Quality = settings.Quality,
                Filename = filename,
                TrimBeginning = trimBeginning,
                TrimBeginningTime = trimStart,
                TrimEnding = trimEnding,
                TrimEndingTime = trimEnd,
                DownloadThreads = settings.Threads,
                ThrottleKib = Settings.Default.DownloadThrottleEnabled ? Settings.Default.MaximumBandwidthKib : -1,
                Oauth = Settings.Default.OAuth,
                FfmpegPath = "ffmpeg",
                TempFolder = Settings.Default.TempPath,
                TrimMode = VideoTrimMode.Exact,
                FileCollisionCallback = info => info,
            };
        }

        private void EnqueueRecording(string channel, long vodId, GqlVideoResponse info, LiveMonitorChannelSettings eff)
        {
            var video = info.data.video;
            var streamer = video.owner?.displayName ?? channel;
            var streamerId = video.owner?.id;
            var createdAt = video.createdAt;
            var title = video.title ?? "";
            var game = video.game?.displayName ?? "";
            var folder = eff.Folder;
            var mode = eff.DownloadMode;

            var baseName = FilenameService.GetFilename(Settings.Default.TemplateVod, title, vodId.ToString(), createdAt, streamer, streamerId,
                TimeSpan.Zero, TimeSpan.FromSeconds(video.lengthSeconds), TimeSpan.FromSeconds(video.lengthSeconds), video.viewCount, game);

            if (mode == ModeLive)
            {
                var vodPath = Path.Combine(folder, baseName + FilenameService.GuessVodFileExtension(eff.Quality));
                TwitchHelper.CreateDirectory(Path.GetDirectoryName(vodPath));

                var liveOptions = new LiveStreamDownloadOptions
                {
                    Id = vodId,
                    Channel = channel,
                    Quality = eff.Quality,
                    Filename = vodPath,
                    DownloadThreads = eff.Threads,
                    ThrottleKib = Settings.Default.DownloadThrottleEnabled ? Settings.Default.MaximumBandwidthKib : -1,
                    Oauth = Settings.Default.OAuth,
                    FfmpegPath = "ffmpeg",
                    TempFolder = Settings.Default.TempPath,
                    TrimBeginning = eff.TrimBeginning,
                    TrimBeginningTime = new TimeSpan(eff.TrimBeginningHour, eff.TrimBeginningMinute, eff.TrimBeginningSecond),
                    TrimEnding = eff.TrimEnding,
                    TrimEndingTime = new TimeSpan(eff.TrimEndingHour, eff.TrimEndingMinute, eff.TrimEndingSecond),
                };

                var downloadTask = new LiveStreamDownloadTask
                {
                    DownloadOptions = liveOptions,
                    Info = { Title = title.Length > 0 ? title : $"{streamer} live" }
                };
                downloadTask.ChangeStatus(TwitchTaskStatus.Ready);

                lock (PageQueue.taskLock)
                {
                    PageQueue.taskList.Add(downloadTask);
                }

                AppendLog($"'{channel}' is LIVE — queued live download for VOD {vodId} (back-catalog + real-time recording).");
            }
            else
            {
                _pendingDownloads[vodId] = (channel, eff);

                if (mode == ModeAfterEndSplit)
                    AppendLog($"'{channel}' is LIVE — will split VOD {vodId} by chapters when stream ends.");
                else
                    AppendLog($"'{channel}' is LIVE — will queue full VOD {vodId} when stream ends.");
            }

            // ── Chat download (+ optional auto-render) ──────────────────────
            if (!eff.DownloadChat)
                return;

            var chatBaseName = FilenameService.GetFilename(Settings.Default.TemplateChat, title, vodId.ToString(),
                createdAt, streamer, streamerId, TimeSpan.Zero, TimeSpan.FromSeconds(video.lengthSeconds),
                TimeSpan.FromSeconds(video.lengthSeconds), video.viewCount, game);

            var chatOptions = new ChatDownloadOptions
            {
                Id = vodId.ToString(),
                DownloadFormat = ChatFormat.Json,
                Compression = ChatCompression.Gzip,
                DelayDownload = true,
                DownloadThreads = 1,
                TempFolder = Settings.Default.TempPath,
            };
            chatOptions.Filename = Path.Combine(folder, chatBaseName + chatOptions.FileExtension);

            var chatTask = new ChatDownloadTask
            {
                DownloadOptions = chatOptions,
                Info = { Title = title.Length > 0 ? $"{title} — Chat" : $"{streamer} live — Chat" }
            };
            chatTask.ChangeStatus(TwitchTaskStatus.Ready);

            lock (PageQueue.taskLock)
            {
                PageQueue.taskList.Add(chatTask);

                if (eff.AutoRender)
                {
                    var preset = GetRenderPresetForSettings(eff);
                    if (preset != null)
                    {
                        var chatFile = chatOptions.Filename;
                        var renderBase = chatFile.EndsWith(chatOptions.FileExtension, StringComparison.OrdinalIgnoreCase)
                            ? chatFile[..^chatOptions.FileExtension.Length]
                            : chatFile;
                        var renderPath = renderBase + GetRenderExtension(preset.VideoContainer);

                        var renderOptions = ChatRenderOptionsFromPreset(preset, chatFile, renderPath);
                        var renderTask = new ChatRenderTask
                        {
                            DownloadOptions = renderOptions,
                            DependantTask = chatTask,
                            Info = { Title = title.Length > 0 ? $"{title} — Chat Render" : $"{streamer} live — Chat Render" }
                        };
                        renderTask.ChangeStatus(TwitchTaskStatus.Waiting);
                        PageQueue.taskList.Add(renderTask);

                        AppendLog($"'{channel}' — will auto-render chat using preset '{preset.Name}' once download finishes.");
                    }
                    else
                    {
                        AppendLog($"'{channel}' — auto-render skipped: no render preset available.");
                    }
                }
            }
        }
    }
}
