using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

namespace TwitchDownloaderAvalonia.Converters
{
    public sealed class ByteArrayToBitmapConverter : IValueConverter
    {
        public static readonly ByteArrayToBitmapConverter Instance = new();
        private static readonly ConditionalWeakTable<byte[], Bitmap> Cache = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not byte[] { Length: > 0 } bytes)
                return null;

            if (Cache.TryGetValue(bytes, out var cached))
                return cached;

            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            Cache.Add(bytes, bitmap);
            return bitmap;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
