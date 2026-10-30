using System.Text.Json.Serialization;
using TabDock.Core.Domain;

namespace TabDock.Infrastructure.Persistence;

/// <summary>
/// Serialización generada en compilación (sin reflexión). Los campos desconocidos se rechazan: un archivo con campos
/// inesperados se trata como corrupto. Los enumerados se guardan como texto para que el archivo sea legible.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    Converters = [typeof(WindowHandleJsonConverter)])]
[JsonSerializable(typeof(RuntimeState))]
[JsonSerializable(typeof(SettingsDocument))]
[JsonSerializable(typeof(PreferencesDocument))]
internal sealed partial class TabDockJsonContext : JsonSerializerContext;
