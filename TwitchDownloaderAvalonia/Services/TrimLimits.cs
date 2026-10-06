namespace TwitchDownloaderAvalonia.Services
{
    internal static class TrimLimits
    {
        public const decimal DEFAULT_HOUR_MAXIMUM = 48;

        public static decimal HourMaximum(TimeSpan length)
        {
            if (length <= TimeSpan.Zero)
                return DEFAULT_HOUR_MAXIMUM;

            return (int)length.TotalHours;
        }
    }
}
