namespace TabDock.Core.Domain;

/// <summary>
/// Contenido de <c>state.json</c>: ventanas capturadas y su estado original. Se escribe <b>antes</b> de mover cualquier
/// ventana, de modo que tras un cierre inesperado el guardián o el siguiente arranque puedan restaurarlas.
/// No contiene títulos ni rutas.
/// </summary>
public sealed record RuntimeState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public int OwnerProcessId { get; init; }

    /// <summary>TabDock terminó sin ventanas capturadas pendientes de restaurar.</summary>
    public bool CleanShutdown { get; init; } = true;

    public IReadOnlyList<CapturedWindowRecord> Windows { get; init; } = [];

    public static RuntimeState Empty { get; } = new();
}
