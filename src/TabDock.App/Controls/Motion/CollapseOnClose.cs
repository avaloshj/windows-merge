using System.Windows;
using System.Windows.Input;

namespace TabDock.App.Controls.Motion;

/// <summary>
/// Cuando <c>IsClosing</c> pasa a <see langword="true"/>, colapsa la altura y funde el elemento (180 ms) y después
/// ejecuta <c>Command</c> con el <c>DataContext</c>: el ViewModel elimina el elemento solo cuando la animación acaba,
/// para que la lista no salte.
/// </summary>
public static class CollapseOnClose
{
    public static readonly DependencyProperty IsClosingProperty = DependencyProperty.RegisterAttached(
        "IsClosing", typeof(bool), typeof(CollapseOnClose), new PropertyMetadata(false, OnIsClosingChanged));

    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command", typeof(ICommand), typeof(CollapseOnClose), new PropertyMetadata(null));

    public static bool GetIsClosing(DependencyObject element) => (bool)element.GetValue(IsClosingProperty);

    public static void SetIsClosing(DependencyObject element, bool value) => element.SetValue(IsClosingProperty, value);

    public static ICommand? GetCommand(DependencyObject element) => (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) => element.SetValue(CommandProperty, value);

    private static void OnIsClosingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && e.NewValue is true)
            _ = CollapseAsync(element);
    }

    private static async Task CollapseAsync(FrameworkElement element)
    {
        element.IsHitTestVisible = false;
        var duration = MotionTokens.Time(MotionTokens.DurationCollapse);
        var ease = MotionTokens.Ease(MotionTokens.EaseExit);

        var fade = Animator.RunAsync(element, UIElement.OpacityProperty, from: null, to: 0, duration, ease);
        var collapse = MotionTokens.Flag(MotionTokens.CollapseAnimateHeight, fallback: true)
            ? Animator.RunAsync(element, FrameworkElement.HeightProperty, element.ActualHeight, 0, duration, ease)
            : Task.CompletedTask;

        await Task.WhenAll(fade, collapse).ConfigureAwait(true);

        var command = GetCommand(element);
        var parameter = element.DataContext;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }
}
