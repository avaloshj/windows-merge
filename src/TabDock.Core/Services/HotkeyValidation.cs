using TabDock.Core.Domain;

namespace TabDock.Core.Services;

/// <summary>Resultado de <see cref="HotkeyValidator"/>.</summary>
/// <param name="ErrorKey">Clave del texto de error; <see langword="null"/> si el atajo es válido.</param>
/// <param name="ConflictsWith">Comando que ya usa ese atajo (solo con <c>Hotkey_Error_Duplicate</c>).</param>
public sealed record HotkeyValidation(string? ErrorKey, HotkeyCommand? ConflictsWith = null)
{
    public static HotkeyValidation Valid { get; } = new(ErrorKey: null);

    public bool IsValid => ErrorKey is null;

    public static HotkeyValidation Invalid(string errorKey, HotkeyCommand? conflictsWith = null) => new(errorKey, conflictsWith);
}
