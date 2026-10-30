using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace TabDock.Win32.Input;

/// <summary>
/// Estado físico de los botones del ratón en este instante. WPF solo lo sabe tras procesar el mensaje del botón, que
/// llega después de <c>WM_ACTIVATE</c> cuando un clic activa la ventana.
/// </summary>
public static class MouseButtons
{
    private const short PressedBit = unchecked((short)0x8000);

    public static bool AnyPressed() =>
        IsPressed(VIRTUAL_KEY.VK_LBUTTON) || IsPressed(VIRTUAL_KEY.VK_RBUTTON) || IsPressed(VIRTUAL_KEY.VK_MBUTTON);

    private static bool IsPressed(VIRTUAL_KEY key) => (PInvoke.GetAsyncKeyState((int)key) & PressedBit) != 0;
}
