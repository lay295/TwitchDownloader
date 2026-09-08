using System.Globalization;
using Avalonia.Data.Converters;

namespace TwitchDownloaderAvalonia.Converters
{
    public sealed class InverseBoolConverter : IValueConverter
    {
        public static readonly InverseBoolConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? Boxed.False : Boxed.True;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? Boxed.False : Boxed.True;
        }
    }
}
