namespace TabDock.Core.Domain;

/// <summary>Preferencias generales del usuario (sección <c>user</c> de <c>settings.json</c>, esquema 2).</summary>
public sealed record UserSettings
{
    public const int MinHealthCheckSeconds = 1;
    public const int MaxHealthCheckSeconds = 10;

    public static IReadOnlyList<string> SupportedLanguages { get; } = ["es", "en"];

    public ThemePreference Theme { get; init; } = ThemePreference.System;

    public bool UseMica { get; init; } = true;

    public Density Density { get; init; } = Density.Comfortable;

    public HideMode HideMode { get; init; } = HideMode.Hide;

    /// <summary>Intervalo de la comprobación periódica (ventanas cerradas sin evento, colgadas, barra de tareas).</summary>
    public int HealthCheckSeconds { get; init; } = 2;

    /// <summary>Reflejo del valor del registro; la fuente de verdad es la clave <c>Run</c> del usuario.</summary>
    public bool StartWithWindows { get; init; }

    public string Language { get; init; } = "es";

    public bool OnboardingCompleted { get; init; }

    /// <summary>
    /// Dibujo por software (sin aceleración gráfica): unos 100 MB menos de memoria con el marco abierto, a cambio de más
    /// CPU en las animaciones y sin Mica. Se aplica al reiniciar TabDock.
    /// </summary>
    public bool LowMemoryRendering { get; init; }

    /// <summary>Corrige valores fuera de rango de un archivo editado a mano en lugar de rechazarlo entero.</summary>
    public UserSettings Normalize() => this with
    {
        HealthCheckSeconds = Math.Clamp(HealthCheckSeconds, MinHealthCheckSeconds, MaxHealthCheckSeconds),
        Language = SupportedLanguages.FirstOrDefault(l => string.Equals(l, Language, StringComparison.OrdinalIgnoreCase)) ?? "es",
        Theme = Enum.IsDefined(Theme) ? Theme : ThemePreference.System,
        Density = Enum.IsDefined(Density) ? Density : Density.Comfortable,

        // taskbarOnly todavía no existe: un archivo que lo pida usa el modo implementado.
        HideMode = HideMode.Hide,
    };
}
