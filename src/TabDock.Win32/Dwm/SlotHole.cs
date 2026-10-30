using TabDock.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace TabDock.Win32.Dwm;

/// <summary>
/// Región de la ventana del marco con un hueco: la zona del hueco deja de pertenecer al marco, así que se ve la ventana
/// gestionada que está debajo y los clics le llegan a ella. Solo se aplica a la ventana propia del marco.
/// </summary>
public static class SlotHole
{
    /// <summary>Aplica (o quita) el hueco.</summary>
    /// <param name="frame">Ventana del marco.</param>
    /// <param name="frameBounds">Rectángulo de la ventana del marco en pantalla (píxeles físicos).</param>
    /// <param name="hole">Hueco en pantalla; <see langword="null"/> quita la región (marco entero).</param>
    public static bool Apply(nint frame, PixelRect frameBounds, PixelRect? hole)
    {
        var hwnd = (HWND)frame;
        if (hole is not { } h || h.Width <= 0 || h.Height <= 0)
            return PInvoke.SetWindowRgn(hwnd, HRGN.Null, true) != 0;

        // Coordenadas de la región: relativas a la esquina superior izquierda de la ventana.
        var whole = PInvoke.CreateRectRgn(0, 0, frameBounds.Width, frameBounds.Height);
        var cut = PInvoke.CreateRectRgn(h.Left - frameBounds.Left, h.Top - frameBounds.Top, h.Right - frameBounds.Left, h.Bottom - frameBounds.Top);
        try
        {
            _ = PInvoke.CombineRgn(whole, whole, cut, RGN_COMBINE_MODE.RGN_DIFF);

            // Si SetWindowRgn tiene éxito, la región pasa a ser del sistema y no se debe liberar.
            if (PInvoke.SetWindowRgn(hwnd, whole, true) == 0)
                return false;

            whole = HRGN.Null;
            return true;
        }
        finally
        {
            if (!whole.IsNull)
                _ = PInvoke.DeleteObject(whole);

            _ = PInvoke.DeleteObject(cut);
        }
    }
}
