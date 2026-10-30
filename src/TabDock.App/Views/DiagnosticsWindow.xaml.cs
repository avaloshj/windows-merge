using System.Windows;
using TabDock.App.Hosting;
using TabDock.App.Localization;
using TabDock.App.Services;
using TabDock.App.ViewModels;

namespace TabDock.App.Views;

public partial class DiagnosticsWindow : Window
{
    private readonly DiagnosticsViewModel _viewModel;
    private readonly DemoFrameFactory _demoFrameFactory;
    private readonly FrameCoordinator _coordinator;

    public DiagnosticsWindow(DiagnosticsViewModel viewModel, DemoFrameFactory demoFrameFactory, FrameCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _demoFrameFactory = demoFrameFactory;
        _coordinator = coordinator;

        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.RefreshCommand.Execute(null);
    }

    private void OnOpenDemoFrameClick(object sender, RoutedEventArgs e) => _demoFrameFactory.Create().Show();

    private void OnCaptureSelectedClick(object sender, RoutedEventArgs e)
    {
        var handles = Grid.SelectedItems.OfType<WindowRowViewModel>().Select(r => r.RawHandle).ToList();
        var result = _coordinator.CaptureIntoNewFrame(TextResources.Get("Frame_ManualGroupTitle"), handles);
        if (!result.IsSuccess)
            MessageBox.Show(this, TextResources.Get(result.Error.MessageKey), TextResources.Get("App_Name"), MessageBoxButton.OK, MessageBoxImage.Information);

        _viewModel.RefreshCommand.Execute(null);
    }
}
