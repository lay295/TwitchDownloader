using System.Text.Json;
using System.Text.Json.Serialization;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class SettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly string _filePath;
        private readonly Lock _saveLock = new();

        public SettingsService()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TwitchDownloader", "avalonia-settings.json"))
        {
        }

        internal SettingsService(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            _filePath = filePath;
            Current = Load();
        }

        public AppSettings Current { get; }

        public void Save()
        {
            lock (_saveLock)
            {
                var json = JsonSerializer.Serialize(Current, JsonOptions);
                var tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
        }

        public void ResetToDefaults()
        {
            Current.CopyFrom(new AppSettings());
            Save();
        }

        private AppSettings Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new AppSettings();

                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }
    }
}
