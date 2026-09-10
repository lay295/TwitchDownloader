namespace TwitchDownloaderAvalonia.Converters
{
    public sealed class DoubleDecimalConverter : IValueConverter
    {
        public static readonly DoubleDecimalConverter Instance = new();
        public static readonly object Zero = 0d;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                double number => (decimal?)number,
                float number => (decimal?)number,
                int number => number,
                _ => null,
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                null => 0d,
                decimal number => (double)number,
                double number => number,
                int number => (double)number,
                _ => BindingOperations.DoNothing,
            };
        }
    }
}
