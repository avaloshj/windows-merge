namespace TabDock.Core.Domain;

/// <summary>Contenido de <c>preferences.json</c>.</summary>
public sealed record PreferencesDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public IReadOnlyList<TabPreference> Tabs { get; init; } = [];

    public static PreferencesDocument Empty { get; } = new();
}
