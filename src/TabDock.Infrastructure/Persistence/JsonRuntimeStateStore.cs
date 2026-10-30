using System.Text.Json;
using Microsoft.Extensions.Logging;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Infrastructure.Persistence;

/// <summary>
/// <c>state.json</c> en <c>%APPDATA%\TabDock</c>. Un archivo ilegible o de otra versión de esquema se aparta como
/// <c>state.json.corrupt-&lt;fecha&gt;</c> y se continúa con un estado vacío.
/// </summary>
public sealed partial class JsonRuntimeStateStore(AppPaths paths, TimeProvider time, ILogger<JsonRuntimeStateStore> logger) : IRuntimeStateStore
{
    private readonly Lock _gate = new();

    public RuntimeState Load()
    {
        lock (_gate)
        {
            var path = paths.StateFile;
            if (!File.Exists(path))
                return RuntimeState.Empty;

            try
            {
                var state = JsonSerializer.Deserialize(AtomicFile.ReadAllText(path), TabDockJsonContext.Default.RuntimeState);
                if (state is not null && state.SchemaVersion == RuntimeState.CurrentSchemaVersion)
                    return state;

                LogUnsupportedSchema(state?.SchemaVersion);
            }
            catch (JsonException ex)
            {
                LogCorrupt(ex.Message);
            }

            LogQuarantined(CorruptFile.Quarantine(path, time) ?? "(no se pudo mover)");
            return RuntimeState.Empty;
        }
    }

    public void Save(RuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_gate)
        {
            Directory.CreateDirectory(paths.Root);
            AtomicFile.WriteAllText(paths.StateFile, JsonSerializer.Serialize(state, TabDockJsonContext.Default.RuntimeState));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "state.json corrupto: {Reason}")]
    private partial void LogCorrupt(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "state.json con versión de esquema no soportada: {SchemaVersion}")]
    private partial void LogUnsupportedSchema(int? schemaVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "state.json apartado como {FileName}")]
    private partial void LogQuarantined(string fileName);
}
