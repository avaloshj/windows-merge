namespace TabDock.Infrastructure;

/// <summary>Rutas de datos de TabDock. Por defecto, <c>%APPDATA%\TabDock</c>.</summary>
public sealed class AppPaths
{
    public AppPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = root;
    }

    public string Root { get; }

    public string Logs => Path.Combine(Root, "logs");

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string PreferencesFile => Path.Combine(Root, "preferences.json");

    public string StateFile => Path.Combine(Root, "state.json");

    public static AppPaths CreateDefault() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TabDock"));

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
    }
}
