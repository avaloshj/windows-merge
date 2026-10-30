namespace TabDock.Win32.Dwm;

/// <summary>Grosor en píxeles físicos por lado.</summary>
public readonly record struct PixelThickness(int Left, int Top, int Right, int Bottom)
{
    public bool IsZero => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;
}
