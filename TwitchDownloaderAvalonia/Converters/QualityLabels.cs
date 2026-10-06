namespace TwitchDownloaderAvalonia.Converters
{
    public static class QualityLabels
    {
        public static string Get(LocalizationService loc, string value) => value switch
        {
            QualityNames.SOURCE => loc.Get("quality.source"),
            QualityNames.SOURCE_PORTRAIT => loc.Get("quality.source_portrait"),
            QualityNames.WORST => loc.Get("quality.worst"),
            QualityNames.WORST_PORTRAIT => loc.Get("quality.worst_portrait"),
            QualityNames.AUDIO_ONLY => loc.Get("quality.audio_only"),
            _ => value,
        };

        public static string WithSize(LocalizationService loc, string qualityName, long sizeInBytes)
        {
            var label = Get(loc, qualityName);
            return sizeInBytes != 0
                ? $"{label} - {VideoSizeEstimator.StringifyByteCount(sizeInBytes)}"
                : label;
        }
    }
}
