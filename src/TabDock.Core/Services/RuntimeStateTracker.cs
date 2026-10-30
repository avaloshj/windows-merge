using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Services;

/// <summary>
/// Mantiene <c>state.json</c> al día con todas las ventanas capturadas por todos los grupos. Cada captura se persiste
/// antes de tocar la ventana: si la escritura falla, la captura se cancela.
/// </summary>
public sealed class RuntimeStateTracker(IRuntimeStateStore store, int ownerProcessId)
{
    private readonly IRuntimeStateStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly Dictionary<WindowHandle, CapturedWindowRecord> _records = [];

    public int Count => _records.Count;

    public IReadOnlyCollection<CapturedWindowRecord> Records => _records.Values;

    public Result Track(Guid groupId, WindowSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var handle = snapshot.Identity.Handle;
        _records[handle] = new CapturedWindowRecord(groupId, snapshot);
        var saved = Persist();
        if (!saved.IsSuccess)
            _records.Remove(handle);

        return saved;
    }

    public void Untrack(WindowHandle handle)
    {
        if (_records.Remove(handle))
            _ = Persist();
    }

    private Result Persist()
    {
        try
        {
            _store.Save(new RuntimeState
            {
                OwnerProcessId = ownerProcessId,
                CleanShutdown = _records.Count == 0,
                Windows = [.. _records.Values],
            });
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Fail(new DomainError("state.saveFailed", "Error_StateSaveFailed"));
        }
    }
}
