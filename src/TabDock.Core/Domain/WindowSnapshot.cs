using TabDock.Core.Abstractions;

namespace TabDock.Core.Domain;

/// <summary>Estado original de una ventana antes de gestionarla; basta para devolverla exactamente a donde estaba.</summary>
/// <param name="Identity">Ventana a la que pertenece; la restauración se niega si ya no coincide.</param>
/// <param name="Placement">Colocación normal/maximizada/minimizada.</param>
/// <param name="ActualBounds">Rectángulo real en pantalla (difiere de la colocación normal si estaba acoplada con Snap).</param>
/// <param name="WasVisible">Visibilidad original.</param>
/// <param name="WasMaximized">Estaba maximizada.</param>
/// <param name="WasMinimized">Estaba minimizada.</param>
/// <param name="CapturedAt">Momento de la captura (UTC).</param>
/// <param name="ShadowSuppressed">
/// Tiene barra de título propia (sin marco nativo, como VS Code con <c>titleBarStyle: custom</c>): mientras esté en el
/// grupo se le quita la sombra de DWM, que se vería sobre la barra lateral. Al restaurar se devuelve.
/// </param>
public sealed record WindowSnapshot(
    WindowIdentity Identity,
    WindowPlacementData Placement,
    PixelRect ActualBounds,
    bool WasVisible,
    bool WasMaximized,
    bool WasMinimized,
    DateTimeOffset CapturedAt,
    bool ShadowSuppressed = false);
