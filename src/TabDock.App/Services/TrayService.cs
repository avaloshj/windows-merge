using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.DependencyInjection;
using TabDock.App.Localization;
using TabDock.App.Views;
using TabDock.Core.Domain;
using TabDock.Infrastructure;
using TabDock.Win32.Shell;

namespace TabDock.App.Services;

/// <summary>
/// Icono y menú de la bandeja del sistema: agrupar por programa, separar todas, pausar, configuración, diagnóstico,
/// logs y salir. El menú se reconstruye cuando cambian las reglas.
/// "Salir" cierra la aplicación, que restaura todas las ventanas al terminar.
/// </summary>
public sealed class TrayService(IServiceProvider services, FrameCoordinator coordinator, AppPaths paths) : IDisposable
{
    private TaskbarIcon? _icon;
    private MenuItem? _pauseItem;

    public void Show(SettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _icon = new TaskbarIcon
        {
            ToolTipText = TextResources.Get("App_Name"),
            Icon = LoadIcon(),
            ContextMenu = BuildMenu(settings),
            NoLeftClickDelay = true,
        };
        _icon.TrayLeftMouseUp += (_, _) => OpenContextMenu();
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    /// <summary>Vuelve a crear el menú (p. ej. tras activar o añadir un programa en Ajustes).</summary>
    public void Rebuild(SettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (_icon is not null)
            _icon.ContextMenu = BuildMenu(settings);
    }

    public void Notify(string message, bool warning = false) =>
        _icon?.ShowNotification(TextResources.Get("App_Name"), message, warning ? NotificationIcon.Warning : NotificationIcon.Info);

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }

    /// <summary>
    /// Icono de la aplicación al tamaño exacto de la bandeja: el .ico trae varios tamaños y se elige el del sistema, así
    /// Windows no tiene que reescalarlo (se vería borroso).
    /// </summary>
    private static System.Drawing.Icon LoadIcon()
    {
        var size = SystemIconSize.Small();
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/TabDock.ico", UriKind.Absolute))!.Stream;
        return new System.Drawing.Icon(stream, size, size);
    }

    private void OpenContextMenu()
    {
        if (_icon?.ContextMenu is { } menu)
            menu.IsOpen = true;
    }

    private ContextMenu BuildMenu(SettingsDocument settings)
    {
        var menu = new ContextMenu();

        foreach (var rule in settings.Rules.Where(r => r.Enabled))
            menu.Items.Add(Item(TextResources.Format("Tray_MergeRule", rule.DisplayName), () => MergeRule(rule)));

        menu.Items.Add(Item(TextResources.Get("Tray_UnmergeAll"), coordinator.DetachAll));
        menu.Items.Add(new Separator());

        _pauseItem = Item(TextResources.Get(coordinator.IsPaused ? "Tray_Resume" : "Tray_Pause"), TogglePause);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(Item(TextResources.Get("Tray_Settings"), () => services.GetRequiredService<WindowLauncher>().ShowSettings()));
        menu.Items.Add(Item(TextResources.Get("Tray_Diagnostics"), () => services.GetRequiredService<WindowLauncher>().ShowDiagnostics()));
        menu.Items.Add(Item(TextResources.Get("Tray_OpenLogs"), OpenLogs));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(TextResources.Get("Tray_Exit"), () => Application.Current.Shutdown()));
        return menu;
    }

    private void MergeRule(AppRule rule)
    {
        var result = coordinator.MergeAll(rule);
        if (!result.IsSuccess)
            Notify(TextResources.Format("Tray_NothingToMerge", rule.DisplayName));
    }

    private void TogglePause()
    {
        coordinator.IsPaused = !coordinator.IsPaused;
        if (_pauseItem is not null)
            _pauseItem.Header = TextResources.Get(coordinator.IsPaused ? "Tray_Resume" : "Tray_Pause");
    }

    private void OpenLogs()
    {
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        startInfo.ArgumentList.Add(paths.Logs);
        using var _ = Process.Start(startInfo);
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
