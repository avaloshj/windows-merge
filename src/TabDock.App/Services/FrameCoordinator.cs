using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TabDock.App.Localization;
using TabDock.App.ViewModels;
using TabDock.App.Views;
using TabDock.Core;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;
using TabDock.Core.Services;
using TabDock.Core.TitleParsing;

namespace TabDock.App.Services;

/// <summary>
/// Crea los marcos y sus sesiones, enlaza cada sesión con su ViewModel, reparte los eventos de ventana y ejecuta los
/// comandos globales ("Merge All Windows", separar, cambiar de pestaña) y el agrupado automático.
/// Se ejecuta íntegramente en el hilo de UI; los eventos del sistema llegan por un canal y se procesan en orden.
/// </summary>
public sealed partial class FrameCoordinator(
    IServiceProvider services,
    IWindowSystem windows,
    IWindowEvents events,
    IVirtualDesktops desktops,
    EligibilityPolicy eligibility,
    GroupingService grouping,
    SettingsService settings,
    TabPreferenceService preferences,
    RuntimeStateTracker tracker,
    SnapshotRestorer restorer,
    IRuntimeStateStore store,
    TimeProvider time,
    ILoggerFactory loggerFactory,
    ILogger<FrameCoordinator> logger) : IDisposable
{
    private static readonly TimeSpan s_debounceTick = TimeSpan.FromMilliseconds(50);

    private readonly List<GroupBinding> _groups = [];
    private readonly CancellationTokenSource _stopping = new();
    private readonly AutoMergeDebouncer _debouncer = new(time);
    private DispatcherTimer? _healthTimer;
    private DispatcherTimer? _debounceTimer;
    private GroupBinding? _lastActiveGroup;
    private bool _started;
    private bool _disposed;

    /// <summary>Mensaje para el usuario (lo muestra la bandeja), p. ej. ventanas elevadas que no se pueden agrupar.</summary>
    public event EventHandler<NoticeEventArgs>? Notice;

    /// <summary>Se ha creado o cerrado un grupo (la introducción lo usa para confirmar el primer agrupado).</summary>
    public event EventHandler? GroupsChanged;

    public int GroupCount => _groups.Count;

    private TabPreferenceService Preferences => preferences;

    /// <summary>En pausa no se agrupan ventanas nuevas ni se atienden atajos; los grupos existentes siguen funcionando.</summary>
    public bool IsPaused { get; set; }

    /// <summary>Instala los hooks y empieza a consumir eventos. Debe llamarse desde el hilo de UI.</summary>
    public void Start()
    {
        if (_started)
            return;

        _started = true;
        events.Start();
        _ = PumpAsync(_stopping.Token);

        _healthTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(settings.Current.User.HealthCheckSeconds) };
        _healthTimer.Tick += (_, _) =>
        {
            foreach (var group in _groups.ToList())
                group.Session.CheckHealth();
        };
        _healthTimer.Start();

        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = s_debounceTick };
        _debounceTimer.Tick += (_, _) => ProcessNewWindows();
    }

    /// <summary>Intervalo de la comprobación periódica de salud (Ajustes → Avanzado).</summary>
    public void SetHealthInterval(TimeSpan interval)
    {
        if (_healthTimer is not null)
            _healthTimer.Interval = interval;
    }

    /// <summary>"Merge All Windows" de una regla: crea su marco o añade al existente las ventanas que falten.</summary>
    public Result<int> MergeAll(AppRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        // UIPI impide gestionar ventanas de procesos elevados: no se intenta forzar, solo se avisa.
        var blocked = grouping.CountBlocked(rule);
        if (blocked > 0)
            Notice?.Invoke(this, new NoticeEventArgs(TextResources.Format("Notice_ElevatedWindows", blocked, rule.DisplayName)));

        var candidates = grouping.FindMergeCandidates(rule).Where(w => !IsManaged(w.Handle)).ToList();
        if (_groups.Find(g => g.Rule?.Id == rule.Id) is { } existing)
        {
            var added = candidates.Count(w => existing.Session.Attach(w).IsSuccess);
            LogMerged(rule.Id, added);
            return Result.Success(added);
        }

        return CaptureIntoNewFrame(rule.DisplayName, candidates, rule);
    }

    /// <summary>Ctrl+Alt+M: agrupa el programa de la ventana en primer plano o, si no tiene regla, la primera regla activa.</summary>
    public Result<int> MergeForeground()
    {
        var foreground = windows.EnumerateTopLevel().FirstOrDefault(w => w.IsVisible && !w.IsCloaked);
        var rule = (foreground is null ? null : grouping.RuleFor(foreground.Handle))
            ?? settings.Current.Rules.FirstOrDefault(r => r.Enabled);

        return rule is null
            ? Result.Fail<int>(new DomainError("capture.noRule", "Error_NothingToCapture"))
            : MergeAll(rule);
    }

    /// <summary>Captura las ventanas elegibles de un ejecutable en un marco nuevo (opción <c>--merge-exe</c>).</summary>
    public Result<int> CaptureExecutable(string executableName)
    {
        var candidates = windows.EnumerateTopLevel()
            .Where(w => string.Equals(w.ExecutableName, executableName, StringComparison.OrdinalIgnoreCase) && eligibility.Evaluate(w).IsEligible)
            .ToList();

        return CaptureIntoNewFrame(executableName, candidates, rule: null);
    }

    /// <summary>Crea un marco y captura en él las ventanas indicadas que sigan siendo elegibles (selección manual).</summary>
    public Result<int> CaptureIntoNewFrame(string title, IReadOnlyCollection<WindowHandle> handles)
    {
        ArgumentNullException.ThrowIfNull(handles);

        var candidates = handles
            .Select(windows.GetInfo)
            .Where(r => r.IsSuccess && eligibility.Evaluate(r.Value).IsEligible)
            .Select(r => r.Value!)
            .ToList();

        return CaptureIntoNewFrame(title, candidates, rule: null);
    }

    /// <summary>Separa todas las ventanas de todos los grupos (Ctrl+Alt+U, salir, cierre de sesión de Windows).</summary>
    public void DetachAll()
    {
        foreach (var group in _groups.ToList())
            group.Close();
    }

    public void Execute(HotkeyCommand command)
    {
        if (IsPaused)
            return;

        LogHotkey(command);
        switch (command)
        {
            case HotkeyCommand.MergeAll:
                _ = MergeForeground();
                break;
            case HotkeyCommand.UnmergeAll:
                DetachAll();
                break;
            case >= HotkeyCommand.GoToTab1 and <= HotkeyCommand.GoToTab9:
                var position = (command - HotkeyCommand.GoToTab1 + 1).ToString(CultureInfo.InvariantCulture);
                CurrentGroup()?.ViewModel.SelectTabAtCommand.Execute(position);
                break;
            case HotkeyCommand.NextTab:
                CurrentGroup()?.ViewModel.SelectRelative(+1);
                break;
            case HotkeyCommand.PreviousTab:
                CurrentGroup()?.ViewModel.SelectRelative(-1);
                break;
            case HotkeyCommand.ToggleSidebar:
                CurrentGroup()?.ViewModel.ToggleSidebarCommand.Execute(null);
                break;
            case HotkeyCommand.QuickSearch:
                ShowQuickSearch();
                break;
        }
    }

    /// <summary>Búsqueda rápida sobre las pestañas de todos los grupos, centrada en el grupo actual.</summary>
    public void ShowQuickSearch()
    {
        var items = _groups
            .SelectMany(g => g.ViewModel.Tabs.Where(t => !t.IsClosing).Select(t => new QuickSearchItem(t.Id, g.Session.Id, t.DisplayName, t.Subtitle, g.ViewModel.Title, t.ColorHex)))
            .ToList();
        if (items.Count == 0)
            return;

        var palette = new QuickSearchWindow(new QuickSearchViewModel(items), ChooseFromSearch);
        var anchor = CurrentGroup()?.Frame;
        var area = anchor is null ? SystemParameters.WorkArea : new Rect(anchor.Left, anchor.Top, anchor.ActualWidth, anchor.ActualHeight);
        palette.Left = area.Left + Math.Max(0, (area.Width - palette.Width) / 2);
        palette.Top = area.Top + Math.Min(120, area.Height / 6);
        palette.Show();
        _ = palette.Activate();
    }

    /// <summary>
    /// Último recurso ante una excepción no controlada: restaura desde los snapshots sin tocar la UI.
    /// Nunca lanza.
    /// </summary>
    public void EmergencyRestore()
    {
        try
        {
            var report = restorer.RestoreAll(tracker.Records.Select(r => r.Snapshot).ToList());
            store.Save(RuntimeState.Empty);
            LogEmergencyRestore(report.Restored, report.Pending);
        }
#pragma warning disable CA1031 // En un cierre de emergencia ninguna excepción debe impedir seguir; el guardián es la red siguiente.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogEmergencyRestoreFailed(ex);
        }
    }

    /// <summary>Idempotente: lo invoca el contenedor de DI al destruir el host.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _healthTimer?.Stop();
        _debounceTimer?.Stop();
        _stopping.Cancel();
        _stopping.Dispose();
        events.Dispose();
    }

    private Result<int> CaptureIntoNewFrame(string title, IEnumerable<WindowInfo> windowsToCapture, AppRule? rule)
    {
        var candidates = windowsToCapture.Where(w => !IsManaged(w.Handle)).ToList();
        if (candidates.Count == 0)
            return Result.Fail<int>(new DomainError("capture.nothingEligible", "Error_NothingToCapture"));

        var frame = services.GetRequiredService<HostFrameWindow>();
        frame.ViewModel.Title = title;
        frame.Show();
        frame.UpdateLayout();

        var session = new TabGroupSession(windows, desktops, frame, tracker, time, loggerFactory.CreateLogger<TabGroupSession>());
        var group = new GroupBinding(this, session, frame, rule);
        _groups.Add(group);
        _lastActiveGroup = group;
        RefreshGroupReferences();

        var captured = candidates.Count(info => session.Attach(info).IsSuccess);
        LogFrameCreated(session.Id, captured, candidates.Count);
        if (captured == 0)
        {
            group.Close();
            return Result.Fail<int>(new DomainError("capture.failed", "Error_WindowOperationFailed"));
        }

        return Result.Success(captured);
    }

    private bool IsManaged(WindowHandle handle) => _groups.Exists(g => g.Session.Owns(handle));

    private void ChooseFromSearch(QuickSearchItem item)
    {
        if (_groups.Find(g => g.Session.Id == item.GroupId) is not { } group
            || group.ViewModel.Tabs.FirstOrDefault(t => t.Id == item.TabId) is not { } tab)
        {
            return;
        }

        _lastActiveGroup = group;
        if (ReferenceEquals(group.ViewModel.SelectedTab, tab))
            _ = group.Session.Activate(tab.Id, focus: true);
        else
            group.ViewModel.SelectedTab = tab;
    }

    /// <summary>"Mover a otro grupo": se separa del origen (vuelve a su sitio) y se captura en el destino como activa.</summary>
    private void MoveWindow(GroupBinding source, Guid windowId, Guid targetGroupId)
    {
        var target = _groups.Find(g => g.Session.Id == targetGroupId);
        var window = source.Session.Windows.FirstOrDefault(w => w.Id == windowId);
        if (target is null || window is null || ReferenceEquals(target, source))
            return;

        var handle = window.Handle;
        if (!source.Session.Detach(windowId).IsSuccess)
            return;

        var info = windows.GetInfo(handle);
        if (info.IsSuccess && eligibility.Evaluate(info.Value).IsEligible)
            _ = target.Session.Attach(info.Value, activate: true);
    }

    /// <summary>Cada marco conoce los demás grupos abiertos (destinos de "Mover a otro grupo").</summary>
    private void RefreshGroupReferences()
    {
        foreach (var group in _groups)
        {
            group.ViewModel.OtherGroups.Clear();
            foreach (var other in _groups.Where(g => !ReferenceEquals(g, group)))
                group.ViewModel.OtherGroups.Add(new GroupReference(other.Session.Id, other.ViewModel.Title));
        }

        GroupsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Grupo de la ventana en primer plano (marco o ventana gestionada); si no, el último usado.</summary>
    private GroupBinding? CurrentGroup()
    {
        var foreground = windows.EnumerateTopLevel().FirstOrDefault(w => w.IsVisible && !w.IsCloaked);
        if (foreground is not null && _groups.Find(g => g.FrameHandle == foreground.Handle || g.Session.Owns(foreground.Handle)) is { } owner)
            return owner;

        return _lastActiveGroup is not null && _groups.Contains(_lastActiveGroup) ? _lastActiveGroup : _groups.LastOrDefault();
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var windowEvent in events.ReadAllAsync(cancellationToken).ConfigureAwait(true))
                Dispatch(windowEvent);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cierre normal.
        }
    }

    private void Dispatch(WindowEvent windowEvent)
    {
        if (_groups.Find(g => g.Session.Owns(windowEvent.Handle)) is { } owner)
        {
            owner.Session.HandleEvent(windowEvent);
            if (windowEvent.Kind == WindowEventKind.Foreground)
                _lastActiveGroup = owner;

            return;
        }

        switch (windowEvent.Kind)
        {
            // Una ventana nueva llega antes de tener título y tamaño definitivos: se espera a que se estabilice.
            case WindowEventKind.Created or WindowEventKind.Shown or WindowEventKind.NameChanged
                when !IsPaused && _groups.Exists(g => g.Rule is { AutoMerge: true }):
                _debouncer.Notify(windowEvent.Handle);
                _debounceTimer?.Start();
                break;
            case WindowEventKind.Destroyed:
                _debouncer.Forget(windowEvent.Handle);
                break;
        }
    }

    /// <summary>Ventanas nuevas tras la espera: se añaden como activas al grupo de su regla, si existe.</summary>
    private void ProcessNewWindows()
    {
        foreach (var handle in _debouncer.TakeDue())
        {
            if (IsPaused || IsManaged(handle) || grouping.EvaluateNewWindow(handle) is not { } match)
                continue;

            if (_groups.Find(g => g.Rule?.Id == match.Rule.Id) is { } group && group.Session.Attach(match.Window, activate: true).IsSuccess)
                LogAutoMerged(match.Rule.Id);
        }

        if (!_debouncer.HasPending)
            _debounceTimer?.Stop();
    }

    private void Forget(GroupBinding group)
    {
        _groups.Remove(group);
        if (ReferenceEquals(_lastActiveGroup, group))
            _lastActiveGroup = _groups.LastOrDefault();

        RefreshGroupReferences();
    }

    private void Touch(GroupBinding group) => _lastActiveGroup = group;

    [LoggerMessage(Level = LogLevel.Information, Message = "Marco {GroupId} creado con {Captured} de {Candidates} ventanas")]
    private partial void LogFrameCreated(Guid groupId, int captured, int candidates);

    [LoggerMessage(Level = LogLevel.Information, Message = "Merge All de la regla {RuleId}: {Added} ventanas añadidas al grupo existente")]
    private partial void LogMerged(Guid ruleId, int added);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ventana nueva añadida automáticamente al grupo de la regla {RuleId}")]
    private partial void LogAutoMerged(Guid ruleId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Atajo {Command}")]
    private partial void LogHotkey(HotkeyCommand command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Restauración de emergencia: {Restored} de {Pending} ventanas")]
    private partial void LogEmergencyRestore(int restored, int pending);

    [LoggerMessage(Level = LogLevel.Critical, Message = "La restauración de emergencia falló; el guardián la reintentará")]
    private partial void LogEmergencyRestoreFailed(Exception exception);

    /// <summary>Enlace entre una sesión del dominio, su marco y las pestañas del ViewModel.</summary>
    private sealed class GroupBinding
    {
        private readonly FrameCoordinator _owner;
        private readonly HostFrameWindow _frame;
        private readonly ITitleParser _parser;
        private readonly Dictionary<Guid, TabViewModel> _tabs = [];
        private readonly Dictionary<Guid, int> _emptyWindowNumbers = [];
        private bool _applyingSessionState;
        private bool _closing;

        public GroupBinding(FrameCoordinator owner, TabGroupSession session, HostFrameWindow frame, AppRule? rule)
        {
            _owner = owner;
            Session = session;
            _frame = frame;
            Rule = rule;
            ViewModel = frame.ViewModel;
            _parser = TitleParserFactory.For(rule?.TitleParser ?? TitleParserKind.Generic);

            Session.WindowAdded += OnWindowAdded;
            Session.WindowUpdated += OnWindowUpdated;
            Session.WindowRemoved += OnWindowRemoved;
            Session.ActiveChanged += OnActiveChanged;
            Session.Emptied += (_, _) => Close(frameIsClosing: false);
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            ViewModel.SelectedTabReactivated += OnSelectedTabReactivated;
            ViewModel.TabCloseRequested += (_, e) => _ = Session.RequestClose(e.Tab.Id);
            ViewModel.TabActionRequested += OnTabAction;
            ViewModel.WindowResolver = tab => Session.Windows.FirstOrDefault(w => w.Id == tab.Id)?.Handle;
            _frame.Closing += OnFrameClosing;
            _frame.Activated += (_, _) => _owner.Touch(this);
        }

        public TabGroupSession Session { get; }

        public AppRule? Rule { get; }

        public HostFrameViewModel ViewModel { get; }

        public HostFrameWindow Frame => _frame;

        public WindowHandle FrameHandle => ((IHostFrame)_frame).Handle;

        /// <summary>Clave de las preferencias: la regla del grupo, o <see cref="Guid.Empty"/> en grupos manuales.</summary>
        private Guid PreferenceKey => Rule?.Id ?? Guid.Empty;

        /// <summary>Restaura todas las ventanas y cierra el marco. Idempotente.</summary>
        public void Close() => Close(frameIsClosing: false);

        private void Close(bool frameIsClosing)
        {
            if (_closing)
                return;

            _closing = true;
            Session.DetachAll();
            Session.Dispose();
            _owner.Forget(this);

            // Dentro del evento Closing, WPF no permite volver a llamar a Close().
            if (!frameIsClosing)
                _frame.Close();
        }

        private void OnFrameClosing(object? sender, CancelEventArgs e) => Close(frameIsClosing: true);

        /// <summary>Nueva pestaña con sus preferencias guardadas (alias, color, fijada y posición de la última sesión).</summary>
        private void OnWindowAdded(object? sender, ManagedWindowEventArgs e)
        {
            var tab = new TabViewModel(e.Window.Id, string.Empty, string.Empty);
            Apply(tab, e.Window, loadPreferences: true);
            _tabs[e.Window.Id] = tab;
            ViewModel.InsertTab(tab);
            _owner.Preferences.Touch(PreferenceKey, tab.ProjectName);
        }

        private void OnWindowUpdated(object? sender, ManagedWindowEventArgs e)
        {
            if (_tabs.TryGetValue(e.Window.Id, out var tab))
                Apply(tab, e.Window, loadPreferences: false);
        }

        private void OnWindowRemoved(object? sender, ManagedWindowEventArgs e)
        {
            _emptyWindowNumbers.Remove(e.Window.Id);
            if (_tabs.Remove(e.Window.Id, out var tab))
                ViewModel.BeginRemoveTab(tab);
        }

        /// <summary>
        /// Nombre de proyecto, archivo activo, cambios sin guardar, entorno remoto y color. Las preferencias se cargan al
        /// añadir la pestaña y cuando cambia el proyecto (p. ej. una ventana vacía abre una carpeta).
        /// </summary>
        private void Apply(TabViewModel tab, ManagedWindow window, bool loadPreferences)
        {
            var parsed = _parser.Parse(window.Title);
            var projectChanged = !string.Equals(tab.ProjectName, parsed.ProjectName, StringComparison.Ordinal);
            tab.ProjectName = parsed.ProjectName;
            tab.BaseName = parsed.IsEmptyWindow
                ? TextResources.Format("Tab_EmptyWindow", EmptyWindowNumber(window.Id))
                : parsed.ProjectName;

            if (loadPreferences || (projectChanged && !parsed.IsEmptyWindow))
            {
                var preference = _owner.Preferences.Find(PreferenceKey, parsed.ProjectName);
                tab.Alias = preference?.Alias;
                tab.ColorOverride = preference?.ColorHex;
                if (loadPreferences)
                {
                    tab.IsPinned = preference?.Pinned ?? false;
                    tab.SavedOrder = preference?.Order;
                }
            }

            tab.Subtitle = parsed.ActiveFile ?? string.Empty;
            tab.IsDirty = parsed.IsDirty;
            tab.RemoteKind = parsed.RemoteKind;
            tab.IsUnresponsive = window.State == ManagedWindowState.Unresponsive;
            Refresh(tab, window.Handle);
        }

        private static void Refresh(TabViewModel tab, WindowHandle handle)
        {
            tab.DisplayName = tab.Alias ?? tab.BaseName;
            tab.ColorHex = tab.ColorOverride
                ?? ProjectColorPalette.ColorFor(tab.ProjectName.Length == 0 ? handle.ToString() : tab.ProjectName);
        }

        /// <summary>Acciones del usuario: se aplican a la pestaña y se guardan por proyecto.</summary>
        private void OnTabAction(object? sender, TabActionEventArgs e)
        {
            var tab = e.Tab;
            var window = Session.Windows.FirstOrDefault(w => w.Id == tab.Id);
            var prefs = _owner.Preferences;
            switch (e.Action)
            {
                case TabAction.Rename:
                    tab.Alias = e.Value as string;
                    prefs.Update(PreferenceKey, tab.ProjectName, p => p with { Alias = tab.Alias });
                    break;
                case TabAction.ChangeColor:
                    tab.ColorOverride = e.Value as string;
                    prefs.Update(PreferenceKey, tab.ProjectName, p => p with { ColorHex = tab.ColorOverride });
                    break;
                case TabAction.TogglePin:
                    prefs.Update(PreferenceKey, tab.ProjectName, p => p with { Pinned = tab.IsPinned });
                    break;
                case TabAction.Reorder:
                    prefs.SetOrder(PreferenceKey, [.. ViewModel.Tabs.Select(t => t.ProjectName)]);
                    break;
                case TabAction.Detach when window is not null:
                    _ = Session.Detach(tab.Id, DropBounds(window, e.Value as (int X, int Y)?));
                    return;
                case TabAction.MoveToGroup when e.Value is Guid target:
                    _owner.MoveWindow(this, tab.Id, target);
                    return;
            }

            if (window is not null)
                Refresh(tab, window.Handle);
        }

        /// <summary>Al soltar fuera de la barra, la ventana queda con su tamaño original y la barra bajo el cursor.</summary>
        private static PixelRect? DropBounds(ManagedWindow window, (int X, int Y)? point)
        {
            if (point is not { } p)
                return null;

            var size = window.Snapshot.ActualBounds;
            var left = p.X - Math.Min(160, size.Width / 3);
            var top = p.Y - 16;
            return new PixelRect(left, top, left + size.Width, top + size.Height);
        }

        /// <summary>Numeración estable de ventanas vacías dentro del grupo: 1, 2, 3…</summary>
        private int EmptyWindowNumber(Guid windowId)
        {
            if (!_emptyWindowNumbers.TryGetValue(windowId, out var number))
            {
                number = Enumerable.Range(1, _emptyWindowNumbers.Count + 1).First(n => !_emptyWindowNumbers.ContainsValue(n));
                _emptyWindowNumbers[windowId] = number;
            }

            return number;
        }

        private void OnActiveChanged(object? sender, EventArgs e)
        {
            _applyingSessionState = true;
            try
            {
                ViewModel.SelectedTab = Session.Active is { } active && _tabs.TryGetValue(active.Id, out var tab) ? tab : null;
            }
            finally
            {
                _applyingSessionState = false;
            }
        }

        /// <summary>Clic en la pestaña activa: su ventana vuelve encima del marco y recibe el foco.</summary>
        private void OnSelectedTabReactivated(object? sender, EventArgs e)
        {
            if (Session.Active is { } active)
            {
                _owner.Touch(this);
                _ = Session.Activate(active.Id, focus: true);
            }
        }

        /// <summary>El usuario eligió otra pestaña (clic, teclado, atajo o búsqueda): se activa su ventana.</summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_applyingSessionState || !string.Equals(e.PropertyName, nameof(HostFrameViewModel.SelectedTab), StringComparison.Ordinal))
                return;

            if (ViewModel.SelectedTab is { IsClosing: false } tab && tab.Id != Session.Active?.Id)
            {
                _owner.Touch(this);
                _ = Session.Activate(tab.Id, focus: true);
            }
        }
    }
}
