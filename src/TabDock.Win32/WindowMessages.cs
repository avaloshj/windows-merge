using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TabDock.Win32;

/// <summary>Mensajes de ventana que el marco (WPF) necesita interpretar.</summary>
public static class WindowMessages
{
    public const int WindowPosChanged = (int)PInvoke.WM_WINDOWPOSCHANGED;

    /// <summary>El <c>WM_WINDOWPOSCHANGED</c> incluye un cambio de orden Z (no lleva <c>SWP_NOZORDER</c>).</summary>
    public static unsafe bool ChangesZOrder(nint lParam) =>
        lParam != 0 && !((WINDOWPOS*)lParam)->flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
}
