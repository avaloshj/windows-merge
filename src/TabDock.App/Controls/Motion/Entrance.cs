using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TabDock.App.Controls.Motion;

/// <summary>
/// Entrada con fundido y desplazamiento horizontal (−8 px → 0, 300 ms). Dentro de un <see cref="ItemsControl"/> se
/// escalona 40 ms por elemento según su índice (máximo 10 elementos escalonados).
/// </summary>
public static class Entrance
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(Entrance), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && e.NewValue is true && !element.IsLoaded)
        {
            element.Opacity = 0;
            element.Loaded += OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        element.Loaded -= OnLoaded;

        var index = ItemIndex(element);
        var maxStaggered = (int)MotionTokens.Number(MotionTokens.StaggerMaxItems, 10);
        var delay = TimeSpan.FromMilliseconds(MotionTokens.Number(MotionTokens.StaggerMilliseconds, 40) * Math.Min(index, maxStaggered));
        var duration = MotionTokens.Time(MotionTokens.DurationEntrance);
        var ease = MotionTokens.Ease(MotionTokens.EaseStandard);

        var translate = new TranslateTransform(MotionTokens.Number(MotionTokens.EntranceOffsetX, -8), 0);
        element.RenderTransform = translate;

        _ = Animator.RunAsync(element, UIElement.OpacityProperty, from: 0, to: 1, duration, ease, delay);
        _ = Animator.RunAsync(translate, TranslateTransform.XProperty, from: null, to: 0, duration, ease, delay);
    }

    private static int ItemIndex(DependencyObject element)
    {
        var owner = ItemsControl.ItemsControlFromItemContainer(element);
        return owner is null ? 0 : Math.Max(0, owner.ItemContainerGenerator.IndexFromContainer(element));
    }
}
