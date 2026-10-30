using System.Diagnostics.CodeAnalysis;

namespace TabDock.Core;

public readonly record struct Result
{
    private Result(DomainError? error) => Error = error;

    public DomainError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Fail(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }

    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Fail<T>(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(default, error);
    }
}
