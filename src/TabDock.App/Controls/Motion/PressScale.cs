using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TabDock.App.Controls.Motion;

/// <summary>Escala 0.98 mientras se mantiene pulsado el botón principal del ratón.</summary>
public static class PressScale
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(PressScale), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        if (e.NewValue is true)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(1, 1);
            element.PreviewMouseLeftButtonDown += OnPressed;
            element.PreviewMouseLeftButtonUp += OnReleased;
            element.MouseLeave += OnReleased;
        }
        else
        {
            element.PreviewMouseLeftButtonDown -= OnPressed;
            element.PreviewMouseLeftButtonUp -= OnReleased;
            element.MouseLeave -= OnReleased;
        }
    }

    private static void OnPressed(object sender, MouseButtonEventArgs e) =>
        ScaleTo((UIElement)sender, MotionTokens.Number(MotionTokens.PressScale, 0.98));

    private static void OnReleased(object sender, MouseEventArgs e) => ScaleTo((UIElement)sender, 1);

    private static void ScaleTo(UIElement element, double scale)
    {
        if (element.RenderTransform is not ScaleTransform transform)
            return;

        var duration = MotionTokens.Time(MotionTokens.DurationHover);
        var ease = MotionTokens.Ease(MotionTokens.EaseStandard);
        _ = Animator.RunAsync(transform, ScaleTransform.ScaleXProperty, from: null, scale, duration, ease);
        _ = Animator.RunAsync(transform, ScaleTransform.ScaleYProperty, from: null, scale, duration, ease);
    }
}
