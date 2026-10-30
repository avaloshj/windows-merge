using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TabDock.App.ViewModels;

namespace TabDock.App.Controls;

/// <summary>
/// Cuadro de renombrado en línea: al activarse toma el foco y selecciona el texto; Intro confirma, Esc cancela y perder
/// el foco confirma.
/// </summary>
public static class InlineRename
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached(
        "IsActive", typeof(bool), typeof(InlineRename), new PropertyMetadata(false, OnIsActiveChanged));

    public static bool GetIsActive(DependencyObject element) => (bool)element.GetValue(IsActiveProperty);

    public static void SetIsActive(DependencyObject element, bool value) => element.SetValue(IsActiveProperty, value);

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
            return;

        box.PreviewKeyDown -= OnPreviewKeyDown;
        box.LostKeyboardFocus -= OnLostKeyboardFocus;
        if (e.NewValue is not true)
            return;

        box.PreviewKeyDown += OnPreviewKeyDown;
        box.LostKeyboardFocus += OnLostKeyboardFocus;
        _ = box.Dispatcher.BeginInvoke(() =>
        {
            box.Focus();
            box.SelectAll();
        }, DispatcherPriority.Input);
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not TabViewModel tab)
            return;

        if (e.Key == Key.Enter)
        {
            tab.CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            tab.CancelRename();
            e.Handled = true;
        }
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is TabViewModel tab)
            tab.CommitRename();
    }
}
