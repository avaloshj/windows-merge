using TabDock.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TabDock.Win32.Dwm;

/// <summary>Cálculos de geometría para ventanas propias con chrome personalizado.</summary>
public static class WindowGeometry
{
    /// <summary>
    /// Cuánto sobresale una ventana maximizada del área de trabajo (o del monitor completo en pantalla completa).
    /// Con <c>WindowChrome</c> el sistema mantiene los bordes de redimensionado fuera de la pantalla, y el contenido
    /// quedaría recortado si no se compensa con un margen igual a este valor.
    /// </summary>
    public static PixelThickness GetOverflow(nint hwnd, bool fullScreen)
    {
        var handle = (HWND)hwnd;
        if (!PInvoke.GetWindowRect(handle, out var window))
            return default;

        var monitor = PInvoke.MonitorFromWindow(handle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!PInvoke.GetMonitorInfo(monitor, ref info))
            return default;

        var area = fullScreen ? info.rcMonitor : info.rcWork;
        return new PixelThickness(
            Left: Math.Max(0, area.left - window.left),
            Top: Math.Max(0, area.top - window.top),
            Right: Math.Max(0, window.right - area.right),
            Bottom: Math.Max(0, window.bottom - area.bottom));
    }

    /// <summary>Rectángulo de la ventana en píxeles físicos de pantalla.</summary>
    public static PixelRect GetBounds(nint hwnd) =>
        PInvoke.GetWindowRect((HWND)hwnd, out var rect)
            ? new PixelRect(rect.left, rect.top, rect.right, rect.bottom)
            : default;

    /// <summary>Mueve y redimensiona una ventana propia sin activarla ni cambiar su orden Z.</summary>
    public static void SetBounds(nint hwnd, PixelRect bounds) =>
        _ = PInvoke.SetWindowPos(
            (HWND)hwnd,
            default,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
}
