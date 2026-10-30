using TabDock.App.Services;
using TabDock.Core.Domain;

namespace TabDock.App.Tests.Fakes;

internal sealed class FakeHotkeyControl : IHotkeyControl
{
    public IReadOnlyList<HotkeyBinding> Conflicts { get; set; } = [];

    public bool IsSuspended { get; private set; }

    public void SuspendAll() => IsSuspended = true;

    public void ResumeAll() => IsSuspended = false;
}
