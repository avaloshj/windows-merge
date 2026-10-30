using TabDock.Core.Diagnostics;

namespace TabDock.Core.Abstractions;

/// <summary>Instantánea de metadatos de una ventana de primer nivel.</summary>
/// <remarks>
/// <see cref="Title"/> y <see cref="ExecutablePath"/> pueden contener datos personales:
/// están marcados con <see cref="SensitiveDataAttribute"/> y se enmascaran al registrarse.
/// </remarks>
public sealed record WindowInfo
{
    public required WindowHandle Handle { get; init; }

    public required int ProcessId { get; init; }

    /// <summary>Hora de inicio del proceso; junto a <see cref="Handle"/> y <see cref="ProcessId"/> identifica la ventana.</summary>
    public DateTimeOffset? ProcessStartTime { get; init; }

    /// <summary>Ruta completa del ejecutable, o vacía si el proceso no es accesible.</summary>
    [SensitiveData(SensitiveDataKind.UserPath)]
    public required string ExecutablePath { get; init; }

    public required string ClassName { get; init; }

    [SensitiveData(SensitiveDataKind.Text)]
    public required string Title { get; init; }

    /// <summary>Límites visibles (sin bordes invisibles), según <c>DWMWA_EXTENDED_FRAME_BOUNDS</c>.</summary>
    public required PixelRect ExtendedFrameBounds { get; init; }

    public required bool IsVisible { get; init; }

    public required bool IsCloaked { get; init; }

    public required bool HasOwner { get; init; }

    public required bool IsToolWindow { get; init; }

    /// <summary>El proceso no se pudo abrir para consulta (protegido o sin permisos).</summary>
    public required bool IsProcessAccessible { get; init; }

    /// <summary>El proceso se ejecuta elevado (o no se pudo leer su token): UIPI impide gestionarlo.</summary>
    public required bool IsElevatedProcess { get; init; }

    public required WindowShowState ShowState { get; init; }

    public string ExecutableName => Path.GetFileName(ExecutablePath);
}
