using System.Text.Json;
using Microsoft.Extensions.Logging;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Infrastructure.Persistence;

/// <summary>
/// <c>preferences.json</c>: alias, colores, pestañas fijadas y orden por proyecto. Un archivo ilegible se aparta como
/// <c>.corrupt-&lt;fecha&gt;</c> y se continúa sin preferencias.
/// </summary>
public sealed partial class JsonPreferencesStore(AppPaths paths, TimeProvider time, ILogger<JsonPreferencesStore> logger) : IPreferencesStore
{
    private readonly Lock _gate = new();

    public PreferencesDocument Load()
    {
        lock (_gate)
        {
            var path = paths.PreferencesFile;
            if (!File.Exists(path))
                return PreferencesDocument.Empty;

            try
            {
                var preferences = JsonSerializer.Deserialize(AtomicFile.ReadAllText(path), TabDockJsonContext.Default.PreferencesDocument);
                if (preferences is not null && preferences.SchemaVersion == PreferencesDocument.CurrentSchemaVersion)
                    return preferences;

                LogUnsupportedSchema(preferences?.SchemaVersion);
            }
            catch (JsonException ex)
            {
                LogCorrupt(ex.Message);
            }

            LogQuarantined(CorruptFile.Quarantine(path, time) ?? "(no se pudo mover)");
            return PreferencesDocument.Empty;
        }
    }

    public void Save(PreferencesDocument preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        lock (_gate)
        {
            Directory.CreateDirectory(paths.Root);
            AtomicFile.WriteAllText(paths.PreferencesFile, JsonSerializer.Serialize(preferences, TabDockJsonContext.Default.PreferencesDocument));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "preferences.json corrupto: {Reason}")]
    private partial void LogCorrupt(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "preferences.json con versión de esquema no soportada: {SchemaVersion}")]
    private partial void LogUnsupportedSchema(int? schemaVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "preferences.json apartado como {FileName}")]
    private partial void LogQuarantined(string fileName);
}
