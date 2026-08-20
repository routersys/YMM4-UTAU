using System.Globalization;
using System.Windows;
using System.Windows.Data;
using UTAU.Notes;
using YukkuriMovieMaker.ItemEditor;

namespace UTAU.Views;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class ShowForUstSourceAttribute : Attribute, ICustomVisibilityAttribute2, ICustomVisibilityAttribute
{
    public Binding GetBinding(object item, object propertyOwner)
        => new(HideForUstSourceAttribute.HasPronunciation(item) ? HideForUstSourceAttribute.PronunciationPropertyName : string.Empty)
        {
            Source = item,
            Mode = BindingMode.OneWay,
            Converter = new VisibilityConverter(),
        };

    internal sealed class VisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string text && UstSource.TryGetPath(text, out _) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
