using Avalonia.Markup.Xaml;
using TwitchDownloaderAvalonia.ViewModels;

namespace TwitchDownloaderAvalonia.Markup
{
    public sealed class TranslateExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return new Binding(nameof(ViewModelBase.Loc))
            {
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
            return value is not LocalizationService loc || parameter is not string key
                ? AvaloniaProperty.UnsetValue
                : loc.Get(key);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
