using System.Text;

namespace TabDock.Infrastructure.Persistence;

/// <summary>
/// Escritura atómica: se escribe un archivo temporal completo, se fuerza a disco y se sustituye el destino.
/// Si el proceso muere a mitad, queda el archivo anterior intacto (como mucho, un <c>.tmp</c> huérfano).
/// Si el archivo está bloqueado un instante (otro proceso de TabDock, indexador, antivirus), se reintenta.
/// </summary>
internal static class AtomicFile
{
    /// <summary>Esperas entre reintentos ante archivos bloqueados.</summary>
    private static readonly int[] s_retryDelaysMs = [20, 40, 80, 160, 320];

    public static void WriteAllText(string path, string contents) => WithRetries(() => Write(path, contents));

    public static string ReadAllText(string path) => WithRetries(() => File.ReadAllText(path));

    private static void Write(string path, string contents)
    {
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(contents);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
            File.Replace(temp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        else
            File.Move(temp, path);
    }

    private static void WithRetries(Action action) => WithRetries(() =>
    {
        action();
        return true;
    });

    private static T WithRetries<T>(Func<T> action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (IOException) when (attempt < s_retryDelaysMs.Length)
            {
                Thread.Sleep(s_retryDelaysMs[attempt]);
            }
            catch (UnauthorizedAccessException) when (attempt < s_retryDelaysMs.Length)
            {
                // File.Replace informa de algunos bloqueos transitorios como acceso denegado.
                Thread.Sleep(s_retryDelaysMs[attempt]);
            }
        }
    }
}
