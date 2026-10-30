using System.Diagnostics;
using System.IO;
using TabDock.App.ViewModels;
using TabDock.Core.Abstractions;
using TabDock.Core.Services;

namespace TabDock.App.Services;

/// <summary>
/// Programas con ventanas elegibles abiertas ahora (para añadir reglas y para la introducción). Solo se leen metadatos:
/// nombre del ejecutable y su descripción de versión.
/// </summary>
public sealed class RunningProgramsProvider(IWindowSystem windows, EligibilityPolicy eligibility)
{
    public IReadOnlyList<RunningProgram> List() =>
        [.. windows.EnumerateTopLevel()
            .Where(w => !string.IsNullOrEmpty(w.ExecutableName) && eligibility.Evaluate(w).IsEligible)
            .GroupBy(w => w.ExecutableName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new RunningProgram(DescriptionOf(g.First().ExecutablePath) ?? g.Key, g.Key))];

    private static string? DescriptionOf(string executablePath)
    {
        if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            return null;

        try
        {
            var description = FileVersionInfo.GetVersionInfo(executablePath).FileDescription;
            return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
