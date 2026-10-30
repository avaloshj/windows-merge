using TabDock.Core.Domain;

namespace TabDock.Core.Abstractions;

/// <summary>Persistencia de <c>state.json</c>. Síncrona: se llama justo antes de mover ventanas y desde el guardián.</summary>
public interface IRuntimeStateStore
{
    /// <summary>Devuelve el estado guardado, o <see cref="RuntimeState.Empty"/> si no existe o estaba corrupto.</summary>
    RuntimeState Load();

    /// <summary>Escribe de forma atómica: o queda el estado anterior completo o el nuevo completo.</summary>
    void Save(RuntimeState state);
}
