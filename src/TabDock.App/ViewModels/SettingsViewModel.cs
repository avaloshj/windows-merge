using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabDock.App.Localization;
using TabDock.App.Services;
using TabDock.Core;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;
using TabDock.Core.Services;

namespace TabDock.App.ViewModels;

/// <summary>
/// Ventana de ajustes: General, Programas, Apariencia, Atajos, Avanzado y Diagnóstico. No hay botón "Aplicar": cada
/// cambio se guarda al momento y <see cref="SettingsApplier"/> lo aplica.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IStartupRegistration _startup;
    private readonly IHotkeyControl _hotkeys;
    private readonly Func<IReadOnlyList<RunningProgram>> _runningPrograms;
    private readonly Func<string, Result> _exportDiagnostics;
    private readonly string _languageAtStart;
    private readonly bool _lowMemoryAtStart;
    private bool _loading;

    public SettingsViewModel(
        SettingsService settings,
        IStartupRegistration startup,
        IHotkeyControl hotkeys,
        Func<IReadOnlyList<RunningProgram>> runningPrograms,
        Func<string, Result> exportDiagnostics)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _startup = startup ?? throw new ArgumentNullException(nameof(startup));
        _hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
        _runningPrograms = runningPrograms ?? throw new ArgumentNullException(nameof(runningPrograms));
        _exportDiagnostics = exportDiagnostics ?? throw new ArgumentNullException(nameof(exportDiagnostics));

        var user = settings.Current.User;
        _languageAtStart = user.Language;
        _lowMemoryAtStart = user.LowMemoryRendering;

        _loading = true;
        Language = user.Language;
        StartWithWindows = startup.IsEnabled();
        Theme = user.Theme;
        UseMica = user.UseMica;
        Density = user.Density;
        HideMode = user.HideMode;
        HealthCheckSeconds = user.HealthCheckSeconds;
        LowMemoryRendering = user.LowMemoryRendering;
        _loading = false;

        ReloadRules();
        ReloadHotkeys();
    }

    /// <summary>La introducción se puede volver a ver desde General.</summary>
    public event EventHandler? OnboardingRequested;

    public IReadOnlyList<OptionItem<string>> Languages { get; } =
    [
        new("es", "Español"),
        new("en", "English"),
    ];

    public IReadOnlyList<OptionItem<ThemePreference>> Themes { get; } =
    [
        new(ThemePreference.System, TextResources.Get("Settings_Theme_System")),
        new(ThemePreference.Light, TextResources.Get("Settings_Theme_Light")),
        new(ThemePreference.Dark, TextResources.Get("Settings_Theme_Dark")),
    ];

    public IReadOnlyList<OptionItem<Density>> Densities { get; } =
    [
        new(Density.Comfortable, TextResources.Get("Settings_Density_Comfortable")),
        new(Density.Compact, TextResources.Get("Settings_Density_Compact")),
    ];

    /// <summary>Solo "Ocultar" está implementado; "Solo barra de tareas" se muestra desactivado (ADR 0002).</summary>
    public IReadOnlyList<OptionItem<HideMode>> HideModes { get; } =
    [
        new(HideMode.Hide, TextResources.Get("Settings_HideMode_Hide")),
        new(HideMode.TaskbarOnly, TextResources.Get("Settings_HideMode_TaskbarOnly"), IsEnabled: false),
    ];

    public IReadOnlyList<OptionItem<TitleParserKind>> TitleParsers { get; } =
    [
        new(TitleParserKind.VsCode, TextResources.Get("Settings_Parser_VsCode")),
        new(TitleParserKind.Generic, TextResources.Get("Settings_Parser_Generic")),
    ];

    public ObservableCollection<RuleRowViewModel> Rules { get; } = [];

    public ObservableCollection<RunningProgram> AvailablePrograms { get; } = [];

    public ObservableCollection<HotkeyRowViewModel> Hotkeys { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LanguageNeedsRestart))]
    public partial string Language { get; set; }

    public bool LanguageNeedsRestart => !string.Equals(Language, _languageAtStart, StringComparison.Ordinal);

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial ThemePreference Theme { get; set; }

    [ObservableProperty]
    public partial bool UseMica { get; set; }

    [ObservableProperty]
    public partial Density Density { get; set; }

    [ObservableProperty]
    public partial HideMode HideMode { get; set; }

    [ObservableProperty]
    public partial int HealthCheckSeconds { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RenderingNeedsRestart))]
    public partial bool LowMemoryRendering { get; set; }

    public bool RenderingNeedsRestart => LowMemoryRendering != _lowMemoryAtStart;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddProgramCommand))]
    public partial RunningProgram? SelectedProgram { get; set; }

    /// <summary>Mensaje de error general (p. ej. no se pudo escribir en el registro); <see langword="null"/> si no hay.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Resultado de la última exportación del diagnóstico.</summary>
    [ObservableProperty]
    public partial string? DiagnosticsStatus { get; set; }

    /// <summary>Programas con ventanas abiertas que todavía no tienen regla.</summary>
    [RelayCommand]
    public void RefreshPrograms()
    {
        var known = _settings.Current.Rules.Select(r => r.ExecutableName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        AvailablePrograms.Clear();
        foreach (var program in _runningPrograms().Where(p => !known.Contains(p.ExecutableName)).OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            AvailablePrograms.Add(program);

        SelectedProgram = AvailablePrograms.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanAddProgram))]
    public void AddProgram()
    {
        if (SelectedProgram is not { } program)
            return;

        _settings.AddRule(_settings.CreateRule(program.DisplayName, program.ExecutableName));
        ReloadRules();
        RefreshPrograms();
    }

    [RelayCommand]
    public void RemoveRule(RuleRowViewModel? row)
    {
        if (row is null)
            return;

        _settings.RemoveRule(row.Id);
        ReloadRules();
        RefreshPrograms();
    }

    /// <summary>La ventana llama a esto al empezar a escribir un atajo: los globales se liberan mientras tanto.</summary>
    public void BeginCapture(HotkeyRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        foreach (var other in Hotkeys)
            other.IsCapturing = ReferenceEquals(other, row);

        _hotkeys.SuspendAll();
    }

    public void EndCapture()
    {
        foreach (var row in Hotkeys)
            row.IsCapturing = false;

        _hotkeys.ResumeAll();
        ShowConflicts();
    }

    /// <summary>Asigna la combinación pulsada si es válida; si no, deja el mensaje en la fila.</summary>
    public bool AssignHotkey(HotkeyRowViewModel row, HotkeyModifiers modifiers, int virtualKey)
    {
        ArgumentNullException.ThrowIfNull(row);

        var candidate = row.Binding with { Modifiers = modifiers, VirtualKey = virtualKey, Enabled = true };
        var validation = HotkeyValidator.Validate(candidate, _settings.Current.Hotkeys);
        if (!validation.IsValid)
        {
            row.Message = validation.ConflictsWith is { } other
                ? TextResources.Format(validation.ErrorKey!, HotkeyRowViewModel.LabelFor(other))
                : TextResources.Get(validation.ErrorKey!);
            return false;
        }

        _settings.UpdateHotkey(candidate);
        row.Binding = candidate;
        row.Message = null;
        return true;
    }

    [RelayCommand]
    public void ResetHotkeys()
    {
        _settings.ResetHotkeys();
        ReloadHotkeys();
    }

    /// <summary>Exporta el diagnóstico a la ruta elegida en el diálogo de guardar.</summary>
    public bool ExportDiagnostics(string path)
    {
        var result = _exportDiagnostics(path);
        DiagnosticsStatus = result.IsSuccess
            ? TextResources.Format("Settings_Diagnostics_Exported", System.IO.Path.GetFileName(path))
            : TextResources.Get(result.Error.MessageKey);
        return result.IsSuccess;
    }

    [RelayCommand]
    public void ShowOnboarding() => OnboardingRequested?.Invoke(this, EventArgs.Empty);

    partial void OnLanguageChanged(string value) => SaveUser(u => u with { Language = value });

    partial void OnThemeChanged(ThemePreference value) => SaveUser(u => u with { Theme = value });

    partial void OnUseMicaChanged(bool value) => SaveUser(u => u with { UseMica = value });

    partial void OnDensityChanged(Density value) => SaveUser(u => u with { Density = value });

    partial void OnHealthCheckSecondsChanged(int value) => SaveUser(u => u with { HealthCheckSeconds = value });

    partial void OnLowMemoryRenderingChanged(bool value) => SaveUser(u => u with { LowMemoryRendering = value });

    partial void OnHideModeChanged(HideMode value)
    {
        // Mientras taskbarOnly no exista, la lista no deja elegirlo; si llegara, se vuelve a "Ocultar".
        if (value != HideMode.Hide && !_loading)
            HideMode = HideMode.Hide;
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
            return;

        var result = _startup.SetEnabled(value);
        if (!result.IsSuccess)
        {
            ErrorMessage = TextResources.Get(result.Error.MessageKey);
            _loading = true;
            StartWithWindows = !value;
            _loading = false;
            return;
        }

        ErrorMessage = null;
        SaveUser(u => u with { StartWithWindows = value });
    }

    private bool CanAddProgram() => SelectedProgram is not null;

    private void SaveUser(Func<UserSettings, UserSettings> change)
    {
        if (!_loading)
            _settings.UpdateUser(change);
    }

    private void ReloadRules()
    {
        Rules.Clear();
        foreach (var rule in _settings.Current.Rules)
            Rules.Add(new RuleRowViewModel(rule, _settings));
    }

    private void ReloadHotkeys()
    {
        foreach (var row in Hotkeys)
            row.PropertyChanged -= OnHotkeyRowChanged;

        Hotkeys.Clear();
        foreach (var binding in _settings.Current.Hotkeys)
        {
            var row = new HotkeyRowViewModel(binding);
            row.PropertyChanged += OnHotkeyRowChanged;
            Hotkeys.Add(row);
        }

        ShowConflicts();
    }

    private void OnHotkeyRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not HotkeyRowViewModel row || !string.Equals(e.PropertyName, nameof(HotkeyRowViewModel.IsActive), StringComparison.Ordinal) || row.IsActive == row.Binding.Enabled)
            return;

        var updated = row.Binding with { Enabled = row.IsActive };
        _settings.UpdateHotkey(updated);
        row.Binding = updated;
        ShowConflicts();
    }

    /// <summary>Marca los atajos que otro programa ya usa (lo sabe el registro real en <see cref="IHotkeyControl"/>).</summary>
    private void ShowConflicts()
    {
        var conflicts = _hotkeys.Conflicts.Select(c => c.Command).ToHashSet();
        foreach (var row in Hotkeys)
        {
            if (conflicts.Contains(row.Command))
                row.Message = TextResources.Get("Hotkey_Error_InUse");
            else if (string.Equals(row.Message, TextResources.Get("Hotkey_Error_InUse"), StringComparison.Ordinal))
                row.Message = null;
        }
    }
}
