using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TwitchDownloaderWPF.Models;

namespace TwitchDownloaderWPF.Services
{
    /// <summary>
    /// Persists per-channel full settings profiles for the Live Monitor.
    /// Key = channel login (lowercase), Value = full settings snapshot.
    /// </summary>
    public static class LiveMonitorChannelSettingsService
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TwitchDownloaderWPF",
            "LiveMonitorChannelSettings.json");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static Dictionary<string, LiveMonitorChannelSettings> Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new Dictionary<string, LiveMonitorChannelSettings>();

                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Dictionary<string, LiveMonitorChannelSettings>>(json, JsonOptions)
                       ?? new Dictionary<string, LiveMonitorChannelSettings>();
            }
            catch
            {
                return new Dictionary<string, LiveMonitorChannelSettings>();
            }
        }

        public static void Save(Dictionary<string, LiveMonitorChannelSettings> map)
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(FilePath, JsonSerializer.Serialize(map, JsonOptions));
            }
            catch { /* best-effort */ }
        }
    }
}
