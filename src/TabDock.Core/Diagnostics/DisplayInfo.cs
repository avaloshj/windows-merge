namespace TabDock.Core.Diagnostics;

/// <summary>Un monitor: resolución en píxeles físicos, escala (DPI) y si es el principal.</summary>
public sealed record DisplayInfo(int Width, int Height, int Dpi, bool IsPrimary);
