namespace TabDock.Core.Abstractions;

/// <summary>
/// Escritorios virtuales de Windows (<c>IVirtualDesktopManager</c>). La API pública solo permite mover ventanas del
/// propio proceso: TabDock mueve su marco, nunca las ventanas gestionadas.
/// </summary>
public interface IVirtualDesktops
{
    /// <summary>Escritorio de la ventana, o <see langword="null"/> si no se puede saber.</summary>
    Guid? GetDesktopId(WindowHandle handle);

    /// <summary>Mueve una ventana <b>propia</b> al escritorio indicado.</summary>
    bool MoveOwnWindowToDesktop(WindowHandle ownWindow, Guid desktopId);
}
