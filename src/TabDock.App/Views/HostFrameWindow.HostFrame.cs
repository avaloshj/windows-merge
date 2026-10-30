using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TabDock.Core.Abstractions;
using TabDock.Win32.Dwm;

namespace TabDock.App.Views;

/// <summary>Implementación de <see cref="IHostFrame"/>: posición del hueco en píxeles físicos y movimiento del marco.</summary>
public partial class HostFrameWindow : IHostFrame
{
    private EventHandler? _frameStateChanged;
    private EventHandler<FrameActivatedEventArgs>? _frameActivated;
    private EventHandler? _zOrderChanged;
    private bool _zOrderCheckPending;
    private bool _holeOpen;

    public event EventHandler? SlotBoundsChanged;

    event EventHandler? IHostFrame.WindowStateChanged
    {
        add => _frameStateChanged += value;
        remove => _frameStateChanged -= value;
    }

    event EventHandler<FrameActivatedEventArgs>? IHostFrame.Activated
    {
        add => _frameActivated += value;
        remove => _frameActivated -= value;
    }

    event EventHandler? IHostFrame.ZOrderChanged
    {
        add => _zOrderChanged += value;
        remove => _zOrderChanged -= value;
    }

    WindowHandle IHostFrame.Handle => new(_hwnd);

    bool IHostFrame.IsMinimized => WindowState == WindowState.Minimized;

    bool IHostFrame.IsMaximized => WindowState == WindowState.Maximized;

    /// <summary>
    /// Se calcula con la posición de layout del hueco (sin transformaciones de render), de modo que la animación de
    /// entrada del contenido no desplaza la ventana gestionada.
    /// </summary>
    public PixelRect SlotBounds
    {
        get
        {
            if (_hwnd == 0 || PresentationSource.FromVisual(RootBorder) is null)
                return default;

            var layout = LayoutInformation.GetLayoutSlot(Slot);
            var topLeft = RootBorder.PointToScreen(layout.TopLeft);
            var bottomRight = RootBorder.PointToScreen(layout.BottomRight);
            return new PixelRect(
                (int)Math.Round(topLeft.X),
                (int)Math.Round(topLeft.Y),
                (int)Math.Round(bottomRight.X),
                (int)Math.Round(bottomRight.Y));
        }
    }

    void IHostFrame.MoveSlotTo(PixelRect slot)
    {
        if (_hwnd == 0)
            return;

        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;

        var window = WindowGeometry.GetBounds(_hwnd);
        var current = SlotBounds;
        WindowGeometry.SetBounds(_hwnd, new PixelRect(
            slot.Left - (current.Left - window.Left),
            slot.Top - (current.Top - window.Top),
            slot.Right + (window.Right - current.Right),
            slot.Bottom + (window.Bottom - current.Bottom)));
    }

    void IHostFrame.SetFrameBounds(PixelRect bounds)
    {
        if (_hwnd == 0)
            return;

        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;

        WindowGeometry.SetBounds(_hwnd, bounds);
    }

    /// <summary>Mientras el hueco está abierto, el marco usa fondo sólido (ADR 0009: Mica con región sin verificar).</summary>
    void IHostFrame.SetSlotHole(bool open)
    {
        if (_holeOpen != open)
        {
            _holeOpen = open;

            // Con ventana, la franja del borde tapado tiene el color de la barra lateral: una sola superficie.
            Slot.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, open ? "Brush.SidebarSurface" : "Brush.BgWindow");
            ApplyWindowStyle();
        }

        UpdateSlotHole();
    }

    /// <summary>
    /// Con la barra del marco visible, el hueco ocupa solo la fila de contenido; si no, también la de la barra (la de la
    /// ventana gestionada hace de barra del grupo). El cambio de tamaño del hueco avisa a la sesión, que recoloca.
    /// </summary>
    void IHostFrame.SetOwnCaptionVisible(bool visible)
    {
        System.Windows.Controls.Grid.SetRow(Slot, visible ? 1 : 0);
        System.Windows.Controls.Grid.SetRowSpan(Slot, visible ? 1 : 2);
    }

    void IHostFrame.Maximize() => WindowState = WindowState.Maximized;

    void IHostFrame.Minimize() => WindowState = WindowState.Minimized;

    void IHostFrame.Restore() => WindowState = WindowState.Normal;

    /// <summary>
    /// El hueco sigue al rectángulo del hueco en pantalla: se recalcula al mover, redimensionar, maximizar, cambiar de
    /// DPI o cambiar el ancho de la barra lateral. Minimizado, sin región.
    /// </summary>
    private void UpdateSlotHole()
    {
        if (_hwnd == 0)
            return;

        var open = _holeOpen && WindowState != WindowState.Minimized;
        _ = SlotHole.Apply(_hwnd, WindowGeometry.GetBounds(_hwnd), open ? Inset(SlotBounds, BorderCoverPixels()) : null);
    }

    /// <summary>
    /// El hueco es algo más pequeño que la ventana: el marco tapa la línea de 1 px que Windows 11 (y VS Code) dibujan en
    /// su borde, y la barra lateral pasa a la ventana sin línea. 1 px a 100 %, 2 px por encima (la línea escala).
    /// </summary>
    private int BorderCoverPixels() => (int)Math.Ceiling(VisualTreeHelper.GetDpi(this).DpiScaleX);

    private static PixelRect Inset(PixelRect rect, int by) =>
        new(rect.Left + by, rect.Top + by, rect.Right - by, rect.Bottom - by);
}
