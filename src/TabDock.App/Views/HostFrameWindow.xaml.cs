using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using CommunityToolkit.Mvvm.Input;
using TabDock.App.Controls.Motion;
using TabDock.App.Theming;
using TabDock.App.ViewModels;
using TabDock.Core.Abstractions;
using TabDock.Win32;
using TabDock.Win32.Dwm;
using TabDock.Win32.Input;

namespace TabDock.App.Views;

/// <summary>
/// Marco anfitrión. Gestiona lo que es propio de la ventana (DWM, maximizar, pantalla completa, ancho de la barra
/// lateral); el estado de las pestañas vive en <see cref="HostFrameViewModel"/> y la relación con las ventanas
/// gestionadas, en <c>HostFrameWindow.HostFrame.cs</c>.
/// </summary>
public partial class HostFrameWindow : Window
{
    private readonly HostFrameViewModel _viewModel;
    private readonly ThemeService _theme;
    private readonly DwmWindowStyler _styler;
    private readonly IWindowSystem _windows;

    private nint _hwnd;
    private bool _isFullScreen;
    private WindowState _stateBeforeFullScreen;

    public HostFrameWindow(HostFrameViewModel viewModel, ThemeService theme, DwmWindowStyler styler, IWindowSystem windows)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(styler);
        ArgumentNullException.ThrowIfNull(windows);

        _viewModel = viewModel;
        _theme = theme;
        _styler = styler;
        _windows = windows;

        WindowCloseCommand = new RelayCommand(Close);
        WindowMinimizeCommand = new RelayCommand(() => WindowState = WindowState.Minimized);
        WindowMaximizeCommand = new RelayCommand(ToggleMaximized);
        WindowFullScreenCommand = new RelayCommand(ToggleFullScreen);

        InitializeComponent();
        DataContext = viewModel;

