using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Tests.Fakes;

/// <summary>Configuración en memoria; cuenta las escrituras.</summary>
internal sealed class FakeSettingsStore(SettingsDocument? initial = null) : ISettingsStore
{
    public SettingsDocument Saved { get; private set; } = initial ?? SettingsDocument.CreateDefault(DateTimeOffset.UnixEpoch);

    public int SaveCount { get; private set; }

    public SettingsDocument Load() => Saved;

    public void Save(SettingsDocument settings)
    {
        Saved = settings;
        SaveCount++;
    }
}
