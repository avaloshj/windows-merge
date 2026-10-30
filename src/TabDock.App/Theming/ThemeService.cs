using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TabDock.Core.Domain;
using TabDock.Win32.Dwm;
using TabDock.Win32.Theming;

namespace TabDock.App.Theming;

/// <summary>
/// Compone los recursos de la aplicación (tokens, movimiento, tema, controles) y los mantiene al día con la
/// preferencia del sistema: tema claro/oscuro, alto contraste y "reducir animaciones".
/// </summary>
public sealed partial class ThemeService(ILogger<ThemeService> logger) : IDisposable
{
    private const string SidebarSurfaceKey = "Brush.SidebarSurface";
    private const string WindowSurfaceKey = "Brush.WindowSurface";
    private const string TabHeightKey = "Size.Tab.Height";
    private const double ComfortableTabHeight = 36;
    private const double CompactTabHeight = 28;

    private ResourceDictionary? _motion;
    private ResourceDictionary? _theme;
    private bool _initialized;

    public event EventHandler? ThemeChanged;

    public ThemePreference Preference { get; private set; }

    public bool PreferMica { get; private set; } = true;

    public bool IsDark { get; private set; }

    public bool IsHighContrast { get; private set; }

    public bool IsReducedMotion { get; private set; }

    public Density Density { get; private set; }

    /// <summary>Mica solo si el usuario la quiere, el sistema la soporta (22H2+) y no hay alto contraste.</summary>
    public bool UseMica => PreferMica && !IsHighContrast && DwmWindowStyler.SupportsSystemBackdrop;

    public void Initialize(ThemePreference preference, bool preferMica, Density density)
    {
        if (_initialized)
            throw new InvalidOperationException("ThemeService ya está inicializado.");

        _initialized = true;
        Preference = preference;
        PreferMica = preferMica;
        Density = density;

        var resources = Application.Current.Resources.MergedDictionaries;
        resources.Clear();
        resources.Add(Load("Tokens"));
        resources.Add(Load("Icons"));
        _motion = Load(MotionName());
        resources.Add(_motion);
        _theme = Load(ThemeName());
        resources.Add(_theme);

        // Controls.xaml usa StaticResource sobre los diccionarios anteriores: debe cargarse el último.
        resources.Add(Load("Controls"));

        UpdateState();
        ApplySurfaces();
        ApplyDensity();

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        LogThemeApplied(IsDark, IsHighContrast, UseMica, IsReducedMotion);
    }

    public void SetPreference(ThemePreference preference)
    {
        Preference = preference;
        Refresh();
    }

    /// <summary>Los marcos abiertos aplican el cambio al recibir <see cref="ThemeChanged"/>.</summary>
    public void SetMica(bool preferMica)
    {
        PreferMica = preferMica;
        Refresh();
    }

    /// <summary>
    /// Altura de pestaña 36 px (cómoda) o 28 px (compacta). El recurso está en el nivel superior de la aplicación, por
    /// encima de <c>Tokens.xaml</c>, y los estilos lo usan como <c>DynamicResource</c>: cambia también en marcos abiertos.
    /// </summary>
    public void SetDensity(Density density)
    {
        Density = density;
        ApplyDensity();
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
    }

    private static ResourceDictionary Load(string name) =>
        new() { Source = new Uri($"pack://application:,,,/Themes/{name}.xaml", UriKind.Absolute) };

    private string ThemeName() => IsHighContrastNow() ? "HighContrast" : IsDarkNow() ? "Dark" : "Light";

    private static string MotionName() => SystemParameters.ClientAreaAnimation ? "Motion" : "Motion.Reduced";

    private static bool IsHighContrastNow() => SystemParameters.HighContrast;

    private bool IsDarkNow() => Preference switch
    {
        ThemePreference.Dark => true,
        ThemePreference.Light => false,
        _ => !SystemTheme.AppsUseLightTheme(),
    };

    private void UpdateState()
    {
        IsHighContrast = IsHighContrastNow();
        IsDark = !IsHighContrast && IsDarkNow();
        IsReducedMotion = !SystemParameters.ClientAreaAnimation;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.Accessibility or UserPreferenceCategory.VisualStyle)
            _ = Application.Current?.Dispatcher.BeginInvoke(Refresh);
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SystemParameters.HighContrast) or nameof(SystemParameters.ClientAreaAnimation))
            _ = Application.Current?.Dispatcher.BeginInvoke(Refresh);
    }

    private void Refresh()
    {
        if (!_initialized)
            return;

        var wasDark = IsDark;
        var wasHighContrast = IsHighContrast;
        var wasReduced = IsReducedMotion;
        UpdateState();

        var resources = Application.Current.Resources.MergedDictionaries;
        if (wasDark != IsDark || wasHighContrast != IsHighContrast)
            _theme = Replace(resources, _theme, Load(ThemeName()));

        // Los storyboards de plantillas ya creadas conservan sus duraciones; el cambio afecta a lo que se cree después.
        if (wasReduced != IsReducedMotion)
            _motion = Replace(resources, _motion, Load(MotionName()));

        ApplySurfaces();
        LogThemeApplied(IsDark, IsHighContrast, UseMica, IsReducedMotion);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ResourceDictionary Replace(Collection<ResourceDictionary> resources, ResourceDictionary? current, ResourceDictionary replacement)
    {
        var index = current is null ? -1 : resources.IndexOf(current);
        if (index >= 0)
            resources[index] = replacement;
        else
            resources.Add(replacement);

        return replacement;
    }

    private void ApplyDensity() =>
        Application.Current.Resources[TabHeightKey] = Density == Density.Compact ? CompactTabHeight : ComfortableTabHeight;

    /// <summary>Con Mica, la ventana y la barra lateral son transparentes para que se vea el material del sistema.</summary>
    private void ApplySurfaces()
    {
        var resources = Application.Current.Resources;
        resources[SidebarSurfaceKey] = UseMica ? Brushes.Transparent : resources["Brush.BgSidebar"];
        resources[WindowSurfaceKey] = UseMica ? Brushes.Transparent : resources["Brush.BgWindow"];
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Tema aplicado: oscuro={IsDark}, altoContraste={IsHighContrast}, mica={UseMica}, animacionesReducidas={ReducedMotion}")]
    private partial void LogThemeApplied(bool isDark, bool isHighContrast, bool useMica, bool reducedMotion);
}