        // El contenido entra con fundido y escala (0.98 → 1); el marco de la ventana usa la animación del sistema.
        Root.Opacity = 0;

        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        StateChanged += OnWindowStateChanged;
        LocationChanged += (_, _) => OnGeometryChanged();
        DpiChanged += (_, _) => OnGeometryChanged();
        Slot.SizeChanged += (_, _) => OnGeometryChanged();
        Activated += (_, _) => _frameActivated?.Invoke(this, new FrameActivatedEventArgs(forwardFocus: !IsClickInside()));
        _theme.ThemeChanged += OnThemeChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplySidebarLayout();
        InitializeTabInteractions();
    }

    public HostFrameViewModel ViewModel => _viewModel;

    public ICommand WindowCloseCommand { get; }

    public ICommand WindowMinimizeCommand { get; }

    public ICommand WindowMaximizeCommand { get; }

    /// <summary>F11: pantalla completa (cubre también la barra de tareas).</summary>
    public ICommand WindowFullScreenCommand { get; }

    protected override void OnClosed(EventArgs e)
    {
        _theme.ThemeChanged -= OnThemeChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        ApplyWindowStyle();
    }

    /// <summary>
    /// Cada cambio de orden Z del marco se comunica a la sesión, que vuelve a poner encima la ventana activa. Se aplaza a
    /// después del mensaje (la activación por clic termina de reordenar las ventanas) y se agrupan los avisos seguidos.
    /// </summary>
    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WindowMessages.WindowPosChanged && WindowMessages.ChangesZOrder(lParam) && !_zOrderCheckPending)
        {
            _zOrderCheckPending = true;
            _ = Dispatcher.BeginInvoke(() =>
            {
                _zOrderCheckPending = false;
                _zOrderChanged?.Invoke(this, EventArgs.Empty);
            });
        }

        return 0;
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;

        var duration = MotionTokens.Time(MotionTokens.DurationFrame);
        var ease = MotionTokens.Ease(MotionTokens.EaseStandard);
        var scaleFrom = MotionTokens.Number(MotionTokens.FrameScaleFrom, 0.98);

        _ = Animator.RunAsync(Root, OpacityProperty, from: 0, to: 1, duration, ease);
        _ = Animator.RunAsync(RootScale, ScaleTransform.ScaleXProperty, scaleFrom, 1, duration, ease);
        _ = Animator.RunAsync(RootScale, ScaleTransform.ScaleYProperty, scaleFrom, 1, duration, ease);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyWindowStyle();

    /// <summary>
    /// Activación por un clic en el propio marco (botón pulsado con el cursor sobre él), no por barra de tareas ni
    /// Alt+Tab. Se mira el estado real del ratón: cuando un clic activa la ventana, WPF todavía no ha procesado el botón
    /// y lo daría por suelto (y el foco se iría a la pestaña anterior, deshaciendo el cambio de pestaña).
    /// </summary>
    private bool IsClickInside()
    {
        if (_hwnd == 0 || !MouseButtons.AnyPressed())
            return false;

        var (x, y) = CursorPosition.Get();
        var bounds = WindowGeometry.GetBounds(_hwnd);
        return x >= bounds.Left && x < bounds.Right && y >= bounds.Top && y < bounds.Bottom;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        Caption.IsMaximized = WindowState == WindowState.Maximized;
        UpdateMaximizedMargin();
        UpdateSlotHole();
        _frameStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnGeometryChanged()
    {
        UpdateMaximizedMargin();
        UpdateSlotHole();
        SlotBoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Modo oscuro de DWM, esquinas redondeadas (Windows 11) y Mica (Windows 11 22H2) o fondo sólido.</summary>
    private void ApplyWindowStyle()
    {
        if (_hwnd == 0)
            return;

        _styler.SetDarkMode(_hwnd, _theme.IsDark);
        _styler.SetCorners(_hwnd, WindowCorners.Round);

        // Con el hueco abierto, fondo sólido. Siempre con vidrio (aunque sea 1 px): sin él, WindowChrome fija su propia
        // región en cada movimiento de la ventana y borraría el hueco.
        var useMica = _theme.UseMica && !_holeOpen;
        var chrome = WindowChrome.GetWindowChrome(this);
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = chrome.CaptionHeight,
            ResizeBorderThickness = chrome.ResizeBorderThickness,
            CornerRadius = chrome.CornerRadius,
            UseAeroCaptionButtons = false,
            NonClientFrameEdges = NonClientFrameEdges.None,
            GlassFrameThickness = useMica ? new Thickness(-1) : new Thickness(0, 1, 0, 0),
        });

        if (HwndSource.FromHwnd(_hwnd)?.CompositionTarget is { } target)
            target.BackgroundColor = useMica ? Colors.Transparent : SystemColors.WindowColor;

        _styler.SetBackdrop(_hwnd, useMica ? WindowBackdrop.Mica : WindowBackdrop.None);

        // Cambiar WindowChrome borra la región de la ventana: se vuelve a poner el hueco.
        UpdateSlotHole();
    }

    private void ToggleMaximized()
    {
        if (_isFullScreen)
        {
            ToggleFullScreen();
            return;
        }

        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    /// <summary>Sin estilo de borde y maximizada, la ventana cubre también la barra de tareas.</summary>
    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            _isFullScreen = false;
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = _stateBeforeFullScreen;
        }
        else
        {
            _isFullScreen = true;
            _stateBeforeFullScreen = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
        }

        UpdateMaximizedMargin();
    }

    /// <summary>
    /// Con <see cref="WindowChrome"/>, una ventana maximizada sobresale de la pantalla el grosor de sus bordes de
    /// redimensionado. Se compensa con un margen igual a lo que sobresale (medido en píxeles y pasado a DIP).
    /// </summary>
    private void UpdateMaximizedMargin()
    {
        if (_hwnd == 0)
            return;

        if (WindowState != WindowState.Maximized)
        {
            RootBorder.Margin = default;
            return;
        }

        var overflow = WindowGeometry.GetOverflow(_hwnd, _isFullScreen);
        var dpi = VisualTreeHelper.GetDpi(this);
        RootBorder.Margin = new Thickness(
            overflow.Left / dpi.DpiScaleX,
            overflow.Top / dpi.DpiScaleY,
            overflow.Right / dpi.DpiScaleX,
            overflow.Bottom / dpi.DpiScaleY);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HostFrameViewModel.IsSidebarCollapsed))
            ApplySidebarLayout();
    }

    /// <summary>Expandida: 180–360 px redimensionable. Colapsada: 56 px fijos, solo iconos.</summary>
    private void ApplySidebarLayout()
    {
        var collapsed = _viewModel.IsSidebarCollapsed;
        if (collapsed)
        {
            SidebarColumn.MinWidth = HostFrameViewModel.CollapsedSidebarWidth;
            SidebarColumn.MaxWidth = HostFrameViewModel.CollapsedSidebarWidth;
            SidebarColumn.Width = new GridLength(HostFrameViewModel.CollapsedSidebarWidth);
        }
        else
        {
            SidebarColumn.MinWidth = HostFrameViewModel.MinSidebarWidth;
            SidebarColumn.MaxWidth = HostFrameViewModel.MaxSidebarWidth;
            SidebarColumn.Width = new GridLength(_viewModel.SidebarWidth);
        }

        Splitter.IsEnabled = !collapsed;
        SearchBox.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        SectionHeader.Visibility = collapsed ? Visibility.Hidden : Visibility.Visible;
        ToggleSidebarButton.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        ToggleSidebarButton.Margin = new Thickness(collapsed ? 0 : 10, 0, 0, 0);
        ToggleSidebarIcon.Data = (Geometry)FindResource(collapsed ? "Icon.PanelLeftOpen" : "Icon.PanelLeftClose");
    }

    private void OnSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (!_viewModel.IsSidebarCollapsed)
            _viewModel.SidebarWidth = SidebarColumn.ActualWidth;
    }
}
