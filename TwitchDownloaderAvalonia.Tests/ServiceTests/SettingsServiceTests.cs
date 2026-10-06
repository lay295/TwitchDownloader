using System.Globalization;
using Avalonia.Data;
using Microsoft.Extensions.Logging;
using TwitchDownloaderAvalonia.Converters;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.Tests.Fakes;
using TwitchDownloaderAvalonia.Update.Services;
using TwitchDownloaderAvalonia.ViewModels;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class SettingsServiceTests
    {
        [Fact]
        public void SaveReplacesFileAtomically()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "avalonia-settings.json");
            var service = new SettingsService(path);
            service.Current.General.OAuth = "token-one";
            service.Save();
            service.Current.General.OAuth = "token-two";
            service.Save();

            var json = File.ReadAllText(path);
            Assert.Contains("token-two", json);
            Assert.DoesNotContain("token-one", json);
            Assert.False(File.Exists(path + ".tmp"));
        }

        [Fact]
        public void SaveWritesCategorizedSettings()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "avalonia-settings.json");
            var service = new SettingsService(path);
            service.Current.Render.Width = 800;
            service.Current.General.OAuth = "abc";
            service.Save();

            var json = File.ReadAllText(path);
            Assert.Contains("\"Render\"", json);
            Assert.Contains("\"Width\": 800", json);
            Assert.Contains("\"General\"", json);
        }

        [Fact]
        public void ResetToDefaultsRaisesEvent()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "avalonia-settings.json");
            var service = new SettingsService(path);
            service.Current.Render.Width = 1234;
            var raised = false;
            service.DefaultsRestored += OnDefaultsRestored;
            service.ResetToDefaults();

            Assert.True(raised);
            Assert.Equal(700, service.Current.Render.Width);

            void OnDefaultsRestored(object? sender, EventArgs args)
            {
                raised = sender == service && args == EventArgs.Empty;
            }
        }

        [Fact]
        public void NewSettingsUseChannelSubfolderTemplates()
        {
            var general = new GeneralSettings();

            Assert.StartsWith("{channel}/", general.TemplateVod);
            Assert.StartsWith("{channel}/", general.TemplateClip);
            Assert.StartsWith("{channel}/", general.TemplateChat);
        }

        [Fact]
        public void ExportAndImportRoundTripSearchAndEnqueueSettings()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "avalonia-settings.json");
            var exportPath = Path.Combine(directory, "backup.json");
            var service = new SettingsService(path);
            service.Current.Search.Kind = SearchKind.Clips;
            service.Current.Search.VideoType = "ARCHIVE";
            service.Current.Search.ClipPeriod = "LAST_WEEK";
            service.Current.Search.PageSize = 50;
            service.Current.Queue.EnqueueDownloadChat = true;
            service.Current.Queue.EnqueueRenderChat = true;
            service.Current.Queue.AutoRemoveFinished = true;
            service.Current.Render.Width = 1920;
            service.Current.RenderPresets.Add(new NamedRenderPreset
            {
                Name = "Portrait",
                Settings = new ChatRenderSettings { Width = 900, Height = 1600 },
            });
            service.Save();
            service.ExportTo(exportPath);

            var importer = new SettingsService(Path.Combine(directory, "other.json"));
            importer.Current.Render.Width = 10;
            var reloaded = false;
            importer.SettingsReloaded += (_, _) => reloaded = true;

            Assert.True(importer.TryImportFrom(exportPath));
            Assert.True(reloaded);
            Assert.Equal(SearchKind.Clips, importer.Current.Search.Kind);
            Assert.Equal("ARCHIVE", importer.Current.Search.VideoType);
            Assert.Equal("LAST_WEEK", importer.Current.Search.ClipPeriod);
            Assert.Equal(50, importer.Current.Search.PageSize);
            Assert.True(importer.Current.Queue.EnqueueDownloadChat);
            Assert.True(importer.Current.Queue.EnqueueRenderChat);
            Assert.True(importer.Current.Queue.AutoRemoveFinished);
            Assert.Equal(1920, importer.Current.Render.Width);
            var preset = Assert.Single(importer.Current.RenderPresets);
            Assert.Equal("Portrait", preset.Name);
            Assert.Equal(900, preset.Settings.Width);
            Assert.Equal(1600, preset.Settings.Height);
        }

        [Fact]
        public void ImportRejectsMissingFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            var service = new SettingsService(Path.Combine(directory, "avalonia-settings.json"));

            Assert.False(service.TryImportFrom(Path.Combine(directory, "missing.json")));
        }

        [Fact]
        public void CopyFromClonesRenderPresets()
        {
            var source = new AppSettings();
            source.RenderPresets.Add(new NamedRenderPreset
            {
                Name = "A",
                Settings = new ChatRenderSettings { FontSize = 40 },
            });

            var dest = new AppSettings();
            dest.CopyFrom(source);
            dest.RenderPresets[0].Settings.FontSize = 12;
            dest.RenderPresets[0].Name = "B";

            Assert.Equal("A", source.RenderPresets[0].Name);
            Assert.Equal(40, source.RenderPresets[0].Settings.FontSize);
        }

        [Fact]
        public async Task ExportAndImportGoThroughFileDialog()
        {
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var directory = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var settings = new SettingsService(Path.Combine(directory, "avalonia-settings.json"));
            settings.Current.Search.Kind = SearchKind.Clips;
            settings.Save();

            var loc = TestLocalization.Instance;
            var status = new AppStatus(loc, settings);
            var dialogs = new FakeDialogService();
            var files = new FakeFileDialogService();
            var queue = new QueueService(loc, settings, status, dialogs);
            using var vm = new SettingsViewModel(
                loc,
                new ThemeService(loggerFactory.CreateLogger<ThemeService>()),
                settings,
                status,
                files,
                dialogs,
                new FileCollisionService(settings, dialogs),
                queue,
                new UpdatePreferencesStore(Path.Combine(directory, "update-prefs.json")));

            var exportPath = Path.Combine(directory, "backup.json");
            files.SaveResult = exportPath;
            await vm.ExportSettingsCommand.ExecuteAsync(null);

            Assert.Equal(("twitchdownloader-settings.json", loc.Get("settings.export_filter"), "json"), Assert.Single(files.Saves));
            Assert.True(File.Exists(exportPath));

            settings.Current.Search.Kind = SearchKind.Videos;
            files.OpenResult = exportPath;
            await vm.ImportSettingsCommand.ExecuteAsync(null);

            Assert.Equal(loc.Get("settings.import"), Assert.Single(files.Opens).Title);
            Assert.Equal(SearchKind.Clips, settings.Current.Search.Kind);
        }
    }

    public class FolderRevealTests
    {
        [Fact]
        public void SelectsExistingShortWindowsPath()
        {
            Assert.True(FolderReveal.CanSelectFile(@"C:\Videos\clip.mp4", fileExists: true, isWindows: true));
        }

        [Fact]
        public void SkipsSelectForMissingFile()
        {
            Assert.False(FolderReveal.CanSelectFile(@"C:\Videos\clip.mp4", fileExists: false, isWindows: true));
        }

        [Fact]
        public void SkipsSelectForLongWindowsPath()
        {
            var path = @"C:\Videos\" + new string('a', FolderReveal.WINDOWS_SELECT_PATH_LIMIT);
            Assert.True(path.Length > FolderReveal.WINDOWS_SELECT_PATH_LIMIT);
            Assert.False(FolderReveal.CanSelectFile(path, fileExists: true, isWindows: true));
        }

        [Fact]
        public void SelectsExistingNonWindowsFileRegardlessOfLength()
        {
            var path = "/tmp/" + new string('a', FolderReveal.WINDOWS_SELECT_PATH_LIMIT);
            Assert.True(FolderReveal.CanSelectFile(path, fileExists: true, isWindows: false));
        }
    }

    public class DecimalConverterTests
    {
        [Fact]
        public void IntConverterDoesNotWriteNullBackAsZero()
        {
            var result = IntDecimalConverter.Instance.ConvertBack(null, typeof(int), null, CultureInfo.InvariantCulture);
            Assert.Equal(BindingOperations.DoNothing, result);
        }

        [Fact]
        public void DoubleConverterDoesNotWriteNullBackAsZero()
        {
            var result = DoubleDecimalConverter.Instance.ConvertBack(null, typeof(double), null, CultureInfo.InvariantCulture);
            Assert.Equal(BindingOperations.DoNothing, result);
        }
    }
}
