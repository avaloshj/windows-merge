namespace TabDock.App.ViewModels;

public sealed class TabActionEventArgs(TabViewModel tab, TabAction action, object? value = null) : EventArgs
{
    public TabViewModel Tab { get; } = tab;

    public TabAction Action { get; } = action;

    public object? Value { get; } = value;
}
