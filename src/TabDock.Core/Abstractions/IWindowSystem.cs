using TabDock.Core.Domain;

namespace TabDock.Core.Abstractions;

/// <summary>
/// Puerto de acceso a las ventanas del sistema. Única vía por la que el dominio consulta o modifica ventanas.
/// Las operaciones nunca lanzan por fallos de Win32: devuelven <see cref="Result"/> con el código de error.
/// </summary>
public interface IWindowSystem
{
    /// <summary>Enumera todas las ventanas de primer nivel en orden Z (de arriba abajo).</summary>
    IReadOnlyList<WindowInfo> EnumerateTopLevel();

    Result<WindowInfo> GetInfo(WindowHandle handle);

    bool IsAlive(WindowHandle handle);

    bool IsHung(WindowHandle handle);

    /// <summary>
    /// Alto (px físicos) de la barra de título nativa: distancia del borde visible superior al área de cliente. 0 si la
    /// ventana dibuja su propia barra.
    /// </summary>
    int GetCaptionInset(WindowHandle handle);

    /// <summary>Indica si la ventana es visible ahora mismo (<c>IsWindowVisible</c>).</summary>
    bool IsVisible(WindowHandle handle);

    /// <summary>La ventana sigue existiendo y pertenece al mismo proceso (mismo PID y hora de inicio).</summary>
    bool Matches(WindowIdentity identity);

    Result<WindowSnapshot> CaptureSnapshot(WindowHandle handle);

    /// <summary>
    /// Sombra y marco que dibuja DWM (<c>DWMWA_NCRENDERING_POLICY</c>). Solo para ventanas con barra de título propia:
    /// en una con marco nativo, Windows dibujaría el marco clásico.
    /// </summary>
    Result SetFrameShadow(WindowHandle handle, bool visible);

    /// <summary>Indica si <paramref name="upper"/> está por encima de <paramref name="lower"/> en el orden Z.</summary>
    bool IsAbove(WindowHandle upper, WindowHandle lower);

    /// <summary>Devuelve la ventana a su estado original. Se niega si la identidad ya no coincide.</summary>
    Result Restore(WindowSnapshot snapshot);

    /// <summary>Muestra la ventana sin activarla (si estaba minimizada, la restaura).</summary>
    Result Show(WindowHandle handle);

    Result Hide(WindowHandle handle);

    /// <summary>
    /// Coloca la ventana para que su área visible coincida con <paramref name="slot"/> (compensa los bordes invisibles).
    /// Si estaba maximizada o minimizada, primero la devuelve a estado normal.
    /// </summary>
    Result PlaceInSlot(WindowHandle handle, PixelRect slot);

    /// <summary>
    /// Sitúa <paramref name="window"/> justo encima de <paramref name="below"/> en el orden Z, sin activarla. El marco va
    /// encima de la ventana activa y la deja ver por un hueco: así tapa su sombra (ADR 0009).
    /// </summary>
    Result PlaceAbove(WindowHandle window, WindowHandle below);

    /// <summary>Indica si la ventana es ahora mismo la de primer plano.</summary>
    bool IsForeground(WindowHandle handle);

    Result BringToForeground(WindowHandle handle);

    /// <summary>Área de trabajo (sin barra de tareas) del monitor donde está la ventana.</summary>
    PixelRect GetWorkArea(WindowHandle handle);

    /// <summary>
    /// Muestra u oculta el botón de la ventana en la barra de tareas (<c>ITaskbarList</c>). Explorer puede volver a
    /// añadirlo al mostrarse la ventana o cambiar su título, por eso se reaplica.
    /// </summary>
    Result SetTaskbarButton(WindowHandle handle, bool visible);

    /// <summary>Pide a la ventana que se cierre (<c>WM_CLOSE</c>); la aplicación puede preguntar o negarse.</summary>
    Result RequestClose(WindowHandle handle);
}
