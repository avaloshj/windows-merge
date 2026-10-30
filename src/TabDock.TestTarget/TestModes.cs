using System.Runtime.InteropServices;

namespace TabDock.TestTarget;

/// <summary>Modos de prueba que reproducen aplicaciones problemáticas.</summary>
internal static partial class TestModes
{
    private static readonly nint s_unawareContext = -1;   // DPI_AWARENESS_CONTEXT_UNAWARE

    /// <summary>Las ventanas que cree este hilo serán "no conscientes de DPI", como una aplicación antigua sin manifiesto.</summary>
    public static void MakeThreadDpiUnaware() => _ = SetThreadDpiAwarenessContext(s_unawareContext);

    /// <summary>
    /// Como Electron/VS Code con barra de título propia: sin barra de título nativa, el área de cliente coincide con el
    /// área visible y DWM sigue dibujando la sombra.
    /// </summary>
    /// <param name="window">Ventana de prueba.</param>
    /// <param name="keepResizeEdges">
    /// <see langword="true"/>: como Electron/VS Code, los bordes de redimensionado siguen siendo marco nativo (invisible);
    /// <see langword="false"/>: sin ningún marco nativo, el área de cliente ocupa toda la ventana.
    /// </param>
    public static void ApplyCustomChrome(System.Windows.Window window, bool keepResizeEdges)
    {
        System.Windows.Shell.WindowChrome.SetWindowChrome(window, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 32,
            ResizeBorderThickness = new System.Windows.Thickness(6),
            GlassFrameThickness = new System.Windows.Thickness(0, 1, 0, 0),
            UseAeroCaptionButtons = false,

            NonClientFrameEdges = keepResizeEdges
                ? System.Windows.Shell.NonClientFrameEdges.Left | System.Windows.Shell.NonClientFrameEdges.Right | System.Windows.Shell.NonClientFrameEdges.Bottom
                : System.Windows.Shell.NonClientFrameEdges.None,
        });
        window.Background = System.Windows.Media.Brushes.WhiteSmoke;
    }

    /// <summary>
    /// Como Chromium: al ocultarse, notifica <c>EVENT_OBJECT_DESTROY</c> (0x8001) sobre su propio HWND con
    /// <c>OBJID_WINDOW</c> y <c>CHILDID_SELF</c> (0), aunque la ventana sigue existiendo.
    /// </summary>
    public static void RaiseDestroyEventOnHide(System.Windows.Window window)
    {
        const uint EventObjectDestroy = 0x8001;
        window.IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false)
                NotifyWinEvent(EventObjectDestroy, new System.Windows.Interop.WindowInteropHelper(window).Handle, 0, 0);
        };
    }

    [LibraryImport("user32.dll")]
    private static partial nint SetThreadDpiAwarenessContext(nint context);

    [LibraryImport("user32.dll")]
    private static partial void NotifyWinEvent(uint winEvent, nint hwnd, int idObject, int idChild);
}
