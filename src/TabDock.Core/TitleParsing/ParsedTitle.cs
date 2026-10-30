using TabDock.Core.Diagnostics;
using TabDock.Core.Domain;

namespace TabDock.Core.TitleParsing;

/// <summary>Información extraída del título de una ventana. Nunca se registra en claro.</summary>
public sealed record ParsedTitle
{
    /// <summary>Nombre del proyecto o carpeta raíz (<c>rootName</c>); vacío si la ventana no tiene carpeta abierta.</summary>
    [SensitiveData(SensitiveDataKind.Text)]
    public string ProjectName { get; init; } = string.Empty;

    [SensitiveData(SensitiveDataKind.Text)]
    public string? ActiveFile { get; init; }

    public bool IsDirty { get; init; }

    public RemoteKind RemoteKind { get; init; }

    /// <summary>Destino remoto (host SSH, distribución WSL, contenedor).</summary>
    [SensitiveData(SensitiveDataKind.Text)]
    public string? RemoteLabel { get; init; }

    [SensitiveData(SensitiveDataKind.Text)]
    public string? ProfileName { get; init; }

    /// <summary>Ventana sin carpeta abierta: la pestaña se nombra "Ventana vacía N".</summary>
    public bool IsEmptyWindow => ProjectName.Length == 0;
}
