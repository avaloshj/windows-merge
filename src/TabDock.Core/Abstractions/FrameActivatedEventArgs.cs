namespace TabDock.Core.Abstractions;

/// <summary>El marco se activó.</summary>
/// <param name="forwardFocus">
/// <see langword="true"/> si no fue un clic dentro del marco (barra de tareas, Alt+Tab): el foco debe pasar a la ventana
/// activa del grupo, que es donde el usuario quiere escribir.
/// </param>
public sealed class FrameActivatedEventArgs(bool forwardFocus) : EventArgs
{
    public bool ForwardFocus { get; } = forwardFocus;
}
