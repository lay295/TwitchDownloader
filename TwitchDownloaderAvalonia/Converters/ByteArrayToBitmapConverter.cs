using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace TwitchDownloaderAvalonia.Converters
{
    public sealed class ByteArrayToBitmapConverter : IValueConverter
    {
        public static readonly ByteArrayToBitmapConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not byte[] { Length: > 0 } bytes)
                return null;

            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
