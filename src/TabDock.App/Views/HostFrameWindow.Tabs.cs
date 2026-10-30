using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TabDock.App.Controls;
using TabDock.App.Converters;
using TabDock.App.Localization;
using TabDock.App.ViewModels;
using TabDock.Core.Domain;
using TabDock.Win32.Input;

namespace TabDock.App.Views;

/// <summary>Interacción con las pestañas: menú contextual, renombrar, arrastrar, desacoplar y vista previa.</summary>
public partial class HostFrameWindow
{
    private static readonly HexToBrushConverter s_brushes = new();

    private TabPreviewWindow? _preview;

    private void InitializeTabInteractions()
    {
        TabList.ContextMenuOpening += OnTabContextMenuOpening;
        TabList.MouseDoubleClick += OnTabDoubleClick;
        TabList.PreviewKeyDown += OnTabListKeyDown;
        TabList.ItemMoveRequested += (_, e) => _ = e.Item is TabViewModel tab && _viewModel.MoveTab(tab, e.TargetIndex);
        TabList.ItemReordered += (_, e) => { if (e.Item is TabViewModel tab) _viewModel.CompleteReorder(tab); };
        TabList.ItemDraggedOut += (_, e) => { if (e.Item is TabViewModel tab) _viewModel.Detach(tab, CursorPosition.Get()); };
        TabList.ItemHoverStarted += OnTabHoverStarted;
        TabList.ItemHoverEnded += (_, _) => _preview?.HidePreview();
        Deactivated += (_, _) => _preview?.HidePreview();
        Closed += (_, _) => _preview?.Close();
    }

    private void OnTabDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TabFrom(e.OriginalSource) is { } tab)
            _viewModel.Rename(tab);
    }

    private void OnTabListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 && _viewModel.SelectedTab is { IsRenaming: false } tab)
        {
            _viewModel.Rename(tab);
            e.Handled = true;
        }
    }

    /// <summary>Menú construido al abrirse (ratón o teclado: Mayús+F10 / tecla de menú) para la pestaña afectada.</summary>
    private void OnTabContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        var tab = TabFrom(e.OriginalSource) ?? _viewModel.SelectedTab;
        if (tab is null || tab.IsClosing)
            return;

        _preview?.HidePreview();
        var menu = new ContextMenu { PlacementTarget = TabList.ItemContainerGenerator.ContainerFromItem(tab) as UIElement ?? TabList };
        menu.Items.Add(Item("Menu_Rename", () => _viewModel.Rename(tab), "F2"));
        menu.Items.Add(ColorMenu(tab));
        menu.Items.Add(Item(tab.IsPinned ? "Menu_Unpin" : "Menu_Pin", () => _viewModel.TogglePin(tab)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Menu_Detach", () => _viewModel.Detach(tab)));
        menu.Items.Add(MoveMenu(tab));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Menu_CloseWindow", () => _viewModel.CloseTabCommand.Execute(tab)));
        var closeOthers = Item("Menu_CloseOthers", () => _viewModel.CloseOthers(tab));
        closeOthers.IsEnabled = _viewModel.Tabs.Count(t => !t.IsClosing) > 1;
        menu.Items.Add(closeOthers);
        menu.IsOpen = true;
    }

    private MenuItem ColorMenu(TabViewModel tab)
    {
        var menu = new MenuItem { Header = TextResources.Get("Menu_Color") };
        menu.Items.Add(Item("Menu_ColorAutomatic", () => _viewModel.SetColor(tab, null)));
        foreach (var hex in ProjectColorPalette.Colors)
        {
            var swatch = new MenuItem
            {
                Header = hex,
                Icon = new Ellipse { Width = 12, Height = 12, Fill = (Brush)s_brushes.Convert(hex, typeof(Brush), null, System.Globalization.CultureInfo.InvariantCulture) },
                IsChecked = string.Equals(tab.ColorHex, hex, StringComparison.OrdinalIgnoreCase),
            };
            AutomationPropertiesHelper.SetName(swatch, TextResources.Format("Menu_ColorSwatch", hex));
            swatch.Click += (_, _) => _viewModel.SetColor(tab, hex);
            menu.Items.Add(swatch);
        }

        return menu;
    }

    private MenuItem MoveMenu(TabViewModel tab)
    {
        var menu = new MenuItem { Header = TextResources.Get("Menu_MoveToGroup") };
        foreach (var group in _viewModel.OtherGroups)
        {
            var target = new MenuItem { Header = group.Title };
            target.Click += (_, _) => _viewModel.MoveToGroup(tab, group.Id);
            menu.Items.Add(target);
        }

        if (menu.Items.Count == 0)
            menu.Items.Add(new MenuItem { Header = TextResources.Get("Menu_NoOtherGroups"), IsEnabled = false });

        return menu;
    }

    /// <summary>Vista previa a la derecha de la barra lateral, a la altura de la pestaña. No para la pestaña activa.</summary>
    private void OnTabHoverStarted(object? sender, ItemHoverEventArgs e)
    {
        if (e.Item is not TabViewModel tab || tab.IsRenaming || ReferenceEquals(tab, _viewModel.SelectedTab))
            return;

        var source = _viewModel.WindowResolver?.Invoke(tab);
        var visible = source is { } handle && _windows.GetInfo(handle) is { IsSuccess: true } info && info.Value.IsVisible;
        var anchor = e.Container.PointToScreen(new Point(e.Container.ActualWidth + 12, 0));
        var dpi = VisualTreeHelper.GetDpi(this);

        _preview ??= new TabPreviewWindow { Owner = this };
        _preview.ShowFor(tab, source, visible, new Point(anchor.X / dpi.DpiScaleX, anchor.Y / dpi.DpiScaleY));
    }

    private static MenuItem Item(string textKey, Action action, string? gesture = null)
    {
        var item = new MenuItem { Header = TextResources.Get(textKey), InputGestureText = gesture ?? string.Empty };
        item.Click += (_, _) => action();
        return item;
    }

    private static TabViewModel? TabFrom(object source)
    {
        var element = source as DependencyObject;
        while (element is not null and not ListBoxItem)
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

        return (element as ListBoxItem)?.DataContext as TabViewModel;
    }

    /// <summary>Envoltorio para fijar el nombre accesible de elementos creados en código.</summary>
    private static class AutomationPropertiesHelper
    {
        public static void SetName(DependencyObject element, string name) =>
            System.Windows.Automation.AutomationProperties.SetName(element, name);
    }
}
