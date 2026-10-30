namespace TabDock.Core.Abstractions;

/// <summary>
/// Flujo de eventos de ventana. El callback del sistema solo encola; los eventos se consumen de forma secuencial
/// fuera del callback, en el hilo de UI.
/// </summary>
public interface IWindowEvents : IDisposable
{
    /// <summary>Instala los hooks. Debe llamarse desde un hilo con bucle de mensajes (el de UI).</summary>
    void Start();

    IAsyncEnumerable<WindowEvent> ReadAllAsync(CancellationToken cancellationToken);
}
