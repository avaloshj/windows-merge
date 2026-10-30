using System.Runtime.InteropServices;
using TabDock.Core.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace TabDock.Win32.Shell;

/// <summary>Monitores conectados con su resolución física y su escala, para el diagnóstico.</summary>
public static class Displays
{
    private const int DefaultDpi = 96;

    public static unsafe IReadOnlyList<DisplayInfo> Enumerate()
    {
        var displays = new List<DisplayInfo>();
        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT?)null, (monitor, _, _, _) =>
        {
            var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (PInvoke.GetMonitorInfo(monitor, ref info))
            {
                var dpi = OperatingSystem.IsWindowsVersionAtLeast(10)
                    && PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Succeeded
                    ? (int)dpiX
                    : DefaultDpi;
                displays.Add(new DisplayInfo(
                    info.rcMonitor.right - info.rcMonitor.left,
                    info.rcMonitor.bottom - info.rcMonitor.top,
                    dpi,
                    (info.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0));
            }

            return true;
        }, default);

        return displays;
    }
}
