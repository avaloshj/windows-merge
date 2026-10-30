using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Tests.Fakes;

internal sealed class FakeStateStore(List<string> journal) : IRuntimeStateStore
{
    public RuntimeState Current { get; set; } = RuntimeState.Empty;

    public bool FailSave { get; set; }

    public int Saves { get; private set; }

    public RuntimeState Load() => Current;

    public void Save(RuntimeState state)
    {
        if (FailSave)
            throw new IOException("Disco ficticio lleno");

        Saves++;
        Current = state;
        journal.Add($"save:{state.Windows.Count}");
    }
}
