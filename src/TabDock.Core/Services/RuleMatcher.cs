using System.Text.RegularExpressions;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Services;

/// <summary>
/// Asocia ventanas a reglas por <b>ruta del ejecutable</b> (todas las ventanas de VS Code pertenecen al mismo proceso de
/// Electron, así que el PID no sirve). Solo se consideran reglas activas; gana la primera que coincide.
/// </summary>
public sealed class RuleMatcher
{
    private static readonly TimeSpan s_regexTimeout = TimeSpan.FromMilliseconds(100);

    private readonly IReadOnlyList<(AppRule Rule, Regex? Path)> _rules;

    public RuleMatcher(IEnumerable<AppRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = [.. rules.Where(r => r.Enabled).Select(r => (r, ToRegex(r.ExecutablePathPattern)))];
    }

    public IEnumerable<AppRule> EnabledRules => _rules.Select(r => r.Rule);

    public AppRule? Match(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        foreach (var (rule, path) in _rules)
        {
            if (Matches(rule, path, window))
                return rule;
        }

        return null;
    }

    private static bool Matches(AppRule rule, Regex? path, WindowInfo window) =>
        string.Equals(window.ExecutableName, rule.ExecutableName, StringComparison.OrdinalIgnoreCase)
        && (path is null || path.IsMatch(window.ExecutablePath))
        && (string.IsNullOrEmpty(rule.WindowClassName) || string.Equals(window.ClassName, rule.WindowClassName, StringComparison.Ordinal));

    /// <summary>Comodines <c>*</c> y <c>?</c>, sin distinguir mayúsculas; el resto se toma literal.</summary>
    private static Regex? ToRegex(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return null;

        var escaped = Regex.Escape(pattern.Trim()).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal);
        return new Regex("^" + escaped + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, s_regexTimeout);
    }
}
