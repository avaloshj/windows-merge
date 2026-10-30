using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabDock.App.Localization;
using TabDock.App.Services;
using TabDock.Core.Domain;
using TabDock.Core.Services;

namespace TabDock.App.ViewModels;

/// <summary>
/// Introducción del primer arranque en 3 pasos: elegir programas → ajuste recomendado de VS Code → probar el atajo de
/// "Merge All Windows". Cerrarla o saltarla también la da por vista; se puede repetir desde Ajustes.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    public const int StepCount = 3;

    /// <summary>
    /// Opcional: barra de título de VS Code dibujada por VS Code (hace de barra del grupo, ADR 0004) y un título que
    /// TabDock interpreta sin ambigüedad. TabDock nunca modifica la configuración de VS Code.
    /// </summary>
    public const string VsCodeSnippet = """
        "window.titleBarStyle": "custom",
        "window.title": "${rootName}${separator}${activeEditorShort}${separator}${appName}"
        """;

    private readonly SettingsService _settings;

    public OnboardingViewModel(SettingsService settings, Func<IReadOnlyList<RunningProgram>> runningPrograms)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentNullException.ThrowIfNull(runningPrograms);

        var running = runningPrograms().Select(p => p.ExecutableName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in settings.Current.Rules)
        {
            var choice = new ProgramChoiceViewModel(rule.Id, rule.DisplayName, running.Contains(rule.ExecutableName)) { IsSelected = rule.Enabled };
            choice.PropertyChanged += OnChoiceChanged;
            Programs.Add(choice);
        }

        var merge = settings.Current.Hotkeys.FirstOrDefault(h => h.Command == HotkeyCommand.MergeAll && h.Enabled);
        MergeHotkeyText = merge is null ? TextResources.Get("Onboarding_NoHotkey") : HotkeyText.Format(merge);
        Step = 1;
    }

    /// <summary>Terminada o saltada: la ventana se cierra.</summary>
    public event EventHandler? Completed;

    public ObservableCollection<ProgramChoiceViewModel> Programs { get; } = [];

    public string MergeHotkeyText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1), nameof(IsStep2), nameof(IsStep3), nameof(StepText), nameof(IsLastStep))]
    [NotifyCanExecuteChangedFor(nameof(BackCommand), nameof(NextCommand))]
    public partial int Step { get; set; }

    /// <summary>Se agrupó al menos una vez durante el paso 3.</summary>
    [ObservableProperty]
    public partial bool HasMerged { get; set; }

    public bool IsStep1 => Step == 1;

    public bool IsStep2 => Step == 2;

    public bool IsStep3 => Step == 3;

    public bool IsLastStep => Step == StepCount;

    public string StepText => TextResources.Format("Onboarding_StepOf", Step, StepCount);

    public bool IsCompleted { get; private set; }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    public void Back() => Step--;

    /// <summary>Al salir del paso 1 se guardan los programas elegidos: el paso 3 ya agrupa con ellos.</summary>
    [RelayCommand(CanExecute = nameof(CanGoNext))]
    public void Next()
    {
        if (Step == 1)
            ApplyPrograms();

        if (Step < StepCount)
            Step++;
        else
            Finish();
    }

    [RelayCommand]
    public void Skip() => Complete();

    /// <summary>La ventana avisa cuando el coordinador crea un grupo.</summary>
    public void NotifyMerged()
    {
        if (IsStep3)
            HasMerged = true;
    }

    private bool CanGoBack() => Step > 1;

    private bool CanGoNext() => Step != 1 || Programs.Any(p => p.IsSelected);

    private void Finish()
    {
        ApplyPrograms();
        Complete();
    }

    private void Complete()
    {
        if (IsCompleted)
            return;

        IsCompleted = true;
        _settings.UpdateUser(u => u with { OnboardingCompleted = true });
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyPrograms()
    {
        var selected = Programs.ToDictionary(p => p.RuleId, p => p.IsSelected);
        _settings.Update(s => s with
        {
            Rules = [.. s.Rules.Select(r => selected.TryGetValue(r.Id, out var on) && on != r.Enabled ? r with { Enabled = on } : r)],
        });
    }

    private void OnChoiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ProgramChoiceViewModel.IsSelected), StringComparison.Ordinal))
            NextCommand.NotifyCanExecuteChanged();
    }
}
