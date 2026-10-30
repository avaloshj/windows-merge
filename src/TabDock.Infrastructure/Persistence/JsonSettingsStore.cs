using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TabDock.Core.Abstractions;
using TabDock.Core.Domain;

namespace TabDock.Infrastructure.Persistence;

/// <summary>
/// <c>settings.json</c>: preferencias, reglas de programas y atajos. Los archivos de versiones anteriores se migran
/// (<see cref="SettingsMigrator"/>). En el primer arranque, o si el archivo está corrupto (se aparta como
/// <c>.corrupt-&lt;fecha&gt;</c>), se crea la configuración por defecto.
/// </summary>
public sealed partial class JsonSettingsStore(AppPaths paths, TimeProvider time, ILogger<JsonSettingsStore> logger) : ISettingsStore
{
    private readonly Lock _gate = new();

    public SettingsDocument Load()
    {
        lock (_gate)
        {
            var path = paths.SettingsFile;
            if (File.Exists(path))
            {
                try
                {
                    if (Read(path) is { } settings)
                        return settings;
                }
                catch (JsonException ex)
                {
                    LogCorrupt(ex.Message);
                }
                catch (NotSupportedException ex)
                {
                    LogUnsupportedSchema(ex.Message);
                }

                LogQuarantined(CorruptFile.Quarantine(path, time) ?? "(no se pudo mover)");
            }

            var defaults = SettingsDocument.CreateDefault(time.GetUtcNow());
            Write(defaults);
            LogDefaultsCreated(defaults.Rules.Count);
            return defaults;
        }
    }

    public void Save(SettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_gate)
            Write(settings);
    }

    /// <summary>Lee el archivo y, si es de una versión anterior, lo migra y guarda la versión migrada.</summary>
    private SettingsDocument? Read(string path)
    {
        if (JsonNode.Parse(AtomicFile.ReadAllText(path)) is not JsonObject root)
            throw new JsonException("La raíz no es un objeto JSON.");

        var version = SettingsMigrator.ReadVersion(root);
        root = SettingsMigrator.MigrateToCurrent(root);
        CompleteDefaults(root);
        var settings = root.Deserialize(TabDockJsonContext.Default.SettingsDocument);
        if (settings is null)
            return null;

        settings = Complete(settings);
        if (version != SettingsDocument.CurrentSchemaVersion)
        {
            Write(settings);
            LogMigrated(version, SettingsDocument.CurrentSchemaVersion);
        }

        return settings;
    }

    /// <summary>
    /// La serialización generada asigna el valor por defecto del tipo (<c>false</c>, 0, <see langword="null"/>) a las
    /// propiedades <c>init</c> que faltan en el JSON, no el de su inicializador: una regla editada a mano sin
    /// <c>enabled</c> quedaría desactivada. Las secciones incompletas (migradas o editadas a mano) se completan con los
    /// valores por defecto del propio modelo antes de deserializar.
    /// </summary>
    private static void CompleteDefaults(JsonObject root)
    {
        var userDefaults = JsonSerializer.SerializeToNode(new UserSettings(), TabDockJsonContext.Default.UserSettings)!.AsObject();
        if (root["user"] is JsonObject user)
            FillMissing(user, userDefaults);
        else
            root["user"] = userDefaults;

        if (root["rules"] is JsonArray rules)
        {
            var ruleDefaults = RuleDefaults();
            foreach (var rule in rules.OfType<JsonObject>())
                FillMissing(rule, ruleDefaults);
        }

        if (root["hotkeys"] is JsonArray hotkeys)
        {
            foreach (var hotkey in hotkeys.OfType<JsonObject>())
                hotkey["enabled"] ??= true;
        }
    }

    /// <summary>Valores por defecto de los campos opcionales de una regla (los obligatorios no se inventan).</summary>
    private static JsonObject RuleDefaults()
    {
        var template = new AppRule
        {
            Id = Guid.Empty,
            DisplayName = string.Empty,
            ExecutableName = string.Empty,
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        };
        var defaults = JsonSerializer.SerializeToNode(template, TabDockJsonContext.Default.AppRule)!.AsObject();
        foreach (var required in new[] { "id", "displayName", "executableName", "createdAt", "updatedAt" })
            defaults.Remove(required);

        return defaults;
    }

    private static void FillMissing(JsonObject target, JsonObject defaults)
    {
        foreach (var (name, value) in defaults)
        {
            if (!target.ContainsKey(name))
                target[name] = value?.DeepClone();
        }
    }

    /// <summary>Un archivo antiguo sin atajos recibe los atajos por defecto que falten.</summary>
    private static SettingsDocument Complete(SettingsDocument settings)
    {
        var missing = HotkeyBinding.Defaults.Where(d => !settings.Hotkeys.Any(h => h.Command == d.Command)).ToList();
        return missing.Count == 0 ? settings : settings with { Hotkeys = [.. settings.Hotkeys, .. missing] };
    }

    private void Write(SettingsDocument settings)
    {
        Directory.CreateDirectory(paths.Root);
        AtomicFile.WriteAllText(paths.SettingsFile, JsonSerializer.Serialize(settings, TabDockJsonContext.Default.SettingsDocument));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "settings.json corrupto: {Reason}")]
    private partial void LogCorrupt(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "settings.json no se puede migrar: {Reason}")]
    private partial void LogUnsupportedSchema(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "settings.json migrado del esquema {From} al {To}")]
    private partial void LogMigrated(int from, int to);

    [LoggerMessage(Level = LogLevel.Warning, Message = "settings.json apartado como {FileName}")]
    private partial void LogQuarantined(string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuración por defecto creada con {Rules} reglas")]
    private partial void LogDefaultsCreated(int rules);
}
