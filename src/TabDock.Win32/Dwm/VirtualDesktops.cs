using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TabDock.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace TabDock.Win32.Dwm;

/// <summary>
/// <c>IVirtualDesktopManager</c> (API pública de Windows 10+). <c>MoveWindowToDesktop</c> solo funciona con ventanas del
/// propio proceso; con ventanas ajenas devuelve error de acceso.
/// </summary>
public sealed partial class VirtualDesktops(ILogger<VirtualDesktops> logger) : IVirtualDesktops
{
    private IVirtualDesktopManager? _manager;
    private bool _unavailable;

    public unsafe Guid? GetDesktopId(WindowHandle handle)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240) || Manager is not { } manager)
            return null;

        try
        {
            Guid desktop;
            manager.GetWindowDesktopId((HWND)handle.Value, &desktop);
            return desktop == Guid.Empty ? null : desktop;
        }
        catch (COMException)
        {
            return null;   // Ventana sin escritorio asignado (oculta, de sistema) o ya destruida.
        }
    }

    public unsafe bool MoveOwnWindowToDesktop(WindowHandle ownWindow, Guid desktopId)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240) || Manager is not { } manager)
            return false;

        try
        {
            manager.MoveWindowToDesktop((HWND)ownWindow.Value, &desktopId);
            return true;
        }
        catch (COMException ex)
        {
            LogMoveFailed(ex.HResult);
            return false;
        }
    }

    private IVirtualDesktopManager? Manager
    {
        get
        {
            if (_manager is not null || _unavailable)
                return _manager;

            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
            {
                _unavailable = true;
                return null;
            }

            try
            {
                _manager = (IVirtualDesktopManager)new VirtualDesktopManager();
            }
            catch (COMException ex)
            {
                _unavailable = true;
                LogUnavailable(ex.HResult);
            }

            return _manager;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "IVirtualDesktopManager no disponible (HRESULT {HResult})")]
    private partial void LogUnavailable(int hResult);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MoveWindowToDesktop falló (HRESULT {HResult})")]
    private partial void LogMoveFailed(int hResult);
}
