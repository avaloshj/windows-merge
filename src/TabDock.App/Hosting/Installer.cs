using Microsoft.Extensions.Logging;
using Serilog.Events;
using Serilog.Extensions.Logging;
using TabDock.Core.Services;
using TabDock.Infrastructure;
using TabDock.Infrastructure.Logging;
using TabDock.Infrastructure.Persistence;
using TabDock.Win32;
using TabDock.Win32.Shell;
using Velopack;

namespace TabDock.App.Hosting;

/// <summary>
/// Integración con el instalador (Velopack). Debe ejecutarse lo primero: cuando el instalador lanza TabDock para un
/// aviso (instalar, desinstalar…), <see cref="VelopackApp.Run"/> lo atiende y termina el proceso.
/// TabDock no busca actualizaciones: no usa red. Un comprobador (desactivable y solo HTTPS) se añadiría con
/// <c>UpdateManager</c> y un feed configurado.
/// </summary>
internal static class Installer
{
    public static void Run() =>
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => OnBeforeUninstall())
            .Run();

    /// <summary>
    /// Garantía de restauración también al desinstalar: se devuelven las ventanas que quedaran pendientes en
    /// <c>state.json</c> y se quita el inicio con Windows (la clave <c>Run</c> apuntaría a un ejecutable borrado).
    /// </summary>
    private static void OnBeforeUninstall()
    {
        var paths = AppPaths.CreateDefault();
        paths.EnsureCreated();
        using var serilog = TabDockLogging.CreateLogger(paths, LogEventLevel.Information, "installer-.log");
        using var loggerFactory = new SerilogLoggerFactory(serilog);

        var store = new JsonRuntimeStateStore(paths, TimeProvider.System, loggerFactory.CreateLogger<JsonRuntimeStateStore>());
        var windows = new WindowSystem(loggerFactory.CreateLogger<WindowSystem>(), TimeProvider.System);
        _ = new SnapshotRestorer(windows, store, loggerFactory.CreateLogger<SnapshotRestorer>()).RestorePending();

        if (Environment.ProcessPath is { } executable)
            _ = new StartupRegistration(executable).SetEnabled(false);
    }
}
