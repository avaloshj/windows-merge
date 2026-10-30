using System.Windows;
using System.Windows.Input;
using TabDock.App.ViewModels;

namespace TabDock.App.Views;

public partial class QuickSearchWindow : Window
{
    private readonly QuickSearchViewModel _viewModel;
    private readonly Action<QuickSearchItem> _choose;
    private bool _closing;

    public QuickSearchWindow(QuickSearchViewModel viewModel, Action<QuickSearchItem> choose)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _choose = choose ?? throw new ArgumentNullException(nameof(choose));
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => QueryBox.Focus();
        Deactivated += (_, _) => CloseOnce();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPreviewKeyDown(e);

        switch (e.Key)
        {
            case Key.Escape:
                CloseOnce();
                e.Handled = true;
                break;
            case Key.Down:
                _viewModel.MoveSelection(+1);
                ResultList.ScrollIntoView(_viewModel.Selected);
                e.Handled = true;
                break;
            case Key.Up:
                _viewModel.MoveSelection(-1);
                ResultList.ScrollIntoView(_viewModel.Selected);
                e.Handled = true;
                break;
            case Key.Enter:
                Choose();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Cerrar desde fuera (Alt+F4, UI Automation) también desactiva la ventana: sin esta marca, <c>Deactivated</c>
    /// volvería a llamar a <see cref="Window.Close"/> mientras ya se cierra y WPF lanzaría una excepción.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e) => Choose();

    private void Choose()
    {
        var item = _viewModel.Selected;
        CloseOnce();
        if (item is not null)
            _choose(item);
    }

    private void CloseOnce()
    {
        if (_closing)
            return;

        _closing = true;
        Close();
    }
}
