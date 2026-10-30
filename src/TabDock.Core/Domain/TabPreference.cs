using TabDock.Core.Diagnostics;

namespace TabDock.Core.Domain;

/// <summary>
/// Preferencias de una pestaña, por regla y proyecto (los HWND cambian entre sesiones; el nombre del proyecto no).
/// Clave única: (<see cref="RuleId"/>, <see cref="ProjectKey"/>).
/// </summary>
public sealed record TabPreference
{
    /// <summary>Regla del grupo; <see cref="Guid.Empty"/> para grupos manuales.</summary>
    public required Guid RuleId { get; init; }

    /// <summary>Nombre de proyecto normalizado (<see cref="Domain.ProjectKey.Normalize"/>).</summary>
    [SensitiveData(SensitiveDataKind.Text)]
    public required string ProjectKey { get; init; }

    [SensitiveData(SensitiveDataKind.Text)]
    public string? Alias { get; init; }

    /// <summary>Color elegido por el usuario (<c>#RRGGBB</c>); si falta, el determinista por nombre.</summary>
    public string? ColorHex { get; init; }

    public bool Pinned { get; init; }

    /// <summary>Posición en la barra lateral la última vez que se ordenó.</summary>
    public int? Order { get; init; }

    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>Marcada para purga (no vista en 90 días); se elimina en el siguiente arranque.</summary>
    public DateTimeOffset? DeletedAt { get; init; }

    public bool IsEmpty => Alias is null && ColorHex is null && !Pinned && Order is null;
}
