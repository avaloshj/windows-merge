namespace TabDock.Core.Domain;

/// <summary>
/// Contenido de <c>settings.json</c>: preferencias generales, reglas y atajos. Esquema 1 (Fase 4): reglas y atajos;
/// esquema 2 (Fase 7): sección <see cref="User"/>. Los archivos antiguos se migran al cargar.
/// </summary>
public sealed record SettingsDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public UserSettings User { get; init; } = new();

    public IReadOnlyList<AppRule> Rules { get; init; } = [];

    public IReadOnlyList<HotkeyBinding> Hotkeys { get; init; } = [];

    /// <summary>Configuración del primer arranque: regla de VS Code activa y variantes desactivadas.</summary>
    public static SettingsDocument CreateDefault(DateTimeOffset now) => new()
    {
        Rules =
        [
            VsCodeRule("Visual Studio Code", "Code.exe", enabled: true, now),
            VsCodeRule("Visual Studio Code - Insiders", "Code - Insiders.exe", enabled: false, now),
            VsCodeRule("VSCodium", "VSCodium.exe", enabled: false, now),
            VsCodeRule("Cursor", "Cursor.exe", enabled: false, now),
        ],
        Hotkeys = HotkeyBinding.Defaults,
    };

    private static AppRule VsCodeRule(string name, string executable, bool enabled, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = name,
        ExecutableName = executable,
        WindowClassName = "Chrome_WidgetWin_1",
        TitleParser = TitleParserKind.VsCode,
        Enabled = enabled,
        AutoMerge = true,
        MergeOnStartup = false,
        CreatedAt = now,
        UpdatedAt = now,
    };
}
