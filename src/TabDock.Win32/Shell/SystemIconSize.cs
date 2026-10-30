using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TabDock.Win32.Shell;

/// <summary>Tamaños de icono del sistema en píxeles físicos (con la escala del monitor principal).</summary>
public static class SystemIconSize
{
    /// <summary>Icono pequeño (bandeja, barra de título): 16 px a 100 %, 20 a 125 %, 24 a 150 %…</summary>
    public static int Small()
    {
        var size = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON);
        return size > 0 ? size : 16;
    }
}
