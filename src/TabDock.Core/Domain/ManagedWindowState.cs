namespace TabDock.Core.Domain;

/// <summary>Estados de una ventana gestionada (ver la máquina de estados en <c>docs/ARCHITECTURE.md</c>).</summary>
public enum ManagedWindowState
{
    Candidate,
    Attaching,
    Attached,
    Unresponsive,
    Closing,
    Detaching,
    Detached,
    Gone,
}
