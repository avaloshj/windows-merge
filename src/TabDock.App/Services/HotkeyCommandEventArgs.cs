using TabDock.Core.Domain;

namespace TabDock.App.Services;

public sealed class HotkeyCommandEventArgs(HotkeyCommand command) : EventArgs
{
    public HotkeyCommand Command { get; } = command;
}
