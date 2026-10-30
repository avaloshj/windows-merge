using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;
using TabDock.Core.Diagnostics;

namespace TabDock.Infrastructure.Logging;

/// <summary>
/// Destructura (<c>{@Objeto}</c>) los tipos que tienen propiedades con <see cref="SensitiveDataAttribute"/>
/// aplicando el enmascaramiento correspondiente a cada una.
/// </summary>
public sealed class SensitiveDataDestructuringPolicy(MaskingContext context) : IDestructuringPolicy
{
    private static readonly ConcurrentDictionary<Type, PropertyPlan[]?> s_plans = new();

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        var plan = s_plans.GetOrAdd(value.GetType(), BuildPlan);
        if (plan is null)
        {
            result = null;
            return false;
        }

        var properties = new List<LogEventProperty>(plan.Length);
        foreach (var item in plan)
        {
            var raw = item.Property.GetValue(value);
            var logValue = item.Kind is { } kind
                ? new ScalarValue(LogMask.Apply(kind, raw as string ?? raw?.ToString(), context))
                : propertyValueFactory.CreatePropertyValue(raw, destructureObjects: true);
            properties.Add(new LogEventProperty(item.Property.Name, logValue));
        }

        result = new StructureValue(properties, value.GetType().Name);
        return true;
    }

    /// <summary>Devuelve <see langword="null"/> si el tipo no tiene propiedades sensibles (lo destructura Serilog).</summary>
    private static PropertyPlan[]? BuildPlan(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Select(p => new PropertyPlan(p, p.GetCustomAttribute<SensitiveDataAttribute>()?.Kind))
            .ToArray();

        return properties.Any(p => p.Kind is not null) ? properties : null;
    }

    private sealed record PropertyPlan(PropertyInfo Property, SensitiveDataKind? Kind);
}
