using TabDock.Core.Domain;

namespace TabDock.Core.Services;

public sealed class ManagedWindowEventArgs(ManagedWindow window) : EventArgs
{
    public ManagedWindow Window { get; } = window;
}
