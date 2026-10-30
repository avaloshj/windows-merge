using TabDock.App.Theming;
using TabDock.Core.Services;

namespace TabDock.App.Services;

/// <summary>
/// Aplica en caliente cada cambio guardado: atajos, menú de la bandeja, tema, Mica, densidad e intervalo de comprobación.
/// El idioma se aplica al reiniciar (los textos ya creados no se traducen en vivo).
/// </summary>
public sealed class SettingsApplier(
    SettingsService settings,
    HotkeyService hotkeys,
    TrayService tray,
    ThemeService theme,
    FrameCoordinator coordinator) : IDisposable
{
    private bool _started;

    public void Start()
    {
        if (_started)
            return;

        _started = true;
        settings.Changed += OnChanged;
    }

    public void Dispose()
    {
        if (_started)
            settings.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.HotkeysChanged)
            _ = hotkeys.Register(e.Current.Hotkeys);

        if (e.RulesChanged)
            tray.Rebuild(e.Current);

        var before = e.Previous.User;
        var after = e.Current.User;
        if (before.Theme != after.Theme)
            theme.SetPreference(after.Theme);

        if (before.UseMica != after.UseMica)
            theme.SetMica(after.UseMica);

        if (before.Density != after.Density)
            theme.SetDensity(after.Density);

        if (before.HealthCheckSeconds != after.HealthCheckSeconds)
            coordinator.SetHealthInterval(TimeSpan.FromSeconds(after.HealthCheckSeconds));
    }
}
