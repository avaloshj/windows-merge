using TabDock.Core.Abstractions;
using TabDock.Core.Diagnostics;

namespace TabDock.Core.Domain;

/// <summary>Una ventana de otro proceso gestionada por un grupo. Las transiciones no válidas se rechazan.</summary>
public sealed class ManagedWindow
{
    private static readonly Dictionary<ManagedWindowState, ManagedWindowState[]> s_transitions = new()
    {
        [ManagedWindowState.Candidate] = [ManagedWindowState.Attaching],
        [ManagedWindowState.Attaching] = [ManagedWindowState.Attached, ManagedWindowState.Detached],
        [ManagedWindowState.Attached] = [ManagedWindowState.Unresponsive, ManagedWindowState.Closing, ManagedWindowState.Detaching, ManagedWindowState.Gone],
        [ManagedWindowState.Unresponsive] = [ManagedWindowState.Attached, ManagedWindowState.Closing, ManagedWindowState.Detaching, ManagedWindowState.Gone],
        [ManagedWindowState.Closing] = [ManagedWindowState.Attached, ManagedWindowState.Detaching, ManagedWindowState.Gone],
        [ManagedWindowState.Detaching] = [ManagedWindowState.Detached],
        [ManagedWindowState.Detached] = [],
        [ManagedWindowState.Gone] = [],
    };

    public ManagedWindow(WindowSnapshot snapshot, string title)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Id = Guid.NewGuid();
        Snapshot = snapshot;
        Title = title ?? string.Empty;
    }

    public Guid Id { get; }

    public WindowSnapshot Snapshot { get; }

    public WindowIdentity Identity => Snapshot.Identity;

    public WindowHandle Handle => Snapshot.Identity.Handle;

    /// <summary>Título completo de la ventana; puede contener datos personales y nunca se registra en claro.</summary>
    [SensitiveData(SensitiveDataKind.Text)]
    public string Title { get; set; }

    public ManagedWindowState State { get; private set; } = ManagedWindowState.Candidate;

    /// <summary>
    /// Visibilidad que TabDock ha pedido para esta ventana. Si Windows informa de un cambio distinto, lo ha provocado
    /// otro (p. ej. <c>code archivo.txt</c> muestra una ventana oculta) y se trata como petición de activación.
    /// </summary>
    public bool ExpectedVisible { get; set; }

    /// <summary>
    /// Alto (px) de la barra de título nativa: del borde visible superior al área de cliente. 0 si la ventana dibuja su
    /// propia barra (VS Code con <c>titleBarStyle: custom</c>); <see langword="null"/> hasta medirlo.
    /// </summary>
    public int? CaptionInset { get; set; }

    /// <summary>Gestionada y viva: participa en el grupo.</summary>
    public bool IsInGroup => State is ManagedWindowState.Attached or ManagedWindowState.Unresponsive or ManagedWindowState.Closing;

    public static bool CanTransition(ManagedWindowState from, ManagedWindowState to) => s_transitions[from].Contains(to);

    public Result MoveTo(ManagedWindowState next)
    {
        if (!CanTransition(State, next))
            return Result.Fail(new DomainError("window.invalidTransition", "Error_InvalidTransition"));

        State = next;
        return Result.Success();
    }
}
