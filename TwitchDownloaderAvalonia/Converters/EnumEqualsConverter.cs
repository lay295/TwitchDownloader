namespace TwitchDownloaderAvalonia.Converters
{
    public sealed class EnumEqualsConverter : IValueConverter
    {
        public static readonly EnumEqualsConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Enum selected && parameter is Enum mode)
                return Boxed.From(Equals(selected, mode));

            return Boxed.False;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is true && parameter is Enum mode)
                return mode;

            return BindingOperations.DoNothing;
        }
    }
}
