// File: Result.cs
// Purpose: Represents the outcome of an operation that can fail in an
// *expected* way (e.g. "video not found", "invalid status transition")
// without resorting to exceptions for control flow. Exceptions stay
// reserved for truly unexpected errors (see Exceptions/DomainException.cs).
// Logic/Approach: A private constructor plus static factory methods
// (Success/Failure) guarantee a Result is never built in a contradictory
// state (e.g. successful but carrying an error message).
namespace VideoPlatform.Domain.Common;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    protected Result(bool isSuccess, string error)
    {
        if (isSuccess && !string.IsNullOrEmpty(error))
            throw new InvalidOperationException("A successful result cannot carry an error message.");
        if (!isSuccess && string.IsNullOrEmpty(error))
            throw new InvalidOperationException("A failed result must carry an error message.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, string.Empty);

    public static Result Failure(string error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, string.Empty);

    public static Result<T> Failure<T>(string error) => new(default!, false, error);
}

// Generic variant: carries a strongly-typed Value once the operation succeeds.
public class Result<T> : Result
{
    private readonly T _value;

    // Throws if read on a failed result instead of silently returning a
    // meaningless default — this is the Fail Fast principle in practice.
    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    protected internal Result(T value, bool isSuccess, string error) : base(isSuccess, error)
    {
        _value = value;
    }

    // Lets a handler `return someValue;` directly instead of
    // `return Result.Success(someValue);` everywhere — pure convenience.
    public static implicit operator Result<T>(T value) => Success(value);
}
