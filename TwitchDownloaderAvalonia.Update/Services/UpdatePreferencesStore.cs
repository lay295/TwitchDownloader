using System.Text.Json;
using TwitchDownloaderAvalonia.Update.Models;

namespace TwitchDownloaderAvalonia.Update.Services
{
    public sealed class UpdatePreferencesStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private readonly Lock _gate = new();

        public UpdatePreferencesStore()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TwitchDownloader", "avalonia-update-prefs.json"))
        {
        }

        public string FilePath { get; }

        internal UpdatePreferencesStore(string filePath) => FilePath = filePath;

        public UpdatePreferences Load()
        {
            lock (_gate)
            {
                try
                {
                    if (!File.Exists(FilePath))
                        return UpdatePreferences.Empty;

                    var json = File.ReadAllText(FilePath);
                    var model = JsonSerializer.Deserialize<UpdatePreferencesDto>(json, JsonOptions);
                    if (model is null)
                        return UpdatePreferences.Empty;

                    DateTimeOffset? remind = null;
                    if (!string.IsNullOrWhiteSpace(model.RemindLaterUntil) && DateTimeOffset.TryParse(model.RemindLaterUntil, out var parsed))
                        remind = parsed;

                    return new UpdatePreferences(
                        !string.IsNullOrWhiteSpace(model.SkippedVersion) ? model.SkippedVersion : null,
                        remind,
                        model.OfferOnStartup ?? model.AutoInstall ?? true);
                }
                catch
                {
                    return UpdatePreferences.Empty;
                }
            }
        }

        public void Save(UpdatePreferences preferences)
        {
            lock (_gate)
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var dto = new UpdatePreferencesDto
                {
                    SkippedVersion = preferences.SkippedVersion,
                    RemindLaterUntil = preferences.RemindLaterUntil?.ToString("O"),
                    OfferOnStartup = preferences.OfferOnStartup,
                };

                var json = JsonSerializer.Serialize(dto, JsonOptions);
                var tempPath = FilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, FilePath, overwrite: true);
            }
        }

        private sealed class UpdatePreferencesDto
        {
            public string? SkippedVersion { get; init; }
            public string? RemindLaterUntil { get; init; }
            public bool? OfferOnStartup { get; init; }
            public bool? AutoInstall { get; init; }
        }
    }
}
