using System.Diagnostics;
using System.IO;
using TabDock.Core.Abstractions;

namespace TabDock.Win32.IntegrationTests;

/// <summary>Lanza <c>TabDock.TestTarget.exe</c> y lo termina al liberar.</summary>
internal sealed class TestTargetProcess : IDisposable
{
    private static readonly TimeSpan s_windowTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromMilliseconds(100);

    private TestTargetProcess(Process process) => Process = process;

    public Process Process { get; }

    public static TestTargetProcess Start(params string[] arguments)
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "TabDock.TestTarget.exe");
        File.Exists(exe).ShouldBeTrue($"No se encontró {exe}; compila la solución completa.");

        var startInfo = new ProcessStartInfo(exe) { UseShellExecute = false };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return new TestTargetProcess(Process.Start(startInfo) ?? throw new InvalidOperationException("No se pudo iniciar TestTarget."));
    }

    /// <summary>Espera a que aparezca una ventana visible del proceso que cumpla el predicado.</summary>
    /// <remarks>
    /// WPF crea la ventana y le asigna el título antes de mostrarla: sin exigir visibilidad se capturaría
    /// en estado <c>NotVisible</c>. Es el mismo motivo por el que el auto-merge aplica debounce.
    /// </remarks>
    public async Task<WindowInfo> WaitForWindowAsync(IWindowSystem windowSystem, Func<WindowInfo, bool> predicate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < s_windowTimeout)
        {
            var match = windowSystem.EnumerateTopLevel().FirstOrDefault(w => w.ProcessId == Process.Id && w.IsVisible && predicate(w));
            if (match is not null)
                return match;

            await Task.Delay(s_pollInterval, cancellationToken);
        }

        throw new TimeoutException($"TestTarget no mostró la ventana esperada en {s_windowTimeout.TotalSeconds} s.");
    }

    public void Dispose()
    {
        if (!Process.HasExited)
        {
            Process.Kill(entireProcessTree: true);
            Process.WaitForExit(5_000);
        }

        Process.Dispose();
    }
}
