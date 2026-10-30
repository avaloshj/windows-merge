namespace TabDock.App.Controls;

public class ItemEventArgs(object item) : EventArgs
{
    public object Item { get; } = item;
}
