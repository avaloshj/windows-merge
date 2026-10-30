using TabDock.Core.Abstractions;

namespace TabDock.Core.Domain;

/// <summary>
/// Copia serializable de <c>WINDOWPLACEMENT</c>. <see cref="Normal"/> está en coordenadas de área de trabajo
/// (no de pantalla), tal como lo devuelve y lo espera Windows.
/// </summary>
public sealed record WindowPlacementData(int Flags, int ShowCommand, int MinX, int MinY, int MaxX, int MaxY, PixelRect Normal);
