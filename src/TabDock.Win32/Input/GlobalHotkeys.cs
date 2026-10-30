using System.Runtime.InteropServices;
using TabDock.Core;
using TabDock.Core.Domain;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace TabDock.Win32.Input;

/// <summary>
/// Atajos globales con <c>RegisterHotKey</c>: Windows envía <c>WM_HOTKEY</c> a la ventana indicada. Sin hooks de
/// teclado globales (no se inyecta nada en otros procesos).
/// </summary>
public static class GlobalHotkeys
{
    /// <summary>Mensaje que recibe la ventana al pulsar un atajo registrado (<c>wParam</c> = identificador).</summary>
    public const int HotkeyMessage = (int)PInvoke.WM_HOTKEY;

    /// <summary><c>ERROR_HOTKEY_ALREADY_REGISTERED</c>: otro programa ya usa la combinación.</summary>
    public const int AlreadyRegisteredError = 1409;

    /// <summary>Registra el atajo sin autorrepetición. Falla con <see cref="AlreadyRegisteredError"/> si hay conflicto.</summary>
    public static Result Register(nint hwnd, int id, HotkeyModifiers modifiers, int virtualKey)
    {
        var flags = (HOT_KEY_MODIFIERS)modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT;
        return PInvoke.RegisterHotKey((HWND)hwnd, id, flags, (uint)virtualKey)
            ? Result.Success()
            : Result.Fail(new DomainError("hotkey.registerFailed", "Error_HotkeyInUse", Marshal.GetLastPInvokeError()));
    }

    public static void Unregister(nint hwnd, int id) => _ = PInvoke.UnregisterHotKey((HWND)hwnd, id);
}
