using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using TabDock.Core.Diagnostics;

namespace TabDock.Infrastructure.Logging;

public static class TabDockLogging
{
    public const int RetainedDays = 7;

    /// <summary>Aplica el enmascaramiento obligatorio. Se usa tanto en producción como en las pruebas.</summary>
    public static LoggerConfiguration WithTabDockMasking(this LoggerConfiguration configuration, MaskingContext context)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration
            .Destructure.With(new SensitiveDataDestructuringPolicy(context))
            .Enrich.With(new MaskingEnricher(context));
    }

    /// <summary>JSON compacto con la normalización final de rutas (cubre excepciones y trazas de pila).</summary>
    public static ITextFormatter CreateFormatter(MaskingContext context) =>
        new MaskingJsonFormatter(new CompactJsonFormatter(), context);

    /// <summary>Logger de la aplicación: JSON compacto, rotación diaria, 7 días de retención.</summary>
    public static Serilog.Core.Logger CreateLogger(AppPaths paths, LogEventLevel minimumLevel, string fileName = "tabdock-.log")
    {
        ArgumentNullException.ThrowIfNull(paths);

        var masking = MaskingContext.FromEnvironment();
        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WithTabDockMasking(masking)
            .Enrich.WithProperty("SessionId", Guid.NewGuid().ToString("N")[..8])
            .WriteTo.File(
                CreateFormatter(masking),
                Path.Combine(paths.Logs, fileName),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedDays,
                shared: false)
            .CreateLogger();
    }
}
