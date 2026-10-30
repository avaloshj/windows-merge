using System.Globalization;

namespace TabDock.Core.Abstractions;

/// <summary>Identificador opaco de una ventana de primer nivel (HWND).</summary>
public readonly record struct WindowHandle(nint Value)
{
    public bool IsNull => Value == 0;

    public override string ToString() => "0x" + Value.ToString("X", CultureInfo.InvariantCulture);
}
