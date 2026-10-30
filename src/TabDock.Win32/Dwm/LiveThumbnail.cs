using TabDock.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace TabDock.Win32.Dwm;

/// <summary>
/// Vista previa en vivo de otra ventana con <c>DwmRegisterThumbnail</c>: DWM compone la imagen directamente en nuestra
/// ventana. TabDock nunca lee ni guarda el contenido (la política de seguridad prohíbe capturarlo).
/// Una ventana oculta (<c>SW_HIDE</c>) no tiene superficie que componer, así que su vista previa sale vacía.
/// </summary>
public sealed class LiveThumbnail : IDisposable
{
    private nint _handle;

    private LiveThumbnail(nint handle) => _handle = handle;

    /// <summary>Registra la vista previa de <paramref name="source"/> en la ventana propia <paramref name="destination"/>.</summary>
    public static LiveThumbnail? Create(nint destination, WindowHandle source)
    {
        var hr = PInvoke.DwmRegisterThumbnail((HWND)destination, (HWND)source.Value, out var handle);
        return hr.Succeeded ? new LiveThumbnail(handle) : null;
    }

    /// <summary>Tamaño de la ventana de origen en píxeles, para conservar su proporción.</summary>
    public (int Width, int Height) SourceSize =>
        _handle != 0 && PInvoke.DwmQueryThumbnailSourceSize(_handle, out var size).Succeeded ? (size.cx, size.cy) : (0, 0);

    /// <summary>Muestra la vista previa en <paramref name="destination"/> (píxeles, relativos al área cliente propia).</summary>
    public bool Show(PixelRect destination)
    {
        if (_handle == 0)
            return false;

        var properties = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = PInvoke.DWM_TNP_RECTDESTINATION | PInvoke.DWM_TNP_VISIBLE | PInvoke.DWM_TNP_OPACITY | PInvoke.DWM_TNP_SOURCECLIENTAREAONLY,
            rcDestination = new RECT { left = destination.Left, top = destination.Top, right = destination.Right, bottom = destination.Bottom },
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = false,
        };
        return PInvoke.DwmUpdateThumbnailProperties(_handle, in properties).Succeeded;
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            _ = PInvoke.DwmUnregisterThumbnail(_handle);
            _handle = 0;
        }
    }
}
