namespace TabDock.Core.Abstractions;

/// <summary>"Iniciar con Windows" sin permisos de administrador (clave <c>Run</c> del usuario actual).</summary>
public interface IStartupRegistration
{
    bool IsEnabled();

    Result SetEnabled(bool enabled);
}
