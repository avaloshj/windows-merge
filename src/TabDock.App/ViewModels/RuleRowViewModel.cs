using CommunityToolkit.Mvvm.ComponentModel;
using TabDock.Core.Domain;
using TabDock.Core.Services;

namespace TabDock.App.ViewModels;

/// <summary>Un programa en Ajustes → Programas. Cada cambio se guarda en el acto.</summary>
public sealed partial class RuleRowViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly bool _loading;

    public RuleRowViewModel(AppRule rule, SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        Id = rule.Id;
        DisplayName = rule.DisplayName;
        ExecutableName = rule.ExecutableName;

        _loading = true;
        Enabled = rule.Enabled;
        AutoMerge = rule.AutoMerge;
        MergeOnStartup = rule.MergeOnStartup;
        TitleParser = rule.TitleParser;
        _loading = false;
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public string ExecutableName { get; }

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    [ObservableProperty]
    public partial bool AutoMerge { get; set; }

    [ObservableProperty]
    public partial bool MergeOnStartup { get; set; }

    [ObservableProperty]
    public partial TitleParserKind TitleParser { get; set; }

    partial void OnEnabledChanged(bool value) => Save(r => r with { Enabled = value });

    partial void OnAutoMergeChanged(bool value) => Save(r => r with { AutoMerge = value });

    partial void OnMergeOnStartupChanged(bool value) => Save(r => r with { MergeOnStartup = value });

    partial void OnTitleParserChanged(TitleParserKind value) => Save(r => r with { TitleParser = value });

    private void Save(Func<AppRule, AppRule> change)
    {
        if (!_loading)
            _settings.UpdateRule(Id, change);
    }
}
