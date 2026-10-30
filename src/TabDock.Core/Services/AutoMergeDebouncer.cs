using TabDock.Core.Abstractions;

namespace TabDock.Core.Services;

/// <summary>
/// Retrasa la evaluación de ventanas nuevas hasta que su título y tamaño se estabilizan: las aplicaciones crean la
/// ventana y le dan título antes de mostrarla. Cada evento de la misma ventana reinicia su espera.
/// </summary>
public sealed class AutoMergeDebouncer(TimeProvider time)
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(150);

    private readonly Dictionary<WindowHandle, long> _pending = [];

    public TimeSpan Delay { get; init; } = DefaultDelay;

    public bool HasPending => _pending.Count > 0;

    public void Notify(WindowHandle handle) => _pending[handle] = time.GetTimestamp();

    public void Forget(WindowHandle handle) => _pending.Remove(handle);

    /// <summary>Devuelve (y retira) las ventanas cuya espera ha terminado.</summary>
    public IReadOnlyList<WindowHandle> TakeDue()
    {
        var now = time.GetTimestamp();
        var due = _pending.Where(p => time.GetElapsedTime(p.Value, now) >= Delay).Select(p => p.Key).ToList();
        foreach (var handle in due)
            _pending.Remove(handle);

        return due;
    }
}
