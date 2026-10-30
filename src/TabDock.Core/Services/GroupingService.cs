using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Services;

/// <summary>Decide qué ventanas se agrupan: "Merge All Windows" de una regla y agrupado automático de ventanas nuevas.</summary>
public sealed class GroupingService(IWindowSystem windows, EligibilityPolicy eligibility, Func<RuleMatcher> rules)
{
    /// <summary>Ventanas elegibles de la regla, en orden Z (la de primer plano primero).</summary>
    public IReadOnlyList<WindowInfo> FindMergeCandidates(AppRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var matcher = rules();
        return [.. windows.EnumerateTopLevel().Where(w => eligibility.Evaluate(w).IsEligible && matcher.Match(w)?.Id == rule.Id)];
    }

    /// <summary>
    /// Ventanas de la regla que no se pueden agrupar (proceso elevado o inaccesible: UIPI impide gestionarlas). TabDock
    /// nunca intenta forzarlas; solo avisa.
    /// </summary>
    public int CountBlocked(AppRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var matcher = rules();
        return windows.EnumerateTopLevel().Count(w => eligibility.Evaluate(w).Status == EligibilityStatus.Disabled && matcher.Match(w)?.Id == rule.Id);
    }

    /// <summary>Regla de la ventana si es elegible; para decidir qué agrupar con Ctrl+Alt+M.</summary>
    public AppRule? RuleFor(WindowHandle handle)
    {
        var info = windows.GetInfo(handle);
        return info.IsSuccess && eligibility.Evaluate(info.Value).IsEligible ? rules().Match(info.Value) : null;
    }

    /// <summary>Si una ventana nueva debe añadirse automáticamente, devuelve su información y su regla.</summary>
    public (WindowInfo Window, AppRule Rule)? EvaluateNewWindow(WindowHandle handle)
    {
        var info = windows.GetInfo(handle);
        if (!info.IsSuccess || !eligibility.Evaluate(info.Value).IsEligible)
            return null;

        return rules().Match(info.Value) is { AutoMerge: true } rule ? (info.Value, rule) : null;
    }
}
