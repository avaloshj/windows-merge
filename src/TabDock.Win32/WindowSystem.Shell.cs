using System.Runtime.InteropServices;
using TabDock.Core;
using TabDock.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;

namespace TabDock.Win32;

/// <summary>Barra de tareas (<c>ITaskbarList</c>) y área de trabajo del monitor.</summary>
public sealed partial class WindowSystem
{
    private ITaskbarList? _taskbar;
    private bool _taskbarUnavailable;

    public PixelRect GetWorkArea(WindowHandle handle)
    {
        var monitor = PInvoke.MonitorFromWindow(ToHwnd(handle), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        return PInvoke.GetMonitorInfo(monitor, ref info)
            ? new PixelRect(info.rcWork.left, info.rcWork.top, info.rcWork.right, info.rcWork.bottom)
            : default;
    }

    /// <summary>
    /// Debe llamarse desde un hilo STA (el de UI o el del guardián): <c>ITaskbarList</c> es un objeto COM de apartamento.
    /// </summary>
    public Result SetTaskbarButton(WindowHandle handle, bool visible)
    {
        if (Taskbar is not { } taskbar)
            return Result.Fail(new DomainError("taskbar.unavailable", "Error_WindowOperationFailed"));

        try
        {
            if (visible)
                taskbar.AddTab((HWND)handle.Value);
            else
                taskbar.DeleteTab((HWND)handle.Value);

            return Result.Success();
        }
        catch (COMException ex)
        {
            return Result.Fail(new DomainError("taskbar.failed", "Error_WindowOperationFailed", ex.HResult));
        }
    }

    private ITaskbarList? Taskbar
    {
        get
        {
            if (_taskbar is not null || _taskbarUnavailable)
                return _taskbar;

            try
            {
                var taskbar = (ITaskbarList)new TaskbarList();
                taskbar.HrInit();
                _taskbar = taskbar;
            }
            catch (COMException)
            {
                // Sin Explorer (p. ej. shell alternativo): no hay barra de tareas que gestionar.
                _taskbarUnavailable = true;
            }

            return _taskbar;
        }
    }
}
