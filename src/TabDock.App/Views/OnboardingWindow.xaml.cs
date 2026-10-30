using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using TabDock.App.Services;
using TabDock.App.ViewModels;

namespace TabDock.App.Views;

public partial class OnboardingWindow : Window
{
    private readonly OnboardingViewModel _viewModel;
    private readonly FrameCoordinator _coordinator;

    public OnboardingWindow(OnboardingViewModel viewModel, FrameCoordinator coordinator)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

        InitializeComponent();
        DataContext = viewModel;
        viewModel.Completed += (_, _) => Close();
        coordinator.GroupsChanged += OnGroupsChanged;
    }

    /// <summary>Cerrar con la X cuenta como "saltar": no se vuelve a mostrar en cada arranque.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.Skip();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _coordinator.GroupsChanged -= OnGroupsChanged;
        base.OnClosed(e);
    }

    private void OnGroupsChanged(object? sender, EventArgs e)
    {
        if (_coordinator.GroupCount > 0)
            _viewModel.NotifyMerged();
    }

    /// <summary>Solo escribe en el portapapeles (nunca lo lee). Otro programa puede tenerlo abierto un instante.</summary>
    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(OnboardingViewModel.VsCodeSnippet);
            CopiedText.Visibility = Visibility.Visible;
        }
        catch (COMException)
        {
            CopiedText.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Lo mismo que el atajo: con esta ventana delante, agrupa el primer programa activo.</summary>
    private void OnMergeNowClick(object sender, RoutedEventArgs e) => _ = _coordinator.MergeForeground();
}
