namespace TabDock.App.Hosting;

/// <summary>
/// Garantiza una sola instancia por sesión de usuario mediante un mutex con nombre en el espacio <c>Local\</c>.
/// El reenvío de argumentos a la instancia existente llega en la Fase 7.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\TabDock.SingleInstance";

    private readonly Mutex _mutex;

    private SingleInstanceGuard(Mutex mutex) => _mutex = mutex;

    public static SingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
            return new SingleInstanceGuard(mutex);

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
