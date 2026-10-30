using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.App.Tests.Fakes;

internal sealed class InMemorySettingsStore(SettingsDocument? initial = null) : ISettingsStore
{
    public SettingsDocument Saved { get; private set; } = initial ?? SettingsDocument.CreateDefault(DateTimeOffset.UnixEpoch);

    public SettingsDocument Load() => Saved;

    public void Save(SettingsDocument settings) => Saved = settings;
}
