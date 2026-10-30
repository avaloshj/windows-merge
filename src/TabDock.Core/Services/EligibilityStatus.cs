namespace TabDock.Core.Services;

public enum EligibilityStatus
{
    /// <summary>Se puede agrupar.</summary>
    Eligible,

    /// <summary>No es una ventana de aplicación (diálogo, herramienta, oculta...).</summary>
    Excluded,

    /// <summary>Es una ventana de aplicación, pero TabDock no puede gestionarla (p. ej. proceso elevado).</summary>
    Disabled,
}
