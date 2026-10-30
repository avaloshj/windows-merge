using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Services;

/// <summary>
/// Configuración vigente. Los cambios se guardan en el acto (no hay botón "Aplicar") y se notifican a quien los aplica:
/// tema, atajos, menú de la bandeja, intervalo de comprobación.
/// </summary>
public sealed class SettingsService
{
    private readonly ISettingsStore _store;
    private readonly TimeProvider _time;

    public SettingsService(ISettingsStore store, TimeProvider time)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        Current = Normalize(store.Load());
    }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public SettingsDocument Current { get; private set; }

    public void Update(Func<SettingsDocument, SettingsDocument> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var previous = Current;
        var next = Normalize(change(previous));
        if (next == previous)
            return;

        _store.Save(next);
        Current = next;
        Changed?.Invoke(this, new SettingsChangedEventArgs(previous, next));
    }

    public void UpdateUser(Func<UserSettings, UserSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Update(s => s with { User = change(s.User) });
    }

    /// <summary>Sustituye una regla (por <see cref="AppRule.Id"/>) y actualiza su fecha de modificación.</summary>
    public void UpdateRule(Guid ruleId, Func<AppRule, AppRule> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Update(s => s with
        {
            Rules = [.. s.Rules.Select(r => r.Id == ruleId ? change(r) with { UpdatedAt = _time.GetUtcNow() } : r)],
        });
    }

    public void AddRule(AppRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Update(s => s.Rules.Any(r => r.Id == rule.Id) ? s : s with { Rules = [.. s.Rules, rule] });
    }

    public void RemoveRule(Guid ruleId) => Update(s => s with { Rules = [.. s.Rules.Where(r => r.Id != ruleId)] });

    /// <summary>Sustituye el atajo de un comando.</summary>
    public void UpdateHotkey(HotkeyBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        Update(s => s with { Hotkeys = [.. s.Hotkeys.Select(h => h.Command == binding.Command ? binding : h)] });
    }

    /// <summary>Vuelve a los atajos por defecto.</summary>
    public void ResetHotkeys() => Update(s => s with { Hotkeys = HotkeyBinding.Defaults });

    /// <summary>Regla genérica para un ejecutable que el usuario elige de la lista de programas abiertos.</summary>
    public AppRule CreateRule(string displayName, string executableName) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = displayName,
        ExecutableName = executableName,
        TitleParser = TitleParserKind.Generic,
        Enabled = true,
        AutoMerge = true,
        MergeOnStartup = false,
        CreatedAt = _time.GetUtcNow(),
        UpdatedAt = _time.GetUtcNow(),
    };

    private static SettingsDocument Normalize(SettingsDocument settings) => settings with { User = settings.User.Normalize() };
}
