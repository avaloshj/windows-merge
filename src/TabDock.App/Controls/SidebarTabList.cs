using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TabDock.App.Controls.Motion;

namespace TabDock.App.Controls;

/// <summary>
/// Lista de pestañas con:
/// <list type="bullet">
/// <item>Indicador de pestaña activa (<c>PART_Indicator</c>) que se desliza al cambiar la selección (200 ms); si el cambio
/// se debe al layout, sigue a la pestaña sin animación, para que no haya saltos.</item>
/// <item>Arrastrar para reordenar (en vivo) y arrastrar fuera de la lista para desacoplar.</item>
/// <item>Aviso de "hover" prolongado (400 ms) para mostrar la vista previa.</item>
/// </list>
/// </summary>
[TemplatePart(Name = IndicatorPartName, Type = typeof(FrameworkElement))]
public sealed class SidebarTabList : ListBox
{
    public static readonly DependencyProperty AllowReorderProperty = DependencyProperty.Register(
        nameof(AllowReorder), typeof(bool), typeof(SidebarTabList), new PropertyMetadata(true));

    /// <summary>Clic en la pestaña que ya estaba seleccionada (no cambia la selección, así que no hay otro aviso).</summary>
    public static readonly DependencyProperty SelectedItemClickCommandProperty = DependencyProperty.Register(
        nameof(SelectedItemClickCommand), typeof(ICommand), typeof(SidebarTabList), new PropertyMetadata(null));

    private const string IndicatorPartName = "PART_Indicator";
    private const string DragFormat = "TabDock.Tab";
    private static readonly TimeSpan s_hoverDelay = TimeSpan.FromMilliseconds(400);

    private readonly TranslateTransform _indicatorOffset = new();
    private readonly DispatcherTimer _hoverTimer;
    private FrameworkElement? _indicator;
    private Rect _lastTarget = Rect.Empty;
    private bool _animateNextMove;

    private Point _pressPoint;
    private object? _pressedItem;
    private bool _pressedWasSelected;
    private bool _dragCancelled;
    private bool _dragging;
    private ListBoxItem? _hoverContainer;
    private bool _hoverShown;

