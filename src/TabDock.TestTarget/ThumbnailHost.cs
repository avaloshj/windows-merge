using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TabDock.TestTarget;

/// <summary>
/// Solo para pruebas: muestra en la ventana la vista previa DWM de otra ventana (<c>--thumbnail-of &lt;hwnd&gt;</c>), para
/// comprobar qué compone DWM cuando el origen está oculto. TestTarget no referencia el producto, por eso declara sus
/// propias llamadas.
/// </summary>
internal static partial class ThumbnailHost
{
    private const uint RectDestination = 0x1;
    private const uint Visible = 0x8;
    private const uint Opacity = 0x4;

    public static void Attach(Window window, nint source)
    {
        window.ContentRendered += (_, _) =>
        {
            var destination = new WindowInteropHelper(window).Handle;
            if (DwmRegisterThumbnail(destination, source, out var thumbnail) != 0)
                return;

            var properties = new ThumbnailProperties
            {
                Flags = RectDestination | Visible | Opacity,
                Destination = new NativeRect { Left = 10, Top = 10, Right = 410, Bottom = 310 },
                OpacityValue = 255,
                IsVisible = 1,
            };
            _ = DwmUpdateThumbnailProperties(thumbnail, ref properties);
        };
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmUpdateThumbnailProperties(nint thumbnail, ref ThumbnailProperties properties);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThumbnailProperties
    {
        public uint Flags;
        public NativeRect Destination;
        public NativeRect Source;
        public byte OpacityValue;
        public int IsVisible;
        public int SourceClientAreaOnly;
    }
}
