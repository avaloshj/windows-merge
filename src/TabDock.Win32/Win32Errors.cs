using System.Runtime.InteropServices;
using TabDock.Core;

namespace TabDock.Win32;

internal static class Win32Errors
{
    private const string OperationFailedKey = "Error_WindowOperationFailed";

    public static DomainError WindowNotFound { get; } = new("window.notFound", "Error_WindowNotFound");

    /// <summary>El HWND existe pero pertenece a otro proceso: Windows lo reutilizó.</summary>
    public static DomainError IdentityMismatch { get; } = new("window.identityMismatch", "Error_WindowNotFound");

    public static DomainError ForegroundDenied { get; } = new("window.foregroundDenied", OperationFailedKey);

    /// <summary>Error con el código de <c>GetLastError</c> de la última llamada Win32.</summary>
    public static DomainError LastError(string code) => new(code, OperationFailedKey, Marshal.GetLastPInvokeError());
}
