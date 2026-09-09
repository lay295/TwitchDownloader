using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Converters
{
    public static class QualityLabels
    {
        public static string Get(string value) => value switch
        {
            "Source" => Loc.Get("quality.source"),
            "Source Portrait" => Loc.Get("quality.source_portrait"),
            "Worst" => Loc.Get("quality.worst"),
            "Worst Portrait" => Loc.Get("quality.worst_portrait"),
            "Audio Only" => Loc.Get("quality.audio_only"),
            _ => value,
        };
    }
}
