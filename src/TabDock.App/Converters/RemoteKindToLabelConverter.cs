using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TabDock.App.Localization;
using TabDock.Core.Domain;

namespace TabDock.App.Converters;

/// <summary>Texto del badge de entorno remoto (<c>SSH</c>, <c>WSL</c>, <c>Container</c>); vacío si es local.</summary>
[ValueConversion(typeof(RemoteKind), typeof(string))]
public sealed class RemoteKindToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RemoteKind.Ssh => TextResources.Get("Remote_Ssh"),
        RemoteKind.Wsl => TextResources.Get("Remote_Wsl"),
        RemoteKind.DevContainer => TextResources.Get("Remote_DevContainer"),
        RemoteKind.Other => TextResources.Get("Remote_Other"),
        _ => string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}
