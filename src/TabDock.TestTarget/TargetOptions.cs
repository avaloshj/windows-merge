using System.Globalization;

namespace TabDock.TestTarget;

/// <summary>
/// Opciones de línea de comandos:
/// <c>--title "texto"</c>, <c>--size 800x600</c>, <c>--tool-window</c>, <c>--owner-dialog</c>, <c>--maximized</c>,
/// <c>--retitle-after &lt;ms&gt;</c> (añade " (renombrada)" al título pasado ese tiempo).
/// </summary>
internal sealed record TargetOptions(string Title, int Width, int Height, bool ToolWindow, bool OwnerDialog)
{
    public const string DefaultTitle = "proyecto-alfa - TabDock.TestTarget";

    public bool Maximized { get; init; }

    public int? RetitleAfterMilliseconds { get; init; }

    /// <summary>Deja de responder (bloquea su hilo de UI) pasado este tiempo.</summary>
    public int? HangAfterMilliseconds { get; init; }

    /// <summary>Sin marco nativo, con barra de título propia y sombra de DWM, como VS Code con <c>titleBarStyle: custom</c>.</summary>
    public bool CustomChrome { get; init; }

    /// <summary>Sin ningún marco nativo: el área de cliente ocupa toda la ventana (p. ej. WPF con <c>WindowChrome</c>).</summary>
    public bool Frameless { get; init; }

    /// <summary>Al ocultarse emite <c>EVENT_OBJECT_DESTROY</c> sin destruirse, como hace Chromium/VS Code.</summary>
    public bool DestroyEventOnHide { get; init; }

    /// <summary>Número de ventanas de primer nivel que abre este proceso (como VS Code: todas en un proceso).</summary>
    public int WindowCount { get; init; } = 1;

    /// <summary>Ráfaga: abre esta cantidad de ventanas, una cada <see cref="BurstIntervalMilliseconds"/>; cada una se cierra al segundo.</summary>
    public int BurstCount { get; init; }

    public int BurstIntervalMilliseconds { get; init; } = 100;

    /// <summary>Crea la ventana sin conciencia de DPI.</summary>
    public bool DpiUnaware { get; init; }

    /// <summary>Solo pruebas: muestra la vista previa DWM de esta ventana (HWND en decimal).</summary>
    public nint? ThumbnailOf { get; init; }

    public static TargetOptions Parse(IReadOnlyList<string> args)
    {
        var options = new TargetOptions(DefaultTitle, Width: 800, Height: 600, ToolWindow: false, OwnerDialog: false);
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--title" when i + 1 < args.Count:
                    options = options with { Title = args[++i] };
                    break;
                case "--size" when i + 1 < args.Count && TryParseSize(args[++i], out var width, out var height):
                    options = options with { Width = width, Height = height };
                    break;
                case "--tool-window":
                    options = options with { ToolWindow = true };
                    break;
                case "--owner-dialog":
                    options = options with { OwnerDialog = true };
                    break;
                case "--maximized":
                    options = options with { Maximized = true };
                    break;
                case "--thumbnail-of" when i + 1 < args.Count && long.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var hwnd):
                    options = options with { ThumbnailOf = (nint)hwnd };
                    break;
                case "--hang-after" when i + 1 < args.Count && int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var hang):
                    options = options with { HangAfterMilliseconds = hang };
                    break;
                case "--custom-chrome":
                    options = options with { CustomChrome = true };
                    break;
                case "--windows" when i + 1 < args.Count && int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= 1:
                    options = options with { WindowCount = count };
                    break;
                case "--burst" when i + 1 < args.Count && int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var burst):
                    options = options with { BurstCount = burst };
                    break;
                case "--burst-ms" when i + 1 < args.Count && int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var burstMs) && burstMs > 0:
                    options = options with { BurstIntervalMilliseconds = burstMs };
                    break;
                case "--destroy-event-on-hide":
                    options = options with { DestroyEventOnHide = true };
                    break;
                case "--frameless":
                    options = options with { Frameless = true };
                    break;
                case "--dpi-unaware":
                    options = options with { DpiUnaware = true };
                    break;
                case "--retitle-after" when i + 1 < args.Count && int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var ms):
                    options = options with { RetitleAfterMilliseconds = ms };
                    break;
            }
        }

        return options;
    }

    private static bool TryParseSize(string value, out int width, out int height)
    {
        width = height = 0;
        var parts = value.Split('x', 'X');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out width)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out height)
            && width > 0
            && height > 0;
    }
}
