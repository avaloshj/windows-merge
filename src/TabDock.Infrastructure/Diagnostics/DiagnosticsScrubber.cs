using System.Text.RegularExpressions;
using TabDock.Core.Diagnostics;

namespace TabDock.Infrastructure.Diagnostics;

/// <summary>
/// Segunda pasada sobre los logs antes de exportarlos. Ya se escriben enmascarados; esta pasada es una defensa
/// adicional por si algún texto se escapó: sustituye la carpeta de perfil, el nombre de usuario, el del equipo y el
/// dominio por marcadores.
/// </summary>
public sealed class DiagnosticsScrubber
{
    public const string MachineToken = "%COMPUTERNAME%";
    public const string DomainToken = "%USERDOMAIN%";

    /// <summary>Por debajo de esta longitud un nombre coincidiría con palabras corrientes y dejaría el texto ilegible.</summary>
    private const int MinNameLength = 3;

    private readonly MaskingContext _context;
    private readonly List<(Regex Pattern, string Token)> _names = [];

    public DiagnosticsScrubber(MaskingContext context, string? machineName, string? domainName)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        AddName(context.UserName, LogMask.UserNameToken);
        AddName(machineName, MachineToken);
        if (!string.Equals(domainName, machineName, StringComparison.OrdinalIgnoreCase))
            AddName(domainName, DomainToken);
    }

    public static DiagnosticsScrubber FromEnvironment() =>
        new(MaskingContext.FromEnvironment(), Environment.MachineName, Environment.UserDomainName);

    public string Scrub(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // En los logs JSON las barras van escapadas (\\): la ruta de perfil también aparece así. Va primero: después
        // el nombre de usuario ya estaría sustituido y la ruta completa no coincidiría.
        var result = text;
        if (_context.UserProfile is { Length: > 0 } profile)
        {
            var escaped = profile.TrimEnd('\\').Replace("\\", "\\\\", StringComparison.Ordinal);
            result = result.Replace(escaped, LogMask.UserProfileToken, StringComparison.OrdinalIgnoreCase);
        }

        result = LogMask.NormalizeUserPath(result, _context);

        foreach (var (pattern, token) in _names)
            result = pattern.Replace(result, token);

        return result;
    }

    private void AddName(string? name, string token)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length < MinNameLength)
            return;

        // Palabra completa: "ana" no debe borrar "mañana".
        var pattern = new Regex(
            @"(?<![\p{L}\p{N}_])" + Regex.Escape(name) + @"(?![\p{L}\p{N}_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        _names.Add((pattern, token));
    }
}
