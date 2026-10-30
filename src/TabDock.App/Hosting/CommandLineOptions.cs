using System.Globalization;
using TabDock.Core.Domain;

namespace TabDock.App.Hosting;

/// <summary>
/// Argumentos admitidos: <c>--lang es|en</c>, <c>--theme system|light|dark</c>, <c>--no-mica</c>, <c>--settings</c>,
/// <c>--onboarding</c>, <c>--export-diagnostics &lt;ruta.zip&gt;</c>, <c>--demo-frame</c>,
/// <c>--merge-exe &lt;nombre.exe&gt;</c>, <c>--diagnostics</c>, <c>--verbose</c> y, de uso interno,
/// <c>--guardian &lt;pid&gt; &lt;inicio&gt;</c>.
/// Idioma, tema y Mica se guardan en <c>settings.json</c>; estos argumentos solo los sustituyen en esta ejecución.
/// </summary>
internal sealed record CommandLineOptions(bool Verbose)
{
    private static readonly string[] s_supportedLanguages = ["es", "en"];

    /// <summary><see langword="null"/>: el de <c>settings.json</c>.</summary>
    public string? Language { get; init; }

    /// <summary><see langword="null"/>: el de <c>settings.json</c>.</summary>
    public ThemePreference? Theme { get; init; }

    /// <summary><see langword="false"/> con <c>--no-mica</c>; <see langword="null"/>: el de <c>settings.json</c>.</summary>
    public bool? UseMica { get; init; }

    /// <summary>Abre el marco anfitrión con pestañas ficticias en lugar de la ventana de diagnóstico.</summary>
    public bool DemoFrame { get; init; }

    /// <summary>Abre la ventana de diagnóstico al arrancar.</summary>
    public bool Diagnostics { get; init; }

    /// <summary>Abre la ventana de ajustes al arrancar.</summary>
    public bool Settings { get; init; }

    /// <summary>Muestra la introducción aunque ya se haya completado.</summary>
    public bool Onboarding { get; init; }

    /// <summary>Exporta el diagnóstico anonimizado a esta ruta y termina, sin agrupar nada.</summary>
    public string? ExportDiagnosticsTo { get; init; }

    /// <summary>
    /// Modo de prueba: agrupa al arrancar las ventanas elegibles de este ejecutable y termina cuando se cierra el último
    /// marco (lo usan los scripts de extremo a extremo). El uso normal son las reglas y Ctrl+Alt+M.
    /// </summary>
    public string? MergeExecutable { get; init; }

    /// <summary>Modo guardián: PID y hora de inicio (ticks UTC) del proceso principal a vigilar.</summary>
    public (int ProcessId, long StartTicks)? GuardianOf { get; init; }

    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = new CommandLineOptions(Verbose: false);
        for (var i = 0; i < args.Count; i++)
        {
            var hasValue = i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            switch (args[i].ToLowerInvariant())
            {
                case "--verbose":
                    options = options with { Verbose = true };
                    break;
                case "--no-mica":
                    options = options with { UseMica = false };
                    break;
                case "--demo-frame":
                    options = options with { DemoFrame = true };
                    break;
                case "--diagnostics":
                    options = options with { Diagnostics = true };
                    break;
                case "--settings":
                    options = options with { Settings = true };
                    break;
                case "--onboarding":
                    options = options with { Onboarding = true };
                    break;
                case "--lang" when hasValue && s_supportedLanguages.Contains(args[i + 1], StringComparer.OrdinalIgnoreCase):
                    options = options with { Language = args[++i].ToLowerInvariant() };
                    break;
                case "--theme" when hasValue && Enum.TryParse<ThemePreference>(args[i + 1], ignoreCase: true, out var theme) && Enum.IsDefined(theme):
                    options = options with { Theme = theme };
                    i++;
                    break;
                case "--export-diagnostics" when hasValue:
                    options = options with { ExportDiagnosticsTo = args[++i] };
                    break;
                case "--merge-exe" when hasValue:
                    options = options with { MergeExecutable = args[++i] };
                    break;
                case Guardian.Argument when i + 2 < args.Count
                    && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                    && long.TryParse(args[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks):
                    options = options with { GuardianOf = (pid, ticks) };
                    i += 2;
                    break;
            }
        }

        return options;
    }
}
