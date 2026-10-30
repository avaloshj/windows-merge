using TabDock.Core.Domain;

namespace TabDock.Core.Abstractions;

/// <summary>Persistencia de <c>settings.json</c>.</summary>
public interface ISettingsStore
{
    /// <summary>Devuelve la configuración guardada o, si no existe o estaba corrupta, la de primer arranque (y la guarda).</summary>
    SettingsDocument Load();

    void Save(SettingsDocument settings);
}
