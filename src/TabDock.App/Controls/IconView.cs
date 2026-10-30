using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TabDock.App.Controls;

/// <summary>Dibuja un icono de trazo (lienzo 24×24, como Lucide) con el color de <see cref="Control.Foreground"/>.</summary>
public sealed class IconView : Control
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(Geometry), typeof(IconView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    static IconView()
    {
        FocusableProperty.OverrideMetadata(typeof(IconView), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(IconView), new FrameworkPropertyMetadata(false));
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}
