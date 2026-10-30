namespace TabDock.App.Controls;

public sealed class ItemMoveEventArgs(object item, int targetIndex) : ItemEventArgs(item)
{
    public int TargetIndex { get; } = targetIndex;
}
