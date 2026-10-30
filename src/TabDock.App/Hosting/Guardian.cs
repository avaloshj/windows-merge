using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using Serilog.Extensions.Logging;
using TabDock.Core.Services;
using TabDock.Infrastructure;
using TabDock.Infrastructure.Logging;
using TabDock.Infrastructure.Persistence;
using TabDock.Win32;

namespace TabDock.App.Hosting;

/// <summary>
/// Proceso guardián (ADR 0003): el mismo ejecutable con <c>--guardian &lt;pid&gt; &lt;inicio&gt;</c>. Espera a que el
/// proceso principal termine y, si quedaron ventanas capturadas en <c>state.json</c>, las restaura.
/// </summary>
internal static class Guardian
{
    public const string Argument = "--guardian";

    /// <summary>Lanza el guardián del proceso actual. Un fallo no impide arrancar: queda la recuperación al iniciar.</summary>
    public static void Launch(Microsoft.Extensions.Logging.ILogger logger)
    {
        try
        {
            using var self = Process.GetCurrentProcess();
            var startInfo = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Ruta del ejecutable desconocida."))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(Argument);
            startInfo.ArgumentList.Add(self.Id.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(StartTicks(self).ToString(CultureInfo.InvariantCulture));

            using var guardian = Process.Start(startInfo);
            GuardianLog.Launched(logger, guardian?.Id ?? 0);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            GuardianLog.LaunchFailed(logger, ex);
        }
    }

    /// <summary>Punto de entrada del modo guardián. Devuelve el código de salida del proceso.</summary>
    public static int Run(int parentProcessId, long parentStartTicks)
    {
        var paths = AppPaths.CreateDefault();
        paths.EnsureCreated();
        using var serilog = TabDockLogging.CreateLogger(paths, LogEventLevel.Information, "guardian-.log");
        using var loggerFactory = new SerilogLoggerFactory(serilog);
        var logger = loggerFactory.CreateLogger("TabDock.Guardian");

        GuardianLog.Watching(logger, parentProcessId);

        // Pase lo que pase durante la espera, se intenta restaurar: es la razón de ser del guardián.
        try
        {
            WaitForParent(parentProcessId, parentStartTicks, logger);
        }
#pragma warning disable CA1031 // Ningún fallo al vigilar debe impedir la restauración.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            GuardianLog.WaitFailed(logger, ex);
        }

        try
        {
            var store = new JsonRuntimeStateStore(paths, TimeProvider.System, loggerFactory.CreateLogger<JsonRuntimeStateStore>());
            var windows = new WindowSystem(loggerFactory.CreateLogger<WindowSystem>(), TimeProvider.System);
            var restorer = new SnapshotRestorer(windows, store, loggerFactory.CreateLogger<SnapshotRestorer>());
            var report = restorer.RestorePending(onlyOwner: parentProcessId);
            GuardianLog.Finished(logger, report.Pending, report.Restored);
            return 0;
        }
#pragma warning disable CA1031 // El guardián no tiene UI: el error se registra y el siguiente arranque reintentará.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            GuardianLog.RestoreFailed(logger, ex);
            return 1;
        }
    }

    private static void WaitForParent(int processId, long startTicks, Microsoft.Extensions.Logging.ILogger logger)
    {
        Process parent;
        try
        {
            parent = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return; // El proceso principal ya no existe.
        }

        using (parent)
        {
            // PID reutilizado por otro proceso: el original ya terminó.
            if (StartTicks(parent) != startTicks)
                return;

            parent.WaitForExit();
            GuardianLog.ParentExited(logger);
        }
    }

    private static long StartTicks(Process process) => process.StartTime.ToUniversalTime().Ticks;
}
