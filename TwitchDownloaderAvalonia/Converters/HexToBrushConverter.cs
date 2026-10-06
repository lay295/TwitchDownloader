using Avalonia.Media;

namespace TwitchDownloaderAvalonia.Converters
{
    public sealed class HexToBrushConverter : IValueConverter
    {
        public static readonly HexToBrushConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string hex && Color.TryParse(hex.Trim(), out var color))
                return new SolidColorBrush(color);

            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
