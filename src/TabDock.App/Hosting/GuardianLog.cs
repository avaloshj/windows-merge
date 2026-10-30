using Microsoft.Extensions.Logging;

namespace TabDock.App.Hosting;

internal static partial class GuardianLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Guardián iniciado (PID {GuardianProcessId})")]
    public static partial void Launched(ILogger logger, int guardianProcessId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo iniciar el guardián; la recuperación quedará para el próximo arranque")]
    public static partial void LaunchFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Guardián vigilando el proceso {ParentProcessId}")]
    public static partial void Watching(ILogger logger, int parentProcessId);

    [LoggerMessage(Level = LogLevel.Information, Message = "El proceso principal terminó")]
    public static partial void ParentExited(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo al vigilar el proceso principal; se restaura igualmente")]
    public static partial void WaitFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Guardián terminado: {Pending} ventanas pendientes, {Restored} restauradas")]
    public static partial void Finished(ILogger logger, int pending, int restored);

    [LoggerMessage(Level = LogLevel.Critical, Message = "El guardián no pudo restaurar; el siguiente arranque lo reintentará")]
    public static partial void RestoreFailed(ILogger logger, Exception exception);
}
