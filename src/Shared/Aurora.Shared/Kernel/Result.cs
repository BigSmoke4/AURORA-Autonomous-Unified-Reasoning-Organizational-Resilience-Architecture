namespace Aurora.Shared.Kernel;

/// <summary>
/// Explicit success/failure envelope for application-layer commands and queries.
/// Used instead of throwing exceptions for expected, user-facing failure conditions
/// (validation, not-found, business-rule violations).
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }

    protected Result(bool isSuccess, string? error)
    {
        if (isSuccess && error is not null) throw new InvalidOperationException("Success results cannot carry an error.");
        if (!isSuccess && error is null) throw new InvalidOperationException("Failure results must carry an error.");
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, null);
    public static Result<T> Failure<T>(string error) => new(default, false, error);
}

public class Result<T> : Result
{
    private readonly T? _value;
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value of a failed result. Error: {Error}");

    internal Result(T? value, bool isSuccess, string? error) : base(isSuccess, error)
    {
        _value = value;
    }
}
