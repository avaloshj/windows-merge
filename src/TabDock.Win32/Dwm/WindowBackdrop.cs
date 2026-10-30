namespace TabDock.Win32.Dwm;

/// <summary>Material de fondo del sistema (Windows 11 22H2+).</summary>
public enum WindowBackdrop
{
    None,

    /// <summary>Mica (<c>DWMSBT_MAINWINDOW</c>): para ventanas principales de larga duración.</summary>
    Mica,

    /// <summary>Acrylic (<c>DWMSBT_TRANSIENTWINDOW</c>): para superficies transitorias.</summary>
    Acrylic,
}
