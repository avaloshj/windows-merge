namespace TabDock.App.ViewModels;

/// <summary>Acciones sobre una pestaña que el coordinador traduce a preferencias guardadas u operaciones de ventana.</summary>
public enum TabAction
{
    /// <summary>Valor: alias (<see cref="string"/>) o <see langword="null"/> para quitarlo.</summary>
    Rename,

    /// <summary>Valor: color <c>#RRGGBB</c> o <see langword="null"/> para volver al automático.</summary>
    ChangeColor,

    TogglePin,

    /// <summary>El orden de la barra cambió (arrastrar, fijar): se guarda la posición de cada proyecto.</summary>
    Reorder,

    /// <summary>Valor: punto de pantalla en píxeles <c>(int X, int Y)</c> donde se soltó, o <see langword="null"/>.</summary>
    Detach,

    /// <summary>Valor: identificador (<see cref="Guid"/>) del grupo destino.</summary>
    MoveToGroup,
}
