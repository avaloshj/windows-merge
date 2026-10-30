namespace TabDock.Core.Diagnostics;

/// <summary>Marca una propiedad cuyo valor nunca debe registrarse en claro.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class SensitiveDataAttribute(SensitiveDataKind kind) : Attribute
{
    public SensitiveDataKind Kind { get; } = kind;
}
