namespace TabDock.App.ViewModels;

public sealed class TabEventArgs(TabViewModel tab) : EventArgs
{
    public TabViewModel Tab { get; } = tab;
}
