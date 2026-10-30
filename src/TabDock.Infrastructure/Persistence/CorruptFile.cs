using System.Globalization;

namespace TabDock.Infrastructure.Persistence;

/// <summary>Aparta un archivo ilegible como <c>&lt;nombre&gt;.corrupt-&lt;fecha UTC&gt;</c> para poder continuar con valores por defecto.</summary>
internal static class CorruptFile
{
    /// <summary>Devuelve el nombre del archivo apartado, o <see langword="null"/> si no se pudo mover.</summary>
    public static string? Quarantine(string path, TimeProvider time)
    {
        var stamp = time.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var target = $"{path}.corrupt-{stamp}";
        try
        {
            File.Move(path, target, overwrite: true);
            return Path.GetFileName(target);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
