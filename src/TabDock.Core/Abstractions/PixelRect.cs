namespace TabDock.Core.Abstractions;

/// <summary>Rectángulo en píxeles físicos de pantalla.</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}
