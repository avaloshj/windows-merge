using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TabDock.App.Localization;
using TabDock.App.Services;
using TabDock.App.ViewModels;
using TabDock.Core.Domain;
using TabDock.Infrastructure;
using TabDock.Infrastructure.Diagnostics;

namespace TabDock.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly IServiceProvider _services;
    private readonly AppPaths _paths;
    private readonly TimeProvider _time;

    public SettingsWindow(SettingsViewModel viewModel, IServiceProvider services, AppPaths paths, TimeProvider time)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _services = services;
        _paths = paths;
        _time = time;

        InitializeComponent();
        DataContext = viewModel;
        viewModel.OnboardingRequested += (_, _) => services.GetRequiredService<WindowLauncher>().ShowOnboarding();
        Loaded += (_, _) => viewModel.RefreshProgramsCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Si se cierra mientras se escribe un atajo, los atajos globales deben volver.
        _viewModel.EndCapture();
        base.OnClosed(e);
    }

    private static HotkeyModifiers ToHotkeyModifiers(ModifierKeys keys)
    {
        var modifiers = HotkeyModifiers.None;
        if (keys.HasFlag(ModifierKeys.Control))
            modifiers |= HotkeyModifiers.Control;
        if (keys.HasFlag(ModifierKeys.Alt))
            modifiers |= HotkeyModifiers.Alt;
        if (keys.HasFlag(ModifierKeys.Shift))
            modifiers |= HotkeyModifiers.Shift;
        if (keys.HasFlag(ModifierKeys.Windows))
            modifiers |= HotkeyModifiers.Windows;

        return modifiers;
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private void OnHotkeyGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HotkeyRowViewModel row })
            _viewModel.BeginCapture(row);
    }

    private void OnHotkeyLostFocus(object sender, KeyboardFocusChangedEventArgs e) => _viewModel.EndCapture();

    /// <summary>
    /// Captura la combinación que pulsa el usuario (no se simula ninguna tecla). Tab sin modificadores sigue moviendo el
    /// foco para no atrapar la navegación por teclado; Esc sale sin cambiar nada.
    /// </summary>
    private void OnHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: HotkeyRowViewModel row } box)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
            return;

        e.Handled = true;
        if (key == Key.Escape)
        {
            box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            return;
        }

        if (IsModifierKey(key))
            return;

        _viewModel.AssignHotkey(row, ToHotkeyModifiers(Keyboard.Modifiers), KeyInterop.VirtualKeyFromKey(key));
    }

    private void OnExportDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = TextResources.Get("Settings_ExportDiagnostics"),
            FileName = DiagnosticsExporter.SuggestedFileName(_time.GetUtcNow()),
            Filter = TextResources.Get("Settings_ZipFilter") + "|*.zip",
            DefaultExt = ".zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(this) == true)
            _viewModel.ExportDiagnostics(dialog.FileName);
    }

    private void OnOpenLogsClick(object sender, RoutedEventArgs e)
    {
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        startInfo.ArgumentList.Add(_paths.Logs);
        using var _ = Process.Start(startInfo);
    }

    private void OnOpenWindowDiagnosticsClick(object sender, RoutedEventArgs e) =>
        _services.GetRequiredService<WindowLauncher>().ShowDiagnostics();
}
