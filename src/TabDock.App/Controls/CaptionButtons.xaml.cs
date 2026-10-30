using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using TabDock.App.Localization;

namespace TabDock.App.Controls;

/// <summary>Botones de ventana de Windows: minimizar, maximizar/restaurar y cerrar.</summary>
public partial class CaptionButtons : UserControl
{
    private const string MaximizeGlyph = "\uE922";
    private const string RestoreGlyph = "\uE923";

    public static readonly DependencyProperty MinimizeCommandProperty = DependencyProperty.Register(
        nameof(MinimizeCommand), typeof(ICommand), typeof(CaptionButtons));

    public static readonly DependencyProperty MaximizeCommandProperty = DependencyProperty.Register(
        nameof(MaximizeCommand), typeof(ICommand), typeof(CaptionButtons));

    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
        nameof(CloseCommand), typeof(ICommand), typeof(CaptionButtons));

    /// <summary>Cambia el glifo y el nombre accesible entre "Maximizar" y "Restaurar".</summary>
    public static readonly DependencyProperty IsMaximizedProperty = DependencyProperty.Register(
        nameof(IsMaximized), typeof(bool), typeof(CaptionButtons), new PropertyMetadata(false, OnIsMaximizedChanged));

    public CaptionButtons()
    {
        InitializeComponent();
    }

    public ICommand? MinimizeCommand
    {
        get => (ICommand?)GetValue(MinimizeCommandProperty);
        set => SetValue(MinimizeCommandProperty, value);
    }

    public ICommand? MaximizeCommand
    {
        get => (ICommand?)GetValue(MaximizeCommandProperty);
        set => SetValue(MaximizeCommandProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public bool IsMaximized
    {
        get => (bool)GetValue(IsMaximizedProperty);
        set => SetValue(IsMaximizedProperty, value);
    }

    private static void OnIsMaximizedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var buttons = (CaptionButtons)d;
        var maximized = (bool)e.NewValue;
        var label = TextResources.Get(maximized ? "Frame_Restore" : "Frame_Maximize");
        buttons.MaximizeButton.Content = maximized ? RestoreGlyph : MaximizeGlyph;
        buttons.MaximizeButton.ToolTip = label;
        AutomationProperties.SetName(buttons.MaximizeButton, label);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => Execute(MinimizeCommand);

    private void OnMaximizeClick(object sender, RoutedEventArgs e) => Execute(MaximizeCommand);

    private void OnCloseClick(object sender, RoutedEventArgs e) => Execute(CloseCommand);

    private static void Execute(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
            command.Execute(null);
    }
}
