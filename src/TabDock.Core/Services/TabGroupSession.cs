using Microsoft.Extensions.Logging;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Services;

/// <summary>
/// Orquesta un grupo: una ventana activa colocada sobre el hueco del marco y el resto ocultas (ADR 0001 y 0002).
/// Todas las llamadas deben hacerse desde el hilo de UI. Ningún camino deja una ventana oculta sin registrar en
/// <c>state.json</c>, y cualquier fallo al capturar restaura la ventana.
/// </summary>
public sealed partial class TabGroupSession : IDisposable
{
    /// <summary>
    /// Correcciones máximas para un mismo hueco. Una ventana puede cambiar de tamaño por su cuenta justo después de
    /// colocarla (al pasar a un monitor con otra escala aplica el tamaño sugerido por <c>WM_DPICHANGED</c>): hay que
    /// volver a colocarla. Pero si tiene un tamaño mínimo mayor que el hueco nunca encajará: el límite evita el bucle.
    /// </summary>
    private const int MaxCorrectionsPerSlot = 3;

    /// <summary>Tolerancia (px) para decidir que un rectángulo toca un borde del área de trabajo.</summary>
    private const int EdgeTolerance = 2;

    /// <summary>Margen para confirmar un aviso de cierre de una ventana que aún existía al recibirlo.</summary>
    private static readonly TimeSpan s_goneRecheckDelay = TimeSpan.FromMilliseconds(250);

    private readonly IWindowSystem _windows;
    private readonly IVirtualDesktops _desktops;
    private readonly IHostFrame _frame;
    private readonly RuntimeStateTracker _tracker;
    private readonly TimeProvider _time;
    private readonly ILogger<TabGroupSession> _logger;
    private readonly List<ManagedWindow> _items = [];
    private readonly List<ManagedWindow> _mostRecentFirst = [];
    private readonly HashSet<WindowHandle> _userMoving = [];
    private readonly Dictionary<WindowHandle, ITimer> _goneChecks = [];
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    private (WindowHandle Handle, PixelRect Slot, int Corrections) _lastPlacement;

