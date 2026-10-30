using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Services;

/// <summary>
/// Preferencias de pestaña indexadas en memoria por (regla, proyecto). Cada cambio se guarda en el acto.
/// Purga en dos pasos: una preferencia no vista en 90 días se marca con <c>DeletedAt</c> y se elimina en el arranque
/// siguiente (así un arranque aislado tras unas vacaciones largas no borra nada de golpe).
/// </summary>
public sealed class TabPreferenceService(IPreferencesStore store, TimeProvider time)
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private readonly Dictionary<(Guid RuleId, string ProjectKey), TabPreference> _items = [];

    public int Count => _items.Count;

    public void Load()
    {
        _items.Clear();
        var now = time.GetUtcNow();
        var changed = false;
        foreach (var preference in store.Load().Tabs)
        {
            if (preference.DeletedAt is not null)
            {
                changed = true;
                continue;
            }

            var kept = now - preference.LastSeenAt > Retention ? preference with { DeletedAt = now } : preference;
            changed |= !ReferenceEquals(kept, preference);
            _items[(kept.RuleId, kept.ProjectKey)] = kept;
        }

        if (changed)
            Save();
    }

    /// <summary>Preferencias vigentes del proyecto, o <see langword="null"/> si no hay o está marcado para purga.</summary>
    public TabPreference? Find(Guid ruleId, string? projectName)
    {
        var key = ProjectKey.Normalize(projectName);
        return key.Length > 0 && _items.TryGetValue((ruleId, key), out var preference) && preference.DeletedAt is null ? preference : null;
    }

    /// <summary>Aplica un cambio (crea la preferencia si no existe) y marca el proyecto como visto.</summary>
    public TabPreference? Update(Guid ruleId, string? projectName, Func<TabPreference, TabPreference> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var key = ProjectKey.Normalize(projectName);
        if (key.Length == 0)
            return null;

        var now = time.GetUtcNow();
        var current = _items.TryGetValue((ruleId, key), out var existing)
            ? existing with { DeletedAt = null }
            : new TabPreference { RuleId = ruleId, ProjectKey = key, LastSeenAt = now };
        var updated = change(current) with { LastSeenAt = now };
        _items[(ruleId, key)] = updated;
        Save();
        return updated;
    }

    /// <summary>El proyecto sigue en uso: renueva su fecha (solo si ya tiene preferencias que conservar).</summary>
    public void Touch(Guid ruleId, string? projectName)
    {
        var key = ProjectKey.Normalize(projectName);
        if (key.Length > 0 && _items.TryGetValue((ruleId, key), out var existing))
        {
            _items[(ruleId, key)] = existing with { LastSeenAt = time.GetUtcNow(), DeletedAt = null };
            Save();
        }
    }

    /// <summary>Guarda la posición de cada proyecto del grupo según el orden visual actual.</summary>
    public void SetOrder(Guid ruleId, IReadOnlyList<string> projectNamesInOrder)
    {
        ArgumentNullException.ThrowIfNull(projectNamesInOrder);

        var now = time.GetUtcNow();
        for (var i = 0; i < projectNamesInOrder.Count; i++)
        {
            var key = ProjectKey.Normalize(projectNamesInOrder[i]);
            if (key.Length == 0)
                continue;

            var current = _items.TryGetValue((ruleId, key), out var existing)
                ? existing
                : new TabPreference { RuleId = ruleId, ProjectKey = key, LastSeenAt = now };
            _items[(ruleId, key)] = current with { Order = i, LastSeenAt = now, DeletedAt = null };
        }

        Save();
    }

    private void Save() => store.Save(new PreferencesDocument { Tabs = [.. _items.Values.Where(p => !p.IsEmpty || p.DeletedAt is not null)] });
}
