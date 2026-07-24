using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TwitchDownloaderWPF.Models;

namespace TwitchDownloaderWPF.Services
{
    public static class ChatRenderPresetService
    {
        private static readonly string _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TwitchDownloaderWPF",
            "ChatRenderPresets.json");

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static List<ChatRenderPreset> Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new List<ChatRenderPreset>();

                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<ChatRenderPreset>>(json, _jsonOptions) ?? new List<ChatRenderPreset>();
            }
            catch
            {
                return new List<ChatRenderPreset>();
            }
        }

        public static void Save(List<ChatRenderPreset> presets)
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

        public static void AddOrUpdate(ChatRenderPreset preset)
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
