using System.Globalization;
using System.Windows;
using System.Windows.Data;
using UTAU.Notes;
using YukkuriMovieMaker.ItemEditor;

namespace UTAU.Views;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class HideForUstSourceAttribute : Attribute, ICustomVisibilityAttribute2, ICustomVisibilityAttribute
{
    public const string PronunciationPropertyName = "Hatsuon";

    public Binding GetBinding(object item, object propertyOwner)
        => new(HasPronunciation(item) ? PronunciationPropertyName : string.Empty)
        {
            Source = item,
            Mode = BindingMode.OneWay,
            Converter = new VisibilityConverter(),
        };

    public static bool HasPronunciation(object? item)
        => item?.GetType().GetProperty(PronunciationPropertyName)?.PropertyType == typeof(string);

    internal sealed class VisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string text && UstSource.TryGetPath(text, out _) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
