using Windows.Win32;

namespace TabDock.Win32.Input;

public static class CursorPosition
{
    /// <summary>Posición del cursor en píxeles físicos de pantalla (proceso Per-Monitor V2).</summary>
    public static (int X, int Y) Get() => PInvoke.GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);
}
