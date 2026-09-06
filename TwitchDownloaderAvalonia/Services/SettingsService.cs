using System.Text.Json;
using System.Text.Json.Serialization;
using TwitchDownloaderAvalonia.Models;

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
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TwitchDownloader");

            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, "avalonia-settings.json");
            Current = Load();
        }

        public AppSettings Current { get; }

        public void Save()
        {
            lock (_saveLock)
            {
                var json = JsonSerializer.Serialize(Current, JsonOptions);
                File.WriteAllText(_filePath, json);
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