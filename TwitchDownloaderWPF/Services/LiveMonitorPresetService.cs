using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TwitchDownloaderWPF.Services
{
    /// <summary>
    /// Persists per-channel render-preset overrides for the Live Monitor.
    /// Key = channel login (lowercase), Value = render preset name.
    /// </summary>
    public static class LiveMonitorPresetService
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TwitchDownloaderWPF",
            "LiveMonitorChannelPresets.json");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static Dictionary<string, string> Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new Dictionary<string, string>();

                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                       ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        public static void Save(Dictionary<string, string> map)
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
