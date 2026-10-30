using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TabDock.App.Converters;

/// <summary>Convierte <c>#RRGGBB</c> en un pincel congelado (cacheado por color).</summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, SolidColorBrush> s_cache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Brushes.Transparent;

        return s_cache.GetOrAdd(hex, static key =>
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(key));
            brush.Freeze();
            return brush;
        });
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}
