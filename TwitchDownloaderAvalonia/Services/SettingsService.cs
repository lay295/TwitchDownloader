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

        public event EventHandler? DefaultsRestored;
        public event EventHandler? SettingsReloaded;

        public string FilePath => _filePath;

        public void Save() => WriteJson(Current, _filePath);

        public void ExportTo(string path) => WriteJson(Current, path);

        public bool TryImportFrom(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                var json = File.ReadAllText(path);
                var imported = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (imported is null)
                    return false;

                Current.CopyFrom(imported);
                Save();
                SettingsReloaded?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void ResetToDefaults()
        {
            Current.CopyFrom(new AppSettings());
            Save();
            DefaultsRestored?.Invoke(this, EventArgs.Empty);
            SettingsReloaded?.Invoke(this, EventArgs.Empty);
        }

        private void WriteJson(AppSettings settings, string path)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            lock (_saveLock)
            {
                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, path, overwrite: true);
            }
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
