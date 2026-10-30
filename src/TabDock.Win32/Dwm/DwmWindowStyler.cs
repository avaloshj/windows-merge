using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace TabDock.Win32.Dwm;

/// <summary>
/// Aplica atributos DWM a ventanas <b>propias</b> de TabDock (modo oscuro, esquinas, Mica/Acrylic).
/// Nunca se usa sobre ventanas de otros procesos.
/// </summary>
public sealed partial class DwmWindowStyler(ILogger<DwmWindowStyler> logger)
{
    /// <summary><c>DWMWA_WINDOW_CORNER_PREFERENCE</c>: Windows 11 (22000).</summary>
    public static bool SupportsRoundedCorners => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    /// <summary><c>DWMWA_SYSTEMBACKDROP_TYPE</c>: Windows 11 22H2 (22621).</summary>
    public static bool SupportsSystemBackdrop => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    /// <summary>
    /// Borde y sombra oscuros. Documentado desde Windows 11; en Windows 10 20H1+ el atributo también existe pero no está
    /// documentado, por lo que un fallo se ignora.
    /// </summary>
    public bool SetDarkMode(nint hwnd, bool dark)
    {
        BOOL value = dark;
        return Set(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, value);
    }

    public bool SetCorners(nint hwnd, WindowCorners corners)
    {
        if (!SupportsRoundedCorners)
            return false;

        var value = corners switch
        {
            WindowCorners.Square => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND,
            WindowCorners.Round => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND,
            WindowCorners.RoundSmall => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUNDSMALL,
            _ => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT,
        };
        return Set(hwnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, value);
    }

    /// <summary>
    /// Requiere que el marco DWM esté extendido a toda la ventana (en WPF: <c>WindowChrome.GlassFrameThickness = -1</c>)
    /// y que el fondo de la ventana sea transparente.
    /// </summary>
    public bool SetBackdrop(nint hwnd, WindowBackdrop backdrop)
    {
        if (!SupportsSystemBackdrop)
            return false;

        var value = backdrop switch
        {
            WindowBackdrop.Mica => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
            WindowBackdrop.Acrylic => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
            _ => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE,
        };
        return Set(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, value);
    }

    private unsafe bool Set<T>(nint hwnd, DWMWINDOWATTRIBUTE attribute, T value)
        where T : unmanaged
    {
        var hr = PInvoke.DwmSetWindowAttribute((HWND)hwnd, attribute, &value, (uint)sizeof(T));
        if (hr.Failed)
            LogAttributeFailed((int)attribute, hr.Value);

        return hr.Succeeded;
    }

    // El atributo se registra como entero: el generador de logs no ve los tipos que genera CsWin32.
    [LoggerMessage(Level = LogLevel.Debug, Message = "DwmSetWindowAttribute({Attribute}) falló con HRESULT {HResult}")]
    private partial void LogAttributeFailed(int attribute, int hResult);
}
