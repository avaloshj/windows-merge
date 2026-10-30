using TabDock.Core.Domain;

namespace TabDock.Core.Abstractions;

/// <summary>Persistencia de <c>preferences.json</c>.</summary>
public interface IPreferencesStore
{
    /// <summary>Devuelve las preferencias guardadas o vacías si no existen o el archivo estaba corrupto.</summary>
    PreferencesDocument Load();

    void Save(PreferencesDocument preferences);
}