    public SidebarTabList()
    {
        LayoutUpdated += (_, _) => UpdateIndicator();
        AllowDrop = true;
        _hoverTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = s_hoverDelay };
        _hoverTimer.Tick += OnHoverTimerTick;
    }

    /// <summary>Se pide mover el elemento a otra posición (durante el arrastre, en vivo).</summary>
    public event EventHandler<ItemMoveEventArgs>? ItemMoveRequested;

    /// <summary>El arrastre terminó dentro de la lista: hay que guardar el orden.</summary>
    public event EventHandler<ItemEventArgs>? ItemReordered;

    /// <summary>El elemento se soltó fuera de la lista: hay que desacoplarlo.</summary>
    public event EventHandler<ItemEventArgs>? ItemDraggedOut;

    /// <summary>El ratón lleva 400 ms sobre un elemento.</summary>
    public event EventHandler<ItemHoverEventArgs>? ItemHoverStarted;

    public event EventHandler? ItemHoverEnded;

    public bool AllowReorder
    {
        get => (bool)GetValue(AllowReorderProperty);
        set => SetValue(AllowReorderProperty, value);
    }

    public ICommand? SelectedItemClickCommand
    {
        get => (ICommand?)GetValue(SelectedItemClickCommandProperty);
        set => SetValue(SelectedItemClickCommandProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _indicator = GetTemplateChild(IndicatorPartName) as FrameworkElement;
        if (_indicator is not null)
            _indicator.RenderTransform = _indicatorOffset;

        _lastTarget = Rect.Empty;
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);

        // Solo se anima si ya había una pestaña activa; la primera selección coloca el indicador directamente.
        _animateNextMove = !_lastTarget.IsEmpty;
        _ = Dispatcher.BeginInvoke(UpdateIndicator, DispatcherPriority.Loaded);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        EndHover();

        // Pulsar el botón × o el cuadro de renombrar no inicia un arrastre.
        var source = e.OriginalSource as DependencyObject;
        _pressedItem = FindAncestor<ButtonBase>(source) is null && FindAncestor<TextBox>(source) is null
            ? ContainerFrom(source)?.DataContext
            : null;
        _pressPoint = e.GetPosition(this);

        // Se mira antes de que el ListBox cambie la selección (lo hace al pulsar, no al soltar).
        _pressedWasSelected = _pressedItem is not null && Equals(_pressedItem, SelectedItem);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (_pressedItem is not null && e.LeftButton == MouseButtonState.Pressed && AllowReorder && MovedBeyondThreshold(e.GetPosition(this)))
        {
            var item = _pressedItem;
            _pressedItem = null;
            StartDrag(item);
            return;
        }

        TrackHover(ContainerFrom(e.OriginalSource as DependencyObject));
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);

        // Sin arrastre de por medio (_pressedItem sigue puesto): volver a la ventana de la pestaña activa.
        if (_pressedItem is { } item && _pressedWasSelected && SelectedItemClickCommand is { } command && command.CanExecute(item))
            command.Execute(item);

        _pressedItem = null;
        _pressedWasSelected = false;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        EndHover();
    }

    protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e)
    {
        base.OnQueryContinueDrag(e);
        if (e.EscapePressed)
            _dragCancelled = true;
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        if (!e.Data.GetDataPresent(DragFormat))
            return;

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var item = e.Data.GetData(DragFormat);
        var target = ContainerFrom(InputHitTest(e.GetPosition(this)) as DependencyObject);
        if (item is null || target is null)
            return;

        var index = ItemContainerGenerator.IndexFromContainer(target);
        if (index >= 0)
            ItemMoveRequested?.Invoke(this, new ItemMoveEventArgs(item, index));
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        if (e.Data.GetDataPresent(DragFormat))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void StartDrag(object item)
    {
        _dragCancelled = false;
        _dragging = true;
        try
        {
            var result = DragDrop.DoDragDrop(this, new DataObject(DragFormat, item), DragDropEffects.Move);

            // Nadie aceptó el elemento y no se canceló con Esc: se soltó fuera de la lista.
            if (result == DragDropEffects.None && !_dragCancelled)
                ItemDraggedOut?.Invoke(this, new ItemEventArgs(item));
            else
                ItemReordered?.Invoke(this, new ItemEventArgs(item));
        }
        finally
        {
            _dragging = false;
        }
    }

    private bool MovedBeyondThreshold(Point current) =>
        Math.Abs(current.X - _pressPoint.X) >= SystemParameters.MinimumHorizontalDragDistance
        || Math.Abs(current.Y - _pressPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;

    private void TrackHover(ListBoxItem? container)
    {
        if (_dragging || ReferenceEquals(container, _hoverContainer))
            return;

        EndHover();
        _hoverContainer = container;
        if (container is not null)
            _hoverTimer.Start();
    }

    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (_hoverContainer is { IsMouseOver: true } container && container.DataContext is { } item)
        {
            _hoverShown = true;
            ItemHoverStarted?.Invoke(this, new ItemHoverEventArgs(item, container));
        }
    }

    private void EndHover()
    {
        _hoverTimer.Stop();
        _hoverContainer = null;
        if (_hoverShown)
        {
            _hoverShown = false;
            ItemHoverEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private ListBoxItem? ContainerFrom(DependencyObject? element)
    {
        var container = FindAncestor<ListBoxItem>(element);
        return container is not null && ItemsControlFromItemContainer(container) == this ? container : null;
    }

    private static T? FindAncestor<T>(DependencyObject? element)
        where T : DependencyObject
    {
        while (element is not null and not T)
            element = element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

        return element as T;
    }

    private void UpdateIndicator()
    {
        if (_indicator?.Parent is not FrameworkElement host)
            return;

        var container = SelectedItem is null ? null : ItemContainerGenerator.ContainerFromItem(SelectedItem) as FrameworkElement;
        if (container is null || !container.IsVisible || container.ActualHeight <= 0)
        {
            _indicator.Opacity = 0;
            _lastTarget = Rect.Empty;
            return;
        }

        var origin = container.TransformToAncestor(host).Transform(new Point(0, 0));
        var target = new Rect(origin.X, origin.Y, container.ActualWidth, container.ActualHeight);
        if (target == _lastTarget)
            return;

        var animate = _animateNextMove;
        _animateNextMove = false;
        _lastTarget = target;

        _indicator.Opacity = 1;
        _indicator.Height = target.Height;
        _indicator.Margin = new Thickness(target.X, 0, host.ActualWidth - target.Right, 0);

        if (animate)
        {
            _ = Animator.RunAsync(
                _indicatorOffset,
                TranslateTransform.YProperty,
                from: null,
                to: target.Y,
                MotionTokens.Time(MotionTokens.DurationIndicator),
                MotionTokens.Ease(MotionTokens.EaseStandard));
        }
        else
        {
            _indicatorOffset.BeginAnimation(TranslateTransform.YProperty, animation: null);
            _indicatorOffset.Y = target.Y;
        }
    }
}
