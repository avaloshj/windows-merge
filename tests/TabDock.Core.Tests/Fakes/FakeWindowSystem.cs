using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Tests.Fakes;

/// <summary>Sistema de ventanas en memoria. Registra cada operación en <see cref="Journal"/> para verificar el orden.</summary>
internal sealed class FakeWindowSystem(List<string> journal) : IWindowSystem
{
    private readonly Dictionary<WindowHandle, FakeWindow> _windows = [];

    public List<string> Journal { get; } = journal;

    public bool FailPlace { get; set; }

    public bool DenyForeground { get; set; }

    public PixelRect WorkArea { get; set; } = new(0, 0, 1920, 1040);

    public FakeWindow Add(int handle, string title, PixelRect bounds)
    {
        var window = new FakeWindow(new WindowHandle(handle), title, bounds);
        _windows[window.Handle] = window;
        return window;
    }

    public FakeWindow this[WindowHandle handle] => _windows[handle];

    public WindowInfo Info(FakeWindow window) => GetInfo(window.Handle).Value!;

    public IReadOnlyList<WindowInfo> EnumerateTopLevel() => [.. _windows.Values.Where(w => w.Alive).Select(ToInfo)];

    public Result<WindowInfo> GetInfo(WindowHandle handle) =>
        _windows.TryGetValue(handle, out var w) && w.Alive
            ? Result.Success(ToInfo(w))
            : Result.Fail<WindowInfo>(new DomainError("window.notFound", "Error_WindowNotFound"));

    public bool IsAlive(WindowHandle handle) => _windows.TryGetValue(handle, out var w) && w.Alive;

    public bool IsHung(WindowHandle handle) => _windows[handle].Hung;

    public int GetCaptionInset(WindowHandle handle) => _windows.TryGetValue(handle, out var w) ? w.CaptionInset : 0;

    public bool IsVisible(WindowHandle handle) => _windows.TryGetValue(handle, out var w) && w.Alive && w.Visible;

    public bool Matches(WindowIdentity identity) => IsAlive(identity.Handle) && _windows[identity.Handle].ProcessId == identity.ProcessId;

    public Result<WindowSnapshot> CaptureSnapshot(WindowHandle handle)
    {
        var w = _windows[handle];
        Journal.Add($"capture:{handle.Value}");
        var placement = new WindowPlacementData(0, 1, 0, 0, 0, 0, w.Bounds);
        return Result.Success(new WindowSnapshot(
            new WindowIdentity(handle, w.ProcessId, null), placement, w.Bounds, w.Visible, w.ShowState == WindowShowState.Maximized, w.ShowState == WindowShowState.Minimized, DateTimeOffset.UnixEpoch, w.OwnChrome));
    }

    public Result Restore(WindowSnapshot snapshot)
    {
        var w = _windows[snapshot.Identity.Handle];
        Journal.Add($"restore:{w.Handle.Value}");
        if (snapshot.ShadowSuppressed)
            Journal.Add($"shadow:{w.Handle.Value}:on");

        w.Bounds = snapshot.ActualBounds;
        w.Visible = snapshot.WasVisible;
        w.ShowState = snapshot.WasMaximized ? WindowShowState.Maximized : WindowShowState.Normal;
        return Result.Success();
    }

    public Result Show(WindowHandle handle)
    {
        Journal.Add($"show:{handle.Value}");
        _windows[handle].Visible = true;
        return Result.Success();
    }

    public Result Hide(WindowHandle handle)
    {
        Journal.Add($"hide:{handle.Value}");
        _windows[handle].Visible = false;
        return Result.Success();
    }

    public Result PlaceInSlot(WindowHandle handle, PixelRect slot)
    {
        Journal.Add($"place:{handle.Value}");
        if (FailPlace)
            return Result.Fail(new DomainError("window.placeFailed", "Error_WindowOperationFailed", 5));

        var w = _windows[handle];
        w.Bounds = slot;
        w.ShowState = WindowShowState.Normal;
        return Result.Success();
    }

    public Result PlaceAbove(WindowHandle window, WindowHandle below)
    {
        Journal.Add($"above:{window.Value}>{below.Value}");
        WindowAboveFrame = false;
        return Result.Success();
    }

    public Result BringToForeground(WindowHandle handle)
    {
        Journal.Add($"focus:{handle.Value}");
        if (!DenyForeground)
            Foreground = handle;

        return DenyForeground ? Result.Fail(new DomainError("window.foregroundDenied", "Error_WindowOperationFailed")) : Result.Success();
    }

    public PixelRect GetWorkArea(WindowHandle handle) => WorkArea;

    /// <summary>La ventana activa quedó por encima del marco (hasta el siguiente <see cref="PlaceAbove"/>).</summary>
    public bool WindowAboveFrame { get; set; }

    /// <summary>Ventana en primer plano (la última que recibió el foco con éxito, o la que fije la prueba).</summary>
    public WindowHandle? Foreground { get; set; }

    public bool IsForeground(WindowHandle handle) => Foreground == handle;

    /// <summary>Solo se pregunta por el marco (9999) frente a la ventana activa.</summary>
    public bool IsAbove(WindowHandle upper, WindowHandle lower) => upper.Value == FakeHostFrame.FrameHandle ? !WindowAboveFrame : WindowAboveFrame;

    public Result SetFrameShadow(WindowHandle handle, bool visible)
    {
        Journal.Add($"shadow:{handle.Value}:{(visible ? "on" : "off")}");
        return Result.Success();
    }

    public Result SetTaskbarButton(WindowHandle handle, bool visible)
    {
        Journal.Add($"taskbar:{handle.Value}:{(visible ? "on" : "off")}");
        return Result.Success();
    }

    public Result RequestClose(WindowHandle handle)
    {
        Journal.Add($"close:{handle.Value}");
        return Result.Success();
    }

    private static WindowInfo ToInfo(FakeWindow w) => new()
    {
        Handle = w.Handle,
        ProcessId = w.ProcessId,
        ExecutablePath = @"C:\Programas\Ficticio\app.exe",
        ClassName = "ClaseFicticia",
        Title = w.Title,
        ExtendedFrameBounds = w.Bounds,
        IsVisible = w.Visible,
        IsCloaked = false,
        HasOwner = false,
        IsToolWindow = false,
        IsProcessAccessible = true,
        IsElevatedProcess = false,
        ShowState = w.ShowState,
    };
}
