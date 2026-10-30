using System.Text.RegularExpressions;
using TabDock.Core.Domain;

namespace TabDock.Core.TitleParsing;

/// <summary>
/// Interpreta el título de VS Code (y de Insiders, VSCodium y Cursor). VS Code omite las variables vacías junto con su
/// separador, así que el número de segmentos varía:
/// <list type="bullet">
/// <item><c>Aplicación</c> → ventana vacía.</item>
/// <item><c>X - Aplicación</c> → X es la carpeta raíz, salvo que parezca un archivo (ventana sin carpeta con un archivo).</item>
/// <item><c>archivo - raíz - Aplicación</c> y <c>archivo - raíz - perfil - Aplicación</c>.</item>
/// </list>
/// Un segmento con marca de entorno remoto (<c>[SSH: …]</c>, <c>[WSL: …]</c>, <c>[Dev Container: …]</c>) se toma
/// siempre como la raíz. Con la plantilla por defecto hay casos ambiguos (perfil sin archivo abierto); la plantilla
/// recomendada <see cref="VsCodeTitleTemplate.RootFirst"/> los evita.
/// </summary>
public sealed partial class VsCodeTitleParser : ITitleParser
{
    /// <summary>Valor por defecto de <c>window.titleSeparator</c>.</summary>
    public const string DefaultSeparator = " - ";

    private const char DirtyIndicator = '●';

    /// <summary>Nombres de aplicación conocidos, del más largo al más corto (Insiders contiene " - ").</summary>
    private static readonly string[] s_appNames =
    [
        "Visual Studio Code - Insiders",
        "Visual Studio Code",
        "Code - OSS",
        "VSCodium - Insiders",
        "VSCodium",
        "Cursor",
    ];

    private readonly VsCodeTitleTemplate _template;
    private readonly string _separator;

    public VsCodeTitleParser(VsCodeTitleTemplate template = VsCodeTitleTemplate.Default, string separator = DefaultSeparator)
    {
        ArgumentException.ThrowIfNullOrEmpty(separator);
        _template = template;
        _separator = separator;
    }

    public ParsedTitle Parse(string? rawTitle)
    {
        var title = rawTitle?.Trim() ?? string.Empty;

        var isDirty = title.StartsWith(DirtyIndicator);
        if (isDirty)
            title = title[1..].TrimStart();

        // Marcas como [Administrador] van tras el nombre de la aplicación: se quitan antes y después de buscarlo.
        title = TrailingTag().Replace(title, string.Empty).Trim();
        title = TrailingTag().Replace(StripAppName(title), string.Empty).Trim();
        if (title.Length == 0)
            return new ParsedTitle { IsDirty = isDirty };

        var segments = title.Split(_separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var (root, file, profile) = _template == VsCodeTitleTemplate.RootFirst ? SplitRootFirst(segments) : SplitDefault(segments);

        var (project, remoteKind, remoteLabel) = ExtractRemote(root);
        return new ParsedTitle
        {
            ProjectName = project,
            ActiveFile = file,
            IsDirty = isDirty,
            RemoteKind = remoteKind,
            RemoteLabel = remoteLabel,
            ProfileName = profile,
        };
    }

    private string StripAppName(string title)
    {
        foreach (var app in s_appNames)
        {
            if (string.Equals(title, app, StringComparison.Ordinal))
                return string.Empty;

            if (title.EndsWith(_separator + app, StringComparison.Ordinal))
                return title[..^(_separator.Length + app.Length)];
        }

        // Aplicación desconocida (p. ej. un derivado no listado): el último segmento es su nombre.
        var last = title.LastIndexOf(_separator, StringComparison.Ordinal);
        return last > 0 ? title[..last] : title;
    }

    private (string Root, string? File, string? Profile) SplitDefault(string[] segments)
    {
        var remoteIndex = Array.FindIndex(segments, s => RemoteMarker().IsMatch(s));
        if (remoteIndex >= 0)
        {
            return (
                segments[remoteIndex],
                remoteIndex > 0 ? Join(segments[..remoteIndex]) : null,
                remoteIndex < segments.Length - 1 ? Join(segments[(remoteIndex + 1)..]) : null);
        }

        return segments.Length switch
        {
            1 when LooksLikeFile(segments[0]) => (string.Empty, segments[0], null),
            1 => (segments[0], null, null),
            2 => (segments[1], segments[0], null),

            // "informe - final.md - proyecto": el archivo contenía el separador; no hay perfil.
            3 when LooksLikeFile(segments[1]) && !LooksLikeFile(segments[0]) => (segments[2], Join(segments[..2]), null),
            3 => (segments[1], segments[0], segments[2]),

            // Más segmentos: el nombre del archivo contenía el separador; la raíz es el último.
            _ => (segments[^1], Join(segments[..^1]), null),
        };
    }

    private (string Root, string? File, string? Profile) SplitRootFirst(string[] segments) => segments.Length switch
    {
        1 => (segments[0], null, null),
        _ => (segments[0], Join(segments[1..]), null),
    };

    private string Join(string[] segments) => string.Join(_separator, segments);

    private static (string Project, RemoteKind Kind, string? Label) ExtractRemote(string root)
    {
        var match = RemoteMarker().Match(root);
        if (!match.Success)
            return (root, RemoteKind.None, null);

        var kind = match.Groups["kind"].Value switch
        {
            "SSH" => RemoteKind.Ssh,
            "WSL" => RemoteKind.Wsl,
            "Dev Container" or "Container" => RemoteKind.DevContainer,
            _ => RemoteKind.Other,
        };
        var project = root.Remove(match.Index, match.Length).Trim();
        return (project, kind, match.Groups["label"].Value.Trim());
    }

    /// <summary>
    /// Un nombre con extensión corta (<c>index.ts</c>, <c>README.md</c>) o un editor sin guardar (<c>Untitled-1</c>,
    /// <c>Sin título-1</c>) se trata como archivo.
    /// </summary>
    private static bool LooksLikeFile(string segment) => FileName().IsMatch(segment) || UntitledEditor().IsMatch(segment);

    [GeneratedRegex(@"\[(?<kind>SSH|WSL|Dev Container|Container|Codespaces|Tunnel)\s*:\s*(?<label>[^\]]+)\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex RemoteMarker();

    /// <summary>Marcas finales que no son entorno remoto: <c>[Administrador]</c>, <c>[Unsupported]</c>, <c>[Modo restringido]</c>…</summary>
    [GeneratedRegex(@"(\s*\[(?!(SSH|WSL|Dev Container|Container|Codespaces|Tunnel)\s*:)[^\]]*\])+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex TrailingTag();

    [GeneratedRegex(@"^(Untitled|Sin título|Sin titulo)-\d+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex UntitledEditor();

    [GeneratedRegex(@"^[^\\/:*?""<>|\s][^\\/:*?""<>|]*\.[A-Za-z0-9]{1,8}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex FileName();
}
