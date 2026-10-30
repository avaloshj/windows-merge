using System.ComponentModel.DataAnnotations;

namespace TabDock.Core.Services;

public sealed class EligibilityOptions
{
    public const string SectionName = "Eligibility";

    [Range(1, 10_000)]
    public int MinWidth { get; set; } = 300;

    [Range(1, 10_000)]
    public int MinHeight { get; set; } = 200;

    /// <summary>Clases de ventana que nunca se gestionan: diálogos, menús, tooltips y el propio shell.</summary>
    [Required]
    public ISet<string> ExcludedClassNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "#32770",                  // Diálogo estándar
        "#32768",                  // Menú emergente
        "tooltips_class32",
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
    };
}
