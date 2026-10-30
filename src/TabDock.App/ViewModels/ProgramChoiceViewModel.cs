using CommunityToolkit.Mvvm.ComponentModel;

namespace TabDock.App.ViewModels;

/// <summary>Un programa en el paso 1 de la introducción.</summary>
public sealed partial class ProgramChoiceViewModel(Guid ruleId, string displayName, bool isRunning) : ObservableObject
{
    public Guid RuleId { get; } = ruleId;

    public string DisplayName { get; } = displayName;

    /// <summary>Tiene ventanas abiertas ahora mismo: ayuda a elegir.</summary>
    public bool IsRunning { get; } = isRunning;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
