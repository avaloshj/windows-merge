using System.Text;

namespace TabDock.Core.Domain;

/// <summary>
/// Clave estable de un proyecto: el nombre normalizado (NFC, sin espacios en los extremos, minúsculas invariantes).
/// Las preferencias de pestaña se guardan por esta clave porque los HWND cambian entre sesiones.
/// </summary>
public static class ProjectKey
{
    public static string Normalize(string? projectName) =>
        string.IsNullOrWhiteSpace(projectName)
            ? string.Empty
            : projectName.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
}
