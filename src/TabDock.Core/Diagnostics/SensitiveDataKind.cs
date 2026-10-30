namespace TabDock.Core.Diagnostics;

public enum SensitiveDataKind
{
    /// <summary>Texto libre (títulos, nombres de proyecto, alias): se registra solo como hash + longitud.</summary>
    Text,

    /// <summary>Ruta de programa: se registra con el perfil de usuario sustituido por <c>%USERPROFILE%</c>.</summary>
    UserPath,
}
