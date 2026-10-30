using TabDock.Core.Domain;

namespace TabDock.Core.Services;

public sealed class SettingsChangedEventArgs(SettingsDocument previous, SettingsDocument current) : EventArgs
{
    public SettingsDocument Previous { get; } = previous;

    public SettingsDocument Current { get; } = current;

    public bool HotkeysChanged => !Previous.Hotkeys.SequenceEqual(Current.Hotkeys);

    public bool RulesChanged => !Previous.Rules.SequenceEqual(Current.Rules);

    public bool UserChanged => Previous.User != Current.User;
}
