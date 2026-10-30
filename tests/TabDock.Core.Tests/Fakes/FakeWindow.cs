using TabDock.Core.Abstractions;

namespace TabDock.Core.Tests.Fakes;

internal sealed class FakeWindow(WindowHandle handle, string title, PixelRect bounds)
{
    public WindowHandle Handle { get; } = handle;

    public int ProcessId { get; } = 1000 + (int)handle.Value;

    public string Title { get; set; } = title;

    public PixelRect Bounds { get; set; } = bounds;

    public bool Visible { get; set; } = true;

    public bool Alive { get; set; } = true;

    public bool Hung { get; set; }

    /// <summary>Barra de título propia (sin marco nativo), como VS Code.</summary>
    public bool OwnChrome { get; set; }

    /// <summary>Alto de la barra de título nativa (0 con barra propia).</summary>
    public int CaptionInset { get; set; }

    public WindowShowState ShowState { get; set; } = WindowShowState.Normal;
}
