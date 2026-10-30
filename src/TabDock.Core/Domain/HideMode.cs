namespace TabDock.Core.Domain;

/// <summary>Cómo se ocultan las pestañas inactivas (ADR 0002).</summary>
public enum HideMode
{
    /// <summary><c>SW_HIDE</c>: la única implementada.</summary>
    Hide,

    /// <summary>Quitar solo el botón de la barra de tareas y dejarla detrás del marco. Pendiente (Fase 8).</summary>
    TaskbarOnly,
}
