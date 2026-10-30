namespace TabDock.Core.Diagnostics;

/// <summary>Datos del usuario actual que nunca deben aparecer en logs ni diagnósticos.</summary>
/// <param name="UserProfile">Carpeta de perfil (p. ej. <c>C:\Users\nombre</c>); se sustituye por <c>%USERPROFILE%</c>.</param>
/// <param name="UserName">Nombre de usuario; cualquier segmento de ruta igual a él se sustituye por <c>%USERNAME%</c>.</param>
public sealed record MaskingContext(string? UserProfile, string? UserName)
{
    public static MaskingContext None { get; } = new(UserProfile: null, UserName: null);

    public static MaskingContext FromEnvironment() =>
        new(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName);
}
