namespace TabDock.Core.Abstractions;

/// <summary>Marco anfitrión visto desde el dominio: dónde está su hueco y cómo moverlo.</summary>
public interface IHostFrame
{
    event EventHandler? SlotBoundsChanged;

    /// <summary>El marco se minimizó, restauró o maximizó.</summary>
    event EventHandler? WindowStateChanged;

    /// <summary>El marco se activó (clic en la barra lateral, barra de tareas o Alt+Tab).</summary>
    event EventHandler<FrameActivatedEventArgs>? Activated;

    /// <summary>
    /// El marco cambió de posición en el orden Z: al activarlo con un clic, al arrastrarlo o al maximizarlo, Windows lo
    /// sube por encima de la ventana activa del grupo.
    /// </summary>
    event EventHandler? ZOrderChanged;

    WindowHandle Handle { get; }

    /// <summary>Hueco donde se coloca la ventana activa, en píxeles físicos de pantalla.</summary>
    PixelRect SlotBounds { get; }

    bool IsMinimized { get; }

    bool IsMaximized { get; }

    /// <summary>Mueve y redimensiona el marco para que su hueco ocupe <paramref name="slot"/>.</summary>
    void MoveSlotTo(PixelRect slot);

    /// <summary>Coloca el marco completo en <paramref name="bounds"/> (p. ej. la zona de Snap elegida por el usuario).</summary>
    void SetFrameBounds(PixelRect bounds);

    /// <summary>
    /// Abre o cierra el hueco del marco: abierto, la zona del hueco no pertenece al marco (se ve y se hace clic en la
    /// ventana activa, que está debajo); cerrado, el marco se ve entero (grupo sin ventana visible).
    /// </summary>
    void SetSlotHole(bool open);

    /// <summary>
    /// Barra propia del marco (título y botones de ventana). Visible: el hueco empieza debajo de ella y tapa la barra de
    /// título nativa de la ventana activa, que se coloca justo debajo. Oculta: la barra de la propia ventana (p. ej. la
    /// de VS Code con barra personalizada) hace de barra del grupo (ADR 0004, 0011).
    /// </summary>
    void SetOwnCaptionVisible(bool visible);

    void Maximize();

    void Minimize();

    /// <summary>Vuelve a estado normal (ni maximizado ni minimizado).</summary>
    void Restore();
}
