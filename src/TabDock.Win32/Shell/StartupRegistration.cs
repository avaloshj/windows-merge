using System.Security;
using Microsoft.Win32;
using TabDock.Core;
using TabDock.Core.Abstractions;

namespace TabDock.Win32.Shell;

/// <summary>
/// "Iniciar con Windows" con el valor <c>TabDock</c> en <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>: no
/// necesita permisos de administrador y solo afecta al usuario actual. Si el usuario lo desactiva desde el Administrador
/// de tareas, Windows lo anota en otra clave (no documentada) y el valor sigue existiendo: TabDock no la lee.
/// </summary>
public sealed class StartupRegistration : IStartupRegistration
{
    public const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "TabDock";

    private readonly string _command;
    private readonly string _keyPath;
    private readonly string _valueName;

    /// <summary>Prepara el valor del registro para este ejecutable.</summary>
    /// <param name="executablePath">Ejecutable que se lanzará al iniciar sesión.</param>
    /// <param name="keyPath">Subclave de <c>HKCU</c>; las pruebas usan una propia para no tocar la real.</param>
    /// <param name="valueName">Nombre del valor.</param>
    public StartupRegistration(string executablePath, string keyPath = DefaultKeyPath, string valueName = DefaultValueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);

        // Entre comillas: la ruta puede contener espacios.
        _command = "\"" + executablePath + "\"";
        _keyPath = keyPath;
        _valueName = valueName;
    }

    /// <summary>Activado solo si el valor apunta a este ejecutable: si TabDock se movió, al activarlo se corrige.</summary>
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false);
        return key?.GetValue(_valueName) is string value && string.Equals(value, _command, StringComparison.OrdinalIgnoreCase);
    }

    public Result SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
            if (enabled)
                key.SetValue(_valueName, _command, RegistryValueKind.String);
            else
                key.DeleteValue(_valueName, throwOnMissingValue: false);

            return Result.Success();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            return Result.Fail(new DomainError("startup.registry", "Error_StartupRegistration"));
        }
    }
}
