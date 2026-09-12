using Avalonia.Markup.Xaml;

namespace TwitchDownloaderAvalonia.Markup
{
    public sealed class TranslateExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return new Binding(nameof(LocalizationService.Culture))
            {
                Source = LocalizationService.Current,
                Mode = BindingMode.OneWay,
                Converter = TranslateConverter.Instance,
                ConverterParameter = Key,
            };
        }
    }

    public sealed class TranslateConverter : IValueConverter
    {
        public static TranslateConverter Instance { get; } = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return parameter is string key
                ? LocalizationService.Current.Get(key)
                : AvaloniaProperty.UnsetValue;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
