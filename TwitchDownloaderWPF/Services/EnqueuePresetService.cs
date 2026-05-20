using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TwitchDownloaderWPF.Models;

namespace TwitchDownloaderWPF.Services
{
    public static class EnqueuePresetService
    {
        private static readonly string _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TwitchDownloaderWPF",
            "EnqueuePresets.json");

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static List<EnqueuePreset> Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new List<EnqueuePreset>();

                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<EnqueuePreset>>(json, _jsonOptions) ?? new List<EnqueuePreset>();
            }
            catch
            {
                return new List<EnqueuePreset>();
            }
        }

        public static void Save(List<EnqueuePreset> presets)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(presets, _jsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch { /* Silently ignore save errors */ }
        }

        public static void AddOrUpdate(EnqueuePreset preset)
        {
            var presets = Load();
            var existing = presets.FindIndex(p => p.Name == preset.Name);
            if (existing >= 0)
                presets[existing] = preset;
            else
                presets.Add(preset);
            Save(presets);
        }

        public static void Delete(string name)
        {
            var presets = Load();
            presets.RemoveAll(p => p.Name == name);
            Save(presets);
        }
    }
}