    public TabGroupSession(IWindowSystem windows, IVirtualDesktops desktops, IHostFrame frame, RuntimeStateTracker tracker, TimeProvider time, ILogger<TabGroupSession> logger)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _desktops = desktops ?? throw new ArgumentNullException(nameof(desktops));
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _frame.SlotBoundsChanged += OnFrameSlotBoundsChanged;
        _frame.WindowStateChanged += OnFrameWindowStateChanged;
        _frame.Activated += OnFrameActivated;
        _frame.ZOrderChanged += OnFrameZOrderChanged;
    }

    public event EventHandler<ManagedWindowEventArgs>? WindowAdded;

    public event EventHandler<ManagedWindowEventArgs>? WindowRemoved;

    public event EventHandler<ManagedWindowEventArgs>? WindowUpdated;

    public event EventHandler? ActiveChanged;

    /// <summary>El grupo se quedó sin ventanas: el marco debe cerrarse.</summary>
    public event EventHandler? Emptied;

    public Guid Id { get; } = Guid.NewGuid();

    public IReadOnlyList<ManagedWindow> Windows => _items;

    public ManagedWindow? Active { get; private set; }

    public bool Owns(WindowHandle handle) => Find(handle) is not null;

    /// <summary>
    /// Captura una ventana. Idempotente: capturar dos veces la misma ventana devuelve la existente.
    /// Con <paramref name="activate"/> pasa a ser la activa (ventana nueva que el usuario acaba de abrir).
    /// </summary>
    public Result<ManagedWindow> Attach(WindowInfo info, bool activate = false)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (Find(info.Handle) is { } existing)
            return Result.Success(existing);

        if (info.Handle == _frame.Handle)
            return Result.Fail<ManagedWindow>(new DomainError("window.isFrame", "Error_CannotCaptureFrame"));

        var snapshot = _windows.CaptureSnapshot(info.Handle);
        if (!snapshot.IsSuccess)
        {
            LogCaptureRejected(snapshot.Error.Code, snapshot.Error.Win32Error);
            return Result.Fail<ManagedWindow>(snapshot.Error);
        }

        var window = new ManagedWindow(snapshot.Value, info.Title);
        _ = window.MoveTo(ManagedWindowState.Attaching);

        // El estado original queda en disco antes de mover u ocultar nada.
        var tracked = _tracker.Track(Id, snapshot.Value);
        if (!tracked.IsSuccess)
        {
            _ = window.MoveTo(ManagedWindowState.Detached);
            LogCaptureRejected(tracked.Error.Code, tracked.Error.Win32Error);
            return Result.Fail<ManagedWindow>(tracked.Error);
        }

        // Sin sombra: la de una ventana sin marco nativo se dibujaría sobre la barra lateral del marco.
        if (snapshot.Value.ShadowSuppressed)
            _ = _windows.SetFrameShadow(window.Handle, visible: false);

        var previous = Active;
        var becomesActive = previous is null || activate;
        var placed = becomesActive ? ShowInSlot(window, focus: false) : HideWindow(window);
        if (!placed.IsSuccess)
        {
            _ = _windows.Restore(window.Snapshot);
            _tracker.Untrack(window.Handle);
            _ = window.MoveTo(ManagedWindowState.Detached);
            LogAttachFailed(window.Id, placed.Error.Code, placed.Error.Win32Error);
            return Result.Fail<ManagedWindow>(placed.Error);
        }

        _ = window.MoveTo(ManagedWindowState.Attached);
        _items.Add(window);
        _mostRecentFirst.Add(window);
        LogAttached(window.Id, Id);
        LogAttachedHandle(window.Id, window.Handle.Value);
        WindowAdded?.Invoke(this, new ManagedWindowEventArgs(window));

        if (becomesActive)
        {
            if (previous is { IsInGroup: true })
                _ = HideWindow(previous);

            SetActive(window);
        }

        return Result.Success(window);
    }

    /// <summary>
    /// Muestra la ventana en el hueco y oculta la anterior (en ese orden, para que no se vea el fondo). El foco se pide
    /// al final: si Windows lo deniega, sus reintentos no retrasan el cambio visible.
    /// </summary>
    public Result Activate(Guid windowId, bool focus = true)
    {
        var target = Find(windowId);
        if (target is null || !target.IsInGroup)
            return Result.Fail(new DomainError("window.notInGroup", "Error_WindowNotInGroup"));

        if (ReferenceEquals(target, Active))
            return ShowInSlot(target, focus);

        var started = _time.GetTimestamp();
        var previous = Active;
        var shown = ShowInSlot(target, focus: false);
        if (!shown.IsSuccess)
        {
            LogActivateFailed(target.Id, shown.Error.Code, shown.Error.Win32Error);
            return shown;
        }

        if (previous is { IsInGroup: true })
        {
            var hidden = HideWindow(previous);
            if (!hidden.IsSuccess)
                LogHideFailed(previous.Id, hidden.Error.Code, hidden.Error.Win32Error);
        }

        SetActive(target);
        var elapsedMs = _time.GetElapsedTime(started).TotalMilliseconds;
        LogActivated(target.Id, elapsedMs);

        if (focus && target.ExpectedVisible && !_windows.BringToForeground(target.Handle).IsSuccess)
            LogForegroundDenied(target.Id);

        return Result.Success();
    }

    /// <summary>
    /// Devuelve la ventana a su estado original y la saca del grupo. Con <paramref name="bounds"/> (pestaña arrastrada
    /// fuera de la barra) se coloca además donde se soltó.
    /// </summary>
    public Result Detach(Guid windowId, PixelRect? bounds = null)
    {
        var window = Find(windowId);
        if (window is null || !window.IsInGroup)
            return Result.Fail(new DomainError("window.notInGroup", "Error_WindowNotInGroup"));

        var restored = RestoreWindow(window);
        if (restored.IsSuccess && bounds is { } target)
            _ = _windows.PlaceInSlot(window.Handle, target);

        Remove(window);
        return restored;
    }

    /// <summary>Restaura todas las ventanas (primero las ocultas, la activa la última) y vacía el grupo.</summary>
    public void DetachAll()
    {
        var ordered = _items.Where(w => !ReferenceEquals(w, Active)).ToList();
        if (Active is not null)
            ordered.Add(Active);

        foreach (var window in ordered.Where(w => w.IsInGroup))
            _ = RestoreWindow(window);

        _items.Clear();
        _mostRecentFirst.Clear();
        _userMoving.Clear();
        Active = null;
        _frame.SetSlotHole(open: false);
        Emptied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pide a la ventana que se cierre; se quita del grupo cuando Windows informa de su destrucción.</summary>
    public Result RequestClose(Guid windowId)
    {
        var window = Find(windowId);
        if (window is null || !window.IsInGroup)
            return Result.Fail(new DomainError("window.notInGroup", "Error_WindowNotInGroup"));

        var previousState = window.State;
        _ = window.MoveTo(ManagedWindowState.Closing);
        var result = _windows.RequestClose(window.Handle);
        if (!result.IsSuccess && previousState != ManagedWindowState.Closing)
            _ = window.MoveTo(ManagedWindowState.Attached);

        return result;
    }

    public void HandleEvent(WindowEvent windowEvent)
    {
        var window = Find(windowEvent.Handle);
        if (window is null || !window.IsInGroup)
            return;

        switch (windowEvent.Kind)
        {
            // El aviso llega al empezar a destruir la ventana, así que puede seguir existiendo un instante; y Chromium
            // (VS Code) lo emite también al ocultarla, sin destruirla. Se confirma poco después: un cierre real se quita
            // enseguida (antes de que Windows reutilice su HWND) y el aviso falso no saca la ventana del grupo.
            case WindowEventKind.Destroyed when _windows.IsAlive(window.Handle):
                ScheduleGoneCheck(window);
                break;

            case WindowEventKind.Destroyed:
                OnGone(window);
                break;

            case WindowEventKind.NameChanged:
                UpdateTitle(window);
                if (ReferenceEquals(window, Active))
                    HideTaskbarButton(window);
                break;

            case WindowEventKind.Shown when !window.ExpectedVisible:
                OnShownWhileHidden(window);
                break;

            case WindowEventKind.Foreground:
                OnForeground(window);
                break;

            case WindowEventKind.Cloaked or WindowEventKind.Uncloaked when ReferenceEquals(window, Active):
                FollowActiveDesktop();
                break;

            case WindowEventKind.MinimizeStart when ReferenceEquals(window, Active):
                _frame.Minimize();
                break;

            case WindowEventKind.MoveSizeStart when ReferenceEquals(window, Active):
                _userMoving.Add(window.Handle);
                break;

            case WindowEventKind.MoveSizeEnd when ReferenceEquals(window, Active):
                _userMoving.Remove(window.Handle);
                SyncWithActiveWindow(window);
                break;

            case WindowEventKind.LocationChanged when ReferenceEquals(window, Active):
                SyncWithActiveWindow(window);
                break;
        }
    }

    /// <summary>
    /// Otro proceso mostró una ventana oculta del grupo (p. ej. "code archivo.txt"): se activa su pestaña. Solo si sigue
    /// visible: el aviso de que TabDock la mostró puede llegar después de que la volviera a ocultar al cambiar de pestaña;
    /// tomarlo por ajeno reactivaba pestañas antiguas en cadena (bucle con cambios de pestaña rápidos).
    /// </summary>
    private void OnShownWhileHidden(ManagedWindow window)
    {
        if (!_windows.IsVisible(window.Handle))
        {
            LogStaleShownIgnored(window.Id);
            return;
        }

        LogActivatedByEvent(window.Id, WindowEventKind.Shown);
        _ = Activate(window.Id, focus: false);
    }

    private void OnForeground(ManagedWindow window)
    {
        if (!ReferenceEquals(window, Active))
        {
            // Los eventos llegan con retraso: al cambiar de pestaña con un clic, puede llegar todavía el aviso de que la
            // pestaña anterior pasó a primer plano. Solo cuenta si sigue en primer plano; si no, deshacía el cambio.
            if (_windows.IsForeground(window.Handle))
            {
                LogActivatedByEvent(window.Id, WindowEventKind.Foreground);
                _ = Activate(window.Id, focus: false);
            }
            else
            {
                LogStaleForegroundIgnored(window.Id);
            }

            return;
        }

        // El usuario hizo clic en la ventana (a través del hueco): Windows la sube; el marco vuelve encima.
        _ = _windows.PlaceAbove(_frame.Handle, window.Handle);
        HideTaskbarButton(window);
    }

    /// <summary>Red de seguridad periódica: ventanas cerradas sin evento y ventanas que no responden.</summary>
    public void CheckHealth()
    {
        if (Active is { ExpectedVisible: true } active && _windows.IsAlive(active.Handle))
        {
            HideTaskbarButton(active);
            FollowActiveDesktop();
        }

        foreach (var window in _items.ToList())
        {
            if (!_windows.Matches(window.Identity))
            {
                OnGone(window);
                continue;
            }

            var hung = _windows.IsHung(window.Handle);
            if (hung && window.State == ManagedWindowState.Attached)
            {
                _ = window.MoveTo(ManagedWindowState.Unresponsive);
                WindowUpdated?.Invoke(this, new ManagedWindowEventArgs(window));
            }
            else if (!hung && window.State == ManagedWindowState.Unresponsive)
            {
                _ = window.MoveTo(ManagedWindowState.Attached);
                WindowUpdated?.Invoke(this, new ManagedWindowEventArgs(window));
            }
        }
    }

    public void Dispose()
    {
        foreach (var timer in _goneChecks.Values)
            timer.Dispose();

        _goneChecks.Clear();
        _frame.SlotBoundsChanged -= OnFrameSlotBoundsChanged;
        _frame.WindowStateChanged -= OnFrameWindowStateChanged;
        _frame.Activated -= OnFrameActivated;
        _frame.ZOrderChanged -= OnFrameZOrderChanged;
    }

    private void ScheduleGoneCheck(ManagedWindow window)
    {
        if (_goneChecks.ContainsKey(window.Handle))
            return;

        _goneChecks[window.Handle] = _time.CreateTimer(_ => RunOnSessionThread(() => ConfirmGone(window)), state: null, s_goneRecheckDelay, Timeout.InfiniteTimeSpan);
    }

    private void ConfirmGone(ManagedWindow window)
    {
        if (_goneChecks.Remove(window.Handle, out var timer))
            timer.Dispose();

        if (!window.IsInGroup || !ReferenceEquals(Find(window.Handle), window))
            return;

        if (_windows.IsAlive(window.Handle))
            LogDestroyIgnored(window.Id);
        else
            OnGone(window);
    }

    /// <summary>La sesión se usa en el hilo de UI: el temporizador vuelve a él (en pruebas, sin contexto, se ejecuta directamente).</summary>
    private void RunOnSessionThread(Action action)
    {
        if (_context is null)
            action();
        else
            _context.Post(_ => action(), state: null);
    }

    private ManagedWindow? Find(WindowHandle handle) => _items.Find(w => w.Handle == handle);

    private ManagedWindow? Find(Guid id) => _items.Find(w => w.Id == id);

    private Result ShowInSlot(ManagedWindow window, bool focus)
    {
        if (_frame.IsMinimized)
            return HideWindow(window);

        // La barra del marco según la ventana que se va a mostrar (si ya se midió; si no, se decide al colocarla). Antes
        // de colocarla: cambiar la barra mueve el hueco.
        if (window.CaptionInset is { } known)
            _frame.SetOwnCaptionVisible(known > 0);

        window.ExpectedVisible = true;
        var shown = _windows.Show(window.Handle);
        if (!shown.IsSuccess)
            return shown;

        var placed = PlaceActive(window, force: true);
        if (!placed.IsSuccess)
            return placed;

        // El foco primero: activar la ventana la sube por encima de todo; después el marco se pone encima de ella.
        HideTaskbarButton(window);
        if (focus)
            _ = _windows.BringToForeground(window.Handle);

        _ = _windows.PlaceAbove(_frame.Handle, window.Handle);
        _frame.SetSlotHole(open: true);
        return Result.Success();
    }

    /// <summary>
    /// El grupo tiene un único botón en la barra de tareas: el del marco. Las inactivas no lo tienen (están ocultas) y el
    /// de la activa se quita; Explorer lo vuelve a añadir a veces, así que se reaplica en cada ocasión.
    /// </summary>
    private void HideTaskbarButton(ManagedWindow window) => _ = _windows.SetTaskbarButton(window.Handle, visible: false);

    /// <summary>El marco va con la ventana activa: si esta está en otro escritorio virtual, el marco se mueve allí.</summary>
    private void FollowActiveDesktop()
    {
        if (Active is not { ExpectedVisible: true } active || _desktops.GetDesktopId(active.Handle) is not { } desktop)
            return;

        if (_desktops.GetDesktopId(_frame.Handle) != desktop && _desktops.MoveOwnWindowToDesktop(_frame.Handle, desktop))
            LogFollowedDesktop(Id);
    }

    private Result HideWindow(ManagedWindow window)
    {
        window.ExpectedVisible = false;
        LogHiding(window.Id);
        return _windows.Hide(window.Handle);
    }

    private Result PlaceActive(ManagedWindow window, bool force)
    {
        var slot = TargetBounds(window);
        var sameSlot = _lastPlacement.Handle == window.Handle && _lastPlacement.Slot == slot;
        if (force || !sameSlot)
        {
            _lastPlacement = (window.Handle, slot, 0);
        }
        else
        {
            if (_lastPlacement.Corrections >= MaxCorrectionsPerSlot)
                return Result.Success();

            _lastPlacement = _lastPlacement with { Corrections = _lastPlacement.Corrections + 1 };
        }

        var placed = _windows.PlaceInSlot(window.Handle, slot);
        if (placed.IsSuccess)
            UpdateCaptionInset(window);

        return placed;
    }

    /// <summary>
    /// Rectángulo donde va el borde visible de la ventana: el hueco, ampliado hacia arriba lo que mide su barra de título
    /// nativa. Así el contenido ocupa el hueco y la barra nativa queda bajo la barra del marco, tapada (ADR 0011).
    /// </summary>
    private PixelRect TargetBounds(ManagedWindow window)
    {
        var slot = _frame.SlotBounds;
        var inset = window.CaptionInset ?? 0;
        return inset > 0 ? new PixelRect(slot.Left, slot.Top - inset, slot.Right, slot.Bottom) : slot;
    }

    /// <summary>
    /// Mide la barra nativa ya colocada la ventana (con su DPI definitivo). Si cambia, el marco muestra u oculta su barra;
    /// eso mueve el hueco y el SlotBoundsChanged posterior recoloca la ventana con la medida nueva.
    /// </summary>
    private void UpdateCaptionInset(ManagedWindow window)
    {
        var inset = Math.Max(0, _windows.GetCaptionInset(window.Handle));
        if (window.CaptionInset == inset)
            return;

        window.CaptionInset = inset;

        // Solo se coloca la ventana que se está mostrando, así que la barra del marco es la suya. Se recoloca ya con la medida nueva; si el marco mueve el hueco más tarde (layout), su aviso vuelve a colocarla.
        _frame.SetOwnCaptionVisible(inset > 0);
        _ = _windows.PlaceInSlot(window.Handle, TargetBounds(window));
    }

    /// <summary>
    /// Reacciona a cambios de la ventana activa: si el usuario la arrastra, el marco la sigue; si la maximiza, se
    /// maximiza el marco; si se movió por otra vía (Snap, teclado), vuelve al hueco.
    /// </summary>
    private void SyncWithActiveWindow(ManagedWindow window)
    {
        if (_frame.IsMinimized)
            return;

        var info = _windows.GetInfo(window.Handle);
        if (!info.IsSuccess)
            return;

        switch (info.Value.ShowState)
        {
            case WindowShowState.Minimized:
                return;
            case WindowShowState.Maximized:
                // Su botón maximizar (o doble clic en su barra) actúa sobre el grupo: alterna el marco. La ventana vuelve
                // a estado normal ya; el SlotBoundsChanged posterior la ajusta al hueco nuevo.
                if (_frame.IsMaximized)
                    _frame.Restore();
                else
                    _frame.Maximize();

                // Maximizar o restaurar el marco lo activa: el foco regresa a la ventana, donde el usuario acababa de
                // hacer clic, y el marco vuelve justo encima de ella.
                _ = PlaceActive(window, force: true);
                _ = _windows.BringToForeground(window.Handle);
                _ = _windows.PlaceAbove(_frame.Handle, window.Handle);
                return;
        }

        var bounds = info.Value.ExtendedFrameBounds;
        if (bounds == TargetBounds(window))
            return;

        if (_userMoving.Contains(window.Handle))
        {
            var inset = window.CaptionInset ?? 0;
            _frame.MoveSlotTo(new PixelRect(bounds.Left, bounds.Top + inset, bounds.Right, bounds.Bottom));
        }
        else if (LooksLikeSnap(bounds, _windows.GetWorkArea(window.Handle)))
        {
            // Snap Layouts (o Win+flechas) sobre la ventana activa: el grupo entero ocupa la zona elegida. El cambio de
            // tamaño del marco provoca SlotBoundsChanged, que encaja la ventana en el hueco nuevo.
            _frame.SetFrameBounds(bounds);
        }
        else
        {
            _ = PlaceActive(window, force: false);
        }
    }

    /// <summary>
    /// Una zona de Snap toca al menos dos bordes del área de trabajo. El reajuste por cambio de DPI conserva la esquina
    /// superior izquierda del hueco y no cumple esta condición, así que se corrige colocándola de nuevo.
    /// </summary>
    private static bool LooksLikeSnap(PixelRect bounds, PixelRect workArea)
    {
        if (workArea.Width <= 0 || bounds == workArea)
            return false;

        var touched = 0;
        touched += Math.Abs(bounds.Left - workArea.Left) <= EdgeTolerance ? 1 : 0;
        touched += Math.Abs(bounds.Top - workArea.Top) <= EdgeTolerance ? 1 : 0;
        touched += Math.Abs(bounds.Right - workArea.Right) <= EdgeTolerance ? 1 : 0;
        touched += Math.Abs(bounds.Bottom - workArea.Bottom) <= EdgeTolerance ? 1 : 0;
        return touched >= 2;
    }

    private void OnFrameSlotBoundsChanged(object? sender, EventArgs e)
    {
        if (Active is null || _frame.IsMinimized || _userMoving.Contains(Active.Handle))
            return;

        var placed = PlaceActive(Active, force: true);
        if (!placed.IsSuccess)
            LogPlaceFailed(Active.Id, placed.Error.Code, placed.Error.Win32Error);
    }

    private void OnFrameWindowStateChanged(object? sender, EventArgs e)
    {
        if (Active is null)
            return;

        var result = _frame.IsMinimized ? HideWindow(Active) : ShowInSlot(Active, focus: false);
        if (!result.IsSuccess)
            LogPlaceFailed(Active.Id, result.Error.Code, result.Error.Win32Error);
    }

    /// <summary>
    /// Mantiene la activa justo encima del marco. Si el marco se activó desde la barra de tareas o con Alt+Tab, el foco
    /// pasa a la ventana activa (donde el usuario quiere escribir).
    /// </summary>
    /// <summary>
    /// Regla del grupo: el marco siempre por encima de la ventana activa, que se ve por su hueco. Si algo mueve el marco
    /// por debajo de ella, se recoloca. Comprobar antes evita un bucle: recolocar el marco vuelve a avisar.
    /// </summary>
    private void OnFrameZOrderChanged(object? sender, EventArgs e)
    {
        if (Active is { ExpectedVisible: true } active && !_frame.IsMinimized && !_windows.IsAbove(_frame.Handle, active.Handle))
            _ = _windows.PlaceAbove(_frame.Handle, active.Handle);
    }

    private void OnFrameActivated(object? sender, FrameActivatedEventArgs e)
    {
        if (Active is not { ExpectedVisible: true } active)
            return;

        if (e.ForwardFocus)
            _ = _windows.BringToForeground(active.Handle);

        _ = _windows.PlaceAbove(_frame.Handle, active.Handle);
    }

    private Result RestoreWindow(ManagedWindow window)
    {
        _ = window.MoveTo(ManagedWindowState.Detaching);
        var restored = _windows.Restore(window.Snapshot);
        _ = window.MoveTo(ManagedWindowState.Detached);
        _tracker.Untrack(window.Handle);

        if (restored.IsSuccess)
            LogDetached(window.Id);
        else
            LogRestoreFailed(window.Id, restored.Error.Code, restored.Error.Win32Error);

        return restored;
    }

    private void OnGone(ManagedWindow window)
    {
        _ = window.MoveTo(ManagedWindowState.Gone);
        _tracker.Untrack(window.Handle);
        LogGone(window.Id);
        Remove(window);
    }

    /// <summary>Quita la ventana de las listas; si era la activa, activa la usada más recientemente.</summary>
    private void Remove(ManagedWindow window)
    {
        var wasActive = ReferenceEquals(window, Active);
        _items.Remove(window);
        _mostRecentFirst.Remove(window);
        _userMoving.Remove(window.Handle);

        if (wasActive)
        {
            Active = null;
            var next = _mostRecentFirst.Find(w => w.IsInGroup);
            if (next is not null && ShowInSlot(next, focus: false).IsSuccess)
            {
                SetActive(next);
            }
            else
            {
                _frame.SetSlotHole(open: false);
                ActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        WindowRemoved?.Invoke(this, new ManagedWindowEventArgs(window));
        if (_items.Count == 0)
            Emptied?.Invoke(this, EventArgs.Empty);
    }

    private void SetActive(ManagedWindow window)
    {
        Active = window;
        _mostRecentFirst.Remove(window);
        _mostRecentFirst.Insert(0, window);
        ActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateTitle(ManagedWindow window)
    {
        var info = _windows.GetInfo(window.Handle);
        if (!info.IsSuccess || string.Equals(info.Value.Title, window.Title, StringComparison.Ordinal))
            return;

        window.Title = info.Value.Title;
        WindowUpdated?.Invoke(this, new ManagedWindowEventArgs(window));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ventana {WindowId} capturada en el grupo {GroupId}")]
    private partial void LogAttached(Guid windowId, Guid groupId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ventana {WindowId}: HWND {Handle}")]
    private partial void LogAttachedHandle(Guid windowId, long handle);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ventana {WindowId} oculta (pestaña inactiva)")]
    private partial void LogHiding(Guid windowId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ventana {WindowId} activada en {ElapsedMs:F1} ms")]
    private partial void LogActivated(Guid windowId, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Windows no concedió el primer plano a la ventana {WindowId}")]
    private partial void LogForegroundDenied(Guid windowId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ventana {WindowId} restaurada y separada")]
    private partial void LogDetached(Guid windowId);

    [LoggerMessage(Level = LogLevel.Information, Message = "El marco del grupo {GroupId} se movió al escritorio virtual de su ventana activa")]
    private partial void LogFollowedDesktop(Guid groupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ventana {WindowId} cerrada")]
    private partial void LogGone(Guid windowId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "La ventana oculta {WindowId} se activa por un evento {EventKind} de otro proceso")]
    private partial void LogActivatedByEvent(Guid windowId, WindowEventKind eventKind);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Aviso de ventana mostrada atrasado ignorado: la ventana {WindowId} vuelve a estar oculta")]
    private partial void LogStaleShownIgnored(Guid windowId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Aviso de primer plano atrasado ignorado: la ventana {WindowId} ya no está en primer plano")]
    private partial void LogStaleForegroundIgnored(Guid windowId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Aviso de cierre ignorado: la ventana {WindowId} sigue existiendo")]
    private partial void LogDestroyIgnored(Guid windowId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Captura rechazada sin tocar la ventana: {Code} (Win32 {Win32Error})")]
    private partial void LogCaptureRejected(string code, int? win32Error);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo capturar la ventana {WindowId}: {Code} (Win32 {Win32Error}); se restauró")]
    private partial void LogAttachFailed(Guid windowId, string code, int? win32Error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo activar la ventana {WindowId}: {Code} (Win32 {Win32Error})")]
    private partial void LogActivateFailed(Guid windowId, string code, int? win32Error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo ocultar la ventana {WindowId}: {Code} (Win32 {Win32Error})")]
    private partial void LogHideFailed(Guid windowId, string code, int? win32Error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo colocar la ventana {WindowId}: {Code} (Win32 {Win32Error})")]
    private partial void LogPlaceFailed(Guid windowId, string code, int? win32Error);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo restaurar la ventana {WindowId}: {Code} (Win32 {Win32Error})")]
    private partial void LogRestoreFailed(Guid windowId, string code, int? win32Error);
}
