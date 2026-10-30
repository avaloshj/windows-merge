using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TabDock.Core;
using TabDock.Core.Diagnostics;
using TabDock.Core.Domain;
using TabDock.Infrastructure.Persistence;

namespace TabDock.Infrastructure.Diagnostics;

/// <summary>
/// Crea el ZIP de diagnóstico anonimizado: logs (ya enmascarados, con una segunda pasada), versión de Windows, monitores
/// y DPI, y la configuración sin alias, nombres de proyecto ni rutas. No incluye <c>preferences.json</c> ni
/// <c>state.json</c>. El archivo se guarda donde elige el usuario; nada se envía a ningún sitio.
/// </summary>
public sealed partial class DiagnosticsExporter(AppPaths paths, TimeProvider time, ILogger<DiagnosticsExporter> logger)
{
    /// <summary>Solo los logs recientes: bastan para un informe y limitan el tamaño.</summary>
    private const int MaxLogFiles = 5;

    public static string SuggestedFileName(DateTimeOffset now) =>
        string.Create(CultureInfo.InvariantCulture, $"tabdock-diagnostico-{now.ToLocalTime():yyyyMMdd-HHmm}.zip");

    public Result Export(string zipPath, SettingsDocument settings, DiagnosticsEnvironment environment, DiagnosticsScrubber scrubber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(scrubber);

        var temp = zipPath + ".tmp";
        try
        {
            int logCount;
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                AddText(zip, "LEEME.txt", Readme(time.GetUtcNow()));
                AddText(zip, "system.json", SystemJson(environment));
                AddText(zip, "settings.json", JsonSerializer.Serialize(Sanitize(settings), TabDockJsonContext.Default.SettingsDocument));
                logCount = AddLogs(zip, scrubber);
            }

            File.Move(temp, zipPath, overwrite: true);
            LogExported(logCount);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            LogExportFailed(ex.GetType().Name);
            return Result.Fail(new DomainError("diagnostics.export", "Error_DiagnosticsExport"));
        }
    }

    /// <summary>
    /// Configuración sin datos que el usuario haya escrito: los nombres de regla se sustituyen por "regla-N" y los
    /// patrones de ruta por su hash. Ejecutables, opciones y atajos se conservan: son lo que sirve para diagnosticar.
    /// </summary>
    internal static SettingsDocument Sanitize(SettingsDocument settings) => settings with
    {
        Rules = [.. settings.Rules.Select((rule, index) => rule with
        {
            DisplayName = string.Create(CultureInfo.InvariantCulture, $"regla-{index + 1}"),
            ExecutablePathPattern = rule.ExecutablePathPattern is null ? null : LogMask.Hash(rule.ExecutablePathPattern),
        })],
    };

    private int AddLogs(ZipArchive zip, DiagnosticsScrubber scrubber)
    {
        if (!Directory.Exists(paths.Logs))
            return 0;

        var files = new DirectoryInfo(paths.Logs).GetFiles("*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(MaxLogFiles)
            .ToList();

        foreach (var file in files)
        {
            // Serilog mantiene abierto el log del día: se lee en modo compartido.
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            AddText(zip, "logs/" + file.Name, scrubber.Scrub(reader.ReadToEnd()));
        }

        return files.Count;
    }

    private static string SystemJson(DiagnosticsEnvironment environment)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("appVersion", environment.AppVersion);
            json.WriteString("osVersion", environment.OsVersion);
            json.WriteString("processArchitecture", environment.ProcessArchitecture);
            json.WriteString("uiCulture", environment.UiCulture);
            json.WriteStartArray("displays");
            foreach (var display in environment.Displays)
            {
                json.WriteStartObject();
                json.WriteNumber("width", display.Width);
                json.WriteNumber("height", display.Height);
                json.WriteNumber("dpi", display.Dpi);
                json.WriteNumber("scalePercent", display.Dpi * 100 / 96);
                json.WriteBoolean("primary", display.IsPrimary);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string Readme(DateTimeOffset now) => string.Create(CultureInfo.InvariantCulture, $"""
        Diagnóstico de TabDock ({now:yyyy-MM-dd HH:mm} UTC)

        Contenido:
        - system.json: versión de TabDock y de Windows, arquitectura, idioma, monitores y escala.
        - settings.json: configuración. Los nombres de regla se sustituyen por "regla-N" y las rutas por un hash.
        - logs/: registros de los últimos días. Títulos de ventana, rutas y nombres de proyecto aparecen solo como
          hash ([h:xxxxxxxx len:N]); usuario, equipo y carpeta de perfil, como %USERNAME%, %COMPUTERNAME% y %USERPROFILE%.

        No incluye alias, colores ni nombres de proyecto (preferences.json), ni el estado de las ventanas (state.json).
        TabDock no envía este archivo a ningún sitio: compártelo solo si quieres.
        """);

    private static void AddText(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Un temporal huérfano no es grave: se sobrescribe en el siguiente intento.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Diagnóstico exportado con {LogFiles} archivos de log")]
    private partial void LogExported(int logFiles);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo exportar el diagnóstico: {ErrorType}")]
    private partial void LogExportFailed(string errorType);
}
