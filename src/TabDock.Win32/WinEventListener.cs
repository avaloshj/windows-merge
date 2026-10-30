using System.Threading.Channels;
using TabDock.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TabDock.Win32;

/// <summary>
/// Escucha eventos de ventana con <c>SetWinEventHook(WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)</c>: sin inyectar
/// código en otros procesos. El callback solo filtra y encola; el consumo es secuencial y fuera del callback.
/// </summary>
public sealed class WinEventListener : IWindowEvents
{
    /// <summary>Rangos de eventos (mínimo, máximo) que se escuchan; estrechos para no recibir ruido.</summary>
    private static readonly (uint Min, uint Max)[] s_ranges =
    [
        (PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND),
        (PInvoke.EVENT_SYSTEM_MOVESIZESTART, PInvoke.EVENT_SYSTEM_MOVESIZEEND),
        (PInvoke.EVENT_SYSTEM_MINIMIZESTART, PInvoke.EVENT_SYSTEM_MINIMIZEEND),
        (PInvoke.EVENT_OBJECT_CREATE, PInvoke.EVENT_OBJECT_HIDE),
        (PInvoke.EVENT_OBJECT_LOCATIONCHANGE, PInvoke.EVENT_OBJECT_NAMECHANGE),
        (PInvoke.EVENT_OBJECT_CLOAKED, PInvoke.EVENT_OBJECT_UNCLOAKED),
    ];

    private readonly Channel<WindowEvent> _channel = Channel.CreateUnbounded<WindowEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    // El delegado debe vivir tanto como los hooks: si el GC lo recolecta, Windows llamaría a memoria liberada.
    private readonly WINEVENTPROC _callback;
    private readonly List<UnhookWinEventSafeHandle> _hooks = [];
    private bool _disposed;

    public WinEventListener()
    {
        _callback = OnWinEvent;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hooks.Count > 0)
            return;

        foreach (var (min, max) in s_ranges)
        {
            var hook = PInvoke.SetWinEventHook(min, max, default, _callback, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
            if (hook.IsInvalid)
                throw new InvalidOperationException($"SetWinEventHook falló para el rango 0x{min:X}–0x{max:X}.");

            _hooks.Add(hook);
        }
    }

    public IAsyncEnumerable<WindowEvent> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        // Cada SafeHandle llama a UnhookWinEvent al liberarse.
        foreach (var hook in _hooks)
            hook.Dispose();

        _hooks.Clear();
        _channel.Writer.TryComplete();
    }

    internal static WindowEventKind? Map(uint eventId) => eventId switch
    {
        PInvoke.EVENT_SYSTEM_FOREGROUND => WindowEventKind.Foreground,
        PInvoke.EVENT_SYSTEM_MOVESIZESTART => WindowEventKind.MoveSizeStart,
        PInvoke.EVENT_SYSTEM_MOVESIZEEND => WindowEventKind.MoveSizeEnd,
        PInvoke.EVENT_SYSTEM_MINIMIZESTART => WindowEventKind.MinimizeStart,
        PInvoke.EVENT_SYSTEM_MINIMIZEEND => WindowEventKind.MinimizeEnd,
        PInvoke.EVENT_OBJECT_CREATE => WindowEventKind.Created,
        PInvoke.EVENT_OBJECT_DESTROY => WindowEventKind.Destroyed,
        PInvoke.EVENT_OBJECT_SHOW => WindowEventKind.Shown,
        PInvoke.EVENT_OBJECT_HIDE => WindowEventKind.Hidden,
        PInvoke.EVENT_OBJECT_LOCATIONCHANGE => WindowEventKind.LocationChanged,
        PInvoke.EVENT_OBJECT_NAMECHANGE => WindowEventKind.NameChanged,
        PInvoke.EVENT_OBJECT_CLOAKED => WindowEventKind.Cloaked,
        PInvoke.EVENT_OBJECT_UNCLOAKED => WindowEventKind.Uncloaked,
        _ => null,
    };

    private unsafe void OnWinEvent(HWINEVENTHOOK hook, uint eventId, HWND hwnd, int idObject, int idChild, uint threadId, uint timestamp)
    {
        if (idObject != (int)OBJECT_IDENTIFIER.OBJID_WINDOW || idChild != (int)PInvoke.CHILDID_SELF || hwnd.IsNull)
            return;

        if (Map(eventId) is not { } kind)
            return;

        // Solo ventanas de primer nivel. Una ventana destruida ya no tiene ancestro: su evento pasa siempre.
        if (kind != WindowEventKind.Destroyed && PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) != hwnd)
            return;

        _ = _channel.Writer.TryWrite(new WindowEvent(kind, new WindowHandle((nint)hwnd.Value)));
    }
}
