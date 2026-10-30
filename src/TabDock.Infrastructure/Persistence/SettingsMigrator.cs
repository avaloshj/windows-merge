using System.Text.Json.Nodes;
using TabDock.Core.Domain;

namespace TabDock.Infrastructure.Persistence;

/// <summary>
/// Migraciones de <c>settings.json</c>, una por versión (v(n) → v(n+1)), aplicadas en cadena sobre el JSON antes de
/// deserializarlo. Así el modelo actual no necesita conocer formatos antiguos.
/// </summary>
internal static class SettingsMigrator
{
    private static readonly Func<JsonObject, JsonObject>[] s_steps =
    [
        V1ToV2,
    ];

    /// <summary>Lee <c>schemaVersion</c>; un archivo sin ella se considera versión 1.</summary>
    public static int ReadVersion(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return root["schemaVersion"] is JsonValue value && value.TryGetValue<int>(out var version) ? version : 1;
    }

    /// <summary>Lleva el documento a <see cref="SettingsDocument.CurrentSchemaVersion"/>.</summary>
    /// <exception cref="NotSupportedException">La versión es más nueva que la de esta aplicación o no es válida.</exception>
    public static JsonObject MigrateToCurrent(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var version = ReadVersion(root);
        if (version < 1 || version > SettingsDocument.CurrentSchemaVersion)
            throw new NotSupportedException($"Versión de esquema no soportada: {version}");

        for (var v = version; v < SettingsDocument.CurrentSchemaVersion; v++)
        {
            root = s_steps[v - 1](root);
            root["schemaVersion"] = v + 1;
        }

        return root;
    }

    /// <summary>
    /// v2 añade la sección <c>user</c>. Quien ya tenía un archivo v1 ha usado TabDock: no se le muestra la introducción.
    /// El resto de valores toma los de por defecto al deserializar.
    /// </summary>
    private static JsonObject V1ToV2(JsonObject root)
    {
        root["user"] ??= new JsonObject { ["onboardingCompleted"] = true };
        return root;
    }
}
