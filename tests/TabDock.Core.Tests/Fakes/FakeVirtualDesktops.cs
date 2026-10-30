using TabDock.Core.Abstractions;

namespace TabDock.Core.Tests.Fakes;

internal sealed class FakeVirtualDesktops : IVirtualDesktops
{
    public Dictionary<WindowHandle, Guid> Desktops { get; } = [];

    public Guid? GetDesktopId(WindowHandle handle) => Desktops.TryGetValue(handle, out var desktop) ? desktop : null;

    public bool MoveOwnWindowToDesktop(WindowHandle ownWindow, Guid desktopId)
    {
        Desktops[ownWindow] = desktopId;
        return true;
    }
}
