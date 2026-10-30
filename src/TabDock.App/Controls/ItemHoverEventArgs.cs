using System.Windows;

namespace TabDock.App.Controls;

public sealed class ItemHoverEventArgs(object item, FrameworkElement container) : ItemEventArgs(item)
{
    /// <summary>Elemento visual de la pestaña, para colocar la vista previa a su altura.</summary>
    public FrameworkElement Container { get; } = container;
}
