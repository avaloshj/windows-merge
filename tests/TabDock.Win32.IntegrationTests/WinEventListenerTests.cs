using System.Threading.Channels;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using TabDock.Core.Abstractions;

namespace TabDock.Win32.IntegrationTests;

/// <summary>
/// Los hooks <c>WINEVENT_OUTOFCONTEXT</c> entregan los eventos en el hilo que los instaló, que necesita un bucle de
/// mensajes: se usa un hilo STA con <see cref="Dispatcher"/>, como el hilo de UI de la aplicación.
/// </summary>
[Trait("Category", "Integration")]
public sealed class WinEventListenerTests : IDisposable
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    private readonly WindowSystem _windows = new(NullLogger<WindowSystem>.Instance, TimeProvider.System);
    private readonly Channel<WindowEvent> _received = Channel.CreateUnbounded<WindowEvent>();
    private readonly Dispatcher _dispatcher;
    private readonly WinEventListener _listener = new();

    public WinEventListenerTests()
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait();
        _dispatcher = dispatcher!;

        _dispatcher.Invoke(() =>
        {
            _listener.Start();
            _ = Forward();
        });
    }

    public void Dispose()
    {
        _dispatcher.Invoke(_listener.Dispose);
        _dispatcher.InvokeShutdown();
    }

    [Fact]
    public async Task Title_change_is_reported()
    {
        using var target = TestTargetProcess.Start("--title", $"proyecto-alfa-{Guid.NewGuid():N}", "--retitle-after", "1500");
        var window = await target.WaitForWindowAsync(_windows, _ => true);

        await WaitForAsync(WindowEventKind.NameChanged, window.Handle);
    }

    [Fact]
    public async Task Hide_and_show_are_reported()
    {
        using var target = TestTargetProcess.Start("--title", $"proyecto-beta-{Guid.NewGuid():N}");
        var window = await target.WaitForWindowAsync(_windows, _ => true);

        _windows.Hide(window.Handle);
        await WaitForAsync(WindowEventKind.Hidden, window.Handle);

        _windows.Show(window.Handle);
        await WaitForAsync(WindowEventKind.Shown, window.Handle);
    }

    [Fact]
    public async Task Destruction_is_reported()
    {
        var target = TestTargetProcess.Start("--title", $"demo-api-{Guid.NewGuid():N}");
        var window = await target.WaitForWindowAsync(_windows, _ => true);

        target.Dispose();

        await WaitForAsync(WindowEventKind.Destroyed, window.Handle);
    }

    [Fact]
    public void Every_listened_event_maps_to_a_kind()
    {
        uint[] ids = [0x0003, 0x000A, 0x000B, 0x0016, 0x0017, 0x8000, 0x8001, 0x8002, 0x8003, 0x800B, 0x800C, 0x8017, 0x8018];

        ids.Select(WinEventListener.Map).ShouldAllBe(kind => kind != null);
        WinEventListener.Map(0x8005).ShouldBeNull();   // EVENT_OBJECT_FOCUS: no se escucha
    }

    private async Task Forward()
    {
        await foreach (var windowEvent in _listener.ReadAllAsync(CancellationToken.None))
            await _received.Writer.WriteAsync(windowEvent);
    }

    private async Task WaitForAsync(WindowEventKind kind, WindowHandle handle)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(s_timeout);
        while (true)
        {
            var windowEvent = await _received.Reader.ReadAsync(timeout.Token);
            if (windowEvent.Kind == kind && windowEvent.Handle == handle)
                return;
        }
    }
}
