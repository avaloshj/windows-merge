using Serilog.Core;
using Serilog.Events;
using TabDock.Core.Diagnostics;

namespace TabDock.Infrastructure.Logging;

/// <summary>
/// Red de seguridad sobre todas las propiedades del evento (incluidas las anidadas):
/// <list type="bullet">
/// <item>Las propiedades con nombres sensibles (<c>Title</c>, <c>ProjectName</c>...) se sustituyen por hash + longitud.</item>
/// <item>Cualquier texto con la carpeta de perfil o el nombre de usuario como segmento de ruta se normaliza (<see cref="LogMask.NormalizeUserPath"/>).</item>
/// </list>
/// </summary>
public sealed class MaskingEnricher(MaskingContext context) : ILogEventEnricher
{
    /// <summary>Nombres de propiedad que nunca se registran en claro, aunque lleguen como texto simple.</summary>
    public static IReadOnlySet<string> SensitivePropertyNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Title",
        "RawTitle",
        "WindowTitle",
        "ProjectName",
        "ProjectKey",
        "DisplayName",
        "Subtitle",
        "ActiveFile",
        "FilePath",
        "Alias",
        "RemoteLabel",
        "ProfileName",
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        foreach (var (name, value) in logEvent.Properties.ToArray())
        {
            var masked = Mask(name, value);
            if (!ReferenceEquals(masked, value))
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, masked));
        }
    }

    private LogEventPropertyValue Mask(string name, LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue { Value: string text }:
                if (SensitivePropertyNames.Contains(name))
                    return LogMask.IsHashed(text) ? value : new ScalarValue(LogMask.Hash(text));

                var normalized = LogMask.NormalizeUserPath(text, context);
                return string.Equals(normalized, text, StringComparison.Ordinal) ? value : new ScalarValue(normalized);

            case ScalarValue scalar when SensitivePropertyNames.Contains(name) && scalar.Value is not null:
                return new ScalarValue(LogMask.Hash(scalar.Value.ToString()));

            case StructureValue structure:
                return MaskStructure(structure);

            case SequenceValue sequence:
                return MaskSequence(name, sequence);

            case DictionaryValue dictionary:
                return MaskDictionary(name, dictionary);

            default:
                return value;
        }
    }

    private StructureValue MaskStructure(StructureValue structure)
    {
        var changed = false;
        var properties = new List<LogEventProperty>(structure.Properties.Count);
        foreach (var property in structure.Properties)
        {
            var masked = Mask(property.Name, property.Value);
            changed |= !ReferenceEquals(masked, property.Value);
            properties.Add(ReferenceEquals(masked, property.Value) ? property : new LogEventProperty(property.Name, masked));
        }

        return changed ? new StructureValue(properties, structure.TypeTag) : structure;
    }

    private SequenceValue MaskSequence(string name, SequenceValue sequence)
    {
        var changed = false;
        var elements = new List<LogEventPropertyValue>(sequence.Elements.Count);
        foreach (var element in sequence.Elements)
        {
            var masked = Mask(name, element);
            changed |= !ReferenceEquals(masked, element);
            elements.Add(masked);
        }

        return changed ? new SequenceValue(elements) : sequence;
    }

    private DictionaryValue MaskDictionary(string name, DictionaryValue dictionary)
    {
        var changed = false;
        var entries = new List<KeyValuePair<ScalarValue, LogEventPropertyValue>>(dictionary.Elements.Count);
        foreach (var (key, element) in dictionary.Elements)
        {
            // La clave del diccionario actúa como nombre de propiedad; si no es sensible, se hereda el del contenedor.
            var keyName = key.Value as string ?? name;
            var masked = Mask(SensitivePropertyNames.Contains(keyName) ? keyName : name, element);
            changed |= !ReferenceEquals(masked, element);
            entries.Add(new(key, masked));
        }

        return changed ? new DictionaryValue(entries) : dictionary;
    }
}
