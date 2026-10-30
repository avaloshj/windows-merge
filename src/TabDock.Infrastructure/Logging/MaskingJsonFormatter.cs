using System.Globalization;
using Serilog.Events;
using Serilog.Formatting;
using TabDock.Core.Diagnostics;

namespace TabDock.Infrastructure.Logging;

/// <summary>
/// Última barrera antes de escribir en disco: normaliza la línea JSON ya formateada, incluida la excepción
/// (<c>@x</c>), cuyos mensajes y trazas de pila pueden contener rutas con el perfil o el nombre de usuario.
/// Las propiedades ya se enmascararon antes (<see cref="MaskingEnricher"/>); aquí se cubre lo que no son propiedades.
/// </summary>
public sealed class MaskingJsonFormatter(ITextFormatter inner, MaskingContext context) : ITextFormatter
{
    // En JSON cada "\" de una ruta se escribe "\\": el perfil se busca en su forma escapada.
    private readonly MaskingContext _jsonContext = context with { UserProfile = context.UserProfile?.Replace(@"\", @"\\", StringComparison.Ordinal) };

    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        using var buffer = new StringWriter(CultureInfo.InvariantCulture);
        inner.Format(logEvent, buffer);
        output.Write(LogMask.NormalizeUserPath(buffer.ToString(), _jsonContext));
    }
}
