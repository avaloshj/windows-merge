using System.Diagnostics.CodeAnalysis;

namespace TabDock.Core;

/// <summary>Resultado con valor. Se crea con <see cref="Result.Success{T}(T)"/> o <see cref="Result.Fail{T}(DomainError)"/>.</summary>
public readonly record struct Result<T>
{
    private readonly T? _value;

    internal Result(T? value, DomainError? error)
    {
        _value = value;
        Error = error;
    }

    public DomainError? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public T? Value => IsSuccess ? _value : default;

    [SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "Result.Fail<T> es la alternativa.")]
    public static implicit operator Result<T>(DomainError error) => Result.Fail<T>(error);
}
