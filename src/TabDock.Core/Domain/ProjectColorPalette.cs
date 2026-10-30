using System.Text;

namespace TabDock.Core.Domain;

/// <summary>Color determinista por proyecto: el mismo nombre produce siempre el mismo color, en cualquier sesión.</summary>
public static class ProjectColorPalette
{
    /// <summary>Colores legibles sobre fondo claro y oscuro, distintos del acento y del semáforo.</summary>
    public static IReadOnlyList<string> Colors { get; } =
    [
        "#3B82F6",
        "#10B981",
        "#F59E0B",
        "#8B5CF6",
        "#EC4899",
        "#14B8A6",
        "#F97316",
        "#6366F1",
        "#84CC16",
        "#06B6D4",
    ];

    public static string ColorFor(string? projectName)
    {
        var key = ProjectKey.Normalize(projectName);
        return Colors[(int)(Fnv1a(key) % (uint)Colors.Count)];
    }

    /// <summary>FNV-1a de 32 bits sobre UTF-8: estable entre ejecuciones (a diferencia de <see cref="string.GetHashCode()"/>).</summary>
    private static uint Fnv1a(string value)
    {
        const uint OffsetBasis = 2_166_136_261;
        const uint Prime = 16_777_619;

        var hash = OffsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= Prime;
        }

        return hash;
    }
}
