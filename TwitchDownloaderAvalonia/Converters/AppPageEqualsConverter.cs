using System.Globalization;
using Avalonia.Data.Converters;
using TwitchDownloaderAvalonia.Models;

namespace TwitchDownloaderAvalonia.Converters
{
    public sealed class AppPageEqualsConverter : IValueConverter
    {
        public static readonly AppPageEqualsConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not AppPage selected)
                return Boxed.False;

            if (parameter is AppPage page)
                return Boxed.From(selected == page);

            return Boxed.From(parameter is string text && Enum.TryParse(text, out AppPage parsed) && selected == parsed);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
