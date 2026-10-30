namespace TabDock.Core;

/// <summary>Error de dominio. <see cref="MessageKey"/> apunta a un recurso localizado.</summary>
/// <param name="Code">Código estable para logs y pruebas (p. ej. <c>window.notFound</c>).</param>
/// <param name="MessageKey">Clave del texto localizado que se muestra al usuario.</param>
/// <param name="Win32Error">Código de <c>GetLastError</c> cuando el fallo procede de Win32.</param>
public sealed record DomainError(string Code, string MessageKey, int? Win32Error = null);
