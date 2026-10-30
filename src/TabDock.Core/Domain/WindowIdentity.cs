using TabDock.Core.Abstractions;

namespace TabDock.Core.Domain;

/// <summary>
/// Identidad estable de una ventana: el HWND solo no basta, porque Windows reutiliza los identificadores cuando una
/// ventana se destruye. Se completa con el proceso y su hora de inicio.
/// </summary>
public readonly record struct WindowIdentity(WindowHandle Handle, int ProcessId, DateTimeOffset? ProcessStartTime);
