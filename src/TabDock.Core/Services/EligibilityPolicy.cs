using TabDock.Core.Abstractions;

namespace TabDock.Core.Services;

/// <summary>
/// Decide si una ventana de primer nivel puede agruparse. Devuelve el primer motivo de exclusión encontrado,
/// en un orden fijo para que el diagnóstico sea estable.
/// </summary>
public sealed class EligibilityPolicy(EligibilityOptions options, int ownProcessId)
{
    private readonly EligibilityOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public EligibilityResult Evaluate(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.ProcessId == ownProcessId)
            return EligibilityResult.Excluded(ExclusionReason.OwnProcess);

        if (!window.IsVisible)
            return EligibilityResult.Excluded(ExclusionReason.NotVisible);

        if (window.IsCloaked)
            return EligibilityResult.Excluded(ExclusionReason.Cloaked);

        if (window.HasOwner)
            return EligibilityResult.Excluded(ExclusionReason.HasOwner);

        if (window.IsToolWindow)
            return EligibilityResult.Excluded(ExclusionReason.ToolWindow);

        if (_options.ExcludedClassNames.Contains(window.ClassName))
            return EligibilityResult.Excluded(ExclusionReason.ExcludedClass);

        if (string.IsNullOrWhiteSpace(window.Title))
            return EligibilityResult.Excluded(ExclusionReason.EmptyTitle);

        // Una ventana minimizada tiene límites ficticios (-32000): su tamaño real no se puede juzgar aquí.
        if (window.ShowState != WindowShowState.Minimized
            && (window.ExtendedFrameBounds.Width < _options.MinWidth || window.ExtendedFrameBounds.Height < _options.MinHeight))
        {
            return EligibilityResult.Excluded(ExclusionReason.TooSmall);
        }

        if (!window.IsProcessAccessible)
            return EligibilityResult.Disabled(ExclusionReason.ProcessInaccessible);

        if (window.IsElevatedProcess)
            return EligibilityResult.Disabled(ExclusionReason.ElevatedProcess);

        return EligibilityResult.Eligible;
    }
}
