using Microsoft.Extensions.Logging;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Core.Services;

/// <summary>
/// Restaura las ventanas que <c>state.json</c> registra como capturadas. Lo usan el proceso guardián (ADR 0003),
/// el arranque tras un cierre inesperado y el cierre de emergencia. Es idempotente: restaurar dos veces no daña nada.
/// </summary>
public sealed partial class SnapshotRestorer(IWindowSystem windows, IRuntimeStateStore store, ILogger<SnapshotRestorer> logger)
{
    /// <summary>
    /// Restaura lo pendiente en <c>state.json</c>. Con <paramref name="onlyOwner"/> (guardián), solo si el estado sigue
    /// perteneciendo a ese proceso: si otra instancia nueva ya escribió su propio estado, no se toca.
    /// </summary>
    public RecoveryReport RestorePending(int? onlyOwner = null)
    {
        var state = store.Load();
        if (state.Windows.Count == 0)
            return new RecoveryReport(0, 0, 0, 0);

        if (onlyOwner is { } owner && state.OwnerProcessId != owner)
        {
            LogNotOwner(owner, state.OwnerProcessId);
            return new RecoveryReport(0, 0, 0, 0);
        }

        var report = RestoreAll(state.Windows.Select(w => w.Snapshot));
        store.Save(RuntimeState.Empty);
        LogRecovered(report.Pending, report.Restored, report.Skipped, report.Failed);
        return report;
    }

    public RecoveryReport RestoreAll(IEnumerable<WindowSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        int pending = 0, restored = 0, skipped = 0, failed = 0;
        foreach (var snapshot in snapshots)
        {
            pending++;
            if (!windows.Matches(snapshot.Identity))
            {
                skipped++;
                continue;
            }

            var result = windows.Restore(snapshot);
            if (result.IsSuccess)
            {
                restored++;
            }
            else
            {
                failed++;
                LogRestoreFailed(result.Error.Code, result.Error.Win32Error);
            }
        }

        return new RecoveryReport(pending, restored, skipped, failed);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recuperación: {Pending} ventanas pendientes, {Restored} restauradas, {Skipped} ya no existían, {Failed} fallidas")]
    private partial void LogRecovered(int pending, int restored, int skipped, int failed);

    [LoggerMessage(Level = LogLevel.Information, Message = "state.json pertenece al proceso {CurrentOwner}, no a {ExpectedOwner}: no se restaura")]
    private partial void LogNotOwner(int expectedOwner, int currentOwner);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo restaurar una ventana: {Code} (Win32 {Win32Error})")]
    private partial void LogRestoreFailed(string code, int? win32Error);
}
