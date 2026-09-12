namespace TwitchDownloaderAvalonia.Converters
{
    /// <summary>
    /// NumericUpDown.Value is decimal?; view-model trim/thread fields stay int.
    /// </summary>
    public sealed class IntDecimalConverter : IValueConverter
    {
        public static readonly IntDecimalConverter Instance = new();
        public static readonly object Zero = 0;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int number)
                return (decimal?)number;

            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                null => 0,
                decimal number => (int)decimal.Truncate(number),
                int number => number,
                _ => BindingOperations.DoNothing,
            };
        }
    }
}
