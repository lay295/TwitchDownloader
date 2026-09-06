using System.Globalization;
using Avalonia.Data.Converters;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderAvalonia.Converters
{
    public sealed class VideoTrimModeEqualsConverter : IValueConverter
    {
        public static readonly VideoTrimModeEqualsConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not VideoTrimMode selected)
                return Boxed.False;

            if (parameter is VideoTrimMode mode)
                return Boxed.From(selected == mode);

            return Boxed.From(parameter is string text && Enum.TryParse(text, out VideoTrimMode parsed) && selected == parsed);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is true && parameter is string text && Enum.TryParse(text, out VideoTrimMode parsed))
                return parsed;

            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }
}
